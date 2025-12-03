## Lwx.Builders — AI Agent Guidance (copilot-instructions)

Purpose
-------
This file provides succinct, actionable instructions for AI agents working in this repo. It helps you be immediately productive: know what to touch, how to build/test, and where to find examples.

Quick environment check ✅
-------------------------
- Project targets: **.NET 9.0**, **C# 13** — confirm with:
```bash
dotnet --list-sdks
```
- Build and test two quick commands:
```bash
dotnet build ./Lwx.Builders.sln
dotnet test ./Lwx.Builders.sln --no-build
```

Big picture (what & why) 💡
-------------------------
- This workspace contains generators and tests for two primary concerns:
  - `Lwx.Builders.MicroService`: a Roslyn incremental source generator for microservice code (endpoints, workers, swagger wiring, Service helper, etc.). Look at `Generator.cs` and `Processors/` for details.
  - `Lwx.Builders.Dto`: an independent incremental source generator handling DTOs (`Attributes/`, `Processors/`, `DtoGenerator.cs`).
- The design uses attribute-based declarations; generators emit partial classes and wiring with compile-time diagnostics. Generated code is placed in each project's `obj/` and emitted into the compilation as `.g.cs` files.

Where to look first — key files 🔎
-------------------------------
- Generator core: `Lwx.Builders.MicroService/Generator.cs`
- DTO core: `Lwx.Builders.Dto/DtoGenerator.cs`
- Processors: `*/Processors/` (each processor is focused and uses primary constructors)
- Attributes: `*/Attributes/` (embedded attribute templates are emitted to consumers)
- Tests: `Lwx.Builders.Dto.Tests/` and `Lwx.Builders.MicroService.Tests/` — `CompileErrorTests.cs` is important for diagnostics
- Sample projects: `Lwx.Builders.Dto.Tests/SampleProjects/` and `Lwx.Builders.MicroService.Tests/SampleProjects/`

Project-specific workflows — build/test/debug 🛠️
------------------------------------------------
- Typical flow when editing generators:
  1. Update generator or processor code in `*/Processors/` or `Generator.cs`.
  2. Build the solution or relevant projects:
```bash
dotnet build ./Lwx.Builders.sln
```
  3. Run tests to validate runtime and compile-time behavior:
```bash
dotnet test ./Lwx.Builders.sln --no-build
```
  4. Re-run or add compile-time diagnostics tests by building sample projects (see tests for details):
```bash
dotnet build Lwx.Builders.Dto.Tests/SampleProjects/<ProjectName> -v:m
```

- Test split and responsibilities:
  - Runtime tests (fast): `PositiveTests.cs`, `NegativeTests.cs` — use `System.Text.Json` run-time verification; NO MSBuild or reflection; keep them fast and deterministic.
  - Compile-time/diagnostic tests: `CompileErrorTests.cs` & `SampleProjects/` — use on-disk projects and `SharedTestHelpers` to run MSBuild and validate diagnostics & generated files.

Conventions & patterns (important) ⚖️
-----------------------------------
- Source generation style (REQUIRED)
  - Use `$$"""` raw interpolated string templates for multi-line generated code.
  - DO NOT include leading indentation inside templates; apply `.FixIndent(levels)` at inclusion sites.
  - Use file-scoped namespaces in generated files: `namespace Foo.Bar;`.
  - Generated file naming: `Endpoint{TypeName}.g.cs` by default; use namespace-qualified names when necessary (see `NamingExceptionJustification`).

- Attribute and analyzer advice
  - Emit attributes in `PostInitializationOutput` so the attributes are available to consumer projects.
  - For consuming sample projects, ensure generator is referenced as analyzer with `OutputItemType="Analyzer" ReferenceOutputAssembly="false"`.

- Diagnostics & validation
  - The generator uses diagnostics `LWX###`, which are defined in `Processors/` and `Generator.cs`. Read those files to see all the validation rules (e.g., `LWX011` / `LWX012` for `Service.cs` rules, `LWX018`/`LWX019` for Endpoints/Workers namespace checks).
  - Rule changes should include tests in `CompileErrorTests.cs` or sample projects where appropriate.

- Coding style & micro rules
  - Use `.NET 9.0` and `C# 13` features (primary constructors, pattern matching) where appropriate.
  - Prefer immutability and `readonly` fields/properties.
  - Avoid `@"..."` string form in C# code generation — follow repository rule: `Do NOT use @" strings`.
  - Avoid modifying auto-generated file contents directly. Update generator templates or processors instead.

Integration points & dependencies 🔗
---------------------------------
- Swagger (Swashbuckle) integration exists in generated `Service` wiring; the generator uses `PublishSwagger` setting and environment checks to include swagger setup.
- Message Bus/Event Hub features: there are processors in MicroService for Service Bus/Consumers/Producers — these are validated and wired by the generator.

How to add a new endpoint/processor (short example)
-------------------------------------------------
1. Add an attribute to `Lwx.Builders.MicroService/Attributes/` (or embed in `PostInitialization` via generator).
2. Add a new Processor under `Lwx.Builders.MicroService/Processors/` conforming to the primary constructor style: `class MyProcessor { public MyProcessor(Generator parent, AttributeInstance attr, SourceProductionContext spc, Compilation compilation) { ... } }`.
3. Hook the processor into the generator run (see `Generator.cs` initialization & where processors are invoked).
4. Add compile-time tests to `CompileErrorTests.cs` and runtime tests as needed.

Checklist before proposing changes (AI agent) ✅
-----------------------------------------------
- Confirm SDK: `dotnet --list-sdks` includes .NET 9.0.
- Build the project: `dotnet build ./Lwx.Builders.sln`.
- Run tests: `dotnet test ./Lwx.Builders.sln --no-build`.
- If adding or adjusting diagnostics, add a compile-time test and build the sample project to reproduce the diagnostic.
- Do NOT modify `// <auto-generated/>` or `.g.cs` files in the repo — update the generator or templates instead.
- Update `AGENTS.md` and `TODO.md` for additional context and change history when big changes are introduced.

When in doubt — ask
-------------------
- If unsure about any change or breaking behavior, ask the operator before making commits.

Notes and constraints
---------------------
- This repo aims to be deterministic and test-driven for generator changes — preserve test speed and determinism.
- Avoid global style changes unless the coding standard explicitly requests it (e.g., `FixIndent`, file-scoped namespaces, raw templates, etc.).

Reference files to update when making changes
-------------------------------------------
- Update local `AGENTS.md` files found in subprojects (`Lwx.Builders.MicroService/AGENTS.md`, `Lwx.Builders.Dto.Tests/AGENTS.md`) to keep the context history current.
- Update `TODO.md` entries where necessary.

---
If you find gaps or contradictory instructions across repository AGENTS.md files, open a PR or draft branch that unifies guidance and cite code samples used to make decisions.

Thank you for keeping the repo clean and maintainable — every change should be tested and documented.
- Look for AGENTS.md on the projects and update it with relevant context history.
- Look for TODO.md on the projects and update it with relevant context history.
- Update your suggestions on TODO.md tasks.
- USE .NET 9.0 and C# 13 features.
- Follow existing coding style and conventions in the codebase.
- Run formatting tools 
- Write clear, maintainable, and well-documented code.
- Build before testing (use one command dotnet build ...; dotnet test ...).
- Do NOT modify generated files directly. These files will include a marker such as a header comment with `// <auto-generated/>` or files using the pattern `<generated>` in the name. Treat those as read-only.
- Prefer immutability and `readonly` fields where meaningful.
- Split large methods in smaller helper methods for clarity.
- Never commit to git.
- Do NOT use @" strings.
- Changes in doc (.md files) doesn't require compile and test.
- When you find something difficult, ask the operator for clarification.
- Avoid composite comands so I can preauthorize them.

Source generation style guidelines (required)
-----------------------------------
When building multi-line code snippets for source generators:
1. Use raw interpolated string literals in block format:
   ```csharp
   srcBuilder.Append($$"""
       {{variable}}.MethodCall();
       
       """);
   ```
2. Generate snippets WITHOUT embedded indentation (no leading spaces inside the raw string). This is required for consistency and to avoid fragile leading whitespace in generated artifacts.
3. Apply indentation at template inclusion time using `.FixIndent(levels)` where `levels` is the number of indentation levels (4 spaces per level). Each generator project must provide or reuse a `FixIndent` helper (string/StringBuilder ext.) so templates can be composed consistently.
   ```csharp
   var source = $$"""
      namespace {{ns}}
      {
         {{srcBuilder.FixIndent(1)}} // 1 level == 4 spaces
      }
      """;
   ```
4. The `FixIndent` extension normalizes line endings and prefixes non-empty lines with the specified indentation (levels * 4 spaces).

5. Use file-scoped namespaces in generated files where possible (C# 10+ style):
   - Emit `namespace {{ns}};` at the top of generated files instead of block-style `namespace {{ns}} { ... }` when the generated file's content does not require nested scope. This produces cleaner output and avoids extra indentation.

Important enforcement and local project guidance
-----------------------------------------------
- This repository's canonical policy is the section above. All generators across the workspace MUST follow this template style.
- If a project's `AGENTS.md` or `TODO.md` suggests an alternative approach to embedding multi-line generated source, update that file to match this policy or remove contradictory guidance so the project falls back to this global instruction.
- When you add or update a generator, ensure:
   - You use $$""" raw interpolated templates for blocks of generated source.
   - Snippets inside templates are not indented (no leading whitespace) — use `.FixIndent()` on the final inclusion site.
   - You add or reuse a `FixIndent` helper in the generator project (string/StringBuilder extension) if one doesn't already exist.
   - Avoid manual string concatenation for multi-line blocks — raw templates are clearer and safer.

If uncertain, prefer the global policy in this file and update local AGENTS.md/TODO.md to match.

Generator output naming conventions (recommended)
-----------------------------------------------
- Prefer generated file names that match the primary type being generated to make it easier to map artifacts back to source types and avoid collisions. Example:
   - Endpoint generators should emit `Endpoint{TypeName}.g.cs` for normal endpoints.
   - If an attribute includes a naming exception/justification (e.g., `NamingExceptionJustification`) and the declared type name might collide, consider emitting a namespace-qualified file name `{{FullNamespace}}.{{TypeName}}.g.cs` to disambiguate.
- Avoid emitting additional marker classes or duplicate placeholder types unless they serve a specific compatibility purpose.

AI agent verification checklist
------------------------------
1. Environment & SDK check
   - Ensure `dotnet --list-sdks` includes .NET 9.0 or above.
   - Abort if no supported SDK is detected.

2. Sanitization & security
   - Do not interpolate untrusted input into generated source. Always sanitize or validate input.