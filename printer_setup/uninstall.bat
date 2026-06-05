@echo off
setlocal
set SVC=printer_info

rem Stop service first (ignore if not running)
sc stop %SVC% >nul 2>&1

rem Wait briefly for SCM cleanup
timeout /t 1 /nobreak >nul

rem Delete service
sc delete %SVC%
set RC=%ERRORLEVEL%

if %RC% EQU 0 (
    echo [OK] printer_info uninstalled
    exit /b 0
)

rem ERROR_SERVICE_DOES_NOT_EXIST = 1060
if %RC% EQU 1060 (
    echo [OK] printer_info not installed, nothing to do
    exit /b 0
)

echo [ERROR] sc delete failed, exit code=%RC% >&2
exit /b 1
