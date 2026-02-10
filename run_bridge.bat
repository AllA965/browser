@echo off
echo Starting Python Bridge Service...
echo.
echo Step 1: Installing dependencies...
echo Note: browser-use requires python 3.11+
pip install -r python_bridge\requirements.txt
echo.
echo Step 2: Starting Service...
echo The service will listen on http://localhost:8000
echo.
python python_bridge\main.py
pause
