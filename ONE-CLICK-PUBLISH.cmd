@echo off
setlocal
title DisplayLift V7 Build and Publish
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\one-click-publish.ps1"
if errorlevel 1 (
  echo.
  echo Publish failed. Review the message above.
  pause
  exit /b 1
)
echo.
echo DisplayLift V7 built, synchronized and launched.
pause
