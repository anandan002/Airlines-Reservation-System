#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

export DOTNET_ROOT="$ROOT_DIR/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"

exec dotnet run "$@"
