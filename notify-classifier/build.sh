#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
configuration="${CONFIGURATION:-Release}"
artifact_root="$repo_root/artifacts"
test_results="$artifact_root/test-results"
coverage_root="$artifact_root/coverage"
android_output="$artifact_root/android"

rm -rf "$test_results" "$coverage_root" "$android_output"
mkdir -p "$test_results" "$coverage_root" "$android_output"

dotnet restore "$repo_root/NotifyClassifier.slnx"
dotnet build "$repo_root/NotifyClassifier.slnx" --configuration "$configuration" --no-restore

dotnet test "$repo_root/tests/NotifyClassifier.Core.Tests/NotifyClassifier.Core.Tests.csproj" \
  --configuration "$configuration" --no-build --no-restore \
  --collect:"XPlat Code Coverage" --results-directory "$test_results/core"
dotnet test "$repo_root/tests/NotifyClassifier.Api.Tests/NotifyClassifier.Api.Tests.csproj" \
  --configuration "$configuration" --no-build --no-restore \
  --collect:"XPlat Code Coverage" --results-directory "$test_results/api"
dotnet test "$repo_root/tests/NotifyClassifier.EndToEnd.Tests/NotifyClassifier.EndToEnd.Tests.csproj" \
  --configuration "$configuration" --no-build --no-restore

dotnet run --project "$repo_root/tools/CrapScore/CrapScore.csproj" \
  --configuration "$configuration" --no-build --no-restore -- \
  "$test_results" --threshold 30 --output "$coverage_root/crap-score.md"

dotnet publish "$repo_root/src/NotifyClassifier.App/NotifyClassifier.App.csproj" \
  --configuration "$configuration" --framework net11.0-android --no-restore \
  -p:AndroidPackageFormat=apk -p:PublishDir="$android_output/"

apk_path="$(find "$android_output" -maxdepth 1 -type f -name '*-Signed.apk' -print -quit)"
if [[ -z "$apk_path" ]]; then
  apk_path="$(find "$android_output" -maxdepth 1 -type f -name '*.apk' -print -quit)"
fi
if [[ -z "$apk_path" ]]; then
  echo "Android publish produced no APK." >&2
  exit 1
fi

cp "$apk_path" "$artifact_root/NotifyClassifier.apk"
echo "APK: $artifact_root/NotifyClassifier.apk"
