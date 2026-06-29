"""
convert_mp3_to_wav.py
---------------------
Convierte todos los .mp3 dentro de una carpeta (recursivamente) a .wav
y borra los .mp3 originales.

Uso:
    python convert_mp3_to_wav.py                        # usa la ruta por defecto (ver abajo)
    python convert_mp3_to_wav.py /ruta/a/StreamingAssets

Requisito: ffmpeg instalado y en el PATH.
"""

import os
import sys
import subprocess

# ── Ruta raíz donde buscar .mp3 ──────────────────────────────────────────────
# Por defecto: la carpeta StreamingAssets junto a este script.
# Cámbiala aquí si la ejecutas desde otro sitio.
DEFAULT_ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)))

def convert_mp3_to_wav(root: str, delete_original: bool = True) -> None:
    mp3_files = []

    for dirpath, _, filenames in os.walk(root):
        for filename in filenames:
            if filename.lower().endswith(".mp3"):
                mp3_files.append(os.path.join(dirpath, filename))

    if not mp3_files:
        print(f"No se encontraron archivos .mp3 en: {root}")
        return

    print(f"Encontrados {len(mp3_files)} archivos .mp3. Convirtiendo...\n")

    ok = 0
    errors = 0

    for mp3_path in mp3_files:
        wav_path = os.path.splitext(mp3_path)[0] + ".wav"

        print(f"  → {os.path.relpath(mp3_path, root)}")

        result = subprocess.run(
            [
                "ffmpeg",
                "-y",               # sobreescribir si ya existe el .wav
                "-i", mp3_path,
                "-ar", "22050",     # sample rate compatible con Unity
                "-ac", "1",         # mono (más ligero, suficiente para voz)
                "-sample_fmt", "s16",
                wav_path,
            ],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.PIPE,
        )

        if result.returncode != 0:
            print(f"     ✗ ERROR: {result.stderr.decode('utf-8', errors='replace').strip()}")
            errors += 1
        else:
            print(f"     ✓ {os.path.relpath(wav_path, root)}")
            ok += 1
            if delete_original:
                os.remove(mp3_path)

    print(f"\nListo. {ok} convertidos, {errors} errores.")
    if delete_original and ok > 0:
        print("Los .mp3 originales han sido eliminados.")


if __name__ == "__main__":
    root = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_ROOT

    if not os.path.isdir(root):
        print(f"Error: la ruta no existe o no es una carpeta:\n  {root}")
        sys.exit(1)

    print(f"Raíz: {root}\n")
    convert_mp3_to_wav(root, delete_original=True)