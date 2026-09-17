@echo off
echo ============================================================
echo   PIWEDE NFC KIOSQUE - GENERATEUR DE VERSION PORTABLE
echo ============================================================
echo.

set DOTNET_EXE=.\.dotnet\dotnet.exe
set OUTPUT_DIR=.\Publish_Portable

if not exist %DOTNET_EXE% (
    echo [ERREUR] Le SDK .NET local n'a pas ete trouve.
    echo Veuillez vous assurer que le dossier .dotnet existe.
    pause
    exit /b 1
)

echo [1/3] Nettoyage des anciennes builds...
if exist %OUTPUT_DIR% rd /s /q %OUTPUT_DIR%

echo [2/3] Publication de la version autonome (Self-Contained)...
echo Cela peut prendre une minute...

%DOTNET_EXE% publish Nexorsys.NFC.APP.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true --output %OUTPUT_DIR%

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERREUR] La publication a echoue. Verifiez les erreurs ci-dessus.
    pause
    exit /b 1
)

echo [3/3] Finalisation...
copy appsettings.json %OUTPUT_DIR%\appsettings.json /Y

echo.
echo ============================================================
echo   SUCCES ! L'application est prete dans : %OUTPUT_DIR%
echo   Vous pouvez copier ce dossier sur n'importe quel PC Windows.
echo ============================================================
echo.
pause
