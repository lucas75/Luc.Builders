# TODO

## ✅ Completed: Multi-Service Architecture (December 2025)

The following changes have been implemented:

1. ✅ **Allow multiple LwxService** - Each namespace prefix can have its own service
2. ✅ **Associate Endpoints and Workers to LwxService by namespace**:
   - `AssemblyNamespace.Abc.Endpoints...` → `AssemblyNamespace.Abc.Service`
   - `AssemblyNamespace.Abc.Workers...` → `AssemblyNamespace.Abc.Service`
   - `AssemblyNamespace.Cde.Endpoints...` → `AssemblyNamespace.Cde.Service`
   - `AssemblyNamespace.Cde.Workers...` → `AssemblyNamespace.Cde.Service`
3. ✅ **Generated output**:
   - `AssemblyNamespace.Abc.Service.Configure(...)` - wires only Abc's endpoints/workers
   - `AssemblyNamespace.Cde.Service.Configure(...)` - wires only Cde's endpoints/workers
4. ✅ **Namespace validation**: Service outside assembly root only allowed in test/lib projects
   - Detection via: assembly name patterns (`.Tests`, `.Lib`), absence of `Program.cs`, `OutputKind`

### New Diagnostics
- `LWX017` - Duplicate Service for same namespace prefix
- `LWX020` - Service namespace must be under assembly namespace
- `LWX021` - Orphan Endpoint (no matching service)
- `LWX022` - Orphan Worker (no matching service)

### Key Implementation Files
- `Generator.cs` - New `ServiceRegistration` class and `ServiceRegistrations` dictionary
- `LwxServiceTypeProcessor.cs` - Rewritten to use service registrations
- `LwxEndpointTypeProcessor.cs` - Registers endpoints to appropriate service by namespace
- `LwxWorkerTypeProcessor.cs` - Registers workers to appropriate service by namespace 
