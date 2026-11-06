@echo off
chcp 65001 >nul
python "%~dp0api_sunucu.py"
if errorlevel 1 pause
