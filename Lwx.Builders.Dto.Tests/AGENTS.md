```markdown
# AGENTS.md — Lwx.Builders.Dto.Tests

This document is the authoritative context and quick-start for AI agents (and humans) working on the `Lwx.Builders.Dto.Tests` project.

Purpose
-------
- Explain why the tests exist and the intended structure (runtime vs MSBuild/sample tests).
- Hold brief, actionable guidelines for automated or human contributors (especially AI agents) who need to maintain or extend tests.

Project context (short)
-----------------------
- The test suite is organized into two major testing approaches:
  - Runtime/Unit tests: fast tests that compile DTO types into the test assembly and use System.Text.Json at runtime.
    * Files: `PositiveTests.cs`, `NegativeTests.cs`, `StructuralTests.cs`, `OtherTests.cs`.
    * Canonical DTOs used by runtime tests: `NormalDto`, `DictDto`, `IgnoreDto` (under `Dto/`).
   - On-disk MSBuild sample projects: used for negative/compile-error tests and to validate generator diagnostics.
    * Location: `SampleProjects/` (examples: `ErrorDto`)
    * Tests that rely on these use `SharedTestHelpers.BuildAndRunSampleProject(...)` for reproducible builds.

Recent history and rationale
----------------------------
- Tests were recently reorganized to be more realistic and faster:
  - Good-path tests were moved into the test assembly + runtime checks (faster).
  - Failure/diagnostic tests stayed as on-disk MSBuild sample projects to allow deterministic builds and inspect generated files.
  - The test fixtures were consolidated to three canonical DTO fixtures: `NormalDto`, `DictDto`, `IgnoreDto`.

Agent responsibilities (what an AI agent should do when editing tests)
------------------------------------------------------------------
1. Preserve test speed & determinism
   - Prefer modifying runtime tests when the change is purely behavioral or about serialization runtimes.
   - Use on-disk sample projects only when you need to reproduce MSBuild-time diagnostics or validate generated source files.

   IMPORTANT: Reflection and compiler/MSBuild access policy
   - Reflection-based inspection of generated types (using System.Reflection) should only be used in `StructuralTests.cs`.
   - Compiler / MSBuild-driven builds or direct inspection of generated source files should only be used in `CompileErrorTests.cs` (or tests that explicitly exercise sample projects under `SampleProjects/`).
   - Do NOT use reflection or run ad-hoc compiler/MSBuild operations from runtime Positive/Negative tests — these tests must remain pure, fast, and deterministic (System.Text.Json runtime behavior only).

2. Test authoring guidelines
   - Prefer xUnit Theories and InlineData for variants (e.g., date/time formats or edge-case values).
   - Keep sample projects minimal and clearly named — they are developer-facing fixtures.
   - Do not commit generated files. Tests should build the sample projects to reproduce generator output and diagnostics.

3. When adding a new sample project
   - Add it under `SampleProjects/<Name>/` and keep it tiny and well-documented.
   - Ensure the ProjectReference to the generator uses: OutputItemType="Analyzer" ReferenceOutputAssembly="false" so the generator runs correctly during build.
   - Add/adjust tests that call `SharedTestHelpers.BuildAndRunSampleProject("<Name>")` to exercise the new sample.

4. When modifying public canonical DTOs
   - Update `Dto/NormalDto.cs`, `Dto/DictDto.cs`, or `Dto/IgnoreDto.cs` only when the change is intended across many tests.
   - If change is narrowly scoped, prefer introducing a small new DTO fixture to avoid breaking unrelated tests.

5. Keep diagnostics reproducible
   - If tests assert diagnostics from the source generator (like LWX004), make sure the sample project and test assert the exact diagnostic ID, location and message where appropriate.

6. CI / Local validation steps for agents
   - Build only the tests project to validate compile-time generator outputs:

```bash
dotnet build ./Lwx.Builders.Dto.Tests/Lwx.Builders.Dto.Tests.csproj
```

   - Run tests quickly locally (no-build is faster if you just compiled already):

```bash
dotnet test ./Lwx.Builders.Dto.Tests/Lwx.Builders.Dto.Tests.csproj --no-build
```

   - When editing samples or generators, run the sample project builds directly for easier debugging:

```bash
dotnet build Lwx.Builders.Dto.Tests/SampleProjects/<ProjectName> -v:m
```

7. Safety and style
   - Do not modify generated files. Tests rely on the generator's outputs; modifying generated outputs will make tests meaningless.
   - Avoid adding reflection or build-time logic into runtime tests. If a test needs to examine generated code or diagnostics, put it in `StructuralTests.cs` or `CompileErrorTests.cs` and follow the sample-projects approach.
   - When adding new tests or DTOs, follow the established small, readable coding style used in the repo and keep methods short.

   Generator template policy
   -------------------------
   - When writing or updating generators in this workspace, use the canonical generator template style defined in `/.github/copilot-instructions.md`:
      - Use $$""" raw interpolated templates for multi-line generated code
      - Do not embed leading indentation inside templates; apply `.FixIndent(levels)` at inclusion sites
      - Add or reuse a `FixIndent` helper in the generator project if missing
      - If this file or tests suggested a different template approach, it was superseded by the canonical policy in the repo root and should be updated accordingly.

8. Documentation & changelog
   - Update this `AGENTS.md` file with a short history of large changes and why they were made.
   - If a change requires broader project knowledge (e.g., cross-repository), leave a short note for maintainers and a link to the relevant discussion/issue.

Where to find things
--------------------
- Test assembly DTOs: `Lwx.Builders.Dto.Tests/Dto/`
- Sample projects: `Lwx.Builders.Dto.Tests/SampleProjects/` (used by compile-error tests)
- Helper utilities: `Lwx.Builders.Dto.Tests/SharedTestHelpers.cs`
- Compile-error tests: `CompileErrorTests.cs`
- Runtime positive/negative tests: `PositiveTests.cs`, `NegativeTests.cs`

If uncertain — ask for guidance
--------------------------------
If an AI agent is unsure about making a change (breaks many tests or requires generator changes), it should:
1) Add a draft PR with minimal changes and tests that demonstrate the issue.
2) Run the full test suite locally and attach a short rationale in `AGENTS.md` describing the problem and planned approach.

---
This document is intended to be succinct and practical. Keep it up-to-date with key edit history.
```
