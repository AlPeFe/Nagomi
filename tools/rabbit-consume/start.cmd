@echo off
REM Arranca el cliente de consumo RabbitMQ (herramienta de desarrollo).
REM Lee credenciales del .env del repo Nagomi y sirve http://127.0.0.1:8095

cd /d "%~dp0..\.."

for /f "usebackq delims=" %%a in (`findstr /b "RABBITMQ_USER=" .env`) do set "RABBIT_USER=%%a"
for /f "usebackq delims=" %%a in (`findstr /b "RABBITMQ_PASSWORD=" .env`) do set "RABBIT_PASSWORD=%%a"
for /f "usebackq delims=" %%a in (`findstr /b "RABBITMQ_VHOST=" .env`) do set "RABBIT_VHOST=%%a"

set "RABBIT_USER=%RABBIT_USER:RABBITMQ_USER==%"
set "RABBIT_PASSWORD=%RABBIT_PASSWORD:RABBITMQ_PASSWORD==%"
set "RABBIT_VHOST=%RABBIT_VHOST:RABBITMQ_VHOST==%"

python "%~dp0rabbit_consume.py"
