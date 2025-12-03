using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using OkService;

var builder = WebApplication.CreateBuilder(args);
Service.Configure(builder);
var app = builder.Build();
Service.Configure(app);
app.Run();
