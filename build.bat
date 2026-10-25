@echo off
REM Baut SpicetifyAutoApply.exe mit dem bei Windows mitgelieferten C#-Compiler.
REM Kein Python, keine Fremd-Tools noetig.
setlocal

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo Konnte csc.exe nicht finden. Ist .NET Framework 4.x installiert?
    pause
    exit /b 1
)

"%CSC%" /nologo /target:winexe /codepage:65001 /out:SpicetifyAutoApply.exe ^
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
    SpicetifyAutoApply.cs

if errorlevel 1 (
    echo.
    echo Build fehlgeschlagen.
    pause
    exit /b 1
)

echo.
echo Build OK: SpicetifyAutoApply.exe
pause
