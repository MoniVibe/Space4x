#!/usr/bin/env bash
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PKG_DIR="$PROJECT_DIR/Packages"

if [ -f "$PKG_DIR/manifest.json.bak" ]; then
  cp -f "$PKG_DIR/manifest.json.bak" "$PKG_DIR/manifest.json"
fi
if [ -f "$PKG_DIR/packages-lock.json.bak" ]; then
  cp -f "$PKG_DIR/packages-lock.json.bak" "$PKG_DIR/packages-lock.json"
fi

if [ -d "$PKG_DIR/Coplay.disabled" ]; then
  mv "$PKG_DIR/Coplay.disabled" "$PKG_DIR/Coplay"
fi
