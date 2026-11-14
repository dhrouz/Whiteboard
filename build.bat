@echo off
setlocal
chcp 65001 >nul
cd /d "%~dp0"

rem --- find the C# compiler that ships with Windows (.NET Framework 4, C# 5) ---
set "CSC=%windir%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%windir%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [ERROR] csc.exe was not found. .NET Framework 4 is part of Windows - please run Windows Update.
    pause
    exit /b 1
)

rem --- optional icon: assets\icon.ico or icon.ico ---
set "ICON="
if exist "assets\icon.ico" set "ICON=/win32icon:assets\icon.ico"
if not defined ICON if exist "icon.ico" set "ICON=/win32icon:icon.ico"

if not exist "bin" mkdir "bin"
if exist "bin\Whiteboard.exe" del /q "bin\Whiteboard.exe" >nul 2>&1

echo Building Whiteboard...
"%CSC%" /nologo /target:winexe /optimize+ /codepage:65001 /out:bin\Whiteboard.exe %ICON% ^
    /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
    /recurse:src\*.cs

if errorlevel 1 (
    echo.
    echo [FAILED] Please copy the error lines above and send them to Claude.
    pause
    exit /b 1
)

echo.
echo [OK] Built: bin\Whiteboard.exe
if /i not "%~1"=="nopause" pause
