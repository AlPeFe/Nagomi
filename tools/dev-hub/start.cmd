@echo off
rem Dev hub de Nagomi: panel local con links a todos los productos + estado en vivo.
rem Uso:  start.cmd   -> http://127.0.0.1:8096
cd /d "%~dp0"
python dev_hub.py --port 8096 --bind 127.0.0.1 --env ..\..\.env
