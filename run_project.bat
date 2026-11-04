@echo off
title MedEase - Healthy System
cd /d "%~dp0"
echo =======================================================================
echo          MEDEASE (HEALTHY SYSTEM) - HE THONG QUAN LY PHONG KHAM
echo =======================================================================
echo.
echo [*] Kiem tra va khoi dong ung dung web...
echo [*] Dia chi truy cap: http://localhost:5164
echo [*] Tai khoan test mac dinh (Mat khau: 123456):
echo     - Admin:      admin@healthysystem.vn
echo     - Bac si:     bs.minhanh@healthysystem.vn
echo     - Le tan:     reception@healthysystem.vn
echo     - Benh nhan:  benhnhan@gmail.com
echo.
echo =======================================================================
echo.
dotnet run --launch-profile "http"
pause
