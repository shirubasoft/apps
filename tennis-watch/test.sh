#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
rm -rf artifacts/test-results
dotnet test tests/TennisWatch.Tests/TennisWatch.Tests.csproj --configuration Release \
  --collect:"XPlat Code Coverage" --results-directory artifacts/test-results
dotnet run --project ../tools/CrapScore/CrapScore.csproj --configuration Release -- \
  artifacts/test-results --output artifacts/coverage/crap-score.md
