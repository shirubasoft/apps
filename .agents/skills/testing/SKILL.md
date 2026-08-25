---
name: testing
description: Write or run tests whenever a task needs new or updated test coverage, regression verification, test execution, or investigation of a test failure.
---

# Testing

## Invariants

Tests should always run in CI with the same script it uses locally.

Tests should always be designed to be deterministic and run in parallel. 

Avoid using mocks.

Avoid repeating setup code.

## Unit tests

Should be data-driven. If it's not possible, refactoring may be needed.

## Integration tests

If they depend on external resources, prefer end-to-end tests. If they don't, prefer unit tests.

## End-to-end tests

Use Aspire's testing builder. Telemetry (traces, logs, metrics) can and should be improved to debug failures.
