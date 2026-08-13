@echo off
chcp 65001 > nul

echo ========================================
echo 正在撤销所有本地修改...
echo ========================================
echo.

:: 显示当前状态
echo 当前修改：
git status --short
echo.

:: 检查是否有修改
git status --porcelain | findstr . > nul
if %errorlevel% neq 0 (
    echo ℹ️  工作区是干净的，没有需要撤销的修改。
    goto end
)

:: ===== 确认提示 =====
echo ⚠️  警告：此操作将丢弃所有未提交的修改！
echo.
set /p confirm="确认执行？(输入 Y/y 确认，其他任意键取消): "

if /i not "%confirm%"=="y" (
    echo.
    echo ❌ 操作已取消
    goto end
)

:: 执行 checkout
echo.
echo 正在恢复文件...
git checkout . --verbose

if %errorlevel% equ 0 (
    echo.
    echo ✅ 撤销成功！
) else (
    echo.
    echo ⚠️  撤销完成（可能有警告，但文件已恢复）
)

:end
echo.
pause