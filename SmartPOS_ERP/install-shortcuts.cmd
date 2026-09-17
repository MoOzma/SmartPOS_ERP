@echo off
cd /d "%~dp0"
if exist "%~dp0Start-POS.exe" (
    start "" "%~dp0Start-POS.exe"
    exit /b 0
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-shortcuts.ps1"
if errorlevel 1 (
    echo تعذر إنشاء الاختصار.
    pause
)
