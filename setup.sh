#!/usr/bin/env sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
cd "$SCRIPT_DIR"

case "$(uname -s)" in
    MINGW*|MSYS*|CYGWIN*)
        ;;
    *)
        echo "ERROR: The current dependency profile supports Windows x64 only." >&2
        exit 1
        ;;
esac

if ! command -v dotnet >/dev/null 2>&1; then
    echo "ERROR: A .NET 8-compatible SDK is required." >&2
    echo "Install the SDK and ensure that dotnet is available on PATH." >&2
    exit 1
fi

echo "[1/2] Staging locked CSW and GizmoSDK dependencies..."
dotnet run --project "tools/CSWUnity.NuGetStager/CSWUnity.NuGetStager.csproj" --configuration Release -- stage

echo
echo "[2/2] Verifying the generated Unity dependency package..."
dotnet run --project "tools/CSWUnity.NuGetStager/CSWUnity.NuGetStager.csproj" --configuration Release --no-build -- verify

echo
echo "Setup completed successfully."
