#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
apk_path="${APK_PATH:-$repo_root/artifacts/NotifyClassifier.apk}"
package_name="dev.danielreis.notifyclassifier"
listener_component="$package_name/$package_name.NotificationCaptureService"
ui_dump="/sdcard/notify-classifier-e2e.xml"
e2e_token="ClassifierE2E$$"

if [[ ! -f "$apk_path" ]]; then
  echo "APK not found: $apk_path" >&2
  echo "Run ./build.sh first." >&2
  exit 1
fi

api_status="$(curl --fail-with-body --silent --show-error --max-time 10 http://localhost:5080/)"
if [[ "$api_status" != *'"model":"gpt-5.6-luna"'* || "$api_status" != *'"mode":"CodexCli"'* ]]; then
  echo "The local API is not running in gpt-5.6-luna CodexCli mode: $api_status" >&2
  exit 1
fi

mapfile -t connected_devices < <(adb devices | awk 'NR > 1 && $2 == "device" { print $1 }')
if [[ -n "${ANDROID_SERIAL:-}" ]]; then
  device_serial="$ANDROID_SERIAL"
elif [[ ${#connected_devices[@]} -eq 1 ]]; then
  device_serial="${connected_devices[0]}"
else
  echo "Connect exactly one Android device or set ANDROID_SERIAL." >&2
  exit 1
fi

adb_for_device=(adb -s "$device_serial")

dump_ui() {
  "${adb_for_device[@]}" shell uiautomator dump "$ui_dump" >/dev/null
}

find_node() {
  local attribute="$1"
  local value="$2"
  "${adb_for_device[@]}" exec-out cat "$ui_dump" |
    grep -oE "<node [^>]*$attribute=\"$value\"[^>]*>" |
    head -1
}

tap_node() {
  local attribute="$1"
  local value="$2"
  local node
  local bounds
  local left
  local top
  local right
  local bottom

  dump_ui
  node="$(find_node "$attribute" "$value")"
  bounds="$(sed -n 's/.*bounds="\[\([0-9]*\),\([0-9]*\)\]\[\([0-9]*\),\([0-9]*\)\]".*/\1 \2 \3 \4/p' <<<"$node")"
  if [[ -z "$bounds" ]]; then
    echo "Could not find $attribute=$value in the current Android UI." >&2
    exit 1
  fi

  read -r left top right bottom <<<"$bounds"
  "${adb_for_device[@]}" shell input tap "$(((left + right) / 2))" "$(((top + bottom) / 2))"
}

wait_for_node() {
  local attribute="$1"
  local value="$2"
  local deadline=$((SECONDS + 30))
  while (( SECONDS < deadline )); do
    dump_ui
    if find_node "$attribute" "$value" >/dev/null; then
      return 0
    fi
    sleep 1
  done

  echo "Timed out waiting for $attribute=$value." >&2
  return 1
}

"${adb_for_device[@]}" install -r "$apk_path"
"${adb_for_device[@]}" shell pm clear "$package_name" >/dev/null
"${adb_for_device[@]}" shell cmd notification allow_listener "$listener_component"
"${adb_for_device[@]}" shell monkey -p "$package_name" -c android.intent.category.LAUNCHER 1 >/dev/null
wait_for_node resource-id "$package_name:id/ApiUrlEntry"

tap_node content-desc Apps
wait_for_node resource-id "$package_name:id/AppSearch"
tap_node resource-id "$package_name:id/AppSearch"
"${adb_for_device[@]}" shell input text com.android.shell
sleep 1
"${adb_for_device[@]}" shell input keyevent BACK
wait_for_node resource-id "$package_name:id/AppToggle_com_android_shell"
tap_node resource-id "$package_name:id/AppToggle_com_android_shell"
sleep 1
dump_ui
if [[ "$(find_node resource-id "$package_name:id/AppToggle_com_android_shell")" != *'checked="true"'* ]]; then
  echo "The Android shell app was not selected." >&2
  exit 1
fi

"${adb_for_device[@]}" shell cmd notification post \
  -t "$e2e_token" notify-classifier-e2e local-e2e-success >/dev/null

deadline=$((SECONDS + 210))
while (( SECONDS < deadline )); do
  tap_node content-desc Monitor
  sleep 1
  tap_node content-desc Queue
  sleep 1
  dump_ui
  queue_xml="$("${adb_for_device[@]}" exec-out cat "$ui_dump")"
  if [[ "$queue_xml" == *"$e2e_token"* &&
        "$queue_xml" == *'Completed • Notification triage'* &&
        "$queue_xml" == *'"category"'* ]]; then
    echo "Android E2E passed on $device_serial: notification retained and classified by gpt-5.6-luna."
    exit 0
  fi
  sleep 3
done

echo "Timed out waiting for the retained notification to reach Completed." >&2
exit 1
