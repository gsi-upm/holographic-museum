import os
import logging
import json
import sys
import time
import re

# ---------------------------------------------------------------------------
# stdout line-buffering — must be FIRST so Unity sees all prints immediately
# ---------------------------------------------------------------------------
sys.stdout.reconfigure(line_buffering=True)
sys.stderr.reconfigure(line_buffering=True)

# ---------------------------------------------------------------------------
# Absolute paths relative to this script
# ---------------------------------------------------------------------------
BASE_DIR        = os.path.dirname(os.path.abspath(__file__))
FLAG_DONE       = os.path.join(BASE_DIR, "done.flag")
FLAG_BUSY       = os.path.join(BASE_DIR, "busy.flag")
INPUT_FILE      = os.path.join(BASE_DIR, "input.txt")
AUDIOGUIDE_FILE = os.path.join(BASE_DIR, "audioguide.wav")

# ---------------------------------------------------------------------------
# Logger
# ---------------------------------------------------------------------------
logging.basicConfig(
    level=logging.DEBUG,
    format="%(asctime)s [%(levelname)s] %(name)s — %(message)s",
    handlers=[
        logging.StreamHandler(sys.stdout),
        logging.FileHandler(os.path.join(BASE_DIR, "audioguide.log"), encoding="utf-8"),
    ],
)
logger = logging.getLogger(__name__)

logger.info("Importing dependencies...")

from langchain_ollama import OllamaEmbeddings
from langchain_openrouter import ChatOpenRouter
from langchain_pinecone import PineconeVectorStore
from langchain_core.messages import HumanMessage, SystemMessage, ToolMessage, AIMessage
from langchain_core.tools import tool
from langgraph.graph import StateGraph, START, END, MessagesState
from langgraph.prebuilt import ToolNode

from pydantic import BaseModel
from pinecone import Pinecone
from typing import List, Optional
from kokoro_onnx import Kokoro
import soundfile as sf
import dotenv

logger.info("All imports OK.")

# ---------------------------------------------------------------------------
# .env
# ---------------------------------------------------------------------------
env_path = os.path.join(BASE_DIR, ".env")
if os.path.exists(env_path):
    dotenv.load_dotenv(env_path)
    logger.info(f"Loaded .env from {env_path}")

# ---------------------------------------------------------------------------
# Kokoro TTS
# ---------------------------------------------------------------------------
KOKORO_MODEL_PATH  = os.path.join(BASE_DIR, "kokoro-v1.0.onnx")
KOKORO_VOICES_PATH = os.path.join(BASE_DIR, "voices-v1.0.bin")

if not os.path.exists(KOKORO_MODEL_PATH):
    logger.error(f"Kokoro model not found: {KOKORO_MODEL_PATH}")
    sys.exit(1)
if not os.path.exists(KOKORO_VOICES_PATH):
    logger.error(f"Kokoro voices not found: {KOKORO_VOICES_PATH}")
    sys.exit(1)

logger.info("Loading Kokoro TTS model...")
kokoro = Kokoro(KOKORO_MODEL_PATH, KOKORO_VOICES_PATH)
logger.info("Kokoro TTS loaded.")

# ---------------------------------------------------------------------------
# Shared LLM
# ---------------------------------------------------------------------------
LLM: ChatOpenRouter = None  # initialized in main()

# ---------------------------------------------------------------------------
# Pinecone
# ---------------------------------------------------------------------------
pinecone_vector_store: PineconeVectorStore = None  # initialized in main()

# ---------------------------------------------------------------------------
# Input parsing
# ---------------------------------------------------------------------------

def parse_input(raw: str) -> tuple[str, Optional[str], Optional[str]]:
    """
    Parses Unity input format: [room:artist][level:difficulty] visitor message
    Returns (message, artist_name, level)

    Examples:
        "[room:picasso][level:hard] ¿Por qué usó el azul?"
        → ("¿Por qué usó el azul?", "picasso", "hard")
    """
    artist = None
    level  = None

    room_match  = re.search(r'\[room:([^\]]+)\]',  raw)
    level_match = re.search(r'\[level:([^\]]+)\]', raw)

    if room_match:
        artist = room_match.group(1).strip().lower()
    if level_match:
        level = level_match.group(1).strip().lower()

    message = re.sub(r'\[[^\]]+\]', '', raw).strip()
    return message, artist, level

# ---------------------------------------------------------------------------
# Bilingual prompts
# ---------------------------------------------------------------------------
PROMPTS = {
    "es": {
        "audioguide": (
            "Eres MUSA — Museum Understanding & Storytelling Agent — un carismático guía holográfico "
            "del museo HART (Holographic Art). Has estado acompañando al visitante a lo largo de la "
            "exposición. Responde en un tono cálido, atractivo y natural — como un amigo muy culto, "
            "no un libro de texto.\n"
            "INSTRUCCIONES:\n"
            "1. Responde directamente a la pregunta del visitante. Si no hay pregunta concreta, haz "
            "una introducción atractiva al artista.\n"
            "2. Prosa fluida — sin listas, viñetas ni encabezados.\n"
            "3. Teje vida, personalidad y obras icónicas como si fuera una historia.\n"
            "4. Usa el contexto para añadir profundidad y detalles específicos.\n"
            "5. No repitas información que ya aparezca en el historial de mensajes.\n"
            "6. Artista: {artist_name} | Pregunta: {question} | Contexto: {context}\n"
            "7. Máximo 1 minuto de contenido hablado — sé conciso, déjalos con ganas de más."
        ),
        "questions": (
            "Eres un actualizador de preguntas de trivia para un museo de arte.\n"
            "El visitante se encuentra en el nivel '{level}' del artista '{artist_name}'.\n"
            "Se te dan las preguntas ACTUALES de ese nivel y el contexto de la conversación reciente.\n\n"
            "Tu tarea: enriquecer o reemplazar solo las preguntas que no reflejen los temas nuevos "
            "hablados. Mantén las que sigan siendo válidas. Devuelve siempre 4 preguntas.\n\n"
            "Taxonomía de Bloom por nivel:\n"
            "  beginner → Recordar (¿Cuál...?, ¿Dónde...?, ¿En qué año...?)\n"
            "  hard     → Comprender/Aplicar (¿Por qué...?, ¿Qué relación...?)\n"
            "  expert   → Analizar/Evaluar (¿Qué implica...?, ¿Qué interpretación...?)\n\n"
            "Centra los distractores en similitud semántica con la respuesta correcta.\n\n"
            "Artista: {artist_name}\n"
            "Nivel activo: {level}\n"
            "Temas nuevos de la conversación: {conversation_topics}\n"
            "Contexto de conocimiento: {context}\n"
            "Preguntas actuales:\n{current_questions}\n\n"
            "Devuelve el set completo de 4 preguntas en el mismo formato JSON."
        ),
        "sys_msg": (
            "Eres un asistente experto en arte que responde en español.\n\n"
            "Contexto de la visita actual:\n"
            "  Artista activo: {artist_name}\n"
            "  Nivel activo:   {level}\n\n"
            "FLUJO OBLIGATORIO para cada mensaje del visitante:\n"
            "  1. SIEMPRE llama primero a generate_audioguide.\n"
            "  2. Llama a get_questions para ver las preguntas actuales del nivel activo.\n"
            "  3. Con las preguntas en contexto, decide si los temas de la conversación\n"
            "     aportan algo nuevo que no esté cubierto ya. Si es así, llama a update_questions.\n"
            "     Si las preguntas ya cubren bien lo hablado, no la llames.\n\n"
            "Argumentos comunes a todas las herramientas:\n"
            "  - artist_name: '{artist_name}'\n"
            "  - language: 'es'\n\n"
            "Para generate_audioguide y get_questions:\n"
            "  - question: el mensaje exacto del visitante (sin los tags de metadatos)\n"
            "  - context_for_search: consulta concreta en español para buscar en Pinecone\n\n"
            "Solo para update_questions:\n"
            "  - level: '{level}'\n"
            "  - conversation_topics: resumen breve de los temas nuevos a incorporar\n"
            "  - context_for_search: misma consulta que antes\n\n"
            "IMPORTANTE — input de transcripción de voz, puede tener errores ortográficos.\n"
            "NUNCA pidas aclaración al usuario."
        ),
        "kokoro_lang": "es",
    },
    "en": {
        "audioguide": (
            "You are MUSA — Museum Understanding & Storytelling Agent — a charismatic holographic "
            "guide at the HART (Holographic Art) museum. You have been accompanying the visitor "
            "throughout the exhibition. Respond in a warm, engaging and natural tone — like a "
            "knowledgeable friend, not a textbook.\n"
            "Any Spanish information from the context must be translated to English.\n"
            "INSTRUCTIONS:\n"
            "1. Answer the visitor's question directly. If no specific question, introduce the artist.\n"
            "2. Flowing prose — no lists, bullet points or headers.\n"
            "3. Weave together life, personality and iconic works as a story.\n"
            "4. Use the context to add depth and specific details.\n"
            "5. Do not repeat information already covered in the message history.\n"
            "6. Artist: {artist_name} | Question: {question} | Context: {context}\n"
            "7. Maximum 1 minute of spoken content — be concise, leave them wanting more."
        ),
        "questions": (
            "You are a trivia question updater for an art museum.\n"
            "The visitor is on level '{level}' for artist '{artist_name}'.\n"
            "You are given the CURRENT questions for that level and the recent conversation context.\n\n"
            "Your task: enrich or replace only questions that don't reflect new topics discussed. "
            "Keep valid ones. Always return 4 questions.\n\n"
            "Bloom's Taxonomy by level:\n"
            "  beginner → Remember (Which...?, Where...?, In what year...?)\n"
            "  hard     → Understand/Apply (Why...?, How does X relate to Y...?)\n"
            "  expert   → Analyze/Evaluate (What does X imply about Y...?)\n\n"
            "Focus distractors on semantic similarity to the correct answer.\n\n"
            "Artist: {artist_name}\n"
            "Active level: {level}\n"
            "New topics from conversation: {conversation_topics}\n"
            "Knowledge context: {context}\n"
            "Current questions:\n{current_questions}\n\n"
            "Return the complete set of 4 questions in the same JSON format."
        ),
        "sys_msg": (
            "You are an expert art assistant that responds in English.\n\n"
            "Current visit context:\n"
            "  Active artist: {artist_name}\n"
            "  Active level:  {level}\n\n"
            "MANDATORY FLOW for each visitor message:\n"
            "  1. ALWAYS call generate_audioguide first.\n"
            "  2. Call get_questions to see the current questions for the active level.\n"
            "  3. With the questions in context, decide whether the conversation topics\n"
            "     introduce something new not already covered. If so, call update_questions.\n"
            "     If the questions already cover the topics well, do not call it.\n\n"
            "Arguments common to all tools:\n"
            "  - artist_name: '{artist_name}'\n"
            "  - language: 'en'\n\n"
            "For generate_audioguide and get_questions:\n"
            "  - question: the exact visitor message (without metadata tags)\n"
            "  - context_for_search: specific query in Spanish to search Pinecone\n\n"
            "Only for update_questions:\n"
            "  - level: '{level}'\n"
            "  - conversation_topics: brief summary of new topics to incorporate\n"
            "  - context_for_search: same query as before\n\n"
            "IMPORTANT — input from voice transcription, may contain spelling errors.\n"
            "NEVER ask the user for clarification."
        ),
        "kokoro_lang": "en-us",
    },
}

# ---------------------------------------------------------------------------
# Pydantic models
# ---------------------------------------------------------------------------

class Answer(BaseModel):
    correct: bool
    choice: str

class QuestionItem(BaseModel):
    question: str
    options: List[Answer]

class QuestionBatch(BaseModel):
    trivia: List[QuestionItem]

class Audioguide(BaseModel):
    audioguide: str

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _retrieve_from_pinecone(artist_name: str, query: str, k: int = 5) -> str:
    try:
        results = pinecone_vector_store.similarity_search(query, k=k, namespace=artist_name)
        context = "\n\n".join(doc.page_content for doc in results) if results else "No context available."
        logger.info(f"Pinecone [{artist_name}]: {len(results)} fragments retrieved")
    except Exception as e:
        logger.error(f"Pinecone error: {e}")
        return "No context available."
    return context


def _load_questions_from_disk(artist_name: str, level: str) -> dict:
    path = os.path.join(BASE_DIR, "Resources", "Questionsdata", artist_name, level, "questions.json")
    if os.path.exists(path):
        try:
            with open(path, "r", encoding="utf-8") as f:
                return json.load(f)
        except Exception as e:
            logger.warning(f"Could not load questions [{artist_name}/{level}]: {e}")
    return {}


def _save_questions_to_disk(artist_name: str, level: str, data: dict) -> None:
    cache_dir = os.path.join(BASE_DIR, "Resources", "Questionsdata", artist_name, level)
    os.makedirs(cache_dir, exist_ok=True)
    with open(os.path.join(cache_dir, "questions.json"), "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
    logger.info(f"Questions saved: {artist_name}/{level}")


def generate_audiofile(text: str, kokoro_lang: str = "en-us") -> str:
    voice_map = {"es": "ef_dora", "en-us": "af_bella"}
    voice = voice_map.get(kokoro_lang, "af_bella")
    print(f"[TTS] lang={kokoro_lang} voice={voice}", flush=True)
    samples, sample_rate = kokoro.create(text, voice=voice, speed=1.0, lang=kokoro_lang)
    sf.write(AUDIOGUIDE_FILE, samples, sample_rate)
    open(FLAG_DONE, "w").close()
    print(f"[AUDIO] Saved: {AUDIOGUIDE_FILE}", flush=True)
    return AUDIOGUIDE_FILE

# ---------------------------------------------------------------------------
# Tools
# ---------------------------------------------------------------------------

@tool
def generate_audioguide(
    artist_name: str,
    question: str,
    context_for_search: str,
    language: str = "en",
) -> str:
    """
    ALWAYS call this first for every visitor message.
    Generates a narrative audio guide about an artist and produces the WAV file for Unity.

    Args:
        artist_name: Artist name in lowercase without spaces (e.g. 'velazquez', 'vangogh')
        question: The exact visitor message (without metadata tags)
        context_for_search: Query in Spanish to search Pinecone for relevant context
        language: 'es' or 'en' (default: 'en')
    """
    print(f"[TOOL] generate_audioguide | artist={artist_name} lang={language}", flush=True)
    lang = language if language in PROMPTS else "en"
    p = PROMPTS[lang]

    context = _retrieve_from_pinecone(artist_name, context_for_search)
    prompt = p["audioguide"].format(
        artist_name=artist_name,
        question=question,
        context=context,
    )

    structured_llm = LLM.with_structured_output(Audioguide)
    response: Audioguide = structured_llm.invoke(prompt)
    generate_audiofile(response.audioguide, kokoro_lang=p["kokoro_lang"])

    logger.info(f"Audioguide done: {artist_name} ({lang})")
    return response.audioguide


@tool
def get_questions(
    artist_name: str,
    question: str,
    context_for_search: str,
    language: str = "en",
) -> str:
    """
    Call this after generate_audioguide, every turn.
    Returns the current questions on disk for the active level so you can decide
    whether the conversation has introduced topics not already covered.

    Args:
        artist_name: Artist name in lowercase without spaces (e.g. 'velazquez', 'vangogh')
        question: The exact visitor message
        context_for_search: Query in Spanish to search Pinecone for relevant context
        language: 'es' or 'en' (default: 'en')
    """
    # level is read from the system prompt context — Unity always sends it.
    # We load all levels so the LLM has full visibility, but the active level
    # is already highlighted in the system prompt.
    print(f"[TOOL] get_questions | artist={artist_name}", flush=True)

    all_questions = {}
    for level in ["beginner", "hard", "expert"]:
        data = _load_questions_from_disk(artist_name, level)
        all_questions[level] = data if data else "No questions yet."

    logger.info(f"get_questions: loaded questions for {artist_name}")
    return json.dumps(all_questions, ensure_ascii=False, indent=2)


@tool
def update_questions(
    artist_name: str,
    question: str,
    context_for_search: str,
    conversation_topics: str,
    level: str,
    language: str = "en",
) -> str:
    """
    Call this ONLY if get_questions revealed that the active level's questions
    do not yet cover the new topics from the conversation.
    Reads the current questions for that level, updates only what is necessary, saves back.

    Args:
        artist_name: Artist name in lowercase without spaces (e.g. 'velazquez', 'vangogh')
        question: The exact visitor message
        context_for_search: Query in Spanish to search Pinecone for relevant context
        conversation_topics: Brief description of new topics to incorporate (based on what get_questions showed was missing)
        level: Active difficulty level ('beginner', 'hard' or 'expert')
        language: 'es' or 'en' (default: 'en')
    """
    print(f"[TOOL] update_questions | artist={artist_name} level={level} lang={language}", flush=True)
    lang = language if language in PROMPTS else "en"
    p = PROMPTS[lang]

    valid_levels = ["beginner", "hard", "expert"]
    if level not in valid_levels:
        logger.warning(f"Unknown level '{level}', defaulting to 'hard'")
        level = "hard"

    context = _retrieve_from_pinecone(artist_name, context_for_search)
    current = _load_questions_from_disk(artist_name, level)
    current_str = json.dumps(current, ensure_ascii=False, indent=2) if current else "No questions yet."

    prompt = p["questions"].format(
        artist_name=artist_name,
        level=level,
        conversation_topics=conversation_topics,
        context=context,
        current_questions=current_str,
    )

    structured_llm = LLM.with_structured_output(QuestionBatch)
    try:
        result: QuestionBatch = structured_llm.invoke(prompt)
        _save_questions_to_disk(artist_name, level, result.model_dump())
        msg = f"Questions updated — {artist_name}/{level}: {len(result.trivia)} questions"
    except Exception as e:
        logger.error(f"Error updating questions [{level}]: {e}")
        msg = f"Error updating questions [{level}]: {e}"

    logger.info(msg)
    return msg

# ---------------------------------------------------------------------------
# LangGraph
# ---------------------------------------------------------------------------

def _build_graph(language: str = "en", artist_name: str = "unknown", level: str = "beginner"):
    lang = language if language in PROMPTS else "en"

    sys_content = PROMPTS[lang]["sys_msg"].format(
        artist_name=artist_name,
        level=level,
    )
    sys_msg = SystemMessage(content=sys_content)

    tools = [generate_audioguide, get_questions, update_questions]
    llm_with_tools = LLM.bind_tools(tools)

    def assistant(state: MessagesState):
        open(FLAG_BUSY, "w").close()
        return {"messages": [llm_with_tools.invoke([sys_msg] + state["messages"])]}

    def should_continue(state: MessagesState) -> str:
        last = state["messages"][-1]
        if hasattr(last, "tool_calls") and last.tool_calls:
            return "tools"
        return "end"

    def format_output(state: MessagesState):
        # The audioguide ToolMessage is always the longest — return it as final response
        best = ""
        for msg in reversed(state["messages"]):
            if isinstance(msg, ToolMessage) and len(msg.content) > len(best):
                best = msg.content
        if best:
            return {"messages": [AIMessage(content=best)]}
        return {}

    builder = StateGraph(MessagesState)
    builder.add_node("assistant", assistant)
    builder.add_node("tools", ToolNode(tools))
    builder.add_node("format_output", format_output)

    builder.add_edge(START, "assistant")
    builder.add_conditional_edges("assistant", should_continue, {
        "tools": "tools",
        "end":   "format_output",
    })
    builder.add_edge("tools", "assistant")
    builder.add_edge("format_output", END)

    return builder.compile()

# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    print("=== Agent starting ===", flush=True)

    # Init Pinecone
    logger.info("Connecting to Pinecone...")
    pinecone_api_key = os.environ.get("PINECONE_API_KEY")
    _pinecone_client = Pinecone(api_key=pinecone_api_key)
    _pinecone_client.index_name = "artist-info"
    embeddings = OllamaEmbeddings(model="nomic-embed-text:latest", base_url="http://localhost:11434")
    pinecone_vector_store = PineconeVectorStore.from_existing_index(_pinecone_client.index_name, embeddings)
    logger.info("Pinecone connected.")

    # Init shared LLM
    logger.info("Initializing shared LLM...")
    LLM = ChatOpenRouter(model="anthropic/claude-sonnet-4-6", temperature=0.8)
    logger.info("LLM ready.")

    # Language from env
    LANG = os.environ.get("AGENT_LANG", "en")
    if LANG not in PROMPTS:
        LANG = "en"
    logger.info(f"Agent language: {LANG}")

    # Persistent state across turns
    session_messages: list = []
    current_artist: str = "unknown"
    current_level:  str = "beginner"

    # Clean stale files
    for _f in [INPUT_FILE, FLAG_DONE, FLAG_BUSY]:
        if os.path.exists(_f):
            os.remove(_f)
            logger.info(f"Cleaned stale file: {_f}")

    print(f"=== Watching for input at: {INPUT_FILE} ===", flush=True)

    while True:
        try:
            if os.path.exists(INPUT_FILE):
                with open(INPUT_FILE, "r", encoding="utf-8") as f:
                    raw = f.read().strip()
                os.remove(INPUT_FILE)
                print(f"[INPUT RAW] {raw}", flush=True)

                if not raw:
                    continue

                if os.path.exists(FLAG_DONE):
                    os.remove(FLAG_DONE)

                # Parse Unity metadata
                message, artist, level = parse_input(raw)
                if artist:
                    current_artist = artist
                if level:
                    current_level = level

                logger.info(f"Turn {len(session_messages)+1} | artist={current_artist} level={current_level} | {message}")

                # Rebuild graph with current artist/level in system prompt
                graph = _build_graph(LANG, artist_name=current_artist, level=current_level)

                session_messages.append(HumanMessage(content=message))
                result = graph.invoke({"messages": session_messages})
                session_messages = result["messages"]

                last_msg = result["messages"][-1]
                print(f"[RESPONSE] {last_msg.content}", flush=True)
                logger.info(f"Response: {last_msg.content}")

                if not os.path.exists(FLAG_DONE):
                    open(FLAG_DONE, "w").close()
                    logger.info("Fallback done.flag written.")

        except Exception as e:
            logger.error(f"Error processing input: {e}", exc_info=True)
            for _f in [FLAG_BUSY, INPUT_FILE]:
                if os.path.exists(_f):
                    os.remove(_f)
            open(FLAG_DONE, "w").close()

        time.sleep(0.5)