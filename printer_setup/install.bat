@echo off
setlocal
set SVC=printer_info
set EXE=%~dp0printer_info.exe

if not exist "%EXE%" (
    echo [ERROR] %EXE% not found >&2
    exit /b 10
)

rem Stop and delete existing service (ignore if not exist)
sc stop %SVC% >nul 2>&1
sc delete %SVC% >nul 2>&1

rem Wait briefly for SCM cleanup
timeout /t 1 /nobreak >nul

sc create %SVC% binPath= "%EXE%" start= auto DisplayName= "printer_info"
if errorlevel 1 (
    echo [ERROR] sc create failed >&2
    exit /b 1
)

sc description %SVC% "printer_info meter reading service" >nul

sc start %SVC%
if errorlevel 1 (
    echo [ERROR] sc start failed >&2
    exit /b 2
)

echo [OK] printer_info installed and started
exit /b 0
