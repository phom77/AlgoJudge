using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.ContentTool.Configuration;
using AlgoJudge.ContentTool.Generation;
using AlgoJudge.ContentTool.Importing;
using AlgoJudge.ContentTool.Packages;
using AlgoJudge.ContentTool.Publishing;
using AlgoJudge.Infrastructure.Data;
using AlgoJudge.Infrastructure.Grading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
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
            if (command.Name is "generate" or "validate-generated")
            {
                return await GenerateTestsAsync(
                    configuration,
                    command.Name,
                    command.Target!,
                    cancellationSource.Token);
            }

            if (command.Name is "validate" or "import")
            {
                var options = configuration
                    .GetSection(ContentImportOptions.SectionName)
                    .Get<ContentImportOptions>() ?? new ContentImportOptions();
                var reader = new ProblemPackageReader(options);
                var package = await reader.ReadAsync(command.Target!, cancellationSource.Token);

                if (command.Name == "validate")
                {
                    Console.WriteLine(
                        $"Valid package '{package.Metadata.Slug}': " +
                        $"execution mode {package.Metadata.ExecutionMode}, " +
                        $"{package.Samples.Count} sample(s), " +
                        $"{package.JudgeTestCases.Count} private test case(s).");
                    return 0;
                }

                return await ImportAsync(
                    configuration,
                    package,
                    command.Replace,
                    cancellationSource.Token);
            }

            return await ChangePublicationAsync(
                configuration,
                command.Name,
                command.Target!,
                cancellationSource.Token);
        }
        catch (PackageValidationException exception)
        {
            Console.Error.WriteLine("Package validation failed:");
            foreach (var error in exception.Errors)
                Console.Error.WriteLine($"- {error}");
            return 2;
        }
        catch (TestGenerationException exception)
        {
            Console.Error.WriteLine($"Test generation failed: {exception.Message}");
            return 2;
        }
        catch (ContentImportConflictException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 3;
        }
        catch (ContentPublicationConflictException exception)
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

    private static async Task<int> ImportAsync(
        IConfiguration configuration,
        ProblemPackage package,
        bool replace,
        CancellationToken cancellationToken)
    {
        await using var context = await CreateReadyDbContextAsync(
            configuration,
            cancellationToken);
        var importer = new ProblemPackageImporter(context);
        var result = await importer.ImportAsync(package, replace, cancellationToken);

        var action = result.Replaced ? "Replaced" : "Imported";
        Console.WriteLine(
            $"{action} Draft problem '{result.Slug}' (ID {result.ProblemId}, " +
            $"judge version {result.JudgeVersion}, {result.SampleCount} sample(s), " +
            $"{result.JudgeTestCaseCount} private test case(s)).");
        return 0;
    }

    private static async Task<int> GenerateTestsAsync(
        IConfiguration configuration,
        string commandName,
        string problemDirectory,
        CancellationToken cancellationToken)
    {
        var options = configuration
            .GetSection(ContentImportOptions.SectionName)
            .Get<ContentImportOptions>() ?? new ContentImportOptions();
        var manifestReader = new GeneratorManifestReader(options.MaxJudgeTestCaseCount);
        var manifest = await manifestReader.ReadAsync(problemDirectory, cancellationToken);
        var loader = new DotNetGenerationComponentLoader();
        var generator = loader.Load<ITestCaseGenerator>(problemDirectory, manifest.Generator);
        var validator = loader.Load<IInputValidator>(problemDirectory, manifest.InputValidator);
        var sandbox = new DockerSandboxService(
            configuration,
            NullLogger<DockerSandboxService>.Instance);
        var referenceRunner = new Cpp17ReferenceSolutionRunner(sandbox);
        var service = new TestGenerationService(options);

        var result = commandName == "generate"
            ? await service.GenerateAsync(
                problemDirectory,
                manifest,
                generator,
                validator,
                referenceRunner,
                cancellationToken)
            : await service.ValidateGeneratedAsync(
                problemDirectory,
                manifest,
                generator,
                validator,
                referenceRunner,
                cancellationToken);

        var action = commandName == "generate" ? "Generated" : "Validated";
        Console.WriteLine(
            $"{action} {result.TestCaseCount} test case(s); suite SHA-256 {result.SuiteSha256}.");
        return 0;
    }

    private static async Task<int> ChangePublicationAsync(
        IConfiguration configuration,
        string commandName,
        string slug,
        CancellationToken cancellationToken)
    {
        await using var context = await CreateReadyDbContextAsync(
            configuration,
            cancellationToken);
        var publicationService = new ProblemPublicationService(context);
        var result = commandName == "publish"
            ? await publicationService.PublishAsync(slug, cancellationToken)
            : await publicationService.UnpublishAsync(slug, cancellationToken);

        var action = result.Changed
            ? commandName == "publish" ? "Published" : "Unpublished"
            : $"Already {result.Status}";
        Console.WriteLine($"{action} problem '{result.Slug}' (ID {result.ProblemId}).");
        return 0;
    }

    private static async Task<AppDbContext> CreateReadyDbContextAsync(
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is required for content operations.");
        }

        var dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var context = new AppDbContext(dbContextOptions);
        try
        {
            await EnsureDatabaseReadyAsync(context, cancellationToken);
            return context;
        }
        catch
        {
            await context.DisposeAsync();
            throw;
        }
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

        if (args[0] is not (
                "validate" or
                "import" or
                "publish" or
                "unpublish" or
                "generate" or
                "validate-generated") ||
            args.Length < 2)
            return false;

        if (args[0] is "publish" or "unpublish" or "generate" or "validate-generated")
        {
            if (args.Length != 2)
                return false;

            command = new ContentToolCommand(args[0], args[1], Replace: false);
            return true;
        }

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
        Console.WriteLine("  generate <problem-directory>");
        Console.WriteLine("  validate-generated <problem-directory>");
        Console.WriteLine("  publish <slug>");
        Console.WriteLine("  unpublish <slug>");
    }

    private sealed record ContentToolCommand(string Name, string? Target, bool Replace);
}
