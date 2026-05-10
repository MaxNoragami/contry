using System.Text;

LoadRootEnvironmentFile(args);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/", () => TypedResults.Ok(new
{
    name = "Contry API",
    status = "ok",
    docs = "/swagger"
}));

app.MapGet("/health", () => TypedResults.Ok(new
{
    status = "healthy"
}));

app.Run();

static void LoadRootEnvironmentFile(string[] args)
{
    var environmentName = GetEnvironmentName(args);
    var envFileName = environmentName switch
    {
        "Development" => ".env.dev",
        "Production" => ".env.prod",
        _ => $".env.{environmentName.ToLowerInvariant()}"
    };

    var envFilePath = FindEnvironmentFilePath(envFileName);

    if (envFilePath is null)
    {
        return;
    }

    foreach (var rawLine in File.ReadAllLines(envFilePath, Encoding.UTF8))
    {
        var line = rawLine.Trim();

        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
        {
            continue;
        }

        var separatorIndex = line.IndexOf('=');

        if (separatorIndex <= 0)
        {
            continue;
        }

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim().Trim('"');

        if (string.IsNullOrWhiteSpace(key) || Environment.GetEnvironmentVariable(key) is not null)
        {
            continue;
        }

        Environment.SetEnvironmentVariable(key, value);
    }
}

static string? FindEnvironmentFilePath(string envFileName)
{
    var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
    var appBaseDirectory = new DirectoryInfo(AppContext.BaseDirectory);

    return FindInAncestors(currentDirectory, envFileName)
        ?? FindInAncestors(appBaseDirectory, envFileName);
}

static string? FindInAncestors(DirectoryInfo? directory, string envFileName)
{
    while (directory is not null)
    {
        var candidate = Path.Combine(directory.FullName, envFileName);

        if (File.Exists(candidate))
        {
            return candidate;
        }

        directory = directory.Parent;
    }

    return null;
}

static string GetEnvironmentName(string[] args)
{
    var aspnetEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

    if (!string.IsNullOrWhiteSpace(aspnetEnvironment))
    {
        return aspnetEnvironment;
    }

    var dotnetEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

    if (!string.IsNullOrWhiteSpace(dotnetEnvironment))
    {
        return dotnetEnvironment;
    }

    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] is "--environment" or "-e")
        {
            return args[i + 1];
        }
    }

    return "Production";
}

public partial class Program;
