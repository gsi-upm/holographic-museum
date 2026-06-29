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

SEARCHING_AUDIO = {
    "es":    os.path.join(BASE_DIR, "searching_es.mp3"),
    "en-us": os.path.join(BASE_DIR, "searching_en.mp3"),
}
FLAG_SEARCHING = os.path.join(BASE_DIR, "searching.flag")

# ---------------------------------------------------------------------------
# Logger
# ---------------------------------------------------------------------------
logging.basicConfig(
    level=logging.DEBUG,
    format="%(asctime)s [%(levelname)s] %(name)s — %(message)s",
    handlers=[
        logging.StreamHandler(sys.stdout),
        logging.FileHandler(os.path.join(BASE_DIR, "agent.log"), encoding="utf-8"),
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
# Shared LLMs — initialized in main()
# ---------------------------------------------------------------------------
LLM: ChatOpenRouter       = None   # Main LLM  (Claude Sonnet) — orchestrator
LLM_CHAT: ChatOpenRouter  = None   # Chat LLM  (Gemini Flash)  — conversational flow
LLM_CLASS: ChatOpenRouter = None   # Classifier (Gemini Flash)  — intent detection

# ---------------------------------------------------------------------------
# Pinecone — initialized in main()
# ---------------------------------------------------------------------------
pinecone_vector_store: PineconeVectorStore = None

# ---------------------------------------------------------------------------
# Room layouts — spatial context per artist room
# ---------------------------------------------------------------------------
ROOM_LAYOUTS = {
    "velazquez": {
        "main_artwork": "Las Meninas",
        "layout": (
            "- centro (escultura): Menina (escultura)\n"
            "- pared del fondo: Las Meninas\n"
            "- pared derecha: La fábula de Aracne\n"
            "- pared izquierda: Retrato de un hombre"
        ),
    },
    "vangogh": {
        "main_artwork": "Noche estrellada, Terraza de café por la noche, La habitación de Arlés",
        "layout": (
            "- entrada: Recreación 3D de la habitación de Van Gogh en Arlés\n"
            "- pared central (centro): Recreación 3D Terraza de café por la noche\n"
            "- pared izquierda (cuadro derecha): Los girasoles\n"
            "- pared izquierda (cuadro izquierda): La noche estrellada\n"
            "- pared derecha (cuadro derecha pegado a escultura entrada): La habitación de Arlés\n"
            "- pared derecha (cuadro izquierda): Almendro en flor\n"
        ),
    },
    "picasso": {
        "main_artwork": "Guernica",
        "layout": (
            "- pared central (fondo): Guernica\n"
            "- centro (escultura): Autorretrato cromado\n"
            "- pared derecha (cuadro izquierda): Señoritas de Avignon\n"
            "- pared derecha (cuadro derecha): Autorretrato 1907\n"
            "- pared izquierda (cuadro izquierda): Dora maar au chat\n"
            "- pared izquierda (cuadro derecha): El viejo y el nuevo año\n"
        ),
    },
    "dali": {
        "main_artwork": "La persistencia de la memoria",
        "layout": (
            "- pared central (fondo): La persistencia de la memoria\n"
            "- centro (escultura): Reloj blando (escultura)\n"
            "- pared derecha: El gran masturbador\n"
            "- pared izquierda: Los elefantes\n"
        ),
    },
    "monet": {
        "main_artwork": "Los Nenúfares e Impresión, sol naciente",
        "layout": (
            "- pared central (fondo) (derecha): Los Nenúfares\n"
            "- pared central (fondo) (izquierda): Puente japonés\n"
            "- centro (escultura): Nenúfar gigante (escultura)\n"
            "- pared derecha: Impresión, sol naciente\n"
            "- pared izquierda(cuadro izquierda): Mujer con sombrilla\n"
            "- pared izquierda(cuadro derecha): El jardín del artista en Gierny\n"

        ),
    },
    "kandinsky": {
        "main_artwork": "Composición Ocho",
        "layout": (
            "- pared izquierda: Recreación 3D de Composición Ocho\n"
            "- pared central (izquierda): \n"
        ),
    },
    "munch": {
        "main_artwork": "El grito",
        "layout": (
            "- pared central (fondo): El grito\n"
        ),
    },
    "koons": {
        "main_artwork": "Balloon Dog",
        "layout": (
            "- pared central (fondo): Balloon Dog\n"
            "- pared derecha (cuadro izquierda): En blanco II\n"
            "- pared derecha (cuadro derecha): Algunos círculos\n"
            "- pared izquierda (cuadro izquierda): Negro y violeta\n"
            "- pared izquierda (cuadro derecha): Tensión suave n.º 85\n"
        ),
    },
}

# ---------------------------------------------------------------------------
# Input parsing
# ---------------------------------------------------------------------------

def parse_input(raw: str) -> tuple[str, Optional[str], Optional[str], Optional[str]]:
    """
    Parses Unity input format: [room:artist][level:difficulty][lang:xx] visitor message
    Returns (message, artist_name, level, language)
    """
    room_match  = re.search(r'\[room:([^\]]+)\]',  raw)
    level_match = re.search(r'\[level:([^\]]+)\]', raw)
    lang_match  = re.search(r'\[lang:([^\]]+)\]',  raw)

    artist = room_match.group(1).strip().lower()  if room_match  else None
    level  = level_match.group(1).strip().lower() if level_match else None
    lang   = lang_match.group(1).strip().lower()  if lang_match  else None

    message = re.sub(r'\[[^\]]+\]', '', raw).strip()
    return message, artist, level, lang

# ---------------------------------------------------------------------------
# Room context helper
# ---------------------------------------------------------------------------

def _get_room_context(artist_name: str) -> tuple[str, str]:
    """Returns (layout_text, main_artwork) for the given artist room."""
    room = ROOM_LAYOUTS.get(artist_name, {})
    return room.get("layout", ""), room.get("main_artwork", "")

# ---------------------------------------------------------------------------
# Bilingual prompts
# ---------------------------------------------------------------------------
PROMPTS = {
    "es": {
        "chat": (
            "Eres MUSA — Museum Understanding & Storytelling Agent — el carismático guía holográfico "
            "del museo HART (Holographic Art). El visitante te acaba de hablar de forma casual.\n\n"
            "CONTEXTO DE LA SALA ACTUAL:\n"
            "  Obras presentes:\n{room_layout}\n"
            "  Obra principal: {main_artwork}\n\n"
            "Responde de forma cálida, amigable y natural, como si fueras el anfitrión del museo. "
            "Si el visitante pregunta qué hay en la sala o qué puede visitar, usa el contexto anterior. "
            "Sé breve — máximo 2-3 frases.\n"
            "Mensaje del visitante: {message}"
        ),
        "questions": (
            "Eres un actualizador de preguntas de trivia para un museo de arte.\n"
            "El visitante se encuentra en el nivel '{level}' del artista '{artist_name}'.\n"
            "Se te dan las preguntas ACTUALES de ese nivel y lo que MUSA acaba de explicar.\n\n"
            "Tu tarea: enriquecer o reemplazar solo las preguntas que no reflejen los temas "
            "que MUSA acaba de explicar. Mantén las que sigan siendo válidas. Devuelve siempre 4 preguntas.\n\n"
            "Taxonomía de Bloom por nivel:\n"
            "  beginner → Recordar (¿Cuál...?, ¿Dónde...?, ¿En qué año...?)\n"
            "  hard     → Comprender/Aplicar (¿Por qué...?, ¿Qué relación...?)\n"
            "  expert   → Analizar/Evaluar (¿Qué implica...?, ¿Qué interpretación...?)\n\n"
            "Centra los distractores en similitud semántica con la respuesta correcta.\n\n"
            "FORMATO OBLIGATORIO:\n"
            "  - Pregunta: máximo 12 palabras.\n"
            "  - Cada opción: máximo 4 palabras. Solo el dato clave, sin frases completas.\n"
            "  - Las 4 opciones en orden ALEATORIO — la correcta NO siempre la primera.\n\n"
            "Artista: {artist_name}\n"
            "Nivel activo: {level}\n"
            "Lo que MUSA acaba de explicar: {musa_response}\n"
            "Preguntas actuales:\n{current_questions}\n\n"
            "Devuelve el set completo de 4 preguntas en el mismo formato JSON."
        ),
        "sys_msg": (
            "Eres MUSA — Museum Understanding & Storytelling Agent — el carismático guía holográfico "
            "del museo HART (Holographic Art). Respondes en español con un tono cálido y natural, "
            "como un amigo muy culto, nunca como un libro de texto.\n\n"
            "ARTISTAS DE LA EXPOSICIÓN (los únicos válidos):\n"
            "  velazquez, vangogh, picasso, dali, monet, kandinsky, munch, koons\n\n"
            "CONTEXTO DE LA VISITA:\n"
            "  Sala activa: {artist_name}\n"
            "  Nivel activo: {level}\n\n"
            "SALA ACTUAL — OBRAS PRESENTES:\n"
            "{room_layout}\n\n"
            "  Obra principal (usa esta como referencia si la pregunta es ambigua): {main_artwork}\n\n"
            "REGLAS ESPACIALES — aplica ANTES de cualquier otra decisión:\n"
            "  1. Si el visitante usa referencias espaciales ('el de la derecha', 'el del fondo',\n"
            "     'la escultura del centro', 'ese cuadro', 'el primero', etc.), resuélvelas\n"
            "     usando el layout anterior para identificar la obra concreta.\n"
            "  2. Si la referencia es ambigua, usa la obra principal sin mencionarlo ni dudarlo.\n"
            "  3. Si la pregunta es general sobre el artista (biografía, técnica, época),\n"
            "     responde sobre el artista sin anclar a ninguna obra concreta.\n"
            "  4. Nunca muestres inseguridad sobre qué obra estás describiendo.\n\n"
            "DETECCIÓN DEL ARTISTA — aplica DESPUÉS de resolver la referencia espacial:\n"
            "  1. El mensaje puede venir de voz con errores fonéticos graves.\n"
            "     Ejemplos: 'Bangkok' → vangogh, 'Monee' → monet, 'Picas' → picasso,\n"
            "     'Can dins' → kandinsky, 'Munk' → munch, 'Velaskes' → velazquez, 'Tali' → dali.\n"
            "  2. Si identificas un artista en el mensaje, USA ESE artista en todas las herramientas.\n"
            "  3. Si no hay artista en el mensaje, usa la sala activa: '{artist_name}'.\n"
            "  4. Si la sala activa es 'unknown', usa 'picasso' como fallback.\n\n"
            "HERRAMIENTAS DISPONIBLES:\n"
            "  - retrieve_context(artist_name, query): busca información en la base de conocimiento.\n"
            "    Llámala cuando necesites datos factuales sobre un artista, obra, técnica o\n"
            "    dato biográfico. No la llames si la respuesta ya está en el layout o en el historial.\n"
            "  - speak(text): convierte tu respuesta en audio para el visitante.\n"
            "    SIEMPRE debes llamarla con el texto final que quieres que escuche el visitante.\n"
            "  - get_questions(artist_name, language): devuelve las preguntas actuales en disco.\n"
            "    Llámala siempre después de speak.\n"
            "  - update_questions(artist_name, level, language, musa_response, current_questions):\n"
            "    llámala SOLO si lo que acabas de explicar introduce temas no cubiertos aún.\n"
            "    Pásale exactamente el texto que enviaste a speak como musa_response.\n\n"
            "FLUJO POR CADA TURNO:\n"
            "  1. Resuelve referencias espaciales si las hay.\n"
            "  2. Si necesitas datos factuales → llama retrieve_context.\n"
            "  3. Elabora tu respuesta (3-4 frases máximo, prosa fluida, sin listas).\n"
            "  4. Llama speak(text) con esa respuesta.\n"
            "  5. Llama get_questions(artist_name, language).\n"
            "  6. Si lo explicado introduce temas nuevos → llama update_questions.\n\n"
            "NUNCA termines un turno sin haber llamado a speak. NUNCA pidas aclaración.\n"
            "Si el nombre del artista u obra tiene errores ortográficos, no se lo menciones al visitante.\n"
        ),
        "kokoro_lang": "es",
    },
    "en": {
        "chat": (
            "You are MUSA — Museum Understanding & Storytelling Agent — the charismatic holographic "
            "guide at the HART (Holographic Art) museum. The visitor has just spoken to you casually.\n\n"
            "CURRENT ROOM CONTEXT:\n"
            "  Works present:\n{room_layout}\n"
            "  Main artwork: {main_artwork}\n\n"
            "Respond in a warm, friendly and natural way, as if you are the museum's host. "
            "If the visitor asks what is in the room or what to visit, use the context above. "
            "Keep it brief — 2-3 sentences maximum.\n"
            "Visitor message: {message}"
        ),
        "questions": (
            "You are a trivia question updater for an art museum.\n"
            "The visitor is on level '{level}' for artist '{artist_name}'.\n"
            "You are given the CURRENT questions for that level and what MUSA just explained.\n\n"
            "Your task: enrich or replace only questions that don't reflect the topics MUSA just "
            "explained. Keep valid ones. Always return 4 questions.\n\n"
            "Bloom's Taxonomy by level:\n"
            "  beginner → Remember (Which...?, Where...?, In what year...?)\n"
            "  hard     → Understand/Apply (Why...?, How does X relate to Y...?)\n"
            "  expert   → Analyze/Evaluate (What does X imply about Y...?)\n\n"
            "Focus distractors on semantic similarity to the correct answer.\n\n"
            "MANDATORY FORMAT:\n"
            "  - Question: 12 words maximum.\n"
            "  - Each option: 4 words maximum. Key fact only, no full sentences.\n"
            "  - The 4 options in RANDOM order — the correct one must NOT always be first.\n\n"
            "Artist: {artist_name}\n"
            "Active level: {level}\n"
            "What MUSA just explained: {musa_response}\n"
            "Current questions:\n{current_questions}\n\n"
            "Return the complete set of 4 questions in the same JSON format."
        ),
        "sys_msg": (
            "You are MUSA — Museum Understanding & Storytelling Agent — the charismatic holographic "
            "guide at the HART (Holographic Art) museum. You respond in English with a warm, natural "
            "tone — like a knowledgeable friend, never a textbook.\n\n"
            "EXHIBITED ARTISTS (the only valid ones):\n"
            "  velazquez, vangogh, picasso, dali, monet, kandinsky, munch, koons\n\n"
            "VISIT CONTEXT:\n"
            "  Active room: {artist_name}\n"
            "  Active level: {level}\n\n"
            "CURRENT ROOM — WORKS PRESENT:\n"
            "{room_layout}\n\n"
            "  Main artwork (use as reference if the question is ambiguous): {main_artwork}\n\n"
            "SPATIAL RULES — apply BEFORE any other decision:\n"
            "  1. If the visitor uses spatial references ('the one on the right', 'the one at the back',\n"
            "     'the sculpture in the center', 'that painting', 'the first one', etc.), resolve them\n"
            "     using the layout above to identify the specific artwork.\n"
            "  2. If the reference is ambiguous, use the main artwork without mentioning it or hesitating.\n"
            "  3. If the question is general about the artist (biography, technique, period),\n"
            "     answer about the artist without anchoring to any specific artwork.\n"
            "  4. Never show uncertainty about which artwork you are describing.\n\n"
            "ARTIST DETECTION — apply AFTER resolving any spatial reference:\n"
            "  1. The message may come from voice transcription with severe phonetic errors.\n"
            "     Examples: 'Bangkok' → vangogh, 'Monee' → monet, 'Picas' → picasso,\n"
            "     'Kandins' → kandinsky, 'Munk' → munch, 'Velaskes' → velazquez.\n"
            "  2. If you identify an artist in the message, USE THAT artist in all tools.\n"
            "  3. If no artist is mentioned, use the active room: '{artist_name}'.\n"
            "  4. If the active room is 'unknown', use 'picasso' as fallback.\n\n"
            "AVAILABLE TOOLS:\n"
            "  - retrieve_context(artist_name, query): searches the knowledge base.\n"
            "    Call it when you need factual data about an artist, artwork, technique or biography.\n"
            "    Do not call it if the answer is already in the layout or conversation history.\n"
            "  - speak(text): converts your response into audio for the visitor.\n"
            "    You MUST always call it with the final text you want the visitor to hear.\n"
            "  - get_questions(artist_name, language): returns current questions on disk.\n"
            "    Always call it after speak.\n"
            "  - update_questions(artist_name, level, language, musa_response, current_questions):\n"
            "    call it ONLY if what you just explained introduces topics not yet covered.\n"
            "    Pass exactly the text you sent to speak as musa_response.\n\n"
            "FLOW FOR EACH TURN:\n"
            "  1. Resolve any spatial references.\n"
            "  2. If you need factual data → call retrieve_context.\n"
            "  3. Compose your response (3-4 sentences max, flowing prose, no lists).\n"
            "  4. Call speak(text) with that response.\n"
            "  5. Call get_questions(artist_name, language).\n"
            "  6. If what you explained introduces new topics → call update_questions.\n\n"
            "NEVER end a turn without having called speak. NEVER ask for clarification.\n"
            "If the artist name or artwork has spelling errors, do not point it out to the visitor.\n"
            "All your answers must be written in plain text without any formatting, lists, or special characters, suitable for TTS synthesis."
        ),
        "kokoro_lang": "en-us",
    },
}

# ---------------------------------------------------------------------------
# Classifier prompt
# ---------------------------------------------------------------------------
CLASSIFIER_PROMPT = """You are an intent classifier for a museum audio guide system.
Classify the visitor's message into exactly one of two categories:

  AUDIOGUIDE  — The message is a question or comment about art, artists, paintings,
                art movements, or anything that a museum guide should answer.
                Also classify as AUDIOGUIDE if the message is likely a
                transcription error of an artist name. Artist names are:
                Picasso, Dali, Monet, Kandinsky, Munch, Koons, Velazquez, Van Gogh.

  CHAT        — The message is purely conversational with no art-related content
                whatsoever. Examples: greetings, small talk, questions about the
                building/facilities, complaints about temperature, personal comments.

Rules:
- If in doubt, choose AUDIOGUIDE.
- Respond with exactly one word: AUDIOGUIDE or CHAT. Nothing else.

Visitor message: {message}"""

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


def _load_questions_from_disk(artist_name: str, lang: str, level: str) -> dict:
    path = os.path.join(BASE_DIR, "Resources", "Questions", lang, artist_name, level, "questions.json")
    if os.path.exists(path):
        try:
            with open(path, "r", encoding="utf-8") as f:
                return json.load(f)
        except Exception as e:
            logger.warning(f"Could not load questions [{artist_name}/{level}]: {e}")
    return {}


def _save_questions_to_disk(artist_name: str, lang: str, level: str, data: dict) -> None:
    cache_dir = os.path.join(BASE_DIR, "Resources", "Questions", lang, artist_name, level)
    os.makedirs(cache_dir, exist_ok=True)
    with open(os.path.join(cache_dir, "questions.json"), "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
    logger.info(f"Questions saved: {artist_name}/{level}")

    # Generate one WAV per question in the same folder
    kokoro_lang = PROMPTS.get(lang, PROMPTS["en"])["kokoro_lang"]
    questions = data.get("trivia", [])
    if questions:
        _generate_question_audio(questions, cache_dir, kokoro_lang)
    else:
        logger.warning(f"No trivia items in data for {artist_name}/{level} — no audio generated")


def _get_room_context(artist_name: str) -> tuple[str, str]:
    """Returns (layout_text, main_artwork) for the given artist room."""
    room = ROOM_LAYOUTS.get(artist_name, {})
    return room.get("layout", ""), room.get("main_artwork", "")


def generate_audiofile(text: str, kokoro_lang: str = "en-us") -> str:
    """Synthesizes text to AUDIOGUIDE_FILE and writes FLAG_DONE."""
    voice_map = {"es": "ef_dora", "en-us": "af_bella"}
    voice = voice_map.get(kokoro_lang, "af_bella")
    print(f"[TTS] lang={kokoro_lang} voice={voice}", flush=True)
    samples, sample_rate = kokoro.create(text, voice=voice, speed=1.0, lang=kokoro_lang)
    sf.write(AUDIOGUIDE_FILE, samples, sample_rate)
    open(FLAG_DONE, "w").close()
    print(f"[AUDIO] Saved: {AUDIOGUIDE_FILE}", flush=True)
    return AUDIOGUIDE_FILE


def play_searching_audio(kokoro_lang: str) -> None:
    """Writes searching.flag so Unity knows whether to play the searching audio."""
    src = SEARCHING_AUDIO.get(kokoro_lang) or SEARCHING_AUDIO.get("en-us")
    if src and os.path.exists(src):
        with open(FLAG_SEARCHING, "w") as f:
            f.write("use=1")
        print(f"[SEARCHING AUDIO] Flag written use=1 for: {src}", flush=True)
    else:
        with open(FLAG_SEARCHING, "w") as f:
            f.write("use=0")
        logger.warning(f"Searching audio not found for lang={kokoro_lang}, flag written with use=0.")

def _generate_question_audio(questions: list, cache_dir: str, kokoro_lang: str) -> None:
    """Synthesizes each question text as a separate WAV file (1.wav, 2.wav, ...)."""
    voice_map = {"es": "ef_dora", "en-us": "af_bella"}
    voice = voice_map.get(kokoro_lang, "af_bella")
    for i, item in enumerate(questions, start=1):
        text = item.get("question", "").strip()
        if not text:
            logger.warning(f"Question {i} has empty text — skipping audio")
            continue
        try:
            samples, sample_rate = kokoro.create(text, voice=voice, speed=1.0, lang=kokoro_lang)
            wav_path = os.path.join(cache_dir, f"{i}.wav")
            sf.write(wav_path, samples, sample_rate)
            print(f"[QUESTION AUDIO] {i}.wav saved", flush=True)
            logger.info(f"Question audio saved: {wav_path}")
        except Exception as e:
            logger.error(f"Error generating audio for question {i}: {e}")

# ---------------------------------------------------------------------------
# Intent classifier
# ---------------------------------------------------------------------------

def classify_intent(message: str) -> str:
    try:
        prompt = CLASSIFIER_PROMPT.format(message=message)
        response = LLM_CLASS.invoke(prompt)
        label = response.content.strip().upper()
        if label not in ("AUDIOGUIDE", "CHAT"):
            logger.warning(f"Classifier returned unexpected label '{label}', defaulting to AUDIOGUIDE")
            label = "AUDIOGUIDE"
        logger.info(f"[CLASSIFIER] → {label}")
        return label
    except Exception as e:
        logger.error(f"Classifier error: {e} — defaulting to AUDIOGUIDE")
        return "AUDIOGUIDE"

# ---------------------------------------------------------------------------
# Conversational flow
# ---------------------------------------------------------------------------

def handle_chat(message: str, lang: str, session_messages: list,
                room_layout: str = "", main_artwork: str = "") -> str:
    p = PROMPTS[lang]
    prompt = p["chat"].format(
        message=message,
        room_layout=room_layout if room_layout else "  No disponible.",
        main_artwork=main_artwork if main_artwork else "No disponible.",
    )
    history = session_messages[-6:] if len(session_messages) > 6 else session_messages
    response = LLM_CHAT.invoke(history + [HumanMessage(content=prompt)])
    text = response.content.strip()
    print(f"[CHAT RESPONSE] {text}", flush=True)
    logger.info(f"[CHAT RESPONSE] {text}")
    generate_audiofile(text, kokoro_lang=p["kokoro_lang"])
    return text

# ---------------------------------------------------------------------------
# Tools
# ---------------------------------------------------------------------------

@tool
def retrieve_context(artist_name: str, query: str) -> str:
    """
    Searches the knowledge base for factual information about an artist or artwork.
    Call this when you need biographical data, historical context, technique details,
    or any factual content about an artist or specific artwork.
    Do NOT call this for questions about the physical layout of the room.

    Args:
        artist_name: Artist name in lowercase without spaces (e.g. 'velazquez', 'vangogh')
        query: Search query in Spanish describing what information is needed
    """
    print(f"[TOOL] retrieve_context | artist={artist_name}", flush=True)
    return _retrieve_from_pinecone(artist_name, query)


@tool
def speak(text: str) -> str:
    """
    Converts the given text to audio and sends it to the visitor.
    You MUST call this once per turn with your complete response.
    The text must be flowing prose, 3-4 sentences maximum, no lists or headers.

    Args:
        text: The response text to speak aloud to the visitor
    """
    print(f"[TOOL] speak | text={text[:80]}...", flush=True)
    logger.info(f"[SPEAK] {text}")
    # kokoro_lang is resolved at graph build time via closure
    generate_audiofile(text, kokoro_lang=_current_kokoro_lang)
    return text


@tool
def get_questions(artist_name: str, language: str = "en") -> str:
    """
    Returns the current trivia questions on disk for all levels of the given artist.
    Call this after speak, every turn, to decide whether update_questions is needed.

    Args:
        artist_name: Artist name in lowercase without spaces
        language: 'es' or 'en'
    """
    print(f"[TOOL] get_questions | artist={artist_name}", flush=True)
    all_questions = {}
    for level in ["beginner", "hard", "expert"]:
        data = _load_questions_from_disk(artist_name, language, level)
        all_questions[level] = data if data else "No questions yet."
    logger.info(f"get_questions: loaded for {artist_name}")
    return json.dumps(all_questions, ensure_ascii=False, indent=2)


@tool
def update_questions(
    artist_name: str,
    level: str,
    language: str,
    musa_response: str,
    current_questions: str,
) -> str:
    """
    Updates trivia questions based solely on what MUSA just explained to the visitor.
    Call this ONLY if the explanation introduced topics not yet covered by the current questions.

    Args:
        artist_name: Artist name in lowercase without spaces
        level: Active difficulty level ('beginner', 'hard' or 'expert')
        language: 'es' or 'en'
        musa_response: The exact text MUSA just spoke to the visitor (from speak)
        current_questions: The current questions JSON string (from get_questions)
    """
    print(f"[TOOL] update_questions | artist={artist_name} level={level}", flush=True)
    lang = language if language in PROMPTS else "en"
    p = PROMPTS[lang]

    valid_levels = ["beginner", "hard", "expert"]
    if level not in valid_levels:
        logger.warning(f"Unknown level '{level}', defaulting to 'hard'")
        level = "hard"

    prompt = p["questions"].format(
        artist_name=artist_name,
        level=level,
        musa_response=musa_response,
        current_questions=current_questions,
    )

    structured_llm = LLM.with_structured_output(QuestionBatch)
    try:
        result: QuestionBatch = structured_llm.invoke(prompt)
        _save_questions_to_disk(artist_name, lang, level, result.model_dump())
        msg = f"Questions updated — {artist_name}/{level}: {len(result.trivia)} questions"
    except Exception as e:
        logger.error(f"Error updating questions [{level}]: {e}")
        msg = f"Error updating questions [{level}]: {e}"

    logger.info(msg)
    return msg

# ---------------------------------------------------------------------------
# LangGraph
# ---------------------------------------------------------------------------

# Module-level variable used by the speak tool closure
_current_kokoro_lang: str = "en-us"

def _build_audioguide_graph(
    language: str = "en",
    artist_name: str = "unknown",
    level: str = "beginner",
    room_layout: str = "",
    main_artwork: str = "",
):
    global _current_kokoro_lang
    lang = language if language in PROMPTS else "en"
    _current_kokoro_lang = PROMPTS[lang]["kokoro_lang"]

    sys_content = PROMPTS[lang]["sys_msg"].format(
        artist_name=artist_name,
        level=level,
        room_layout=room_layout if room_layout else "  No hay información de layout para esta sala.",
        main_artwork=main_artwork if main_artwork else artist_name,
    )
    sys_msg = SystemMessage(content=sys_content)

    tools = [retrieve_context, speak, get_questions, update_questions]
    llm_with_tools = LLM.bind_tools(tools)

    def assistant(state: MessagesState):
        open(FLAG_BUSY, "w").close()
        response = llm_with_tools.invoke([sys_msg] + state["messages"])
        if hasattr(response, "tool_calls") and response.tool_calls:
            for tc in response.tool_calls:
                logger.info(f"[ORCHESTRATOR] → tool={tc['name']} args={list(tc['args'].keys())}")
        else:
            logger.info(f"[ORCHESTRATOR] → direct: {response.content[:100]}")
        return {"messages": [response]}

    def should_continue(state: MessagesState) -> str:
        last = state["messages"][-1]
        if hasattr(last, "tool_calls") and last.tool_calls:
            return "tools"
        return END

    builder = StateGraph(MessagesState)
    builder.add_node("assistant", assistant)
    builder.add_node("tools", ToolNode(tools))

    builder.add_edge(START, "assistant")
    builder.add_conditional_edges("assistant", should_continue, {
        "tools": "tools",
        END: END,
    })
    builder.add_edge("tools", "assistant")

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

    # Init LLMs
    logger.info("Initializing LLMs...")
    LLM       = ChatOpenRouter(model="~google/gemini-flash-latest", temperature=0.8)
    LLM_CHAT  = ChatOpenRouter(model="~google/gemini-flash-latest", temperature=0.8)
    LLM_CLASS = ChatOpenRouter(model="~google/gemini-flash-latest", temperature=0.0)
    logger.info("LLMs ready.")

    # Language from CLI arg, fallback to env var, then "en"
    if len(sys.argv) > 1 and sys.argv[1] in PROMPTS:
        LANG = sys.argv[1]
        logger.info(f"Language from CLI arg: {LANG}")
    else:
        LANG = os.environ.get("AGENT_LANG", "en")
        logger.info(f"Language from env/default: {LANG}")
    logger.info(f"Agent language: {LANG}")

    # Persistent state across turns
    session_messages: list    = []
    current_artist: str       = "unknown"
    current_level:  str       = "beginner"
    current_layout: str       = ""
    current_main_artwork: str = ""

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

                message, artist, level, msg_lang = parse_input(raw)
                if artist:
                    current_artist = artist
                    current_layout, current_main_artwork = _get_room_context(current_artist)
                    logger.info(f"Room context loaded for '{current_artist}' — main='{current_main_artwork}'")
                if level:
                    current_level = level
                if msg_lang and msg_lang in PROMPTS:
                    LANG = msg_lang
                    logger.info(f"Language updated from message tag: {LANG}")

                kokoro_lang = PROMPTS[LANG]["kokoro_lang"]
                logger.info(
                    f"Turn {len(session_messages)+1} | artist={current_artist} "
                    f"level={current_level} lang={LANG} | {message}"
                )

                intent = classify_intent(message)

                if intent == "CHAT":
                    logger.info("[FLOW] CHAT selected")
                    open(FLAG_BUSY, "w").close()
                    chat_response = handle_chat(message, LANG, session_messages, room_layout=current_layout, main_artwork=current_main_artwork,)
                    session_messages.append(HumanMessage(content=message))
                    session_messages.append(AIMessage(content=chat_response))
                    print(f"[RESPONSE] {chat_response}", flush=True)

                else:
                    logger.info("[FLOW] AUDIOGUIDE selected")
                    play_searching_audio(kokoro_lang)

                    graph = _build_audioguide_graph(
                        LANG,
                        artist_name=current_artist,
                        level=current_level,
                        room_layout=current_layout,
                        main_artwork=current_main_artwork,
                    )

                    session_messages.append(HumanMessage(content=message))
                    result = graph.invoke({"messages": session_messages})
                    session_messages = result["messages"]

                    last_msg = result["messages"][-1]
                    print(f"[RESPONSE] {last_msg.content}", flush=True)
                    logger.info(f"Response: {last_msg.content}")

                    # Safety net: if speak was never called
                    if not os.path.exists(FLAG_DONE):
                        text_to_speak = last_msg.content.strip()
                        if text_to_speak:
                            logger.warning("speak tool never called — TTS fallback")
                            generate_audiofile(text_to_speak, kokoro_lang=kokoro_lang)
                        else:
                            open(FLAG_DONE, "w").close()
                            logger.warning("Empty response — done.flag written with no audio.")

        except Exception as e:
            logger.error(f"Error processing input: {e}", exc_info=True)
            for _f in [FLAG_BUSY, INPUT_FILE]:
                if os.path.exists(_f):
                    os.remove(_f)
            open(FLAG_DONE, "w").close()

        time.sleep(0.5)
