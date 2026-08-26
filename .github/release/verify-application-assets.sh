#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 3 || -z "$1" || -z "$2" ]]; then
  echo "Usage: $0 <release-version> <version-file> <asset> [<asset> ...]" >&2
  exit 2
fi

release_version="$1"
version_file="$2"
shift 2

if [[ ! -f "$version_file" ]]; then
  echo "The CI artifact does not contain $version_file." >&2
  exit 1
fi

artifact_version="$(<"$version_file")"
if [[ "$artifact_version" != "$release_version" ]]; then
  echo "CI built version $artifact_version, but semantic-release selected $release_version." >&2
  exit 1
fi

for asset_path in "$@"; do
  if [[ ! -s "$asset_path" ]]; then
    echo "The CI artifact does not contain a nonempty $asset_path." >&2
    exit 1
  fi
done
