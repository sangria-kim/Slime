@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo Install the .NET 10 SDK first: https://dotnet.microsoft.com/download/dotnet/10.0
  pause
  exit /b 1
)
dotnet run --project tests\Slime.Core.Tests -c Release
if errorlevel 1 goto :failed
dotnet publish src\Slime.Desktop\Slime.Desktop.csproj -c Release -r win-x64 --self-contained true -o dist\win-x64
if errorlevel 1 goto :failed
echo.
echo Ready: dist\win-x64\Slime.exe
pause
exit /b 0
:failed
echo Build failed. See the error above.
pause
exit /b 1
