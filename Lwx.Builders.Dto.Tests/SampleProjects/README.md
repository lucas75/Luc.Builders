SampleProjects — test fixtures for the DTO source generator
==========================================================

This folder contains several small .NET projects used by the unit tests in the `Lwx.Builders.Dto.Tests` project. They are intentionally simple standalone projects that reference the generator project as an analyzer so the generator runs during compilation.

Why these exist
----------------
- They help us reproduce how the generator behaves when executed in a real MSBuild compilation (instead of only using in-memory Roslyn test hosts).
- Tests run 'dotnet build' on these projects and then inspect the compiler output and the generated sources under `obj/generated`.

Structure
---------
Each sample project lives under a subfolder matching the project name (e.g. `ErrorDto` for failing fixtures).
The canonical "good" DTO fixtures (NormalDto, DictDto, IgnoreDto) are compiled directly into the test project under `Lwx.Builders.Dto.Tests/Dto` (namespace `Lwx.Builders.Dto.Tests.Dto`) so runtime tests can instantiate and exercise them.
- DTO types live under `Dto/` within the project and use the namespace <ProjectName>.Dto. The repo now prefers two canonical fixtures:
	- `ErrorDto` — a merged fixture intentionally containing failing/edge-case DTO types to exercise generator diagnostics.
 - Supporting types (converters/utilities) live under `Dto/` (e.g. helper/source files such as "Util.cs") when present.
- Each project contains a minimal `Program.cs` to make the project runnable for validation.

How tests use them
-------------------
- Tests call `dotnet build` on a sample project, collect any diagnostics from the build output, and read generated source files from `obj/generated`.

When to add a new sample
------------------------
- Add a sample if you need to validate generator behavior in a specific real-world layout or to capture a failing behavior to prevent regressions.
