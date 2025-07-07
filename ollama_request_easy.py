import requests
import json
import os

ruta_response = os.path.join(os.path.dirname(__file__), "RESPONSE.JSON")
with open(ruta_response, "r", encoding="utf-8") as f:
    response_json_content = f.read()

# Variables editables
dbpedia_entry = "https://dbpedia.org/page/Wassily_Kandinsky"
dificultad = "no previous"

prompt = (
    "You are an MCQ expert maker. It is your job to create a quiz of 4 multiple choice questions with 4 options each "
    f"based on this dbPedia entry:{dbpedia_entry} for museum visitors that have {dificultad} art knowledge. "
    "Make sure all the questions are not repeted and check the questions to be conforming the dbPedia page as well. "
    "Make sure to format your response like RESPONSE.JSON below and use it as a guide. Ensure to make 4 MCQ with 4 options each.\n"
    "RESPONSE.JSON:\n"
    f"{response_json_content}"
)

response = requests.post(
    "http://localhost:11434/api/generate",
    json={"model": "llama3.2:1b", "prompt": prompt},
    stream=True
)

respuesta_completa = ""

for linea in response.iter_lines(decode_unicode=True):
    if not linea.strip():
        continue
    try:
        dato = json.loads(linea)
        chunk = dato.get("response", "")
        respuesta_completa += chunk
    except json.JSONDecodeError as e:
        print("Error procesando línea JSON:", linea)
        continue

print("Respuesta completa del modelo:\n")
print(respuesta_completa)

try:
    datos_json = json.loads(respuesta_completa)
    es_json = True
except json.JSONDecodeError:
    es_json = False

escritorio = os.path.join(os.path.expanduser("~"), "Escritorio")
nombre_archivo = "respuesta_llama.json" if es_json else "respuesta_llama.txt"
ruta_archivo = os.path.join(escritorio, nombre_archivo)

with open(ruta_archivo, "w", encoding="utf-8") as f:
    if es_json:
        json.dump(datos_json, f, indent=4, ensure_ascii=False)
    else:
        f.write(respuesta_completa)

print(f"Respuesta guardada en: {ruta_archivo}")
