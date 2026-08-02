@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\one-click-publish.ps1"
if errorlevel 1 (
  echo.
  echo Publish failed. Review the message above.
  pause
  exit /b 1
)
echo.
echo Build, tests and GitHub synchronization completed successfully.
pause
