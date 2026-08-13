@echo off
chcp 65001 > nul
title 恢复并获取

echo ========================================
echo   🔄 恢复并获取最新代码
echo ========================================
echo.

:: ---- 第一步：检查是否有修改 ----
echo 【检查本地修改...】
git status --porcelain | findstr . > nul

if %errorlevel% equ 0 (
    :: 有修改，显示列表并确认
    echo ⚠️  发现本地修改，需要先撤销才能拉取。
    echo.
    echo 【修改列表】
    git status
    echo.
    
    echo ⚠️  警告：此操作将丢弃所有未提交的修改！
    echo.
    set /p confirm="确认执行？(输入 Y/y 确认，其他任意键取消): "
    
    if /i not "%confirm%"=="y" (
        echo.
        echo ❌ 操作已取消
        pause
        exit /b
    )
    
    :: 执行 checkout
    echo.
    echo 正在恢复文件...
    git checkout .
    echo.
    
    if %errorlevel% equ 0 (
        echo ✅ 撤销成功！
    ) else (
        echo ⚠️  撤销完成
    )
    echo.
) else (
    echo ✅ 工作区是干净的，无需撤销。
    echo.
)

:: ---- 第二步：拉取代码 ----
echo 【拉取最新代码...】
git pull --verbose --log --progress --stat

echo.
if %errorlevel% equ 0 (
    echo ✅ 拉取成功！代码已更新到最新版本。
) else (
    echo ❌ 拉取失败，请检查错误信息！
)

echo.
pause