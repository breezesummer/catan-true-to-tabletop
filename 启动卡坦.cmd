@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\development\Play-AiStrategy.ps1"
if errorlevel 1 pause
