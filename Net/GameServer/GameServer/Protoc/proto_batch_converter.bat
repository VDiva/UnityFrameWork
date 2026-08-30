@echo off
chcp 65001 >nul
setlocal EnableExtensions EnableDelayedExpansion

REM Always run relative to this batch file.
pushd "%~dp0"
if errorlevel 1 (
    echo [错误] 无法进入批处理所在目录。
    pause
    exit /b 1
)

set "OUTPUT_DIR=%~dp0script"
set "UNITY_MSGDATA_DIR=%~dp0..\..\..\..\Assets\FrameWork\Scripts\MsgData"

echo ===================================================
echo    Protocol Buffer 批量转换工具
echo ===================================================
echo.

REM 自动查找 protoc.exe
set "PROTOC_PATH="
for %%d in (Protoc bin . tools) do (
    if not defined PROTOC_PATH if exist "%%d\protoc.exe" (
        set "PROTOC_PATH=%%d\protoc.exe"
    )
)

if not defined PROTOC_PATH (
    echo [错误] 找不到 protoc.exe！
    echo.
    echo 请将 protoc.exe 放在以下任一位置:
    echo   - Protoc\protoc.exe
    echo   - bin\protoc.exe
    echo   - 当前目录\protoc.exe
    echo.
    popd
    pause
    exit /b 1
)

echo [信息] 找到 protoc.exe: %PROTOC_PATH%
echo.

REM 创建生成代码的临时输出目录
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"
if errorlevel 1 (
    echo [错误] 无法创建输出目录: %OUTPUT_DIR%
    popd
    pause
    exit /b 1
)

REM 统计转换结果
set "total=0"
set "success=0"

echo 开始扫描 .proto 文件...
echo ------------------------------------------------

dir /b "*.proto" >nul 2>&1
if errorlevel 1 (
    echo [警告] 当前目录没有找到 .proto 文件！
    echo.
    echo 请将 .proto 文件放在: %CD%
    echo.
    popd
    pause
    exit /b 0
)

for %%f in (*.proto) do (
    set /a total+=1
    echo [!total!] 正在转换: %%~nxf
    "%PROTOC_PATH%" --csharp_out="%OUTPUT_DIR%" "%%f"
    if !errorlevel! equ 0 (
        set /a success+=1
        echo        [成功]
    ) else (
        echo        [失败]
    )
)

echo ------------------------------------------------
echo.
echo 转换完成！成功: %success% / 总计: %total%
echo.

REM 将生成的 C# 代码复制到 Unity Assets/Script/MsgData。
if %success% gtr 0 (
    if not exist "%UNITY_MSGDATA_DIR%" mkdir "%UNITY_MSGDATA_DIR%"
    if errorlevel 1 (
        echo [错误] 无法创建 Unity 目标目录:
        echo        %UNITY_MSGDATA_DIR%
        popd
        pause
        exit /b 1
    )

    echo [信息] 正在复制生成代码到 Unity:
    echo        %UNITY_MSGDATA_DIR%
    copy /Y "%OUTPUT_DIR%\*.cs" "%UNITY_MSGDATA_DIR%\" >nul
    if errorlevel 1 (
        echo        [复制失败]
        popd
        pause
        exit /b 1
    )
    echo        [复制成功]
    echo.
)

set "exit_code=0"
if not "%success%"=="%total%" set "exit_code=1"

popd
pause
exit /b %exit_code%
