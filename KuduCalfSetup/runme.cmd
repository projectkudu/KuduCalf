@echo off
powershell -NoProfile -NoLogo -ExecutionPolicy RemoteSigned -File "%~dp0Setup.ps1"
set /P ignored=Press Enter To Continue