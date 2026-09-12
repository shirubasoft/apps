#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
android_serial="${ANDROID_SERIAL:-}"
adb_args=()
if [[ -n "$android_serial" ]]; then adb_args=(-s "$android_serial"); fi
adb "${adb_args[@]}" get-state >/dev/null
cd src/TennisWatch
dotnet build TennisWatch.csproj -c Debug -t:SignAndroidPackage
adb "${adb_args[@]}" install -r bin/Debug/net10.0-android/dev.danielreis.tenniswatch-Signed.apk
android_activity="$(adb "${adb_args[@]}" shell cmd package resolve-activity --brief dev.danielreis.tenniswatch | tail -1 | tr -d '\r')"
adb "${adb_args[@]}" shell am start -W -n "$android_activity"
exec adb "${adb_args[@]}" logcat --pid="$(adb "${adb_args[@]}" shell pidof dev.danielreis.tenniswatch | tr -d '\r')"
