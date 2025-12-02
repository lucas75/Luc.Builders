using Lwx.Builders.MicroService.Atributes;
using Microsoft.AspNetCore.Builder;

namespace ExampleOrg.Product.ServiceAbc;

[LwxServiceConfig(
    Title = "MyUnit Worker 001 API",
    Description = "API for MyUnit Worker 001",
    Version = "v1.0.0",
    PublishSwagger = LwxStage.Development
)]
public static partial class Service
{
    
}
