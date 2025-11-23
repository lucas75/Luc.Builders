# Lwx.Archetype.MicroService - Project Context Summary

## Current Project State

This is a Roslyn incremental source generator for C# microservice archetypes, targeting .NET 9.0 with C# 13 features. The generator processes custom attributes to automatically generate ASP.NET Core microservice boilerplate code including endpoints, DTOs, consumers, producers, and Swagger documentation.

## Architecture Overview

- **Generator**: `LwxArchetypeGenerator.cs` orchestrates the generation process using incremental generators
- **Processors**: Object-oriented design with individual processor classes (e.g., `LwxEndpointTypeProcessor`, `LwxDtoTypeProcessor`) that implement `Execute()` methods
- **Attributes**: Embedded as resources with LogicalName for proper resource naming
- **Primary Constructors**: Used throughout for clean parameter handling
- **Diagnostic System**: Custom error codes (LWX001-LWX005) for compile-time validation

## Recent Development History
 - Modified the auto-generated Program.g.cs to place the Main method as top-level statements, enhancing the ServiceConfig class by calling its Configure methods for builder and app configuration.
 - Enhanced incremental source generator to detect ServiceConfig classes by name and validate their Configure methods for correct signatures (public static void Configure(WebApplicationBuilder) or Configure(WebApplication)), reporting diagnostics LWX014 and LWX015 for invalid signatures or unexpected public methods.
 - Enforced presence of `ServiceConfig.cs` in projects using the generator (diagnostic LWX011).
 - Enforced presence of `ServiceConfig.cs` in projects using the generator (diagnostic LWX011).
 - Enforced that `[LwxServiceConfig]` may only appear in a file named `ServiceConfig.cs` (diagnostic LWX012).
- Enhanced `LwxDtoTypeProcessor` with strict validation: forbids properties without `[LwxDtoProperty]` or `[LwxDtoIgnore]`, forbids fields entirely, reports compilation errors for non-compliant DTOs
- Added `[LwxDtoIgnoreAttribute]` for excluding properties from generation while satisfying validation rules
- Centralized attribute name constants in `LwxConstants.cs` with `const string` for full names and `static readonly string` for short names using `Replace("Attribute", "")`
- Refactored `LwxArchetypeGenerator.cs` to use constants from `LwxConstants` in switch statements and attribute detection
- Moved `AttributeNames` array from generator to `LwxConstants` for better maintainability
 - Encapsulated Swagger configuration in dynamically generated `LwxConfigure` extension methods, using `[LwxServiceConfig]` metadata to conditionally include Swagger setup code
 - Upgraded Swagger generation to respect `PublishSwagger` property with environment-based activation (Development/Production stages), and set OpenAPI info (Title, Description, Version) and Swagger UI DocumentTitle from attribute properties
- Removed embedded `LwxEndpointExtensions.cs` template, replaced with dynamic generation in main generator for attribute-aware code inclusion
 - Updated `LwxServiceConfigTypeProcessor` to minimal diagnostic checking, with actual generation moved to main generator
- Ensured clean builds with no runtime dependencies on generator assembly, maintaining embedded source distribution pattern

### Endpoint Naming & Namespace/Filepath Validation (Latest)
- Revised endpoint naming validation to support multiple acceptable class-name styles derived from the HTTP URI:
  - Full segment style: EndpointAbcCdeEfg for GET /abc/cde/efg
  - Verb-suffix style: EndpointAbcCdeEfgGet (or EndpointAbcCdeGet) — generator accepts useful variants for ergonomics
  - Parameter naming: uses ParamX for path parameters (eg. EndpointAbcParamCdeEfg)
- Generator now supports placing generated endpoint classes in nested folders/namespaces (e.g. .Endpoints, .Endpoints.Abc, .Endpoints.Abc.Cde) and will generate mapping helpers accordingly
- Added strict filename/namespace matching validation for classes with Lwx attributes (diagnostic LWX007). The rule enforces that types are declared in files that match the namespace -> path layout. Example: type MyCompany.MyProject.Abc.Cde should be located at Abc/Cde.cs relative to the project root namespace
- Applied the namespace/path validation across processors (endpoints, DTOs, workers, bus consumers/producers, timers, swagger) so any Lwx-decorated type is validated at compile time
 - Added support for explicit naming exceptions via the attribute property `NamingExceptionJustification` on `[LwxEndpoint]`. When provided the generator will accept a non-standard class name and emit an informational diagnostic (LWX008) that includes the justification.

### Completed DTO Processor Implementation (Previous)
- Implemented `LwxDtoTypeProcessor` to generate partial property implementations for DTO classes
- Added `DtoType` enum with `Normal` (backing fields) and `Dictionary` (dynamic storage) options
- Created `LwxDtoPropertyAttribute` for customizing JSON serialization (JsonName, JsonConverter)
- Added support for `JsonIgnore` on nullable properties and `JsonStringEnumConverter` for enums
- Implemented type validation with diagnostics for unsupported property types
- Added enum constant validation to warn missing `JsonPropertyName` attributes
- Updated attribute embedding to include new DTO-related attributes

### Completed Refactoring (Previous)
- Refactored large `Execute()` method in `LwxEndpointTypeProcessor` into focused private methods:
  - `ExtractUriArgument()` - URI extraction from attributes
  - `ValidateEndpointNaming()` - Naming scheme validation with diagnostics
  - `ComputeEndpointNames()` - Namespace and class name computation
  - `ExtractAttributeMetadata()` - Security/summary/description/publish metadata
  - `GenerateSourceFiles()` - Multi-file source generation

### Previous Major Changes
- Migrated from static dispatch to object-oriented processor instantiation
- Added primary constructors to all processor classes
- Suppressed unused parameter warnings (CS9113) at project level
- Enhanced endpoint generation with Configure methods
- Added metadata validation and diagnostic reporting
- Organized processors into modular classes
- Standardized embedded resource usage
- Refactored duplicated code to utility classes
- Added Swagger processor with dependency validation
- Improved attribute documentation with XML docs

## Established Patterns

### Code Style
- C# 13 with primary constructors and pattern matching
- PascalCase naming for all identifiers
- Comprehensive XML documentation for public APIs
- Immutable readonly properties preferred
- Split large methods into smaller helpers

### Source Generation
- Raw string literals ($$"""...""") for generated code
- File naming patterns: `LwxEndpoint_{name}.g.cs`, `LwxEndpoint_{name}.Configure.g.cs`
- Namespace computation: Strip ".Endpoints" suffixes for root namespaces
- Diagnostic reporting with custom descriptors and location info

### Project Configuration
- Embedded resources use LogicalName: `Lwx.Archetype.MicroService.%(RelativeDir)%(Filename)%(Extension)`
- Project-level warning suppressions in NoWarn
- Swashbuckle.AspNetCore dependency for Swagger features
- **Deployment Note**: Attributes in `Attributes/` and templates in `Generator/Templates/` are embedded as source and generated in consuming projects via post-initialization. Incremental source generators must never use `ReferenceOutputAssembly=true` to avoid runtime dependencies on the generator assembly.

### Validation Rules
- Endpoint naming: `EndpointAbc` for path `/abc`, `EndpointAbcParamDef` for `/abc/{def}`
- Namespace requirement: Endpoints must be in `*.Endpoints` namespaces
- Dependency checking: Validates required NuGet packages at compile-time

## Current Working State

- All processors use primary constructors with `(FoundAttribute attr, SourceProductionContext ctx, Compilation _)`
- Large methods have been refactored into maintainable private methods
- DTO processor generates partial property implementations with backing fields or dictionary storage
- Build passes cleanly with no errors (warnings for nullable in generated code)
- Tests pass successfully
- Ready for continued development of additional processor types or feature enhancements

## Key Files and Locations

- **Generator Core**: `Generator/LwxArchetypeGenerator.cs`
- **Processors**: `Generator/Processors/` directory with individual processor classes
- **Attributes**: `Attributes/` directory with embedded attribute templates
- **Project Config**: `Lwx.Archetype.MicroService.csproj` with embedded resource configuration
- **Tests**: Located in separate test project for validation

## Context History Maintenance

This `AGENTS.md` file serves as a living context history for AI agents working on this project. When making significant changes:

1. **Update Recent Development History**: Add new completed work to the "Recent Development History" section, moving previous entries down as appropriate
2. **Document New Patterns**: When establishing new conventions or patterns, add them to the "Established Patterns" section
3. **Update Working State**: Reflect current project status in the "Current Working State" section
4. **Maintain Architecture Overview**: Keep the architecture description current as the system evolves
5. **Preserve Chronological Order**: Keep development history in reverse chronological order (most recent first)

This ensures that future AI agents can understand the project's evolution and continue development with full context of established patterns and recent changes.

This context summary enables continuation of development work on the source generator, maintaining consistency with established patterns and recent architectural decisions.