using Lwx.Archetype.MicroService.Atributes;
using Microsoft.AspNetCore.Builder;

namespace MyCompany.MyUnit.Worker001;

[LwxServiceConfig(
    Title = "MyUnit Worker 001 API",
    Description = "API for MyUnit Worker 001",
    Version = "v1.0.0",
    PublishSwagger = LwxStage.Development,
    GenerateMain = true
)]
public static partial class ServiceConfig
{
    public static void Configure(WebApplicationBuilder builder)
    {
        // Additional configuration here
    }

    public static void Configure(WebApplication app)
    {
        // Additional configuration here
    }
}
