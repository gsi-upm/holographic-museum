# HART — Setup del proyecto

Este repositorio no incluye algunos archivos pesados (modelos de IA, entorno
virtual de Python, claves de API). Sigue estos pasos después de clonar el
repo para que el proyecto funcione.

## 1. Clonar el repositorio

```bash
git clone https://github.com/gsi-upm/holographic-museum
cd holographic-museum
```

Si el repo usa Git LFS (para los assets de Camila, Whisper y `sunday in the
park.fbx`), asegúrate de tener LFS instalado antes de clonar:

```bash
git lfs install
git clone https://github.com/gsi-upm/holographic-museum/tree/main
```

## 2. Descargar el modelo de TTS (Kokoro)

Los archivos `kokoro-v1.0.onnx` y `voices-v1.0.bin` no están en el repo por
tamaño. Hay que descargarlos manualmente y colocarlos en:

```
Art Gallery/Assets/StreamingAssets/LangGraph/
```

| Archivo | Fuente | Tamaño aprox. |
|---|---|---|
| `kokoro-v1.0.onnx` | [Hugging Face — hexgrad/Kokoro-82M](https://huggingface.co/hexgrad/Kokoro-82M) | ~310 MB |
| `voices-v1.0.bin` | [Hugging Face — hexgrad/Kokoro-82M](https://huggingface.co/hexgrad/Kokoro-82M) | ~60 MB |

Estructura final esperada:

```
Art Gallery/Assets/StreamingAssets/LangGraph/
├── agent.py
├── kokoro-v1.0.onnx       ← descargado
├── voices-v1.0.bin        ← descargado
├── searching_es.mp3
├── searching_en.mp3
└── ...
```

> Si en el futuro se cambia de modelo TTS, recuerda que el embedding usado
> en Pinecone (`nomic-embed-text`, 768 dimensiones) es independiente del TTS
> y no necesita tocarse.

## 3. Configurar el entorno Python (venv)

El entorno virtual **no** va dentro de `Assets/StreamingAssets/`, porque
Unity genera archivos `.meta` que corrompen los paquetes instalados ahí.
Crea el venv en otra ubicación, por ejemplo en la raíz del proyecto Unity:

```bash
cd "Art Gallery"
python3 -m venv venv
source venv/bin/activate      # Windows: venv\Scripts\activate
pip install -r requirements.txt
```

`AgentManager.cs` busca el intérprete en este orden:

1. `<StreamingAssets>/LangGraph/venv/bin/python`
2. `<ProjectRoot>/venv/bin/python`
3. `/usr/bin/python3` o `/usr/local/bin/python3`

Si creas el venv en la raíz del proyecto (recomendado), no necesitas tocar
nada más en el script de Unity.

### Dependencias principales

```
langchain
langchain-ollama
langchain-openrouter
langchain-pinecone
langgraph
pinecone-client
kokoro-onnx
soundfile
python-dotenv
pydantic
```

(Ajusta `requirements.txt` si añades o quitas integraciones.)

## 4. Variables de entorno

Crea un archivo `.env` dentro de:

```
Art Gallery/Assets/StreamingAssets/LangGraph/.env
```

con el siguiente contenido:

```env
PINECONE_API_KEY=tu_clave_aqui
OPENROUTER_API_KEY=tu_clave_aqui
```

> Este archivo está en `.gitignore` y nunca debe subirse al repositorio.

## 5. Ollama (embeddings)

El agente usa Ollama en local para generar embeddings con
`nomic-embed-text` (768 dimensiones, debe coincidir con la dimensión del
índice de Pinecone).

```bash
ollama pull nomic-embed-text
ollama serve
```

Por defecto el agente espera Ollama en `http://localhost:11434`.

## 6. Modelo Whisper (transcripción de voz)

La carpeta `Assets/inference-engine-whisper-tiny/` sí está incluida en el
repo (vía Git LFS), pero requiere configuración manual en la escena de
Unity porque no se auto-registra:

1. Añade el componente `RunWhisper` al GameObject correspondiente.
2. Asigna los modelos `decoder_model.onnx` y `decoder_with_past_model.onnx`
   desde `Assets/inference-engine-whisper-tiny/models/` en el Inspector.
3. Verifica que `AgentManager.cs` tiene la referencia `whisperTranscriber`
   asignada en el Inspector.

Si tienes dudas sobre este paso, revisa la documentación de
[Unity Sentis](https://docs.unity3d.com/Packages/com.unity.sentis@latest)
o pregunta al equipo.

## 7. Primer arranque

1. Abre el proyecto en Unity.
2. Entra en Play Mode.
3. Selecciona idioma desde el menú — esto lanza `agent.py` automáticamente
   vía `AgentManager.StartPythonAgent()`.
4. Revisa `Art Gallery/Assets/StreamingAssets/LangGraph/agent.log` si algo
   no arranca: ahí quedan todos los logs del agente Python (stdout/stderr
   de Unity están silenciados a propósito).

## Resumen de archivos NO incluidos en el repo

| Archivo / carpeta | Por qué falta | Cómo conseguirlo |
|---|---|---|
| `kokoro-v1.0.onnx` | Tamaño (~310 MB) | Paso 2 de este documento |
| `voices-v1.0.bin` | Tamaño (~60 MB) | Paso 2 de este documento |
| `venv/` | No se versiona nunca | Paso 3 de este documento |
| `.env` | Contiene claves privadas | Paso 4 de este documento |
| `agent.log`, `*.flag`, `input.txt`, `audioguide.wav` | Generados en runtime | Se crean solos al ejecutar |
