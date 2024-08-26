

using System.Data;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.SqlServer.Design.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace EzDbEf;

public class SolutionGenerator
{
    private readonly string _connectionString;
    private readonly List<DatabaseMaskParser.DatabaseObject> _databaseMasks;
    private readonly string _outputPath;
    private readonly string _assemblyPrefix;
    private readonly ILogger _logger;
    private readonly string _packageVersion;
    private readonly string _serverName;
    private string _solutionPath;
    private readonly string _dalPath;

    public SolutionGenerator(string connectionString, List<DatabaseMaskParser.DatabaseObject> databaseMasks, string outputPath, string assemblyPrefix, ILogger logger, string packageVersion)
    {
        _connectionString = connectionString;
        _databaseMasks = databaseMasks;
        _outputPath = outputPath;
        _assemblyPrefix = assemblyPrefix;
        _logger = logger;
        _packageVersion = packageVersion;
        _serverName = ExtractServerName(connectionString);
        _solutionPath = Path.Combine(outputPath, "src", $"{_serverName}.sln");
        _dalPath = Path.Combine(outputPath, "src", "DAL");
        Log($"Output path: {_outputPath}");
        Log($"Solution path: {_solutionPath}");
        Log($"DAL path: {_dalPath}");
    }

    public async Task<string> GenerateAsync()
    {
        Log($"Starting solution generation for server: {_serverName}");

        var solutionFile = new FileInfo(_solutionPath);
        solutionFile.Directory?.Create();

        await CreateSolutionFileWithHeaderAsync(_solutionPath);
        Log($"Created solution file: {_solutionPath}");

        Directory.CreateDirectory(_dalPath);

        foreach (var database in _databaseMasks.Select(m => m.Database).Distinct())
        {
            await GenerateDatabaseProjectAsync(database);
        }

        var databases = await GetDatabasesAsync();
        Log($"Found {databases.Count} matching databases");

        foreach (var database in databases)
        {
            Log($"Processing database: {database}");
            await GenerateDatabaseProjectAsync(database);
        }

        Log("Solution generation completed successfully");

        await CompileSolutionAsync(_solutionPath);
        await GenerateNuGetPackagesAsync(_solutionPath);

        return _solutionPath;
    }

    private async Task<List<string>> GetDatabasesAsync()
    {
        Log("Starting database retrieval process");
        var databases = new List<string>();
        var systemDatabases = new HashSet<string> { "master", "tempdb", "model", "msdb" };

        try
        {
            using var connection = new SqlConnection(_connectionString);

            await TestConnectionAsync(connection);

            Log("Opening connection to SQL Server");
            await connection.OpenAsync();
            Log($"Successfully connected to SQL Server. Connection State: {connection.State}");

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
            command.CommandTimeout = 30;

            Log("Executing SQL command");
            using var reader = await command.ExecuteReaderAsync();

            Log("Processing query results");
            while (await reader.ReadAsync())
            {
                var dbName = reader.GetString(0);
                Log($"Processing database: {dbName}");

                if (!systemDatabases.Contains(dbName) &&
                    (_databaseMasks.Count == 0 || _databaseMasks.Any(mask => mask.Regex.IsMatch(dbName))))
                {
                    databases.Add(dbName);
                    Log($"Found matching database: {dbName}");
                }
                else
                {
                    Log($"Skipping database: {dbName}");
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

    private void CreateProjectFile(string projectPath, string projectName, string packageVersion)
    {
        Log($"Creating project file: {projectPath}");
        var projectContent = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>{projectName}</RootNamespace>
    <AssemblyName>{projectName}</AssemblyName>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <Version>{packageVersion}</Version>
    <FileVersion>{packageVersion}</FileVersion>
    <AssemblyVersion>{packageVersion}</AssemblyVersion>
    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
    <PackageRequireLicenseAcceptance>false</PackageRequireLicenseAcceptance>
    <PackageId>{projectName}</PackageId>
    <PackageVersion>{packageVersion}</PackageVersion>
    <Authors>Your Name or Company</Authors>
    <Description>Generated Entity Framework Core models for database access</Description>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Microsoft.EntityFrameworkCore.SqlServer"" Version=""8.0.0"" />
    <PackageReference Include=""Microsoft.EntityFrameworkCore.Design"" Version=""8.0.0"">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <Compile Include=""Models\**\*.cs"" />
  </ItemGroup>
</Project>";

        File.WriteAllText(projectPath, projectContent);
        Log($"Created project file: {projectPath}");
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
            .AddDbContext<DbContext>(options => options.UseSqlServer(GetDatabaseSpecificConnectionString(_connectionString, database)))
            .AddEntityFrameworkDesignTimeServices()
            .AddSingleton<IOperationReporter>(new OperationReporter(_logger));

        serviceCollection.AddEntityFrameworkDesignTimeServices();
        new SqlServerDesignTimeServices().ConfigureDesignTimeServices(serviceCollection);

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

        Log($"Generated EF Core model for database: {database}");
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

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, e) => 
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
                Log($"Build output: {e.Data}");
            }
        };
        process.ErrorDataReceived += (sender, e) => 
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
                Log($"Build error: {e.Data}");
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var fullOutput = outputBuilder.ToString();
            var fullError = errorBuilder.ToString();
            Log($"Full build output:\n{fullOutput}");
            Log($"Full build errors:\n{fullError}");
            throw new Exception($"Solution compilation failed. Exit code: {process.ExitCode}\nErrors: {fullError}");
        }

        Log("Solution compiled successfully");
    }

    private async Task GenerateNuGetPackagesAsync(string solutionPath)
    {
        Log("Generating NuGet packages");
        var solutionDir = Path.GetDirectoryName(solutionPath)!;
        var artifactsDir = Path.Combine(_outputPath, "artifacts");
        Directory.CreateDirectory(artifactsDir);

        var projectDirs = Directory.GetDirectories(Path.Combine(solutionDir, "DAL"));

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
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                Log($"Error generating NuGet package for {projectName}: {error}");
                throw new Exception($"NuGet package generation failed for {projectName}: {error}");
            }

            Log($"Generated NuGet package for {projectName}");
            Log($"Output: {output}");
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

        var serverName = dataSource.Replace('\\', '_');
        serverName = serverName.Split(',')[0];
        serverName = Regex.Replace(serverName, "[^a-zA-Z0-9_]", "");

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

    private async Task CreateSolutionFileWithHeaderAsync(string solutionPath)
    {
        Log($"Creating solution file with header at: {solutionPath}");
        var solutionContent = new StringBuilder();
        solutionContent.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
        solutionContent.AppendLine("# Visual Studio Version 17");
        solutionContent.AppendLine("VisualStudioVersion = 17.0.31903.59");
        solutionContent.AppendLine("MinimumVisualStudioVersion = 10.0.40219.1");
        solutionContent.AppendLine("Global");
        solutionContent.AppendLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
        solutionContent.AppendLine("\t\tDebug|Any CPU = Debug|Any CPU");
        solutionContent.AppendLine("\t\tRelease|Any CPU = Release|Any CPU");
        solutionContent.AppendLine("\tEndGlobalSection");
        solutionContent.AppendLine("\tGlobalSection(SolutionProperties) = preSolution");
        solutionContent.AppendLine("\t\tHideSolutionNode = FALSE");
        solutionContent.AppendLine("\tEndGlobalSection");
        solutionContent.AppendLine("EndGlobal");

        await File.WriteAllTextAsync(solutionPath, solutionContent.ToString());
        Log($"Solution file created successfully at: {solutionPath}");
    }

    private async Task AddProjectToSolutionAsync(string solutionPath, string projectPath, string projectName)
    {
        Log($"Adding project to solution: {projectName}");
        var projectGuid = Guid.NewGuid().ToString().ToUpper();
        var relativeProjectPath = Path.GetRelativePath(Path.GetDirectoryName(solutionPath)!, projectPath);

        var solutionContent = await File.ReadAllTextAsync(solutionPath);
        var lines = solutionContent.Split(Environment.NewLine).ToList();

        int insertIndex = lines.FindIndex(l => l.StartsWith("Global"));
        if (insertIndex == -1)
        {
            insertIndex = lines.Count;
        }

        lines.Insert(insertIndex, $"Project(\"{{9A19103F-16F7-4668-BE54-9A1E7A4F7556}}\") = \"{projectName}\", \"{relativeProjectPath.Replace('\\', '/')}\", \"{{{projectGuid}}}\"");
        lines.Insert(insertIndex + 1, "EndProject");

        int configIndex = lines.FindIndex(l => l.Trim().StartsWith("GlobalSection(ProjectConfigurationPlatforms) = postSolution"));
        if (configIndex == -1)
        {
            configIndex = lines.FindIndex(l => l.Trim().StartsWith("GlobalSection(SolutionProperties)"));
            if (configIndex == -1)
            {
                configIndex = lines.Count - 1;
            }
            lines.Insert(configIndex, "\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");
            lines.Insert(configIndex + 1, "\tEndGlobalSection");
        }

        lines.Insert(configIndex + 1, $"\t\t{{{projectGuid}}}.Debug|Any CPU.ActiveCfg = Debug|Any CPU");
        lines.Insert(configIndex + 2, $"\t\t{{{projectGuid}}}.Debug|Any CPU.Build.0 = Debug|Any CPU");
        lines.Insert(configIndex + 3, $"\t\t{{{projectGuid}}}.Release|Any CPU.ActiveCfg = Release|Any CPU");
        lines.Insert(configIndex + 4, $"\t\t{{{projectGuid}}}.Release|Any CPU.Build.0 = Release|Any CPU");

        await File.WriteAllLinesAsync(solutionPath, lines);

        Log($"Added project to solution: {projectName}");
    }

    private async Task GenerateDatabaseProjectAsync(string database)
    {
        Log($"Generating project for database: {database}");
        var projectName = $"{_assemblyPrefix}.DAL.{database}";
        var projectPath = Path.Combine(_dalPath, database, $"{projectName}.csproj");

        Log($"Project path: {projectPath}");

        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        Log($"Created directory: {Path.GetDirectoryName(projectPath)}");

        CreateProjectFile(projectPath, projectName, _packageVersion);
        Log($"Created project file: {projectPath}");

        await AddProjectToSolutionAsync(_solutionPath, projectPath, projectName);
        Log($"Added project to solution: {projectName}");

        await GenerateEfCoreModelAsync(database, projectPath);
        Log($"Generated EF Core model for database: {database}");
    }
}