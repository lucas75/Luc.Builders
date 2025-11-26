This folder contains sample projects used by the unit tests in Lwx.Builders.Dto.Tests.

Purpose
-------
- Provide small example projects that exercise the source generator in realistic compilation setups.
- Some sample projects are intended to succeed (produce generated code), others intentionally fail (emit diagnostics) so tests can verify generator behavior.

How to build and inspect
------------------------
1. From the repository root run:

   dotnet build Lwx.Builders.Dto.Tests/SampleProjects/<ProjectName> -v:m

2. Generated sources are emitted to the project's obj/generated folder (we set EmitCompilerGeneratedFiles and CompilerGeneratedFilesOutputPath in the samples).

3. Tests use the NegativeTests tests to run and inspect these sample projects (see `NegativeTests.cs` which calls `SharedTestHelpers.BuildAndRunSampleProject`). You can run the tests by executing the normal test target:

   dotnet test Lwx.Builders.Dto.Tests/Lwx.Builders.Dto.Tests.csproj

Notes for maintainers
---------------------
- Keep these sample projects minimal and readable; they're developer-facing fixtures to make debugging generator behavior easier.
- If you add a new sample project, ensure the ProjectReference to the generator uses OutputItemType="Analyzer" and ReferenceOutputAssembly="false" so the generator runs during project compilation.
