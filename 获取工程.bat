@echo off
chcp 65001 > nul
echo ========================================
echo 正在拉取最新代码...
echo ========================================

git pull --verbose --log --progress --stat

echo.
echo ========================================
pause