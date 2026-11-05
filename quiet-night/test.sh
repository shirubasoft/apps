#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
./scripts/fetch-models.sh
rm -rf artifacts/test-results artifacts/evaluation
QUIET_NIGHT_REPORT_DIR="$PWD/artifacts/evaluation" \
  dotnet test tests/QuietNight.Tests/QuietNight.Tests.csproj --configuration Release \
  --collect:"XPlat Code Coverage" --results-directory artifacts/test-results
dotnet run --project ../tools/CrapScore/CrapScore.csproj --configuration Release -- \
  artifacts/test-results --output artifacts/coverage/crap-score.md
