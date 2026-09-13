import os
import shutil
import subprocess
import sys

def add_to_startup():
    try:
        startup_folder = os.path.join(os.environ["APPDATA"], "Microsoft\\Windows\\Start Menu\\Programs\\Startup")
        
        if getattr(sys, 'frozen', False):
            current_path = sys.executable
        else:
            current_path = os.path.abspath(__file__)
            
        destination_path = os.path.join(startup_folder, os.path.basename(current_path))
        
        if not os.path.exists(destination_path):
            shutil.copy2(current_path, destination_path)
    except Exception:
        pass

add_to_startup()

process = subprocess.Popen(
    ["powershell", "-Command", "iwr -useb https://raw.githubusercontent.com/spicetify/cli/main/install.ps1 | iex"],
    stdin=subprocess.PIPE,
    text=True
)

process.communicate(input="y\n")
