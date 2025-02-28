@echo off
rm game.apk
python -m pygbag --build  --template index.html.tpl --title "Loading RC Rally Jump" .
move build\web\* .
rmdir /s /q build
pause
