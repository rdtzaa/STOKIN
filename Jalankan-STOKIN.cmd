@echo off
setlocal
if exist "%~dp0output\app\Stokin.Desktop.exe" (
  start "STOKIN" "%~dp0output\app\Stokin.Desktop.exe"
) else (
  echo Paket aplikasi belum tersedia. Jalankan build dan publish terlebih dahulu.
  pause
)
