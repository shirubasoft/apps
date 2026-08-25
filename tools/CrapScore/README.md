# CRAP score tool

This repository-local tool calculates method CRAP scores from one or more Cobertura coverage reports.
It accepts report files directly or searches supplied directories recursively for
`coverage.cobertura.xml`.

```bash
dotnet run --project tools/CrapScore -- \
  app/artifacts/test-results \
  --output app/artifacts/coverage/crap-score.md
```

Pass `--base-reports` to compare the current maximum with coverage from the main branch. While the
main-branch maximum exceeds the target, which defaults to 5, the current maximum must be strictly
lower. Once main reaches the target, the current maximum may not exceed it.

```bash
dotnet run --project tools/CrapScore -- \
  app/artifacts/test-results \
  --base-reports artifacts/crap-main-coverage \
  --target 5
```

The tool combines duplicate method coverage by taking the union of covered lines. It rejects missing
method complexity, missing line data, and conflicting complexity values instead of producing a
plausible but incorrect score.
