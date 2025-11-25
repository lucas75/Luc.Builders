# Lwx.Builders.Dto

Lwx.Builders.Dto provides attributes and a source-generator that produces JSON-friendly DTO partial implementations for consumer projects.

This README focuses on how to use the library in your projects: installation, common examples, and troubleshooting. Implementation and development details live in `CONTRIBUTING.md`.

---

## Quick start

1. Install the package or reference the generator in your solution.

   - If available on NuGet: `dotnet add package Lwx.Builders.Dto`
   - Or add a project reference to the generator project in your solution.

2. Use the provided attributes in your consumer code and mark DTO classes/properties `partial` where required. Build your project — generated code will be added to the compilation automatically.

## Basic usage example

```csharp

## Quick start

1. Install the package or reference the generator in your solution.

   - If available on NuGet: `dotnet add package Lwx.Builders.Dto`
   - Or add a project reference to the generator project in your solution.

2. Use the provided attributes in your consumer code and mark DTO classes/properties `partial` where required. Build your project — generated code will be added to the compilation automatically.

## Example: All-in-one DTO

This example demonstrates primitive properties, a date property with a converter, an enum property, and an ignored property in a single DTO:

```csharp
[LwxDto(Type = DtoType.Normal)]
public partial class ExampleDto
{
  // Primitive property
  [LwxDtoProperty(JsonName = "id")]
  public required partial int Id { get; set; }

  // Nullable primitive property
  [LwxDtoProperty(JsonName = "age")]
  public partial int? Age { get; set; }

  // String property
  [LwxDtoProperty(JsonName = "name")]
  public partial string? Name { get; set; }

  // Date property with converter
  [LwxDtoProperty(JsonName = "start", JsonConverter = typeof(MyDateOnlyConverter))]
  public partial DateOnly Start { get; set; }

  // Enum property
  [LwxDtoProperty(JsonName = "status")]
  public partial MyStatus Status { get; set; }

  // Ignored property
  [LwxDtoIgnore]
  public partial int TempId { get; set; }
}

public enum MyStatus
{
  [JsonPropertyName("active")] Active,
  [JsonPropertyName("inactive")] Inactive
}

public sealed class MyDateOnlyConverter : JsonConverter<DateOnly>
{
  // Implement Read/Write methods as needed
}
```

## Basic usage example

```csharp
using Lwx.Builders.Dto.Atributes;

[LwxDto(Type = DtoType.Normal)]
public partial class SimpleResponseDto
{
  [LwxDtoProperty(JsonName = "ok")]
  public required partial bool Ok { get; set; }

  [LwxDtoProperty(JsonName = "err-id")]
  public partial string? ErrId { get; set; }

  [LwxDtoProperty(JsonName = "err-msg")]
  public partial string? ErrMsg { get; set; }
}
```

Build your project and the generator will produce the corresponding `.g.cs` file with JSON-friendly property accessors.
  // Ignored property
  [LwxDtoIgnore]
  public partial int TempId { get; set; }
}
```

## Run tests (developer note)

To run the test suite for this package:

```bash
dotnet build
dotnet test Lwx.Builders.Dto.Tests
```

For contributing and development guidance see `CONTRIBUTING.md`.


