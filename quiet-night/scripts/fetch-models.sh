#!/usr/bin/env bash
# Downloads the prebuilt YAMNet ONNX model (Google, Apache-2.0) that training/finetune.py fine-tunes into the app's
# sound model, and verifies its checksum.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

url="https://huggingface.co/audiomagic/yamnet-onnx/resolve/f25b741c2f0bdc6d7e6db24b5fddda23347dbafd/yamnet.onnx"
sha256="d3835ffbbd4a1bb3e777f0ca217b5007907f5171dd5d17c4236b95b2af8f908e"
target="models/yamnet.onnx"

if [[ -f "$target" ]] && echo "$sha256  $target" | sha256sum --check --status; then
  exit 0
fi
mkdir -p "$(dirname "$target")"
curl --fail --location --silent --show-error --retry 3 --output "$target.part" "$url"
echo "$sha256  $target.part" | sha256sum --check --quiet
mv "$target.part" "$target"
echo "Model: $target"
