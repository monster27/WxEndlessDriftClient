@echo off
chcp 65001 > nul
title 恢复并获取

echo ========================================
echo   恢复并获取最新代码
echo ========================================
echo.

:: ========================================
:: 第一步：检查并撤销修改
:: ========================================
echo [1/2] 检查本地修改...
echo.

:: 显示当前修改
git status
echo.

:: 检查是否有修改
git status --porcelain | findstr . > nul
if %errorlevel% neq 0 (
    echo 工作区是干净的，没有需要撤销的修改。
    echo.
    goto pull
)

:: 有修改，确认是否撤销
echo 警告：此操作将丢弃所有未提交的修改！
echo.
set /p confirm="确认执行？(输入 Y/y 确认，其他任意键取消): "

if /i not "%confirm%"=="y" (
    echo.
    echo 操作已取消
    pause
    exit /b
)

:: 执行撤销
echo.
echo 正在恢复文件...
git checkout .
echo.

if %errorlevel% equ 0 (
    echo 撤销成功！
) else (
    echo 撤销完成
)
echo.

:: ========================================
:: 第二步：拉取最新代码
:: ========================================
:pull
echo [2/2] 拉取最新代码...
echo.

git pull --verbose --log --progress --stat

echo.
if %errorlevel% equ 0 (
    echo 拉取成功！
) else (
    echo 拉取失败，请检查错误信息！
)

echo.
pause