import subprocess
import sys
import os

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
VENV_DIR = os.path.join(BASE_DIR, "..", "venv")  # una carpeta arriba, junto a Assets/

def get_venv_python():
    """Devuelve la ruta al intérprete dentro del venv según el OS."""
    if sys.platform == "win32":
        return os.path.join(VENV_DIR, "Scripts", "python.exe")
    return os.path.join(VENV_DIR, "bin", "python")

def create_venv():
    print(f"[bootstrap] Creating venv at: {VENV_DIR}", flush=True)
    subprocess.check_call([sys.executable, "-m", "venv", VENV_DIR])
    print("[bootstrap] venv created.", flush=True)

def install_requirements(python_exe):
    req = os.path.join(BASE_DIR, "requirements.txt")
    if not os.path.exists(req):
        print("[bootstrap] requirements.txt not found, skipping install.", flush=True)
        return
    print("[bootstrap] Installing requirements into venv...", flush=True)
    subprocess.check_call([python_exe, "-m", "pip", "install", "--upgrade", "pip"])
    subprocess.check_call([python_exe, "-m", "pip", "install", "-r", req])
    print("[bootstrap] Requirements installed.", flush=True)

if __name__ == "__main__":
    print(f"[bootstrap] System interpreter: {sys.executable}", flush=True)

    venv_python = get_venv_python()

    # 1. Crear venv si no existe
    if not os.path.exists(venv_python):
        create_venv()

    # 2. Instalar dependencias si no están (comprueba una librería clave)
    check = subprocess.run(
        [venv_python, "-c", "import langchain_ollama"],
        capture_output=True
    )
    if check.returncode != 0:
        install_requirements(venv_python)

    # 3. Lanzar agent.py con el Python del venv
    print(f"[bootstrap] Launching agent.py with: {venv_python}", flush=True)
    agent = os.path.join(BASE_DIR, "agent.py")
    result = subprocess.run([venv_python, agent])
    sys.exit(result.returncode)