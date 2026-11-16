@echo off
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 >nul

rem ===========================================================================
rem  Apollo Game Scanner — сборка и публикация релиза
rem
rem  Примеры:
rem    release.cmd                        двойной клик: сборка + GitHub Release
rem    release.cmd 1.2.0                  собрать версию 1.2.0 (только локально)
rem    release.cmd 1.2.0 --github         собрать и создать GitHub Release
rem    release.cmd v1.2.0 --framework     framework-dependent (нужен Runtime 8)
rem    release.cmd 1.2.0 --github --yes   без подтверждения
rem    release.cmd --local                только локальная сборка
rem
rem  Результат: dist\v<версия>\ApolloGameScanner.exe (+ README.md) и
rem             dist\ApolloGameScanner-<версия>-win-x64[-fdd].zip (+ .sha256)
rem
rem  ВАЖНО: не используйте имена переменных OutDir, Version, PublishDir и т.п. —
rem  они экспортируются в MSBuild и ломают параметры публикации.
rem ===========================================================================

set "APOLLO_ROOT=%~dp0"
set "APOLLO_ROOT=!APOLLO_ROOT:~0,-1!"
set "APOLLO_PROJECT=%APOLLO_ROOT%\src\ApolloGameScanner\ApolloGameScanner.csproj"
set "REL_DIR=%APOLLO_ROOT%\dist"

set "REL_VER="
set "REL_SELF=1"
set "REL_GITHUB=0"
set "REL_YES=0"
set "REL_ALLOW_DIRTY=0"

rem Двойной клик по файлу (без аргументов) сразу собирает и публикует релиз
rem на GitHub, а в конце оставляет окно открытым.
set "REL_NO_ARGS="
if "%~1"=="" (
    set "REL_NO_ARGS=1"
    set "REL_GITHUB=1"
)

:parse
if "%~1"=="" goto parsed
if /i "%~1"=="-h" goto usage
if /i "%~1"=="--help" goto usage
if /i "%~1"=="/?" goto usage
if /i "%~1"=="--local" (
    set "REL_GITHUB=0"
    shift
    goto parse
)
if /i "%~1"=="--github" (
    set "REL_GITHUB=1"
    shift
    goto parse
)
if /i "%~1"=="--framework" (
    set "REL_SELF=0"
    shift
    goto parse
)
if /i "%~1"=="--self-contained" (
    set "REL_SELF=1"
    shift
    goto parse
)
if /i "%~1"=="--yes" (
    set "REL_YES=1"
    shift
    goto parse
)
if /i "%~1"=="--allow-dirty" (
    set "REL_ALLOW_DIRTY=1"
    shift
    goto parse
)
if defined REL_VER goto usage
set "REL_VER=%~1"
shift
goto parse

:parsed
if not exist "%APOLLO_PROJECT%" (
    echo [ОШИБКА] Не найден проект: %APOLLO_PROJECT%
    if defined REL_NO_ARGS pause
    exit /b 1
)

if not defined REL_VER (
    for /f "usebackq delims=" %%v in (`powershell -NoProfile -Command "$p = [xml](Get-Content -Raw -LiteralPath '%APOLLO_PROJECT%'); $p.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1"`) do (
        if not defined REL_VER set "REL_VER=%%v"
    )
)

if not defined REL_VER (
    echo [ОШИБКА] Не удалось определить версию. Укажите её аргументом, например: release.cmd 1.0.0
    if defined REL_NO_ARGS pause
    exit /b 1
)

if /i "!REL_VER:~0,1!"=="v" set "REL_VER=!REL_VER:~1!"
echo(!REL_VER!| findstr /r /c:"^[0-9][0-9.]*$" >nul
if errorlevel 1 (
    echo [ОШИБКА] Некорректная версия: "!REL_VER!". Ожидается, например, 1.0.0
    if defined REL_NO_ARGS pause
    exit /b 1
)

set "REL_TAG=v!REL_VER!"
set "REL_REPO="
if "!REL_GITHUB!"=="1" (
    git -C "%APOLLO_ROOT%" rev-parse --is-inside-work-tree >nul 2>&1
    if errorlevel 1 (
        echo [ОШИБКА] Каталог не является git-репозиторием, GitHub Release невозможен.
        if defined REL_NO_ARGS pause
        exit /b 1
    )

    for /f "delims=" %%u in ('git -C "%APOLLO_ROOT%" remote get-url origin 2^>nul') do set "REL_ORIGIN=%%u"
    if not defined REL_ORIGIN (
        echo [ОШИБКА] У репозитория нет remote "origin".
        if defined REL_NO_ARGS pause
        exit /b 1
    )

    set "REL_REPO=!REL_ORIGIN:.git=!"
    set "REL_REPO=!REL_REPO:*github.com=!"
    set "REL_REPO=!REL_REPO:~1!"

    for /f "delims=" %%s in ('git -C "%APOLLO_ROOT%" status --porcelain') do (
        if "!REL_ALLOW_DIRTY!"=="0" (
            echo [ОШИБКА] В рабочем дереве есть незакоммиченные изменения:
            echo          %%s
            echo          Закоммитьте их или добавьте --allow-dirty.
            if defined REL_NO_ARGS pause
            exit /b 1
        )
    )
)

if "!REL_SELF!"=="1" (
    set "REL_MODE=self-contained (Runtime не нужен)"
    set "REL_SC=true"
    set "REL_SUFFIX=win-x64"
) else (
    set "REL_MODE=framework-dependent (нужен .NET Desktop Runtime 8)"
    set "REL_SC=false"
    set "REL_SUFFIX=win-x64-fdd"
)

set "REL_OUT=%REL_DIR%\v!REL_VER!"
set "REL_ZIP_NAME=ApolloGameScanner-!REL_VER!-!REL_SUFFIX!.zip"
set "REL_ZIP=%REL_DIR%\!REL_ZIP_NAME!"

echo ============================================================
echo  Apollo Game Scanner — релиз !REL_TAG!
echo  Режим:    !REL_MODE!
echo  Платформа: win-x64
echo  Каталог:   dist
echo ============================================================
echo.

if "!REL_YES!"=="0" (
    set "REL_ANSWER="
    set /p "REL_ANSWER=Продолжить? [y/N] "
    if /i not "!REL_ANSWER!"=="y" (
        echo Отменено.
        if defined REL_NO_ARGS pause
        exit /b 0
    )
    echo.
)

echo [1/4] Публикация проекта...
if exist "!REL_OUT!" rd /s /q "!REL_OUT!"
if exist "!REL_OUT!" (
    echo [ОШИБКА] Не удалось очистить каталог !REL_OUT!
    if defined REL_NO_ARGS pause
    exit /b 1
)
mkdir "!REL_OUT!" >nul 2>&1
if errorlevel 1 (
    echo [ОШИБКА] Не удалось создать каталог !REL_OUT!
    if defined REL_NO_ARGS pause
    exit /b 1
)

set "REL_ARGS=-c Release -r win-x64 --self-contained !REL_SC! -v minimal -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -p:Version=!REL_VER!"
if "!REL_SELF!"=="1" set "REL_ARGS=!REL_ARGS! -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true"

dotnet publish "%APOLLO_PROJECT%" !REL_ARGS! -o "!REL_OUT!"
if errorlevel 1 (
    echo.
    echo [ОШИБКА] dotnet publish завершился с ошибкой.
    if defined REL_NO_ARGS pause
    exit /b 1
)

del /q "!REL_OUT!\*.pdb" >nul 2>&1
copy /y "%APOLLO_ROOT%\README.md" "!REL_OUT!\README.md" >nul
if errorlevel 1 (
    echo [ПРЕДУПРЕЖДЕНИЕ] Не удалось скопировать README.md в пакет.
)

if not exist "!REL_OUT!\ApolloGameScanner.exe" (
    echo [ОШИБКА] В !REL_OUT! нет ApolloGameScanner.exe.
    if defined REL_NO_ARGS pause
    exit /b 1
)

echo.
echo [2/4] Упаковка в ZIP...
if exist "!REL_ZIP!" del /q "!REL_ZIP!"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '!REL_OUT!\*' -DestinationPath '!REL_ZIP!' -CompressionLevel Optimal -Force"
if errorlevel 1 (
    echo [ОШИБКА] Не удалось создать архив !REL_ZIP!
    if defined REL_NO_ARGS pause
    exit /b 1
)

echo.
echo [3/4] Контрольная сумма SHA-256...
set "REL_SHA="
for /f "usebackq delims=" %%h in (`powershell -NoProfile -Command "(Get-FileHash -Algorithm SHA256 '!REL_ZIP!').Hash"`) do set "REL_SHA=%%h"
if not defined REL_SHA (
    echo [ОШИБКА] Не удалось вычислить SHA-256.
    if defined REL_NO_ARGS pause
    exit /b 1
)
> "!REL_ZIP!.sha256" echo !REL_SHA!  !REL_ZIP_NAME!

echo.
if "!REL_GITHUB!"=="1" (
    echo [4/4] Публикация GitHub Release !REL_TAG! ^(!REL_REPO!^)...

    where gh >nul 2>&1
    if errorlevel 1 (
        if exist "%ProgramFiles%\GitHub CLI\gh.exe" (
            set "REL_GH=%ProgramFiles%\GitHub CLI\gh.exe"
        ) else (
            echo [ОШИБКА] GitHub CLI не найден. Установите: winget install GitHub.cli
            if defined REL_NO_ARGS pause
            exit /b 1
        )
    ) else (
        set "REL_GH=gh"
    )

    git -C "%APOLLO_ROOT%" rev-parse -q --verify "refs/tags/!REL_TAG!" >nul 2>&1
    if errorlevel 1 (
        git -C "%APOLLO_ROOT%" tag -a "!REL_TAG!" -m "Apollo Game Scanner !REL_TAG!"
        if errorlevel 1 (
            echo [ОШИБКА] Не удалось создать тег !REL_TAG!.
            if defined REL_NO_ARGS pause
            exit /b 1
        )
    )

    git -C "%APOLLO_ROOT%" push origin "!REL_TAG!"
    if errorlevel 1 (
        echo [ОШИБКА] Не удалось отправить тег !REL_TAG! в origin.
        if defined REL_NO_ARGS pause
        exit /b 1
    )

    "!REL_GH!" release view "!REL_TAG!" -R "!REL_REPO!" >nul 2>&1
    if errorlevel 1 (
        "!REL_GH!" release create "!REL_TAG!" "!REL_ZIP!" "!REL_ZIP!.sha256" -R "!REL_REPO!" --title "Apollo Game Scanner !REL_TAG!" --generate-notes
    ) else (
        "!REL_GH!" release upload "!REL_TAG!" "!REL_ZIP!" "!REL_ZIP!.sha256" -R "!REL_REPO!" --clobber
    )
    if errorlevel 1 (
        echo [ОШИБКА] gh не смог создать/обновить релиз !REL_TAG!.
        if defined REL_NO_ARGS pause
        exit /b 1
    )
) else (
    echo [4/4] GitHub Release пропущен ^(нет флага --github^).
)

echo.
echo ============================================================
echo  Готово: !REL_TAG!
echo    EXE:      !REL_OUT!\ApolloGameScanner.exe
echo    Архив:    !REL_ZIP!
echo    SHA-256:  !REL_SHA!
if "!REL_GITHUB!"=="1" echo    Релиз:    https://github.com/!REL_REPO!/releases/tag/!REL_TAG!
echo ============================================================
if defined REL_NO_ARGS pause
exit /b 0

:usage
echo Использование:
echo   release.cmd [версия] [параметры]
echo.
echo Параметры:
echo   --github          создать тег и GitHub Release через gh CLI
echo   --local           только локальная сборка (без GitHub)
echo   --framework       framework-dependent сборка (нужен .NET Desktop Runtime 8)
echo   --self-contained  self-contained сборка (по умолчанию, Runtime не нужен)
echo   --yes             не спрашивать подтверждение
echo   --allow-dirty     разрешить релиз при незакоммиченных изменениях
echo   -h, --help        эта справка
echo.
echo Без аргументов (двойной клик) собирает текущую версию и публикует GitHub Release.
echo Версия по умолчанию берётся из src\ApolloGameScanner\ApolloGameScanner.csproj.
echo Результаты: dist\v^<версия^>\ и dist\ApolloGameScanner-^<версия^>-win-x64.zip
exit /b 0
