@echo off
setlocal
echo =============================================
echo  Building VestalFocuser beta 0.6.10
echo =============================================
echo.

set PROJ_DIR=%~dp0
set DLL=%PROJ_DIR%bin\Debug\net48\ASCOM.Autofocus.Focuser.dll

echo [1/2] Building...
dotnet build "%PROJ_DIR%ASCOM.Autofocus.Focuser.csproj" --configuration Debug
if %ERRORLEVEL% NEQ 0 (
    echo [FAILED] Build failed.
    exit /b 1
)

echo [2/3] Registering COM 32-bit (admin required)...
"%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe" /codebase "%DLL%"
if %ERRORLEVEL% NEQ 0 (
    echo [WARN] 32-bit RegAsm failed (may need admin).
)

echo [3/3] Registering COM 64-bit (admin required)...
"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" /codebase "%DLL%"
if %ERRORLEVEL% NEQ 0 (
    echo [WARN] 64-bit RegAsm failed (may need admin).
)

echo.
echo [OK] Build & Registration complete.
echo DLL: %DLL%
echo.
