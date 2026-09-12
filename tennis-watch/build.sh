#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
configuration="${CONFIGURATION:-Release}"
artifact_root="$repo_root/artifacts"
test_results="$artifact_root/test-results"
coverage_root="$artifact_root/coverage"
android_output="$artifact_root/android"
release_version=""
release_msbuild_args=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --release-version)
      if [[ $# -lt 2 || -z "$2" ]]; then
        echo "--release-version requires a value." >&2
        exit 2
      fi
      release_version="$2"
      shift 2
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 2
      ;;
  esac
done

if [[ -n "$release_version" ]]; then
  if [[ ! "$release_version" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]]; then
    echo "Release version must be an exact stable semantic version." >&2
    exit 2
  fi

  version_major="${BASH_REMATCH[1]}"
  version_minor="${BASH_REMATCH[2]}"
  version_patch="${BASH_REMATCH[3]}"
  if (( version_minor > 999 || version_patch > 999 )); then
    echo "Release version minor and patch components must not exceed 999." >&2
    exit 2
  fi

  android_version_code=$((version_major * 1000000 + version_minor * 1000 + version_patch))
  if (( android_version_code < 1 || android_version_code > 2100000000 )); then
    echo "Release version produces an Android version code outside 1..2100000000." >&2
    exit 2
  fi

  release_msbuild_args=(
    "-p:VersionPrefix=$release_version"
    "-p:ApplicationDisplayVersion=$release_version"
    "-p:ApplicationVersion=$android_version_code"
  )
fi

rm -rf "$test_results" "$coverage_root" "$android_output"
rm -f "$artifact_root/TennisWatch.apk" "$artifact_root/release-version.txt"
mkdir -p "$test_results" "$coverage_root" "$android_output"

cd "$repo_root"
./test.sh

dotnet build "$repo_root/src/TennisWatch.AppHost/TennisWatch.AppHost.csproj" --configuration "$configuration"

cd "$repo_root/src/TennisWatch"
# Regenerate Android bindings together; stale incremental bindings can crash at startup.
dotnet clean TennisWatch.csproj --configuration "$configuration" --framework net10.0-android
dotnet publish TennisWatch.csproj \
  --configuration "$configuration" --framework net10.0-android \
  -p:AndroidPackageFormat=apk -p:PublishDir="$android_output/" \
  "${release_msbuild_args[@]}"

apk_path="$(find "$android_output" -maxdepth 1 -type f -name '*-Signed.apk' -print -quit)"
if [[ -z "$apk_path" ]]; then
  apk_path="$(find "$android_output" -maxdepth 1 -type f -name '*.apk' -print -quit)"
fi
if [[ -z "$apk_path" ]]; then
  echo "Android publish produced no APK." >&2
  exit 1
fi

cp "$apk_path" "$artifact_root/TennisWatch.apk"
if [[ -n "$release_version" ]]; then
  printf '%s\n' "$release_version" > "$artifact_root/release-version.txt"
fi
echo "APK: $artifact_root/TennisWatch.apk"
