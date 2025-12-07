# AGENTS — Lwx.Builders.MicroService.Tests

This directory contains sample projects used by compile-time tests and for developer validation.

SampleProjects:
- OkService: A small, valid microservice demonstrating correct generator usage.

Note: Error generating sample projects were removed in favor of in-memory generator tests using the `GeneratorTestHarness`. The tests now construct source inputs dynamically (see `CompileErrorTests.cs`) instead of relying on committed sample projects.

Notes:
- Tests should compile these sample projects via MSBuild and assert expected diagnostics. These sample projects should not be compiled as part of the unit test project's own build; the test project has `Compile Remove` entries for `SampleProjects/**`.
- Do not create tests now; sample projects are prepared and will be asserted in future compile-time tests.

## Test Files

### MessageHandlerTests.cs (January 2025)
Tests for the `[LwxMessageHandler]` mechanism:
- `MessageHandler_Valid_NoErrors` - Valid message handler with Execute method
- `MessageHandler_MissingQueueProvider_EmitsError` - QueueProvider not implementing ILwxQueueProvider
- `MessageHandler_InvalidHandlerErrorPolicy_EmitsError` - HandlerErrorPolicy not implementing ILwxErrorPolicy
- `MessageHandler_InvalidProviderErrorPolicy_EmitsError` - ProviderErrorPolicy not implementing ILwxProviderErrorPolicy
- `MessageHandler_InvalidNaming_EmitsWarning` - Class name not matching MessageHandler{Name} pattern
- `MessageHandler_WrongNamespace_EmitsError` - Handler not in .MessageHandlers namespace
- `MessageHandler_UnannotatedClass_EmitsError` - Unannotated class in MessageHandlers namespace
- `MessageHandler_OrphanNoService_EmitsError` - MessageHandler without matching Service

## MockServer

**IMPORTANT:** Do not change MockServer.cs without asking the operator first. This class is critical for test infrastructure and any modifications could break tests or performance.
