@echo off
rem Run this from the build folder. The two .exe files are written to the app folder (one level up).
setlocal
cd /d "%~dp0.."

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo ERROR: the .NET Framework C# compiler was not found.
    echo Install .NET Framework 4.8 and try again.
    pause
    exit /b 1
)

set "REFS=/reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:Microsoft.CSharp.dll"
set "SRC=src\AppInfo.cs src\CommandLine.cs src\Presets.cs src\MediaInfo.cs src\Converter.cs src\Installer.cs src\SetupFfmpeg.cs"

echo Building Fashion Flow AVI Converter...

"%CSC%" /nologo /target:winexe /optimize+ /nowarn:0649 /main:AviConverter.GuiProgram /out:AVIConverter.exe /win32icon:assets\icon.ico /win32manifest:src\app.manifest /resource:assets\logo.png,AVIConverter.logo.png %REFS% %SRC% src\GuiProgram.cs
if errorlevel 1 goto fail

"%CSC%" /nologo /target:exe /optimize+ /nowarn:0649 /main:AviConverter.CliProgram /out:avi-convert.exe /win32icon:assets\icon.ico /win32manifest:src\app.manifest %REFS% %SRC% src\CliProgram.cs
if errorlevel 1 goto fail

echo.
echo Build complete:
echo   AVIConverter.exe   (window)
echo   avi-convert.exe    (command line)
echo.
exit /b 0

:fail
echo.
echo BUILD FAILED.
exit /b 1
