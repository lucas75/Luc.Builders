# TODO — Luc.Util.Web

This TODO collects short-term work items and follow-ups for the Lwx source generator and the sample consumer app.

## Completed / Implemented
- Enforce endpoint naming variants derived from URI (full segment style and optional HTTP-verb suffix).
- Path parameter naming uses "Param" prefix (e.g., EndpointAbcParamCdeEfg).
- Allow nested endpoint folders/namespaces under `*.Endpoints` (e.g., `Endpoints`, `Endpoints.Abc`, `Endpoints.Abc.Cde`).
- Enforce that classes with `Lwx...` attributes must have file path matching their namespace (diagnostic LWX007).
- Added validation across processors (DTOs, endpoints, workers, timers, service bus/event hub)
 - Added support for naming exceptions via `[LwxEndpoint(NamingExceptionJustification = "...")]` and informational diagnostic LWX008 when used
- Enhanced incremental source generator to detect ServiceConfig classes by name and validate their Configure methods for correct signatures (public static void Configure(WebApplicationBuilder) or Configure(WebApplication)).

## Short-term (next)
- Add unit tests / integration tests for the naming rules and diagnostics (LWX001, LWX007).
 - Add unit tests / integration tests for the naming rules and diagnostics (LWX001, LWX007, LWX008).

 - Add support for a dedicated microservice descriptor attribute `LwxServiceConfig` (ServiceConfig.cs).
 - Update generator to prefer `LwxServiceConfig` and use it for swagger/service configuration generation.

### Examples

1. Standard: GET /abc/cde/efg => class should be `EndpointAbcCdeEfg` under `*.Endpoints` or nested namespace
2. Exception: if legacy class name must remain, annotate with: `[LwxEndpoint("GET /mismatch/start", NamingExceptionJustification = "Legacy naming preserved for backward compatibility")]` and generator will accept it and emit LWX008.

## Medium-term
- Add tests for edge-cases (generic endpoints, overloaded verb forms, exotic characters in segments).

## Long-term
- Add project templates and sample apps demonstrating best practice of file/namespace layout.
- Add CI checks that fail builds for LWX007 in "strict" mode if enabled.
