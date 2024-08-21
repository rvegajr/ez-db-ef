
using System.Data;

namespace EzDbEf;

public class SolutionGenerator
{
    private readonly string _connectionString;
    private readonly List<DatabaseMaskParser.DatabaseObject> _parsedMasks;
    private readonly string _outputPath;
    private readonly string _assemblyPrefix;
    private readonly ILogger _logger;
    private readonly string _serverName;

    public SolutionGenerator(
        string connectionString,
        List<DatabaseMaskParser.DatabaseObject> parsedMasks,
        string outputPath,
        string assemblyPrefix,
        ILogger logger)
    {
        _connectionString = connectionString;
        _parsedMasks = parsedMasks;
        _outputPath = outputPath;
        _assemblyPrefix = assemblyPrefix;
        _logger = logger;
        _serverName = ExtractServerName(connectionString);
    }

    public async Task GenerateAsync()
    {
        Log($"Starting solution generation for server: {_serverName}");

        var solutionPath = Path.Combine(_outputPath, $"{_serverName}.sln");
        var solutionFile = new FileInfo(solutionPath);
        solutionFile.Directory?.Create();
        File.WriteAllText(solutionPath, "");
        Log($"Created solution file: {solutionPath}");

        var databases = await GetDatabasesAsync();
        Log($"Found {databases.Count} matching databases");

        foreach (var database in databases)
        {
            Log($"Processing database: {database}");
            var projectName = $"{_assemblyPrefix}.DAL.{database}";
            var projectPath = Path.Combine(_outputPath, database, $"{projectName}.csproj");

            Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
            Log($"Created directory: {Path.GetDirectoryName(projectPath)}");

            CreateProjectFile(projectPath, projectName);
            Log($"Created project file: {projectPath}");

            await AddProjectToSolutionAsync(solutionPath, projectPath, projectName);
            Log($"Added project to solution: {projectName}");

            await GenerateEfCoreModelAsync(database, projectPath);
            Log($"Generated EF Core model for database: {database}");
        }

        Log("Solution generation completed successfully");

        await CompileSolutionAsync(solutionPath);
        await GenerateNuGetPackagesAsync(solutionPath);
    }

    private async Task<List<string>> GetDatabasesAsync()
    {
        Log("Starting database retrieval process");
        var databases = new List<string>();
        var systemDatabases = new HashSet<string> { "master", "tempdb", "model", "msdb" };

        try
        {
            using var connection = new SqlConnection(_connectionString);

            // Test connection before opening
            await TestConnectionAsync(connection);

            Log("Opening connection to SQL Server");
            await connection.OpenAsync();
            Log($"Successfully connected to SQL Server. Connection State: {connection.State}");

            // Verify connection state
            if (connection.State != ConnectionState.Open)
            {
                throw new InvalidOperationException($"Expected connection to be open, but it is {connection.State}");
            }

            Log("Preparing SQL command to retrieve databases");
            var commandText = @"
            SELECT name 
            FROM sys.databases 
            WHERE database_id > 4 
            AND name NOT LIKE 'System%'
            AND state = 0  -- Only online databases
            ORDER BY name";

            using var command = new SqlCommand(commandText, connection);
            command.CommandTimeout = 30; // Set an appropriate timeout

            Log("Executing SQL command");
            using var reader = await command.ExecuteReaderAsync();

            Log("Processing query results");
            while (await reader.ReadAsync())
            {
                var dbName = reader.GetString(0);
                Log($"Processing database: {dbName}");

                if (!systemDatabases.Contains(dbName) &&
                    _parsedMasks.Any(mask => DatabaseMaskParser.IsMatch(mask, dbName, "*", "*")))
                {
                    databases.Add(dbName);
                    Log($"Found matching database: {dbName}");
                }
                else
                {
                    Log($"Skipping non-matching database: {dbName}");
                }
            }

            Log($"Retrieved {databases.Count} matching databases");
        }
        catch (SqlException ex)
        {
            Log($"SQL error occurred while retrieving databases: {ex.Message}");
            Log($"SQL Error Number: {ex.Number}");
            Log($"SQL State: {ex.State}");
            Log($"SQL Server: {ex.Server}");
            throw new DatabaseAccessException("Failed to retrieve databases due to a SQL error", ex);
        }
        catch (InvalidOperationException ex)
        {
            Log($"Invalid operation occurred while retrieving databases: {ex.Message}");
            throw new DatabaseAccessException("Failed to retrieve databases due to an invalid operation", ex);
        }
        catch (Exception ex)
        {
            Log($"Unexpected error occurred while retrieving databases: {ex.Message}");
            throw new DatabaseAccessException("Failed to retrieve databases due to an unexpected error", ex);
        }

        return databases;
    }

    private async Task TestConnectionAsync(SqlConnection connection)
    {
        try
        {
            Log("Testing connection with a 5 second timeout");
            var connectionTestTask = connection.OpenAsync();
            if (await Task.WhenAny(connectionTestTask, Task.Delay(5000)) == connectionTestTask)
            {
                Log("Connection test successful");
                await connection.CloseAsync();
            }
            else
            {
                throw new TimeoutException("Connection test timed out after 5 seconds");
            }
        }
        catch (SqlException ex)
        {
            Log($"SQL error occurred during connection test: {ex.Message}");
            Log($"SQL Error Number: {ex.Number}");
            Log($"SQL State: {ex.State}");
            Log($"SQL Server: {ex.Server}");
            throw new DatabaseAccessException("Failed to connect to the database server", ex);
        }
        catch (TimeoutException ex)
        {
            Log($"Timeout occurred during connection test: {ex.Message}");
            throw new DatabaseAccessException("Connection to the database server timed out", ex);
        }
    }

    private void CreateProjectFile(string projectPath, string projectName)
    {
        Log($"Creating project file: {projectPath}");
        var projectXml = new XElement("Project",
            new XAttribute("Sdk", "Microsoft.NET.Sdk"),
            new XElement("PropertyGroup",
                new XElement("TargetFramework", "net8.0"),
                new XElement("ImplicitUsings", "enable"),
                new XElement("Nullable", "enable"),
                new XElement("RootNamespace", projectName),
                new XElement("AssemblyName", projectName)
            ),
            new XElement("ItemGroup",
                new XElement("PackageReference",
                    new XAttribute("Include", "Microsoft.EntityFrameworkCore.SqlServer"),
                    new XAttribute("Version", "8.0.0")
                ),
                new XElement("PackageReference",
                    new XAttribute("Include", "Microsoft.EntityFrameworkCore.Design"),
                    new XAttribute("Version", "8.0.0")
                )
            )
        );

        projectXml.Save(projectPath);
    }

    private async Task AddProjectToSolutionAsync(string solutionPath, string projectPath, string projectName)
    {
        Log($"Adding project to solution: {projectName}");
        var projectGuid = Guid.NewGuid().ToString().ToUpper();
        var relativeProjectPath = Path.GetRelativePath(Path.GetDirectoryName(solutionPath)!, projectPath);

        var projectEntry = $@"Project(""{{9A19103F-16F7-4668-BE54-9A1E7A4F7556}}"") = ""{projectName}"", ""{relativeProjectPath.Replace('\\', '/')}"", ""{{{projectGuid}}}""
EndProject
";

        await File.AppendAllTextAsync(solutionPath, projectEntry);
    }

    private async Task GenerateEfCoreModelAsync(string database, string projectPath)
    {
        Log($"Generating EF Core model for database: {database}");
        var projectDir = Path.GetDirectoryName(projectPath)!;
        var outputDir = Path.Combine(projectDir, "Models");
        Directory.CreateDirectory(outputDir);
        Log($"Created Models directory: {outputDir}");

        var optionsBuilder = new DbContextOptionsBuilder<DbContext>();
        optionsBuilder.UseSqlServer(GetDatabaseSpecificConnectionString(_connectionString, database));

        var serviceCollection = new ServiceCollection()
            .AddEntityFrameworkSqlServer()
            .AddLogging();

        var serviceProvider = serviceCollection.BuildServiceProvider();

        var scaffolder = serviceProvider.GetRequiredService<IReverseEngineerScaffolder>();

        var dbOptions = new DatabaseModelFactoryOptions();
        var modelOptions = new ModelReverseEngineerOptions
        {
            UseDatabaseNames = true
        };

        var codeOptions = new ModelCodeGenerationOptions
        {
            UseDataAnnotations = true,
            UseNullableReferenceTypes = true,
            ContextName = $"{database}Context",
            ContextNamespace = $"{_assemblyPrefix}.DAL.{database}.Models",
            ModelNamespace = $"{_assemblyPrefix}.DAL.{database}.Models",
        };

        Log("Starting database scaffolding");
        var scaffoldedModel = await Task.Run(() => scaffolder.ScaffoldModel(
            GetDatabaseSpecificConnectionString(_connectionString, database),
            dbOptions,
            modelOptions,
            codeOptions));

        foreach (var file in scaffoldedModel.AdditionalFiles)
        {
            var path = Path.Combine(outputDir, file.Path);
            await File.WriteAllTextAsync(path, file.Code);
        }

        var contextPath = Path.Combine(outputDir, $"{codeOptions.ContextName}.cs");
        await File.WriteAllTextAsync(contextPath, scaffoldedModel.ContextFile.Code);

        Log($"Saved {scaffoldedModel.AdditionalFiles.Count + 1} files to {outputDir}");

        await GenerateDbContextAsync(database, projectDir, outputDir);
        Log($"Generated DbContext for database: {database}");
    }

    private async Task GenerateDbContextAsync(string database, string projectDir, string outputDir)
    {
        Log($"Generating DbContext for database: {database}");
        var dbContextName = $"{database}Context";
        var dbContextPath = Path.Combine(outputDir, $"{dbContextName}.cs");

        var dbContextContent = $@"
using Microsoft.EntityFrameworkCore;

namespace {_assemblyPrefix}.DAL.{database}.Models
{{
    public partial class {dbContextName} : DbContext
    {{
        public {dbContextName}(DbContextOptions<{dbContextName}> options)
            : base(options)
        {{
        }}

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {{
            if (!optionsBuilder.IsConfigured)
            {{
                optionsBuilder.UseSqlServer(""Name=ConnectionStrings:{database}"");
            }}
        }}

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {{
            OnModelCreatingPartial(modelBuilder);
        }}

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }}
}}";

        await File.WriteAllTextAsync(dbContextPath, dbContextContent);

        // Update .csproj file to include the generated files
        var projectXml = XElement.Load(Path.Combine(projectDir, $"{_assemblyPrefix}.DAL.{database}.csproj"));
        var itemGroup = projectXml.Elements("ItemGroup").FirstOrDefault() ?? new XElement("ItemGroup");
        itemGroup.Add(new XElement("Compile", new XAttribute("Include", $"Models\\**\\*.cs")));
        projectXml.Add(itemGroup);
        projectXml.Save(Path.Combine(projectDir, $"{_assemblyPrefix}.DAL.{database}.csproj"));
    }

    private async Task CompileSolutionAsync(string solutionPath)
    {
        Log("Compiling solution");
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{solutionPath}\" -c Release",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new Exception($"Solution compilation failed: {error}");
        }

        Log("Solution compiled successfully");
    }

    private async Task GenerateNuGetPackagesAsync(string solutionPath)
    {
        Log("Generating NuGet packages");
        var solutionDir = Path.GetDirectoryName(solutionPath)!;
        var artifactsDir = Path.Combine(solutionDir, "artifacts");
        Directory.CreateDirectory(artifactsDir);

        var projectDirs = Directory.GetDirectories(solutionDir);

        foreach (var projectDir in projectDirs)
        {
            var projectFile = Directory.GetFiles(projectDir, "*.csproj").FirstOrDefault();
            if (projectFile == null) continue;

            var projectName = Path.GetFileNameWithoutExtension(projectFile);
            Log($"Generating NuGet package for {projectName}");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"pack \"{projectFile}\" -c Release -o \"{artifactsDir}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                throw new Exception($"NuGet package generation failed for {projectName}: {error}");
            }

            Log($"Generated NuGet package for {projectName}");
        }

        Log($"All NuGet packages generated and copied to {artifactsDir}");
    }

    private string GetDatabaseSpecificConnectionString(string baseConnectionString, string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(baseConnectionString);
        builder.InitialCatalog = databaseName;
        Log($"Created connection string for database: {databaseName}");
        return builder.ConnectionString;
    }

    private static string ExtractServerName(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var dataSource = builder.DataSource;

        // Replace backslashes with underscores
        var serverName = dataSource.Replace('\\', '_');

        // Remove port if present
        serverName = serverName.Split(',')[0];

        // Remove any non-alphanumeric characters except underscores
        serverName = Regex.Replace(serverName, "[^a-zA-Z0-9_]", "");

        // Ensure the name starts with a letter
        if (!char.IsLetter(serverName[0]))
        {
            serverName = "Server" + serverName;
        }

        return serverName;
    }

    public class DatabaseAccessException : Exception
    {
        public DatabaseAccessException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    private void Log(string message)
    {
        _logger.LogInformation(message);
    }
}