@echo off
setlocal
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0ClientPatcher.ps1"
if errorlevel 1 (
  echo.
  echo The patcher could not start. Extract the entire ZIP to a folder first.
  pause
)
