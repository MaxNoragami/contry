using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Contry.Infrastructure.Persistence;

public sealed class ContryDbContextFactory : IDesignTimeDbContextFactory<ContryDbContext>
{
    public ContryDbContext CreateDbContext(string[] args)
    {
        LoadRootEnvironmentFile(args);

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings__Database must be set to create the design-time ContryDbContext.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<ContryDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ContryDbContext(optionsBuilder.Options);
    }

    private static void LoadRootEnvironmentFile(string[] args)
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

        foreach (var rawLine in File.ReadAllLines(envFilePath))
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

    private static string? FindEnvironmentFilePath(string envFileName)
    {
        var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
        var appBaseDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        return FindInAncestors(currentDirectory, envFileName)
            ?? FindInAncestors(appBaseDirectory, envFileName);
    }

    private static string? FindInAncestors(DirectoryInfo? directory, string envFileName)
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

    private static string GetEnvironmentName(string[] args)
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

        return "Development";
    }
}
