@echo off
chcp 65001 > nul
title Git Checkout

echo ========================================
echo ⚠️  警告：即将撤销所有本地修改！
echo ========================================
echo.
echo 当前修改列表：
git status --short
echo.
echo 此操作将丢弃所有未提交的更改！
echo.

set /p confirm="确认执行？(输入 y 确认): "
if /i not "%confirm%"=="y" (
    echo 操作已取消
    pause
    exit /b
)

echo.
echo 正在执行 git checkout . ...
git checkout . --verbose

echo.
if %errorlevel% equ 0 (
    echo ✅ 已成功撤销所有修改！
) else (
    echo ❌ 操作失败，请检查错误信息！
)
pause