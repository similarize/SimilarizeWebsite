@echo off
rm game.apk
python -m pygbag --build .
move build\web\* .
rmdir /s /q build
pause
