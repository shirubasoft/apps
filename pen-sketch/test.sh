#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
dotnet test tests/PenSketch.Tests/PenSketch.Tests.csproj --configuration Release \
  --collect:"XPlat Code Coverage" --results-directory artifacts/test-results
dotnet run --project ../tools/CrapScore/CrapScore.csproj --configuration Release -- \
  artifacts/test-results --output artifacts/coverage/crap-score.md
