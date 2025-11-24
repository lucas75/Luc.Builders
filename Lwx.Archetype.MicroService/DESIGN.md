ExampleProduct.Worker001# HARD RULES FOR MICROSERVICE ARCHETYPE

## DEFINITIONS

- For the purpose of these rules, the "root namespace" of a project is defined as the default namespace specified in the project file (e.g., `<RootNamespace>` in a .csproj file). This is typically the same as the project name unless explicitly overridden. In the examples below the project is ExampleOrg.Product.ServiceAbc and the root namespace is ExampleOrg.Product.ServiceAbc.

## RULES FOR CONSUMING PROJECTS

### General

- ALL classes with Lwx attributes (endpoints, DTOs, workers, timers, service bus consumers/producers) MUST be defined in files whose paths match their namespaces relative to the project root namespace. For example, a class `ExampleOrg.Product.ServiceAbc.Abc.Cde` should be located at `Abc/Cde.cs` relative to the project root namespace.

# Lwx Archetype — Formal Design and Rules (RFC-style)

Status: PROPOSED

Authors: Lwx Archetype maintainers

Abstract
========

This document specifies the design, consumer requirements and static-analysis rules for the Lwx.Archetype.MicroService source generator and its associated attributes.

It is written in a formal "RFC-style" using normative keywords (MUST, MUST NOT, SHOULD, MAY) to describe the constraints and guarantees a consuming project must obey for the generator to operate correctly.

Terminology and Conventions
===========================

1.  The term "root namespace" refers to the default project namespace (typically from the project file's `<RootNamespace>`). Example: a project `ExampleOrg.Product.ServiceAbc` will have root namespace `ExampleOrg.Product.ServiceAbc`.

Note: in the examples that follow this document assumes the consuming project is named `ExampleOrg.Product.ServiceAbc` and the root namespace is `ExampleOrg.Product.ServiceAbc` unless explicitly stated otherwise.

2.  Normative language uses the following interpretation (as per RFC 2119):

    - MUST — This requirement is an absolute constraint.
    - MUST NOT — This requirement is an absolute prohibition.
    - SHOULD — There may exist valid reasons to ignore this recommendation, but the full implications must be understood and documented.
    - MAY — This feature is entirely optional.

3.  File/namespace mapping: all Lwx-decorated types MUST be located so that the file path relative to the project root matches the namespace segments. The generator enforces this rule (see diagnostic LWX007).

Scope
=====

This specification is limited to rules and behaviors that the source generator enforces or relies upon: ServiceConfig, endpoints, DTOs, workers, timers, and service bus handlers. Coverage includes attribute contracts, required method signatures, namespace / file placement, generated outputs, diagnostics and runtime mapping behaviors.

Requirements for Consuming Projects
==================================

General
------

- A consuming project that uses the Lwx generator MUST ensure that every class annotated with any Lwx attribute (e.g., `[LwxEndpoint]`, `[LwxServiceConfig]`) has a file path that mirrors its namespace relative to the project's root namespace. Example: `ExampleOrg.Product.ServiceAbc.Abc.Cde` MUST be declared in `Abc/Cde.cs`.

- The generator uses this convention to produce canonical placeholder types and mapping helpers. The generator reports diagnostic `LWX007` when this rule is violated.

ServiceConfig (root-level application configuration)
--------------------------------------------------

1.  The consuming project MUST declare a type named `ServiceConfig` in the root namespace and placed in `ServiceConfig.cs` at the project root. The generator will report `LWX011` if no such type exists.

2.  The `ServiceConfig` type MUST be annotated with the `[LwxServiceConfig]` attribute. If the attribute appears on any other file or type the generator MUST report a diagnostic `LWX012`.

3.  Required attribute properties on `[LwxServiceConfig]` are:

    - Title (string) — MUST be present.
    - Description (string) — MUST be present.
    - Version (string) — MUST be present.
    - PublishSwagger (LwxStage) — MUST be present and define whether the Swagger UI will be published.

4.  GenerateMain behaviour

    - If `GenerateMain = true` is specified on the attribute, the consuming project MUST NOT declare its own `Main` entry point. If a `Program.cs` (or user-provided Main) is present when `GenerateMain = true`, the archetype WILL emit an error diagnostic (LWX013) because the generator synthesizes the application entry point.

    - When `GenerateMain = true`, the generator expects `ServiceConfig` to optionally provide two static methods to participate in startup configuration:

        - `public static void Configure(WebApplicationBuilder builder)` — OPTIONAL but, when present, MUST be `public static void` and have the exact parameter type `WebApplicationBuilder`.

        - `public static void Configure(WebApplication app)` — OPTIONAL but, when present, MUST be `public static void` and have the exact parameter type `WebApplication`.

    - The generator will emit diagnostics for invalid or unexpected `ServiceConfig` members:

        - `LWX014` — emitted when a `Configure` method's signature does not match the required forms.
        - `LWX015` — emitted when unexpected public methods are detected on `ServiceConfig` (the generator expects only the specified `Configure` methods when `GenerateMain` is used).

5.  `ServiceConfig` MUST be `partial` so the generator can produce a companion partial type for enhancements (for example, the generated `Main` lives in `ServiceConfig.Main.g.cs` when `GenerateMain = true`).

Endpoints
---------

1.  Endpoints are declared with the `[LwxEndpoint]` attribute and MUST satisfy file-location and namespace constraints: their namespace MUST contain the `.Endpoints` segment (e.g., `ExampleOrg.Product.ServiceAbc.Endpoints[.Segment...]`). Violation of this namespace rule MUST produce `LWX002`.

2.  The `[LwxEndpoint]` attribute MAY contain a `Uri` value in the form `"VERB /path"` (e.g., `"GET /users/{id}"`). When provided, the generator uses the `Uri` to compute a canonical endpoint type name and mapping behavior.

3.  Canonical naming rules

    - For a path `/abc/{id}/x` the canonical type name is `EndpointAbcParamIdX`.

    - The generator accepts these naming variants as valid for a derived path:

        - `EndpointAbcParamIdX` (canonical)
        - `EndpointAbcParamIdX<Verb>` (e.g., `EndpointAbcParamIdXGet`)
        - Prefix/shortened forms when unambiguous (for example `EndpointAbcParamId`) — but these are accepted only when they are unambiguous within the generator's derivation.

    - If the declared class name does not match one of the acceptable variants and the attribute does not include `NamingExceptionJustification`, the generator MUST emit `LWX001` and skip generating mapping glue for that type.

    - If `NamingExceptionJustification` is provided then the generator emits `LWX008` (informational) and accepts the non-standard name.

4.  Handler contract

    - Each `[LwxEndpoint]` annotated type MUST expose a callable handler method `Execute` (exact name must match) that the generator will map into an ASP.NET pipeline handler.

    - Allowed `Execute` signatures: the generator accepts common minimal-API-compatible method signatures. Examples of acceptable forms include, but are not limited to:

        - `public static async Task Execute(HttpContext context)`
        - `public static IResult Execute([FromRoute] string id)`
        - `public static async Task<IResult> Execute(YourDto dto)`

    - If `Execute` is missing or not callable the generator MUST emit a diagnostic explaining the missing handler.

5.  Generated mapping behaviour

    - For each valid endpoint the generator MUST emit a consumer-side mapping helper file named `LwxEndpoint_<TypeName>.Configure.g.cs` containing a method `Configure(WebApplication app)` that:

        - Chooses the correct `Map*` method based on the HTTP verb present in the `Uri` (GET→MapGet, POST→MapPost, PUT→MapPut, DELETE→MapDelete, PATCH→MapMethods).
        - Applies stage gating using `Publish` (e.g., Development/Production/None) so endpoints only map in allowed runtime environments.
        - Applies `RequireAuthorization` when `SecurityProfile` is set.
        - Applies `WithDisplayName` when `Summary` is supplied.
        - Adds a `LwxEndpointMetadata` instance so runtime code can identify generated endpoints.

6.  The generator also emits lightweight canonical placeholder types under the root project's `.Endpoints` namespace so other code can take a stable dependency on the generated canonical types. Filenames follow `LwxEndpoint_<TypeName>*.g.cs`.

Attribute Reference (Endpoints)
------------------------------

- `Uri` (string): optional, form `VERB /path`.
- `SecurityProfile` (string): optional, maps to an authorization policy.
- `Summary` (string): optional, used as human-friendly display name in mapping.
- `Description` (string): optional, free text for future use.
- `Publish` (LwxStage enum): optional, controls stage gating for mapping.
- `NamingExceptionJustification` (string): optional, when present permits non-standard type names and causes `LWX008` to be emitted.

Other Lwx Types (DTOs, Workers, Timers, ServiceBus handlers)
-----------------------------------------------------------

- The generator provides processors for DTOs, worker classes, timers and service bus handlers. The same file/namespace layout rule applies — types MUST be declared in files that mirror their namespaces. The generator emits `LWX007` when this constraint is violated.

- Handlers and worker types MUST implement reasonably predictable entry methods (for example, worker `Execute` patterns or typed callbacks). The generator validates signatures where practical and reports diagnostics for obvious mismatches.

Diagnostics and Semantic Rules (summary)
======================================

The generator uses a concise set of diagnostic codes to report rule violations and noteworthy information. Implementations MUST recognize and respect the following codes.

- LWX001 — Error: Invalid endpoint class name (declared name doesn't match expected name derived from URI).
- LWX002 — Error: Invalid endpoint namespace (namespace does not contain `.Endpoints`).
- LWX007 — Error: File path does not match the declared namespace for a type annotated with an Lwx attribute.
- LWX008 — Info: Endpoint naming exception accepted (when `NamingExceptionJustification` is provided).
- LWX011 — Error: ServiceConfig missing in root namespace.
- LWX012 — Error: [LwxServiceConfig] attribute used in a non-ServiceConfig file.
- LWX014 — Error: Invalid ServiceConfig.Configure signature.
- LWX015 — Error: Unexpected public methods found on ServiceConfig when using GenerateMain.
- LWX016 — Info: Generator emitted `ServiceConfig` Main generation source; diagnostic includes generated source (informational).

Generated Sources and Developer Ergonomics
=========================================

1.  The generator produces deterministic filenames in the compiler output. Consumers who want to inspect the generated source SHOULD enable the compilation properties `EmitCompilerGeneratedFiles` and `CompilerGeneratedFilesOutputPath` to write generated files to disk (commonly under the project's `obj/` folder).

2.  When `GenerateMain = true` the generator will create a companion file `ServiceConfig.Main.g.cs` containing a partial `ServiceConfig` type with a `Main` method in the same namespace as the declared `ServiceConfig` symbol. The presence of `ServiceConfig.Main.g.cs` MAY be surfaced as an informational diagnostic (`LWX016`) that includes the generated source text.

3.  Endpoint generated files follow the `LwxEndpoint_<TypeName>*.g.cs` convention and include both placeholder types (for stable type references) and `Configure` helpers in the consuming namespace to perform the `app.Map*` wiring.

Testing and CI guidance
=======================

- Unit and integration tests SHOULD validate generation behavior by either:

    1.  Enabling `EmitCompilerGeneratedFiles` and asserting the presence and content of generated `.g.cs` files on disk, or

    2.  Using Roslyn's `GeneratorDriver` and compilation-based assertion techniques to inspect emitted sources and diagnostics programmatically.

- Tests that only depend on textual CLI output MAY miss information-level diagnostics (`LWX016` and `LWX008`) because build tooling may omit info-level logs. Prefer inspection of emitted files or programmatic Roslyn assertions.

Migration notes and warnings
==========================

- Consumers that opt-in to `GenerateMain = true` MUST remove existing `Main` entry points to avoid conflicts. The generator will enforce this condition and fail compilation diagnostic checks if both exist.

- The namespace and file-path rules are strictly enforced. When a large repository cannot be instantly migrated, consumer projects MAY use `NamingExceptionJustification` for endpoints as a bridge, but such exceptions should be tracked and removed when feasible to prevent accumulating technical debt.

Security and runtime behavior
=============================

- The generator wires `RequireAuthorization` only when `SecurityProfile` has been explicitly set on the attribute. No authorization behavior is implied otherwise.

- Stage gating (`Publish`) is used to ensure mapping visibility across development and production environments — consumers MUST review `Publish` settings before enabling production mapping.

Appendix: Examples
==================

ServiceConfig example (normative)

```csharp
namespace ExampleOrg.Product.ServiceAbc;

[LwxServiceConfig(
    Title = "MyUnit Worker 001 API",
    Description = "API for MyUnit Worker 001",
    Version = "v1.0.0",
    PublishSwagger = LwxStage.Development,
    GenerateMain = true
)]
public partial class ServiceConfig
{
    // Optional: builder-time configuration. If present, the signature MUST be
    // `public static void Configure(WebApplicationBuilder builder)`.
    public static void Configure(WebApplicationBuilder builder) { }

    // Optional: runtime configuration. If present, the signature MUST be
    // `public static void Configure(WebApplication app)`.
    public static void Configure(WebApplication app) { }
}
```

Endpoint example (normative)

```csharp
namespace ExampleOrg.Product.ServiceAbc.Endpoints;

[LwxEndpoint("GET /abc/{id}",
    SecurityProfile = "MyAuth",
    Summary = "Read ABC by id",
    Publish = LwxStage.Development)]
public static class EndpointAbcParamId
{
    // Required handler. Examples of acceptable signatures follow.
    public static async Task Execute(HttpContext ctx) { /* ... */ }
}
```

Normative change-log
====================

This document formalizes generator behavior introduced across versions and consolidates enforcement points for `ServiceConfig` and endpoint declarations. Any further rule additions or changes MUST be reflected here and accompanied by unit/integration tests to prevent regressions.

End of document.


