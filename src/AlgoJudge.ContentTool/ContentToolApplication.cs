using AlgoJudge.ContentTool.Configuration;
using AlgoJudge.ContentTool.Importing;
using AlgoJudge.ContentTool.Packages;
using AlgoJudge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace AlgoJudge.ContentTool;

public static class ContentToolApplication
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (!TryParseCommand(args, out var command))
        {
            WriteUsage();
            return 64;
        }

        if (command.Name == "help")
        {
            WriteUsage();
            return 0;
        }

        using var cancellationSource = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationSource.Cancel();
        };

        try
        {
            var configuration = BuildConfiguration();
            var options = configuration
                .GetSection(ContentImportOptions.SectionName)
                .Get<ContentImportOptions>() ?? new ContentImportOptions();
            var reader = new ProblemPackageReader(options);
            var package = await reader.ReadAsync(command.PackagePath!, cancellationSource.Token);

            if (command.Name == "validate")
            {
                Console.WriteLine(
                    $"Valid package '{package.Metadata.Slug}': " +
                    $"{package.Samples.Count} sample(s), " +
                    $"{package.JudgeTestCases.Count} private test case(s).");
                return 0;
            }

            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.Error.WriteLine(
                    "ConnectionStrings:DefaultConnection is required for import.");
                return 4;
            }

            var dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            await using var context = new AppDbContext(dbContextOptions);
            await EnsureDatabaseReadyAsync(context, cancellationSource.Token);
            var importer = new ProblemPackageImporter(context);
            var result = await importer.ImportAsync(
                package,
                command.Replace,
                cancellationSource.Token);

            var action = result.Replaced ? "Replaced" : "Imported";
            Console.WriteLine(
                $"{action} Draft problem '{result.Slug}' (ID {result.ProblemId}, " +
                $"judge version {result.JudgeVersion}, {result.SampleCount} sample(s), " +
                $"{result.JudgeTestCaseCount} private test case(s)).");
            return 0;
        }
        catch (PackageValidationException exception)
        {
            Console.Error.WriteLine("Package validation failed:");
            foreach (var error in exception.Errors)
                Console.Error.WriteLine($"- {error}");
            return 2;
        }
        catch (ContentImportConflictException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 3;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Operation cancelled.");
            return 130;
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 4;
        }
        catch (DbUpdateException)
        {
            Console.Error.WriteLine(
                "The database rejected the import. No package content was committed.");
            return 4;
        }
        catch (NpgsqlException)
        {
            Console.Error.WriteLine(
                "The PostgreSQL operation failed. No package content was committed.");
            return 4;
        }
        catch (ArgumentException)
        {
            Console.Error.WriteLine("ContentTool configuration is invalid.");
            return 4;
        }
        catch (IOException)
        {
            Console.Error.WriteLine("ContentTool configuration could not be loaded.");
            return 4;
        }
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();
    }

    private static async Task EnsureDatabaseReadyAsync(
        AppDbContext context,
        CancellationToken cancellationToken)
    {
        var pendingMigrations = (await context.Database
            .GetPendingMigrationsAsync(cancellationToken))
            .ToArray();
        if (pendingMigrations.Length > 0)
        {
            throw new InvalidOperationException(
                "The database has pending migrations. Apply them before importing content.");
        }
    }

    private static bool TryParseCommand(string[] args, out ContentToolCommand command)
    {
        command = new ContentToolCommand("help", null, Replace: false);
        if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
            return true;

        if (args[0] is not ("validate" or "import") || args.Length < 2)
            return false;

        var replace = false;
        foreach (var option in args.Skip(2))
        {
            if (option == "--replace" && args[0] == "import" && !replace)
            {
                replace = true;
                continue;
            }

            return false;
        }

        command = new ContentToolCommand(args[0], args[1], replace);
        return true;
    }

    private static void WriteUsage()
    {
        Console.WriteLine("AlgoJudge ContentTool");
        Console.WriteLine("  validate <package.zip>");
        Console.WriteLine("  import <package.zip> [--replace]");
    }

    private sealed record ContentToolCommand(string Name, string? PackagePath, bool Replace);
}
