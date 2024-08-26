using System.CommandLine;
using System.Data.SqlClient;

namespace EzDbEf;

class CommandLineSetup
{
    public static RootCommand CreateRootCommand(IServiceProvider serviceProvider)
    {
        var connectionStringOption = new Option<string>(
            "--connection-string",
            "The database connection string or server name")
        { IsRequired = true };

        var dbMasksOption = new Option<string[]>(
            "--db-masks",
            "Database name masks (e.g., dbname.schema.table)")
        { IsRequired = false, AllowMultipleArgumentsPerToken = true };

        var outputPathOption = new Option<string>(
            "--output-path",
            () => Path.Combine(Environment.CurrentDirectory, "output"),
            "Output path for generated files");

        var assemblyPrefixOption = new Option<string>(
            "--assembly-prefix",
            () => "Noctusoft.EzDbEF",
            "Prefix for generated assemblies");

        var verboseOption = new Option<bool>(
            "--verbose",
            () => false,
            "Enable verbose output");
        
        var packageVersionOption = new Option<string>(
            "--package-version",
            () => "1.0.0",
            "Version for generated NuGet packages");

        var generateApiOption = new Option<bool>(
            "--generate-api",
            () => false,
            "Generate API project");

        var rootCommand = new RootCommand("EzDbEf - Easy Database Entity Framework Generator")
            {
                connectionStringOption,
                dbMasksOption,
                outputPathOption,
                assemblyPrefixOption,
                verboseOption,
                packageVersionOption,
                generateApiOption
            };
        rootCommand.SetHandler(async (string connectionStringOrServerName, string[] dbMasks, string outputPath, string assemblyPrefix, bool verbose, string packageVersion, bool generateApi) =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<CommandLineSetup>>();
            await ExecuteAsync(connectionStringOrServerName, dbMasks, outputPath, assemblyPrefix, verbose, packageVersion, generateApi, logger);
        }, connectionStringOption, dbMasksOption, outputPathOption, assemblyPrefixOption, verboseOption, packageVersionOption, generateApiOption);

        return rootCommand;
    }

    private static async Task ExecuteAsync(string connectionStringOrServerName, string[] dbMasks, string outputPath, string assemblyPrefix, bool verbose, string packageVersion, bool generateApi, ILogger logger)
    {
        string connectionString = ValidateAndGetConnectionString(connectionStringOrServerName, verbose, logger);

        logger.LogInformation("Connection String: {ConnectionString}", connectionString);
        logger.LogInformation("Database Masks: {DbMasks}", string.Join(", ", dbMasks));
        logger.LogInformation("Output Path: {OutputPath}", outputPath);
        logger.LogInformation("Assembly Prefix: {AssemblyPrefix}", assemblyPrefix);
        logger.LogInformation("Verbose: {Verbose}", verbose);

        var parsedMasks = DatabaseMaskParser.ParseMasks(dbMasks);
        
        var solutionGenerator = new SolutionGenerator(connectionString, parsedMasks, outputPath, assemblyPrefix, logger, packageVersion);
        string solutionPath = await solutionGenerator.GenerateAsync();

        if (generateApi)
        {
            var apiGenerator = new ApiGenerator(outputPath, assemblyPrefix, logger, connectionString, Path.GetFileName(solutionPath));
            await apiGenerator.GenerateAsync();
        }
    }

    private static string ValidateAndGetConnectionString(string input, bool verbose, ILogger logger)
    {
        if (IsConnectionString(input))
        {
            logger.LogInformation("Input is a valid connection string.");
            return input;
        }
        else
        {
            logger.LogInformation("Input is not a valid connection string. Treating it as a server name.");
            return $"Server={input};Integrated Security=True;";
        }
    }

    private static bool IsConnectionString(string input)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(input);
            return !string.IsNullOrEmpty(builder.DataSource);
        }
        catch
        {
            return false;
        }
    }
}