# TODO

## ✅ Completed: LwxSetting Mechanism (December 2025)

Replaced `[FromConfig]` constructor parameter injection with `[LwxSetting]` static partial properties:

1. ✅ **New `[LwxSetting("Key")]` attribute** - Marks static partial properties for configuration injection
2. ✅ **Partial property generation** - Generator creates backing fields named `__N_Key`
3. ✅ **Service.ConfigureSettings(builder)** - Reads configuration and sets backing fields via reflection
4. ✅ **Removed `[FromConfig]` mechanism** - Simplified worker registration (no more factory-based DI)
5. ✅ **Updated sample projects and tests** - Migrated to new mechanism

### New Diagnostics
- `LWX030` - LwxSetting must be on static property
- `LWX031` - LwxSetting must be on partial property
- `LWX032` - LwxSetting property must be getter-only
- `LWX033` - LwxSetting unsupported type (only primitives allowed)
- `LWX034` - LwxSetting missing key
- `LWX035` - LwxSetting containing type must be partial

### Key Implementation Files
- `Attributes/LwxSettingAttribute.cs` - New attribute definition
- `Processors/LwxSettingTypeProcessor.cs` - Property processor
- `Processors/LwxSettingPostInitializationProcessor.cs` - Embeds attribute
- `Processors/LwxServiceTypeProcessor.cs` - Generates ConfigureSettings method
- `Processors/LwxWorkerTypeProcessor.cs` - Simplified (removed FromConfig handling)

---

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
