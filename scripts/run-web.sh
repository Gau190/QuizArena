#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
DOTNET="${DOTNET:-dotnet}"
PORT="${PORT:-5227}"
# Do not `source .env` — semicolons in the connection string break the shell.
# QuizArena loads .env via DotEnv.Load() in Program.cs.
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Production}"
"$DOTNET" run --no-launch-profile --project src/QuizArena.Web --urls "http://localhost:${PORT}"
