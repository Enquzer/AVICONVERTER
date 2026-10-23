@echo off
rem Builds dist\FashionFlow-AVI-Converter-1.0.0.zip (app + FFmpeg + installer).
setlocal
cd /d "%~dp0.."

if not exist "avi-convert.exe" (
    echo.
    echo   avi-convert.exe was not found. Run build\Build.bat first.
    echo.
    pause
    exit /b 1
)

"avi-convert.exe" --package
set "RC=%ERRORLEVEL%"

echo.
pause
exit /b %RC%
