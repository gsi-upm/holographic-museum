"""
tts_test.py — Prueba de TTS via OpenRouter
=========================================
Compara modelos TTS de OpenRouter para el proyecto HART/MUSA.
Genera un WAV/MP3 por cada modelo y muestra el coste estimado.

Uso:
    python tts_test.py                          # ES, todos los modelos, texto por defecto
    python tts_test.py --lang en                # EN
    python tts_test.py --lang es --model kokoro # Solo Kokoro
    python tts_test.py --text "Hola, soy MUSA"  # Texto personalizado

Requiere:
    pip install requests python-dotenv

Variables de entorno (.env o env):
    OPENROUTER_API_KEY=sk-or-...
"""

import os
import sys
import argparse
import requests
import time
import dotenv

# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------

dotenv.load_dotenv()
API_KEY  = os.environ.get("OPENROUTER_API_KEY") or os.environ.get("OPENROUTER_KEY")
BASE_URL = "https://openrouter.ai/api/v1/audio/speech"

# ---------------------------------------------------------------------------
# Modelos disponibles
# ---------------------------------------------------------------------------
# Cada entrada: model_id, alias, voices_es, voices_en, notas
MODELS = {
    "kokoro": {
        "id":          "hexgrad/kokoro-82m",
        "description": "Kokoro 82M — el mismo que usas en local, muy barato",
        "price_note":  "$0.62 / 1M chars",
        "voices": {
            "es": ["ef_dora", "em_alex", "em_santa"],   # 1F 2M en español
            "en": ["af_bella", "af_heart", "am_adam"],  # voces en inglés
        },
        "default_voice": {"es": "ef_dora", "en": "af_bella"},
        "supports_instructions": False,
    },
    "openai": {
        "id":          "openai/gpt-4o-mini-tts-2025-12-15",
        "description": "GPT-4o Mini TTS — admite instrucciones de estilo",
        "price_note":  "$0.60 / 1M tokens",
        "voices": {
            "es": ["coral", "shimmer", "nova", "alloy", "sage"],
            "en": ["coral", "shimmer", "nova", "alloy", "sage", "echo", "fable", "onyx", "ash"],
        },
        "default_voice": {"es": "coral", "en": "coral"},
        "supports_instructions": True,
        "instructions": {
            "es": (
                "Eres MUSA, una guía holográfica de museo. "
                "Habla en un tono cálido, cercano y entusiasta. "
                "Articula claramente, con pausas naturales. Velocidad moderada."
            ),
            "en": (
                "You are MUSA, a holographic museum guide. "
                "Speak in a warm, engaging, and enthusiastic tone. "
                "Articulate clearly with natural pauses. Moderate pace."
            ),
        },
    },
    "grok": {
        "id":          "x-ai/grok-voice-tts-1.0",
        "description": "Grok Voice TTS — tags inline para énfasis y pausa",
        "price_note":  "$15 / 1M tokens (más caro)",
        "voices": {
            "es": ["Eve", "Ara", "Rex", "Sal", "Leo"],
            "en": ["Eve", "Ara", "Rex", "Sal", "Leo"],
        },
        "default_voice": {"es": "Eve", "en": "Eve"},
        "supports_instructions": False,
    },
    "gemini": {
        "id":          "google/gemini-3.1-flash-tts-preview",
        "description": "Gemini 3.1 Flash TTS — 70+ idiomas, 200+ emotion tags",
        "price_note":  "$1 input + $20 output / 1M tokens",
        "voices": {
            "es": ["Aoede", "Charon", "Fenrir", "Kore", "Leda"],
            "en": ["Aoede", "Charon", "Fenrir", "Kore", "Leda"],
        },
        "default_voice": {"es": "Aoede", "en": "Aoede"},
        "supports_instructions": False,
    },
}

# ---------------------------------------------------------------------------
# Textos de prueba por idioma — estilo MUSA
# ---------------------------------------------------------------------------
DEFAULT_TEXT = {
    "es": (
        "Bienvenido al museo HART. Soy MUSA, tu guía holográfica. "
        "Hoy exploraremos el fascinante mundo de Pablo Picasso — "
        "un artista que no solo pintó cuadros, sino que reinventó "
        "la manera en que el mundo ve el arte."
    ),
    "en": (
        "Welcome to the HART museum. I'm MUSA, your holographic guide. "
        "Today we'll explore the fascinating world of Pablo Picasso — "
        "an artist who didn't just paint pictures, but reinvented "
        "the way the world sees art."
    ),
}

# ---------------------------------------------------------------------------
# Core: llamada a la API
# ---------------------------------------------------------------------------

def call_tts(model_key: str, text: str, lang: str, voice: str = None) -> tuple[bytes, dict]:
    """
    Llama al endpoint TTS de OpenRouter.
    Devuelve (audio_bytes, info_dict).
    """
    cfg   = MODELS[model_key]
    model = cfg["id"]
    v     = voice or cfg["default_voice"].get(lang, "alloy")

    payload = {
        "model":           model,
        "input":           text,
        "instructions":    """Voice Affect: Calm, composed, and reassuring; Con acento pensinsular español neutro.""",
        "voice":           v,
        "response_format": "mp3",
    }

    # GPT-4o Mini TTS soporta instrucciones de estilo
    if cfg.get("supports_instructions") and "instructions" in cfg:
        instr = cfg["instructions"].get(lang, "")
        if instr:
            payload["provider"] = {
                "openai": {"instructions": instr}
            }

    headers = {
        "Authorization": f"Bearer {API_KEY}",
        "Content-Type":  "application/json",
    }

    t0 = time.time()
    r  = requests.post(BASE_URL, json=payload, headers=headers, timeout=60)
    elapsed = time.time() - t0

    info = {
        "model":   model_key,
        "model_id": model,
        "voice":   v,
        "lang":    lang,
        "chars":   len(text),
        "elapsed": round(elapsed, 2),
        "status":  r.status_code,
        "gen_id":  r.headers.get("X-Generation-Id", "—"),
        "price":   cfg["price_note"],
    }

    if r.status_code != 200:
        print(f"  ✗ Error {r.status_code}: {r.text[:300]}")
        return b"", info

    return r.content, info


def estimate_cost(model_key: str, char_count: int) -> str:
    """Estimación muy aproximada del coste. Solo orientativa."""
    # Los precios por carácter son aproximados; la API cobra por token/char según modelo
    price_per_million = {
        "kokoro": 0.62,
        "openai": 0.60,
        "grok":   15.00,
        "gemini": 1.00,  # solo input
    }
    cpp = price_per_million.get(model_key, 1.0)
    cost = (char_count / 1_000_000) * cpp
    return f"${cost:.6f}"


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def parse_args():
    p = argparse.ArgumentParser(description="Prueba de TTS via OpenRouter para HART/MUSA")
    p.add_argument("--lang",   choices=["es", "en"], default="es",
                   help="Idioma del texto y voz (es / en). Default: es")
    p.add_argument("--model",  choices=list(MODELS.keys()) + ["all"], default="all",
                   help="Modelo a probar. Default: all")
    p.add_argument("--text",   type=str, default=None,
                   help="Texto personalizado. Si no se pasa, usa el texto de prueba de MUSA.")
    p.add_argument("--voice",  type=str, default=None,
                   help="Voz concreta (opcional). Si no se pasa, usa la voz por defecto del modelo.")
    p.add_argument("--list-voices", action="store_true",
                   help="Lista las voces disponibles por modelo y sale.")
    p.add_argument("--outdir", type=str, default=".",
                   help="Directorio de salida para los MP3. Default: directorio actual.")
    return p.parse_args()


def list_voices():
    print("\n Voces disponibles por modelo:\n")
    for key, cfg in MODELS.items():
        print(f"  [{key}] {cfg['id']}  —  {cfg['price_note']}")
        print(f"    ES: {', '.join(cfg['voices']['es'])}")
        print(f"    EN: {', '.join(cfg['voices']['en'])}")
        if cfg.get("supports_instructions"):
            print("    ✓ Admite instructions de estilo (parámetro extra)")
        print()


def run(args):
    if not API_KEY:
        print("✗ No se encontró OPENROUTER_API_KEY en el entorno o en .env")
        sys.exit(1)

    text = args.text or DEFAULT_TEXT[args.lang]
    models_to_run = list(MODELS.keys()) if args.model == "all" else [args.model]

    os.makedirs(args.outdir, exist_ok=True)

    print(f"\n{'='*60}")
    print(f"  HART / MUSA — Prueba TTS via OpenRouter")
    print(f"  Idioma : {args.lang.upper()}")
    print(f"  Modelos: {', '.join(models_to_run)}")
    print(f"  Chars  : {len(text)}")
    print(f"  Texto  : {text[:80]}{'...' if len(text) > 80 else ''}")
    print(f"{'='*60}\n")

    results = []

    for mkey in models_to_run:
        cfg = MODELS[mkey]
        print(f"▶ [{mkey}] {cfg['id']}")
        print(f"  {cfg['description']}  |  {cfg['price_note']}")

        audio_bytes, info = call_tts(mkey, text, args.lang, args.voice)

        if audio_bytes:
            fname = os.path.join(args.outdir, f"musa_{mkey}_{args.lang}.mp3")
            with open(fname, "wb") as f:
                f.write(audio_bytes)
            size_kb = len(audio_bytes) / 1024
            cost_est = estimate_cost(mkey, len(text))
            print(f"  ✓ Guardado: {fname}  ({size_kb:.1f} KB)")
            print(f"  ⏱ {info['elapsed']}s  |  Voz: {info['voice']}  |  Coste est.: {cost_est}  |  Gen ID: {info['gen_id']}")
            info["output_file"] = fname
            info["size_kb"] = round(size_kb, 1)
            info["cost_est"] = cost_est
        else:
            print(f"  ✗ Sin audio — ver error arriba")

        results.append(info)
        print()

    # Resumen
    print(f"\n{'='*60}")
    print("  RESUMEN")
    print(f"{'='*60}")
    print(f"  {'Modelo':<10}  {'Voz':<12}  {'Tiempo':<8}  {'KB':<8}  {'Coste est.'}")
    print(f"  {'-'*55}")
    for r in results:
        kb  = r.get("size_kb", "—")
        est = r.get("cost_est", "—")
        print(f"  {r['model']:<10}  {r['voice']:<12}  {r['elapsed']}s{'':>4}  {str(kb):<8}  {est}")
    print()
    print("  Nota: el coste real lo ves en openrouter.ai/activity")
    print()


if __name__ == "__main__":
    args = parse_args()
    if args.list_voices:
        list_voices()
    else:
        run(args)