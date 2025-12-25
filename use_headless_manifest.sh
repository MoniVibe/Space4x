#!/usr/bin/env bash
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PKG_DIR="$PROJECT_DIR/Packages"

cp -f "$PKG_DIR/manifest.json" "$PKG_DIR/manifest.json.bak"
cp -f "$PKG_DIR/packages-lock.json" "$PKG_DIR/packages-lock.json.bak"

cp -f "$PKG_DIR/manifest.headless.json" "$PKG_DIR/manifest.json"
cp -f "$PKG_DIR/packages-lock.headless.json" "$PKG_DIR/packages-lock.json"

# Temporarily disable embedded Coplay package if present.
if [ -d "$PKG_DIR/Coplay" ]; then
  mv "$PKG_DIR/Coplay" "$PKG_DIR/Coplay.disabled"
fi

# Optional clean for CI-like runs:
# rm -rf "$PROJECT_DIR/Library/PackageCache"
