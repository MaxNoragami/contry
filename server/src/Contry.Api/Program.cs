using Contry.Api.Configuration;
using Contry.Api.DependencyInjection;
using Contry.Infrastructure;

EnvironmentLoader.LoadRootEnvironmentFile(args);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddContryApi(builder.Configuration);
builder.Services.AddContryInfrastructure(builder.Configuration);

var app = builder.Build();

await app.UseContryApiAsync();
app.MapContryEndpoints();

app.Run();
