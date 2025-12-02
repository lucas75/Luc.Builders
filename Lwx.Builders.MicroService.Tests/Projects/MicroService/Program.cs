using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace MicroService;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configure Lwx services (generated helpers will be on Service)
        Service.LwxConfigure(builder);

        var app = builder.Build();

        Service.LwxConfigure(app);

        app.Run();
    }
}
