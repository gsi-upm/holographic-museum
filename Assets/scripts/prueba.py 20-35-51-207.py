from langchain_ollama import ChatOllama
from langchain_openai import ChatOpenAI
from langchain_core.messages import HumanMessage
import json
import os
import logging

logger = logging.getLogger(__name__)

# llm = ChatOllama(
#     model="llama3.1:8b",
#     base_url="http://localhost:11434",
#     temperature=0,
# )
# llm = ChatOllama(
#     model="llama3.1:405b",
#     base_url="https://ollama.gsi.upm.es",
#     temperature=0,
# )

# try:
#     response = llm.invoke([HumanMessage(content="Di solo: hola")])
#     print("✅ Conexión OK:", response.content)
# except Exception as e:
#     print("❌ Error de conexión:", e)

questions = {
  "trivia": [
    {
      "question": "Which country was Pablo Picasso born in?",
      "options": [
        { "correct": True, "choice": "Spain" },
        { "correct": False, "choice": "France" },
        { "correct": False, "choice": "Italy" },
        { "correct": False, "choice": "Mexico" }
      ]
    },
    {
      "question": "Which famous anti-war painting was created by Picasso?",
      "options": [
        { "correct": False, "choice": "The Starry Night" },
        { "correct": True, "choice": "Guernica" },
        { "correct": False, "choice": "Las Meninas" },
        { "correct": False, "choice": "The Scream" }
      ]
    },
    {
      "question": "Picasso is most closely associated with which art movement?",
      "options": [
        { "correct": False, "choice": "Impressionism" },
        { "correct": False, "choice": "Surrealism" },
        { "correct": True, "choice": "Cubism" },
        { "correct": False, "choice": "Baroque" }
      ]
    },
    {
      "question": "What is one of Picasso's most famous paintings featuring distorted figures and suffering during war?",
      "options": [
        {"correct": False, "choice": "Water Lilies"},
        {"correct": False, "choice": "Girl with a Pearl Earring"},
        {"correct": True, "choice": "Guernica"},
        {"correct": False, "choice": "The Birth of Venus"}
      ]
    }
  ]
}

cache_dir = "../Resources/Questions/Picasso/beginner"
os.makedirs(cache_dir, exist_ok=True)
path = os.path.join(cache_dir, "questions.json")

with open(path, "w", encoding="utf-8") as f:
    json.dump(questions, f, ensure_ascii=False, indent=2)

logger.info("Guardadas %d preguntas en %s", len(questions["trivia"]), path)