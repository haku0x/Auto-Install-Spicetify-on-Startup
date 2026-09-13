# Auto-Install Spicetify on Startup

A lightweight Python script that automatically adds itself to the Windows startup folder and installs, updates, or applies Spicetify every time your PC restarts.

## 🚀 Features

- **Self-Replicating Startup:** Automatically copies itself to the Windows Startup folder on first run so it executes on every boot.
- **Automated Installation:** Executes the official Spicetify PowerShell installation script silently in the background.
- **Zero Interaction:** Automatically pipes "yes" (`y`) into the installer prompt to ensure a fully automated, hands-off setup.
- **Bundles to EXE:** Perfect for compiling into a standalone executable using PyInstaller (`--onefile --noconsole`).

---

## 📋 Prerequisites

Before running or compiling the script, ensure you have:
- **Windows 10 / 11**
- **Spotify** installed (official desktop client recommended)
- **PowerShell** enabled for script execution

---

## ⚙️ Installation & Usage

1. **Clone the repository:**
   ```bash
   git clone [https://github.com/haku0x/Auto-Install-Spicetify-on-Startup.git](https://github.com/haku0x/Auto-Install-Spicetify-on-Startup.git)
