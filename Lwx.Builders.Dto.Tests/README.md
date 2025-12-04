# Lwx.Builders.Dto.Tests

Unit and integration tests for the `Lwx.Builders.Dto` source generator.

## Running Tests

```bash
dotnet test ./Lwx.Builders.Dto.Tests/Lwx.Builders.Dto.Tests.csproj
```

## Test Structure

- `PositiveTests.cs`, `NegativeTests.cs` — Runtime tests using System.Text.Json
- `StructuralTests.cs` — Reflection-based inspection of generated types
- `CompileErrorTests.cs` — Compile-time diagnostic validation
- `Dto/` — Canonical test DTOs (NormalDto, DictDto, IgnoreDto)
- `SampleProjects/` — On-disk projects for compile-time tests

## See Also

- [Lwx.Builders.Dto README](../Lwx.Builders.Dto/README.md)
- [AGENTS.md](AGENTS.md) for AI agent context
