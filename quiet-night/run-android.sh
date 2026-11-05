#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
android_serial="${ANDROID_SERIAL:-}"
adb_args=()
if [[ -n "$android_serial" ]]; then adb_args=(-s "$android_serial"); fi
adb "${adb_args[@]}" get-state >/dev/null
./scripts/fetch-models.sh
dotnet build src/QuietNight/QuietNight.csproj -c Debug -t:SignAndroidPackage
adb "${adb_args[@]}" install -r src/QuietNight/bin/Debug/net11.0-android/dev.danielreis.quietnight-Signed.apk
android_activity="$(adb "${adb_args[@]}" shell cmd package resolve-activity --brief dev.danielreis.quietnight | tail -1 | tr -d '\r')"
adb "${adb_args[@]}" shell am start -W -n "$android_activity"
exec adb "${adb_args[@]}" logcat --pid="$(adb "${adb_args[@]}" shell pidof dev.danielreis.quietnight | tr -d '\r')"
