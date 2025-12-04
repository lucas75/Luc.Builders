# ExampleOrg.Product.ServiceAbc

Example microservice demonstrating `Lwx.Builders.MicroService` usage.

## Structure

```
ExampleOrg.Product.ServiceAbc/
├── Service.cs          # [LwxService] definition
├── Program.cs          # Entry point (generated)
├── Endpoints/          # HTTP endpoints
│   ├── EndpointAbcCde.cs
│   ├── EndpointOldStart.cs
│   ├── ExampleProc/
│   └── Proc01/
├── Workers/            # Background workers
│   └── TheWorker.cs
└── Dto/                # Data transfer objects
    └── SimpleResponseDto.cs
```

## Running

```bash
dotnet run --project ./ExampleOrg.Product.ServiceAbc/ExampleOrg.Product.ServiceAbc.csproj
```

## See Also

- [Lwx.Builders.MicroService](../Lwx.Builders.MicroService/README.md) — The generator library
- [Lwx.Builders.Dto](../Lwx.Builders.Dto/README.md) — DTO generator
