# Lwx.Builders.Dto

A Roslyn incremental source generator that produces JSON-friendly DTO partial implementations. Define your DTO properties with attributes, and the generator handles backing fields, JSON serialization attributes, and converters.

## Installation

Reference the generator in your project:

```xml
<ProjectReference Include="../Lwx.Builders.Dto/Lwx.Builders.Dto.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

## Quick Start

```csharp
using Lwx.Builders.Dto.Atributes;

[LwxDto(Type = DtoType.Normal)]
public partial class UserDto
{
    [LwxDtoProperty(JsonName = "id")]
    public required partial int Id { get; set; }

    [LwxDtoProperty(JsonName = "name")]
    public partial string? Name { get; set; }

    [LwxDtoProperty(JsonName = "email")]
    public partial string? Email { get; set; }
}
```

Build your project and the generator produces the backing field implementations with proper `[JsonPropertyName]` attributes.

## DTO Types

### Normal (Backing Fields)

```csharp
[LwxDto(Type = DtoType.Normal)]
public partial class ProductDto
{
    [LwxDtoProperty(JsonName = "product_id")]
    public required partial int ProductId { get; set; }

    [LwxDtoProperty(JsonName = "price")]
    public partial decimal? Price { get; set; }
}
```

Generated code uses private backing fields for each property.

### Dictionary (Dynamic Storage)

```csharp
[LwxDto(Type = DtoType.Dictionary)]
public partial class DynamicDto
{
    [LwxDtoProperty(JsonName = "key")]
    public partial string? Key { get; set; }

    [LwxDtoProperty(JsonName = "value")]
    public partial object? Value { get; set; }
}
```

Generated code uses a dictionary for storage, useful for dynamic scenarios.

## Property Types

### Primitives

```csharp
[LwxDtoProperty(JsonName = "count")]
public partial int Count { get; set; }

[LwxDtoProperty(JsonName = "is_active")]
public partial bool? IsActive { get; set; }

[LwxDtoProperty(JsonName = "amount")]
public partial decimal Amount { get; set; }
```

### Strings

```csharp
[LwxDtoProperty(JsonName = "name")]
public partial string? Name { get; set; }

[LwxDtoProperty(JsonName = "code")]
public required partial string Code { get; set; }
```

### Dates with Converters

```csharp
[LwxDtoProperty(JsonName = "created_at", JsonConverter = typeof(MyDateTimeConverter))]
public partial DateTime CreatedAt { get; set; }

[LwxDtoProperty(JsonName = "birth_date", JsonConverter = typeof(DateOnlyConverter))]
public partial DateOnly? BirthDate { get; set; }
```

### Enums

```csharp
[LwxDtoProperty(JsonName = "status")]
public partial OrderStatus Status { get; set; }

public enum OrderStatus
{
    [JsonPropertyName("pending")] Pending,
    [JsonPropertyName("confirmed")] Confirmed,
    [JsonPropertyName("shipped")] Shipped
}
```

Enum properties automatically get `[JsonStringEnumConverter]`.

### Ignored Properties

```csharp
[LwxDtoIgnore]
public partial int InternalId { get; set; }
```

Use `[LwxDtoIgnore]` for properties that should not be serialized but need generator coverage.

## Complete Example

```csharp
using System.Text.Json.Serialization;
using Lwx.Builders.Dto.Atributes;

[LwxDto(Type = DtoType.Normal)]
public partial class OrderDto
{
    [LwxDtoProperty(JsonName = "order_id")]
    public required partial int OrderId { get; set; }

    [LwxDtoProperty(JsonName = "customer_name")]
    public partial string? CustomerName { get; set; }

    [LwxDtoProperty(JsonName = "total")]
    public partial decimal Total { get; set; }

    [LwxDtoProperty(JsonName = "status")]
    public partial OrderStatus Status { get; set; }

    [LwxDtoProperty(JsonName = "created_at", JsonConverter = typeof(UtcDateTimeConverter))]
    public partial DateTime CreatedAt { get; set; }

    [LwxDtoIgnore]
    public partial int TempCalculation { get; set; }
}
```

## Diagnostics

| Code | Description |
|------|-------------|
| LWX003 | Unsupported property type — add a `JsonConverter` |
| LWX004 | Enum constants missing `[JsonPropertyName]` (warning) |
| LWX005 | Property missing `[LwxDtoProperty]` or `[LwxDtoIgnore]` |
| LWX006 | Fields are not allowed in DTO classes |
| LWX007 | Consider using `DateTimeOffset` instead of `DateTime` (warning) |

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for architecture details and development guidelines.

## Run tests (developer note)

To run the test suite for this package:

```bash
dotnet build
dotnet test Lwx.Builders.Dto.Tests
```

For contributing and development guidance see `CONTRIBUTING.md`.


