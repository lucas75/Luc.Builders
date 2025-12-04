# ExampleOrg.Product.ServiceAbc.Tests

Tests for the example microservice project.

## Running Tests

```bash
dotnet test ./ExampleOrg.Product.ServiceAbc.Tests/ExampleOrg.Product.ServiceAbc.Tests.csproj
```

## Test Structure

- `UnitTest1.cs` — Service attribute validation tests
- `WorkerConfigTests.cs` — Worker configuration tests
- `MockServer.cs` — Test infrastructure for in-memory server
- `MockServer.appsettings.json` — Static test configuration

## See Also

- [ExampleOrg.Product.ServiceAbc](../ExampleOrg.Product.ServiceAbc/) — The example service being tested
