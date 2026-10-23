@echo off
rem Removes the per-user installation of Fashion Flow AVI Converter.
rem The app asks for confirmation before deleting anything.
setlocal

set "INSTALLED=%LOCALAPPDATA%\Programs\FashionFlow\AVI Converter\AVIConverter.exe"

if exist "%INSTALLED%" (
    "%INSTALLED%" --uninstall
) else (
    "%~dp0AVIConverter.exe" --uninstall
)
