using Lwx.Archetype.MicroService.Atributes;

namespace MyCompany.MyUnit.Worker001;

[LwxServiceConfig(
    Title = "MyUnit Worker 001 API",
    Description = "API for MyUnit Worker 001",
    Version = "v1.0.0",
    PublishSwagger = LwxStage.Development
)]
public static partial class ServiceConfig
{
}
