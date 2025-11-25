using Lwx.Builders.MicroService.Atributes;
using Microsoft.AspNetCore.Builder;

namespace ExampleOrg.Product.ServiceAbc;

[LwxServiceConfig(
    Title = "MyUnit Worker 001 API",
    Description = "API for MyUnit Worker 001",
    Version = "v1.0.0",
    PublishSwagger = LwxStage.Development,
    GenerateMain = true
)]
public static partial class ServiceConfig
{
    // Optional builder-time configuration hook invoked from generated Main()
    public static void Configure(WebApplicationBuilder builder)
    {
    }

    // Optional runtime configuration hook invoked from generated Main()
    public static void Configure(WebApplication app)
    {
    }
}
