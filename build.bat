@echo off
rem Builds iPhonePhotoImporter.exe using the C# compiler that ships with Windows (.NET Framework 4.x).
rem Compila iPhonePhotoImporter.exe con el compilador de C# que ya viene con Windows (.NET Framework 4.x).
setlocal
cd /d "%~dp0src"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
"%CSC%" /nologo /target:winexe /optimize+ /win32icon:icono.ico /out:"..\iPhonePhotoImporter.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll *.cs
if errorlevel 1 (
    echo.
    echo Build failed / Hubo errores al compilar.
    if /i not "%CI%"=="true" pause
    exit /b 1
)
echo Done / Listo: iPhonePhotoImporter.exe
