@echo off
title Watchdog - Equipment Monitor

set EXE=%~dp0bin\Debug\EquipmentMonitor.exe

:loop
echo [%date% %time%] Starting...
start "EquipmentMonitor" /wait "%EXE%"
echo [%date% %time%] Crashed or closed. Restarting in 5 seconds...
timeout /t 5 /nobreak >nul
goto loop
