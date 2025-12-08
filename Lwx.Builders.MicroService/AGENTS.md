# Lwx.Builders.MicroService - Project Context Summary

## Current Project State

This is a Roslyn incremental source generator for C# microservice archetypes, targeting .NET 9.0 with C# 13 features. The generator processes custom attributes to automatically generate ASP.NET Core microservice boilerplate code including endpoints, DTOs, consumers, producers, and Swagger documentation.

## Architecture Overview

- **Generator**: `Generator.cs` (class `Generator`) orchestrates the generation process using incremental generators
- **Processors**: Object-oriented design with individual processor classes (e.g., `LwxEndpointTypeProcessor`, `LwxSettingTypeProcessor`) that implement `Execute()` methods
- **Attributes**: Embedded as resources with LogicalName for proper resource naming
- **Primary Constructors**: Used throughout for clean parameter handling
- **Diagnostic System**: Custom error codes (LWX001-LWX061) for compile-time validation

## Recent Development History

### Method-Level Attributes (January 2025)
- **BREAKING CHANGE**: Moved all endpoint attributes from class level to method level
- `[LwxEndpoint]`, `[LwxMessageEndpoint]`, and `[LwxTimer]` now go on the `Execute` method, not the class
- Processors updated to use `_containingType` field for class-based validation
- Attribute target changed from `AttributeTargets.Class` to `AttributeTargets.Method`
- New diagnostics: LWX070-LWX075 for attribute placement validation
  - LWX070: `[LwxEndpoint]` must be on Execute method
  - LWX071: Endpoint attribute not on method named Execute
  - LWX072: `[LwxMessageEndpoint]` must be on Execute method
  - LWX073: Message endpoint attribute not on method named Execute
  - LWX074: `[LwxTimer]` must be on Execute method
  - LWX075: Timer attribute not on method named Execute
- Generator.cs LWX018 updated to check for method-level attributes
- All example and test files updated to use new syntax

### LwxTimer Mechanism (January 2025)
- Implemented timer-triggered endpoints with both interval-based and cron-based scheduling
- New `[LwxTimer]` attribute with properties: CronExpression, IntervalSeconds, Stage, RunOnStartup, Summary, Description, NamingExceptionJustification
- New processor: `LwxTimerTypeProcessor` generates hosted BackgroundService classes
- ServiceRegistration updated with TimerNames/TimerInfos for listing
- Service.ConfigureTimers(builder) method generated for timer wiring
- Supports void Execute() and Task Execute() methods
- Supports DI parameters in Execute method (resolved via IServiceProvider.CreateScope())
- Supports CancellationToken parameter for cooperative cancellation
- Naming convention: `EndpointTimer{Name}` in `.Endpoints` namespace
- New diagnostics: LWX060 (invalid timer class name), LWX061 (invalid timer namespace)
- Generator.cs LWX018 updated to allow `[LwxTimer]` alongside `[LwxEndpoint]` and `[LwxMessageEndpoint]`

### MessageEndpoint Mechanism (January 2025)
- Renamed from MessageHandler to MessageEndpoint for consistency
- Implemented complete message queue processing infrastructure with configurable providers and error policies
- New core interfaces: `ILwxQueueProvider`, `ILwxQueueMessage`, `ILwxErrorPolicy`, `ILwxProviderErrorPolicy`
- Default policies: `LwxDefaultErrorPolicy` (dead-letters on error), `LwxDefaultProviderErrorPolicy` (logs only)
- `[LwxMessageEndpoint]` attribute with Uri, QueueStage, UriStage, QueueProvider, QueueConfigSection, QueueReaders, HandlerErrorPolicy, ProviderErrorPolicy properties
- New processor: `LwxMessageEndpointTypeProcessor` generates hosted background services and HTTP endpoints
- ServiceRegistration updated with MessageEndpointNames/MessageEndpointInfos
- Namespace convention: `.Endpoints` with `EndpointMsg{PathSegments}` naming
- Queue providers should be in `.Services` namespace (not `.Endpoints`)
- New diagnostics: LWX040-LWX049 for MessageEndpoint validation

### Removed: ServiceBus/EventHub Processors (January 2025)
- Removed unused stub processors for Azure ServiceBus and EventHub
- `LwxMessageEndpoint` provides generic abstraction for any queue provider

### LwxSetting Configuration Mechanism (December 2025)
- Replaced `[FromConfig]` constructor parameter injection with `[LwxSetting]` static partial property mechanism
- New `LwxSettingAttribute` for annotating static partial properties with configuration keys (e.g., `[LwxSetting("Section:Key")]`)
- New `LwxSettingTypeProcessor` generates backing fields (`__N_Key`) and partial property implementations
- `LwxServiceTypeProcessor` now generates `ConfigureSettings(builder)` method that reads configuration and sets backing fields via reflection
- Removed `FromConfigAttribute` and factory-based DI registration from workers
- New diagnostics: LWX030-LWX035 for setting validation (static, partial, getter-only, primitive types, non-empty key, partial containing type)

### Multi-Service Architecture (December 2025)
- Allow multiple `[LwxService]` declarations - each namespace prefix can have its own service
- Endpoints/workers associated by namespace hierarchy (e.g., `Assembly.Abc.Endpoints.*` → `Assembly.Abc.Service`)
- `ServiceRegistration` class holds endpoints, workers, and settings for each namespace prefix
- New diagnostics: LWX017 (duplicate service), LWX020-LWX022 (orphan endpoints/workers)

### Previous Development
 - Modified the auto-generated Program.g.cs to place the Main method as top-level statements, enhancing the Service class by calling its Configure methods for builder and app configuration.
 - Enhanced incremental source generator to detect Service classes by name and validate their Configure methods for correct signatures (public static void Configure(WebApplicationBuilder) or Configure(WebApplication)), reporting diagnostics LWX014 and LWX015 for invalid signatures or unexpected public methods.
 - Enforced presence of `Service.cs` in projects using the generator (diagnostic LWX011).
 - Enforced that `[LwxService]` may only appear in a file named `Service.cs` (diagnostic LWX012).
 - DTO processing and attributes have been moved to a dedicated project `Lwx.Builders.Dto` (see that project for `LwxDto` attributes and processors)
- Centralized attribute name constants in `LwxConstants.cs` with `const string` for full names and `static readonly string` for short names using `Replace("Attribute", "")`
 - Moved small shared helper types into `Processors/ProcessorUtils.cs` and centralized `LwxConstants` to make processors more self-contained.
- Refactored `Generator.cs` to use constants from the nested `LwxConstants` in switch statements and attribute detection

### Endpoint Naming & Namespace/Filepath Validation (Latest)
- Revised endpoint naming validation to support multiple acceptable class-name styles derived from the HTTP URI:
  - Full segment style: EndpointAbcCdeEfg for GET /abc/cde/efg
  - Verb-suffix style: EndpointAbcCdeEfgGet (or EndpointAbcCdeGet) — generator accepts useful variants for ergonomics
  - Parameter naming: uses ParamX for path parameters (eg. EndpointAbcParamCdeEfg)
- Generator now supports placing generated endpoint classes in nested folders/namespaces (e.g. .Endpoints, .Endpoints.Abc, .Endpoints.Abc.Cde) and will generate mapping helpers accordingly
- Added strict filename/namespace matching validation for classes with Lwx attributes (diagnostic LWX007). The rule enforces that types are declared in files that match the namespace -> path layout. Example: type MyCompany.MyProject.Abc.Cde should be located at Abc/Cde.cs relative to the project root namespace
- Applied the namespace/path validation across processors (endpoints, DTOs, workers, bus consumers/producers, timers, swagger) so any Lwx-decorated type is validated at compile time
 - Added support for explicit naming exceptions via the attribute property `NamingExceptionJustification` on `[LwxEndpoint]`. When provided the generator will accept a non-standard class name without emitting a diagnostic (behavior is silent — mapping is still generated).

### Completed DTO Processor Implementation (Previous)
- Implemented `LwxDtoTypeProcessor` to generate partial property implementations for DTO classes
- Added `DtoType` enum with `Normal` (backing fields) and `Dictionary` (dynamic storage) options
 - Created `LwxDtoPropertyAttribute` for customizing JSON serialization (JsonName, JsonConverter) — now located in `Lwx.Builders.Dto/Attributes` and supported by the `Lwx.Builders.Dto` generator
- Added support for `JsonIgnore` on nullable properties and `JsonStringEnumConverter` for enums
- Implemented type validation with diagnostics for unsupported property types
- Added enum constant validation to warn missing `JsonPropertyName` attributes
- Updated attribute embedding to include new DTO-related attributes
- Fixed typo in attribute namespace: renamed `Lwx.Builders.MicroService.Atributes` to `Lwx.Builders.MicroService.Atributtes` across the repository (attributes, processors, tests, and sample projects). Updated processors and templates so the generator emits the corrected namespace in generated sources.

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
  - Follow global policy: all generators must use raw $$""" templates with no embedded leading indentation, and apply `.FixIndent(levels)` when composing templates. See `/.github/copilot-instructions.md` for the canonical rules. If this file previously suggested a different approach, it was intentionally replaced to follow the global guidance.
 - File naming patterns: by default `Endpoint<TypeName>.g.cs` (for example `EndpointAbcCde.g.cs`); when a `NamingExceptionJustification` is present the generator will include the full namespace in the filename to disambiguate (for example `ExampleOrg.Product.ServiceAbc.Endpoints.EndpointOldStart.g.cs`).
 - Use file-scoped namespaces in generated files when possible (e.g. `namespace ExampleOrg.Product.ServiceAbc.Endpoints;`) to reduce indentation and produce cleaner output.
- Namespace computation: Strip ".Endpoints" suffixes for root namespaces
- Diagnostic reporting with custom descriptors and location info

### Project Configuration
  - Embedded resources use LogicalName: `Lwx.Builders.MicroService.%(RelativeDir)%(Filename)%(Extension)`
- Project-level warning suppressions in NoWarn
- Swashbuckle.AspNetCore dependency for Swagger features
- **Deployment Note**: Attributes in `Attributes/` and templates in `Templates/` are embedded as source and generated in consuming projects via post-initialization. Incremental source generators must never use `ReferenceOutputAssembly=true` to avoid runtime dependencies on the generator assembly.

### Validation Rules
- Endpoint naming: `EndpointAbc` for path `/abc`, `EndpointAbcParamDef` for `/abc/{def}`
- Namespace requirement: Endpoints must be in `*.Endpoints` namespaces
- Dependency checking: Validates required NuGet packages at compile-time

## Current Working State

 - All processors use primary constructors with `(AttributeInstance attr, SourceProductionContext ctx, Compilation _)` (microservice generator). The lightweight attribute model is defined at the generator root so processors can use it without duplicating types.
- Large methods have been refactored into maintainable private methods
- DTO processor generates partial property implementations with backing fields or dictionary storage
- Build passes cleanly with no errors (warnings for nullable in generated code)
- Tests pass successfully
 - Tests pass successfully for the solution; `Lwx.Builders.Dto` also includes its own unit/integration coverage in the solution test run.
- Ready for continued development of additional processor types or feature enhancements

## Multi-Service Architecture (December 2025)

The generator now supports multiple services per project, associated by namespace hierarchy:

### Namespace-Based Service Association
- Endpoints/workers are associated with services based on their namespace prefix
- `Assembly.Abc.Endpoints.Foo` → belongs to service at `Assembly.Abc` (Service.cs in that namespace)
- `Assembly.Cde.Workers.Bar` → belongs to service at `Assembly.Cde`
- Each service generates its own `Service.Configure(...)` with only its associated endpoints/workers

### Data Model Changes
- `Generator` now uses `Dictionary<string, ServiceRegistration>` instead of flat lists
- `ServiceRegistration` class holds endpoints/workers for a specific namespace prefix
- `ComputeServicePrefix()` extracts the service namespace from endpoint/worker namespaces

### New Diagnostics
- `LWX017` - Duplicate Service: Now triggers when two services share the same namespace prefix
- `LWX020` - Service namespace validation: Service must be under assembly namespace
- `LWX021` - Orphan Endpoint: Endpoint has no matching service in its namespace hierarchy
- `LWX022` - Orphan Worker: Worker has no matching service in its namespace hierarchy

### Test/Library Project Detection
Services in test or library projects have relaxed namespace placement rules. Detection via:
- Assembly name ending in `.Tests`, `.Test`, `.Lib`, `.Library`
- Absence of `Program.cs` in the compilation
- `OutputKind.DynamicallyLinkedLibrary` compilation option

# Key Files and Locations

- **Generator Core**: `Generator.cs`
- **Processors**: `Processors/` directory with individual processor classes
- **Attributes**: `Attributes/` directory with embedded attribute templates
- **Project Config**: `Lwx.Builders.MicroService.csproj` with embedded resource configuration
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