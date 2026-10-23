@echo off
rem Drag video files or folders onto this .bat to convert them to AVI.
rem Uses the "safe" 480p Xvid preset. For other options see docs\README.md
rem or run avi-convert.exe from a Command Prompt.
setlocal
cd /d "%~dp0.."

title AVI Converter

if "%~1"=="" (
    echo.
    echo   Drag one or more video files or folders onto this file.
    echo.
    echo   Default output: AVI, 854x480 Xvid video + MP3 audio.
    echo   The .avi is written next to the source file.
    echo.
    pause
    exit /b 1
)

"%~dp0..\avi-convert.exe" %* -Preset safe -Recurse
set "RC=%ERRORLEVEL%"

echo.
if not "%RC%"=="0" (
    echo Conversion finished with problems. Read the messages above.
) else (
    echo All done.
)
pause
exit /b %RC%
