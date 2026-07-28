# Source code guidance

These rules add detail to the repository guidance for source and project files under `src/`.

- Application projects target .NET 8. Roslyn/source-generator projects may target `netstandard2.0` and use the language version already declared in their project file.
- Use async/await for I/O, dependency injection, file-scoped namespaces, and explicit types for public APIs.
- Use `var` only when the type is obvious. Prefer immutability and avoid unnecessary allocations when the resulting code remains clear.
- Prefer collection expressions when their target type and behavior are unambiguous; do not change `ToList`/`ToArray` solely for stylistic reasons.
- Follow SOLID principles and do not use obsolete APIs.
- For source changes, apply the verification commands in the repository-root `AGENTS.md`. Do not run tests unless the user explicitly requests them.
