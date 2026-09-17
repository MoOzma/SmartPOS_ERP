@echo off
setlocal
cd /d "%~dp0"
if exist "%~dp0Start-POS.exe" (
    start "" "%~dp0Start-POS.exe"
    exit /b 0
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-shortcuts.ps1" >nul 2>&1
set ASPNETCORE_ENVIRONMENT=Production
start "" "%~dp0SmartPOS_ERP.exe"
timeout /t 3 /nobreak >nul

set "URL=http://127.0.0.1:5202"
set "BROWSER="

if exist "%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe" set "BROWSER=%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe"
if exist "%ProgramFiles%\Microsoft\Edge\Application\msedge.exe" set "BROWSER=%ProgramFiles%\Microsoft\Edge\Application\msedge.exe"
if not defined BROWSER if exist "%ProgramFiles%\Google\Chrome\Application\chrome.exe" set "BROWSER=%ProgramFiles%\Google\Chrome\Application\chrome.exe"
if not defined BROWSER if exist "%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe" set "BROWSER=%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe"
if not defined BROWSER if exist "%LocalAppData%\Google\Chrome\Application\chrome.exe" set "BROWSER=%LocalAppData%\Google\Chrome\Application\chrome.exe"

if defined BROWSER (
    start "" "%BROWSER%" --start-fullscreen --new-window "%URL%"
) else (
    start "" "%URL%"
)
