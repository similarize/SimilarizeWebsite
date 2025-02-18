@echo off
python -m pygbag --build .
move build\web\* .
rmdir /s /q build
rm game.apk
pause
