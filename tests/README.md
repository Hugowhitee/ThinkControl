# Test plan

The test projects will be added during the v0.1 foundation work after the first concrete resolvers/contracts land.

Required test groups:

- Core unit tests: fan-state validation, profile coordination, diagnostics redaction
- DeviceProfiles tests: exact matching, unknown-device fallback, schema validation, remote metadata write-gating
- Hardware unit tests with fakes: provider selection, conflict handling, Auto rollback semantics
- Integration tests: UI/service IPC authorization and protocol version handling
- architecture tests: prevent UI -> Hardware and Core -> Windows-specific references

Hardware-in-the-loop X9 tests remain opt-in and must never run on generic CI runners.
