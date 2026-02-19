#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PUBLISH_DIR="$SCRIPT_DIR/publish"

echo "=== SmartStudy Publish ==="

# Clean previous output
if [ -d "$PUBLISH_DIR" ]; then
  echo "Cleaning previous publish output..."
  rm -rf "$PUBLISH_DIR"
fi

# Run dotnet publish
echo "Running dotnet publish..."
dotnet publish "$SCRIPT_DIR/Server/SmartStudy/SmartStudy.csproj" \
  -c Release \
  -o "$PUBLISH_DIR" \
  --no-self-contained

# Verify Front/ was copied
if [ -d "$PUBLISH_DIR/Front" ]; then
  echo "Front/ directory found in publish output."
else
  echo "ERROR: Front/ directory missing from publish output!"
  exit 1
fi

# Verify web.config
if [ -f "$PUBLISH_DIR/web.config" ]; then
  echo "web.config found in publish output."
else
  echo "ERROR: web.config missing from publish output!"
  exit 1
fi

echo ""
echo "Publish complete! Output: $PUBLISH_DIR"
echo "Upload the contents of '$PUBLISH_DIR' to IIS via FileZilla."
