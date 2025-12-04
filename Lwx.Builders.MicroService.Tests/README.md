# Lwx.Builders.MicroService.Tests

Unit and integration tests for the `Lwx.Builders.MicroService` source generator.

## Running Tests

```bash
dotnet test ./Lwx.Builders.MicroService.Tests/Lwx.Builders.MicroService.Tests.csproj
```

## Test Structure

- `MockServerTests.cs` — Runtime tests using in-memory ASP.NET Core TestHost
- `CompileErrorTests.cs` — Compile-time diagnostic validation
- `MockServices/` — Test infrastructure (MockServer, counters)
- `MockServices001/`, `MockServices002/` — Sample services for testing

## See Also

- [Lwx.Builders.MicroService README](../Lwx.Builders.MicroService/README.md)
- [AGENTS.md](AGENTS.md) for AI agent context
