#!/usr/bin/env bash
# The one gate: restore from the lock files, build with warnings as errors, check formatting, run every test.
# CI runs exactly this. Run it before every push.
set -euo pipefail
cd "$(dirname "$0")/.."

# The toolchain sends nothing home either (AGENTS.md, standing constraints).
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export TESTINGPLATFORM_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

solution=RaidBridge.slnx

dotnet restore "$solution" --locked-mode
dotnet build "$solution" --no-restore --configuration Release
# Vendored code keeps upstream's style (AGENTS.md), so the format check skips it.
dotnet format "$solution" --no-restore --verify-no-changes --exclude third_party/
dotnet test --solution "$solution" --no-build --configuration Release
