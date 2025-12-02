using System.Reflection;
using Lwx.Builders.MicroService.Atributes;
using Xunit;

namespace ExampleOrg.Product.ServiceAbc.Tests;

public class ServiceConfigTests
{
    [Fact]
    public void ServiceConfig_ClassExists_WithAttribute()
    {
        var t = typeof(ExampleOrg.Product.ServiceAbc.Service);
        Assert.NotNull(t);

        var attr = t.GetCustomAttribute<LwxServiceConfigAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void Service_PublishSwagger_IsDevelopment()
    {
        var t = typeof(ExampleOrg.Product.ServiceAbc.Service);
        var attr = t.GetCustomAttribute<LwxServiceConfigAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(LwxStage.Development, attr.PublishSwagger);
    }
}
