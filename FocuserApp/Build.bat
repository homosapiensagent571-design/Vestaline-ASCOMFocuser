@echo off
setlocal

set CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo [ERROR] csc.exe not found. Install .NET Framework 4.x.
    exit /b 1
)

set OUT=bin\VestalFocuser.exe
if not exist bin mkdir bin

echo.
echo =============================================
echo  Building VestalFocuser beta 0.6.10
echo  (ASCOM + Direct Serial Dual Mode)
echo =============================================
echo.

"%CSC%" ^
  /target:winexe ^
  /platform:x86 ^
  /out:%OUT% ^
  /reference:System.dll ^
  /reference:System.Core.dll ^
  /reference:System.Windows.Forms.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Web.Extensions.dll ^
  /reference:Microsoft.CSharp.dll ^
  Program.cs ^
  MainForm.cs ^
  SerialService.cs ^
  ASCOMFocuserService.cs ^
  Config.cs

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [FAILED] Compilation failed.
    exit /b 1
)

echo.
echo [OK] Build successful: %OUT%
echo.
echo Run: %OUT%
echo.

copy /Y autofocus.config bin\ >nul 2>&1
exit /b 0
