using System.Reflection;
using Lwx.Archetype.MicroService.Atributes;
using Xunit;

namespace ExampleCompany.ExampleProduct.Worket001.Tests;

public class ServiceConfigTests
{
    [Fact]
    public void ServiceConfig_ClassExists_WithAttribute()
    {
        var t = typeof(ExampleCompany.ExampleProduct.Worker001.ServiceConfig);
        Assert.NotNull(t);

        var attr = t.GetCustomAttribute<LwxServiceConfigAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void ServiceConfig_PublishSwagger_IsDevelopment()
    {
        var t = typeof(ExampleCompany.ExampleProduct.Worker001.ServiceConfig);
        var attr = t.GetCustomAttribute<LwxServiceConfigAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(LwxStage.Development, attr.PublishSwagger);
    }
}
