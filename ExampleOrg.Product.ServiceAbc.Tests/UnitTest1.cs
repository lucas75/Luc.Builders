using System.Reflection;
using Lwx.Builders.MicroService.Atributtes;
using Xunit;

namespace ExampleOrg.Product.ServiceAbc.Tests;

public class ServiceTests
{
    /// <summary>
    /// Verifies the Service class exists and has the [LwxService] attribute applied.
    /// </summary>
    [Fact(DisplayName = "Service: class exists and is annotated with [LwxService]")]
    public void Service_ClassExists_WithAttribute()
    {
        var t = typeof(ExampleOrg.Product.ServiceAbc.Service);
        Assert.NotNull(t);

        var attr = t.GetCustomAttribute<LwxServiceAttribute>();
        Assert.NotNull(attr);
    }

    /// <summary>
    /// Verifies the Service's PublishSwagger property is set to DevelopmentOnly.
    /// </summary>
    [Fact(DisplayName = "Service: PublishSwagger is DevelopmentOnly")]
    public void Service_PublishSwagger_IsDevelopment()
    {
        var t = typeof(ExampleOrg.Product.ServiceAbc.Service);
        var attr = t.GetCustomAttribute<LwxServiceAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(LwxStage.DevelopmentOnly, attr.PublishSwagger);
    }
}
