@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\one-click-publish.ps1" -RepoName "display-lift" -Visibility "public"
if errorlevel 1 (
  echo.
  echo Publish failed. Review the message above.
  pause
  exit /b 1
)
echo.
echo Build and GitHub publish finished successfully.
pause
