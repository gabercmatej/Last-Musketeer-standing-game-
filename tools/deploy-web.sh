#!/usr/bin/env bash
# Deploys the Unity Web build in Builds/Web to Netlify.
# First time only: npx netlify-cli login   (then this script asks you to create/link a site)
set -euo pipefail
cd "$(dirname "$0")/.."

BUILD_DIR="Builds/Web"
if [ ! -f "$BUILD_DIR/index.html" ]; then
  echo "No Web build found in $BUILD_DIR. Build the Web profile into that folder first." >&2
  exit 1
fi

npx --yes netlify-cli deploy --prod --dir "$BUILD_DIR"
