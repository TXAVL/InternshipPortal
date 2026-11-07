@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ===================================================
echo [1/4] Kiem tra va Build du an (Release)...
echo ===================================================
dotnet build InternshipPortal.Web/InternshipPortal.Web.csproj --configuration Release
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Build that bai! Vui long kiem tra va sua loi truoc khi submit.
    exit /b %ERRORLEVEL%
)
echo [OK] Build Release thanh cong!

:: Dam bao git co the goi duoc neu chua co trong PATH
where git >nul 2>nul
if %ERRORLEVEL% neq 0 (
    if exist "C:\Program Files\Git\cmd\git.exe" (
        set "PATH=%PATH%;C:\Program Files\Git\cmd"
    ) else if exist "C:\Program Files (x86)\Git\cmd\git.exe" (
        set "PATH=%PATH%;C:\Program Files (x86)\Git\cmd"
    ) else if exist "%LOCALAPPDATA%\Programs\Git\cmd\git.exe" (
        set "PATH=%PATH%;%LOCALAPPDATA%\Programs\Git\cmd"
    )
)

:: Khoi tao git neu chua co
git rev-parse --is-inside-work-tree >nul 2>nul
if %ERRORLEVEL% neq 0 (
    echo [INFO] Khoi tao Git repository...
    git init -b main
)

echo.
echo ===================================================
echo [2/4] Chuyen / tao nhanh feature/day-1-views...
echo ===================================================
git checkout -B feature/day-1-views

echo.
echo ===================================================
echo [3/4] Commit ma nguon...
echo ===================================================
git add .
git commit -m "feat(views): Hoàn thiện giao diện Ngày 1" >nul 2>&1
echo [OK] Da commit code vao nhanh feature/day-1-views.

echo.
echo ===================================================
echo [4/4] Uy quyen GitHub & Tu dong tao Pull Request...
echo ===================================================

:: Tim trinh thong dich Python tren may
set "PYTHON_EXE="
where python >nul 2>nul
if %ERRORLEVEL% equ 0 (
    set "PYTHON_EXE=python"
) else if exist "C:\Program Files\Python314\python.exe" (
    set "PYTHON_EXE=C:\Program Files\Python314\python.exe"
) else if exist "C:\Program Files\Python312\python.exe" (
    set "PYTHON_EXE=C:\Program Files\Python312\python.exe"
) else if exist "%LOCALAPPDATA%\Programs\Python\Python312\python.exe" (
    set "PYTHON_EXE=%LOCALAPPDATA%\Programs\Python\Python312\python.exe"
) else if exist "C:\Users\admin\Desktop\TXA-PACKAGE\tools\.venv\Scripts\python.exe" (
    set "PYTHON_EXE=C:\Users\admin\Desktop\TXA-PACKAGE\tools\.venv\Scripts\python.exe"
)

if defined PYTHON_EXE (
    "%PYTHON_EXE%" submit-pr.py
) else (
    echo [THONG BAO] May nay chua cai Python. Chay truc tiep bang Git va mo trinh duyet:
    git push -u origin feature/day-1-views --force
    set "REPO_URL="
    for /f "tokens=*" %%i in ('git config --get remote.origin.url 2^>nul') do set "REPO_URL=%%i"
    if defined REPO_URL (
        set "CLEAN_URL=!REPO_URL!"
        set "CLEAN_URL=!CLEAN_URL:git@github.com:=https://github.com/!"
        if "!CLEAN_URL:~-4!"==".git" set "CLEAN_URL=!CLEAN_URL:~0,-4!"
        set "PR_URL=!CLEAN_URL!/compare/main...feature/day-1-views?expand=1"
        echo Dang mo link Pull Request: !PR_URL!
        start "" "!PR_URL!"
    )
)

echo.
echo ===================================================
echo [HOAN TAT] Quy trinh hoan thanh!
echo ===================================================
