@echo off
rem Installs Fashion Flow AVI Converter for the current user.
rem No administrator rights are needed.
setlocal
cd /d "%~dp0"

if not exist "%~dp0AVIConverter.exe" (
    echo.
    echo   AVIConverter.exe was not found in this folder.
    echo   Extract the whole ZIP package first, then run Install.bat.
    echo.
    pause
    exit /b 1
)

"%~dp0AVIConverter.exe" --install
set "RC=%ERRORLEVEL%"

if not "%RC%"=="0" (
    echo.
    echo Installation failed. Read the message on screen.
    pause
)
exit /b %RC%
