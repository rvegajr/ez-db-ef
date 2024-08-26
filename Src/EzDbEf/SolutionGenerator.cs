
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

    public SolutionGenerator(string connectionString, List<DatabaseMaskParser.DatabaseObject> databaseMasks, string outputPath, string assemblyPrefix, ILogger logger, string packageVersion)
    {
        _connectionString = connectionString;
        _databaseMasks = databaseMasks;
        _outputPath = outputPath;
        _assemblyPrefix = assemblyPrefix;
        _logger = logger;
        _packageVersion = packageVersion;
        _serverName = ExtractServerName(connectionString);
    }

    
    public async Task GenerateAsync()
    {
        Log($"Starting solution generation for server: {_serverName}");

        var solutionPath = Path.Combine(_outputPath, $"{_serverName}.sln");
        var solutionFile = new FileInfo(solutionPath);
        solutionFile.Directory?.Create();
        
        // Create the solution file with the proper header
        await CreateSolutionFileWithHeaderAsync(solutionPath);
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

            CreateProjectFile(projectPath, projectName, _packageVersion);
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
        var projectContent = new StringBuilder();
        projectContent.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        projectContent.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
        projectContent.AppendLine("  <PropertyGroup>");
        projectContent.AppendLine("    <TargetFramework>net8.0</TargetFramework>");
        projectContent.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
        projectContent.AppendLine("    <Nullable>enable</Nullable>");
        projectContent.AppendLine($"    <RootNamespace>{projectName}</RootNamespace>");
        projectContent.AppendLine($"    <AssemblyName>{projectName}</AssemblyName>");
        projectContent.AppendLine("    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>");
        projectContent.AppendLine($"    <Version>{packageVersion}</Version>");
        projectContent.AppendLine($"    <FileVersion>{packageVersion}</FileVersion>");
        projectContent.AppendLine($"    <AssemblyVersion>{packageVersion}</AssemblyVersion>");
        projectContent.AppendLine("    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>");
        projectContent.AppendLine("    <PackageRequireLicenseAcceptance>false</PackageRequireLicenseAcceptance>");
        projectContent.AppendLine($"    <PackageId>{projectName}</PackageId>");
        projectContent.AppendLine($"    <PackageVersion>{packageVersion}</PackageVersion>");
        projectContent.AppendLine("    <Authors>Your Name or Company</Authors>");
        projectContent.AppendLine("    <Description>Generated Entity Framework Core models for database access</Description>");
        projectContent.AppendLine("  </PropertyGroup>");
        projectContent.AppendLine("  <ItemGroup>");
        projectContent.AppendLine("    <PackageReference Include=\"Microsoft.EntityFrameworkCore.SqlServer\" Version=\"8.0.0\" />");
        projectContent.AppendLine("    <PackageReference Include=\"Microsoft.EntityFrameworkCore.Design\" Version=\"8.0.0\">");
        projectContent.AppendLine("      <PrivateAssets>all</PrivateAssets>");
        projectContent.AppendLine("      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>");
        projectContent.AppendLine("    </PackageReference>");
        projectContent.AppendLine("  </ItemGroup>");
        projectContent.AppendLine("  <ItemGroup>");
        projectContent.AppendLine("    <Compile Include=\"Models\\**\\*.cs\" />");
        projectContent.AppendLine("  </ItemGroup>");
        projectContent.AppendLine("</Project>");

        File.WriteAllText(projectPath, projectContent.ToString());
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

        // Create a new ServiceCollection and register the required services
        var serviceCollection = new ServiceCollection()
            .AddEntityFrameworkSqlServer()
            .AddDbContext<DbContext>(options => options.UseSqlServer(GetDatabaseSpecificConnectionString(_connectionString, database)))
            .AddEntityFrameworkDesignTimeServices()
            .AddSingleton<IOperationReporter>(new OperationReporter(_logger));

        // Add required services for scaffolding
        serviceCollection.AddEntityFrameworkDesignTimeServices();
        new SqlServerDesignTimeServices().ConfigureDesignTimeServices(serviceCollection);

        // Build the service provider
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Get the required service
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

        // Find the position to insert the new project
        int insertIndex = lines.FindIndex(l => l.StartsWith("Global"));
        if (insertIndex == -1)
        {
            insertIndex = lines.Count;
        }

        // Insert the new project
        lines.Insert(insertIndex, $"Project(\"{{9A19103F-16F7-4668-BE54-9A1E7A4F7556}}\") = \"{projectName}\", \"{relativeProjectPath.Replace('\\', '/')}\", \"{{{projectGuid}}}\"");
        lines.Insert(insertIndex + 1, "EndProject");

        // Find or create the GlobalSection(ProjectConfigurationPlatforms) section
        int configIndex = lines.FindIndex(l => l.Trim().StartsWith("GlobalSection(ProjectConfigurationPlatforms) = postSolution"));
        if (configIndex == -1)
        {
            // If the section doesn't exist, create it
            configIndex = lines.FindIndex(l => l.Trim().StartsWith("GlobalSection(SolutionProperties)"));
            if (configIndex == -1)
            {
                configIndex = lines.Count - 1; // Insert before the last "EndGlobal"
            }
            lines.Insert(configIndex, "\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");
            lines.Insert(configIndex + 1, "\tEndGlobalSection");
        }

        // Add the new project configurations
        lines.Insert(configIndex + 1, $"\t\t{{{projectGuid}}}.Debug|Any CPU.ActiveCfg = Debug|Any CPU");
        lines.Insert(configIndex + 2, $"\t\t{{{projectGuid}}}.Debug|Any CPU.Build.0 = Debug|Any CPU");
        lines.Insert(configIndex + 3, $"\t\t{{{projectGuid}}}.Release|Any CPU.ActiveCfg = Release|Any CPU");
        lines.Insert(configIndex + 4, $"\t\t{{{projectGuid}}}.Release|Any CPU.Build.0 = Release|Any CPU");

        // Write the updated content back to the file
        await File.WriteAllLinesAsync(solutionPath, lines);

        Log($"Added project to solution: {projectName}");
    }    

    public class DatabaseMask
    {
        public string Pattern { get; set; }
        public Regex Regex { get; set; }

        public DatabaseMask(string pattern)
        {
            Pattern = pattern;
            Regex = new Regex(WildcardToRegex(pattern), RegexOptions.IgnoreCase);
        }

        private static string WildcardToRegex(string pattern)
        {
            return "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        }
    }
        
}