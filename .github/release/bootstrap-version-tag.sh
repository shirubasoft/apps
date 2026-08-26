#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 || -z "$1" ]]; then
  echo "Usage: $0 <tag-prefix>" >&2
  exit 2
fi

tag_prefix="$1"
baseline_tag="${tag_prefix}0.0.0"

git check-ref-format "refs/tags/$baseline_tag" >/dev/null

if git describe --tags --abbrev=0 --match "${tag_prefix}[0-9]*" >/dev/null 2>&1; then
  exit 0
fi

first_commit="$(git rev-list --max-parents=0 HEAD | tail -n 1)"
if [[ -z "$first_commit" ]]; then
  echo "Cannot find the repository's first commit." >&2
  exit 1
fi

git tag "$baseline_tag" "$first_commit"
