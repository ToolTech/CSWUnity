@echo off
setlocal

pushd "%~dp0" >nul

dotnet --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: A .NET 8-compatible SDK is required.
    echo Install the SDK and ensure that dotnet is available on PATH.
    goto :failed
)

echo [1/2] Staging locked CSW and GizmoSDK dependencies...
dotnet run --project "tools\CSWUnity.NuGetStager\CSWUnity.NuGetStager.csproj" --configuration Release -- stage
if errorlevel 1 goto :failed

echo.
echo [2/2] Verifying the generated Unity dependency package...
dotnet run --project "tools\CSWUnity.NuGetStager\CSWUnity.NuGetStager.csproj" --configuration Release --no-build -- verify
if errorlevel 1 goto :failed

echo.
echo Setup completed successfully.
set "EXIT_CODE=0"
goto :finished

:failed
set "EXIT_CODE=%ERRORLEVEL%"
if "%EXIT_CODE%"=="0" set "EXIT_CODE=1"
echo.
echo ERROR: Setup failed with exit code %EXIT_CODE%.

:finished
echo.
if "%~1"=="" pause
popd
exit /b %EXIT_CODE%
