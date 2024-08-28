using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;

namespace EzDbEf;

public class ApiGenerator
{
    private readonly string _outputPath;
    private readonly string _assemblyPrefix;
    private readonly ILogger _logger;
    private readonly ApiStartupGenerator _startupGenerator;
    private readonly ApiProjectGenerator _apiProjectGenerator;
    private readonly string _connectionString;
    private readonly string _solutionName;

    public ApiGenerator(string outputPath, string assemblyPrefix, ILogger logger, string connectionString, string solutionName)
    {
        _outputPath = outputPath;
        _assemblyPrefix = assemblyPrefix;
        _logger = logger;
        _startupGenerator = new ApiStartupGenerator(outputPath, assemblyPrefix, logger);
        _apiProjectGenerator = new ApiProjectGenerator(outputPath, assemblyPrefix, logger, solutionName);
        _connectionString = connectionString;
        _solutionName = solutionName;
    }

    public async Task GenerateAsync()
    {
        _logger.LogInformation("Starting API generation");

        var apiProjectPath = Path.Combine(_outputPath, "src", "API");
        Directory.CreateDirectory(apiProjectPath);

        await _apiProjectGenerator.CreateApiProjectAsync(apiProjectPath);
        await _apiProjectGenerator.AddApiProjectToSolutionAsync(apiProjectPath);
        await _apiProjectGenerator.AddProjectReferencesAsync(apiProjectPath);
        await GenerateControllersAsync(apiProjectPath);
        //await _startupGenerator.GenerateStartupFilesAsync(apiProjectPath);
        await ConfigureAppSettingsAsync(apiProjectPath);

        _logger.LogInformation("API generation completed successfully");
    }

    private async Task GenerateControllersAsync(string apiProjectPath)
    {
        _logger.LogInformation("Generating API controllers");
        // TODO: Implement controller generation
        await Task.CompletedTask;
    }

    private async Task ConfigureAppSettingsAsync(string apiProjectPath)
    {
        _logger.LogInformation("Configuring appsettings.json");
        
        var appSettingsPath = Path.Combine(apiProjectPath, "appsettings.json");
        var appSettings = new
        {
            Logging = new
            {
                LogLevel = new
                {
                    Default = "Information",
                    Microsoft = "Warning",
                    Microsoft_AspNetCore = "Warning"
                }
            },
            AllowedHosts = "*",
            ConnectionStrings = new
            {
                DefaultConnection = _connectionString
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(appSettings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(appSettingsPath, json);
        
        _logger.LogInformation($"appsettings.json file created: {appSettingsPath}");
    }
}