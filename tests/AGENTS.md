# Test guidance

These rules add detail to the repository guidance for test code under `tests/`.

- Keep tests deterministic and focused on observable behavior rather than implementation details.
- Prefer meaningful names and an Arrange/Act/Assert structure. Comments are optional and should clarify only non-obvious steps.
- Use xUnit, `AutoFixture.Xunit2`, and FluentAssertions as established by the repository. Prefer `[AutoData]`, `[InlineAutoData]`, and `[MemberData]` when generated data makes a test clearer.
- Use FluentAssertions for assertions and an assertion scope when a test performs multiple related checks.
- Use a mocking library already referenced by the test project when mocking is necessary. Do not mock extension methods such as `ILogger` calls.
- Do not run tests unless the user explicitly requests them; build affected projects when verification is appropriate.
