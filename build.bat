@echo off
python "%~dp0tools\modern.py" build
exit /b %errorlevel%
