using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace EzDbEf
{
    public class ApiProjectGenerator
    {
        private readonly string _outputPath;
        private readonly string _assemblyPrefix;
        private readonly ILogger _logger;
        private readonly string _solutionName;

        public ApiProjectGenerator(string outputPath, string assemblyPrefix, ILogger logger, string solutionName)
        {
            _outputPath = outputPath;
            _assemblyPrefix = assemblyPrefix;
            _logger = logger;
            _solutionName = solutionName;
        }

        public async Task CreateApiProjectAsync(string apiProjectPath)
        {
            _logger.LogInformation("Creating API project");
            
            var projectName = $"{_assemblyPrefix}.API";
            var projectFile = Path.Combine(apiProjectPath, $"{projectName}.csproj");

            // Clear out existing files
            if (Directory.Exists(apiProjectPath))
            {
                _logger.LogInformation("Clearing existing API directory");
                ClearDirectory(apiProjectPath);
            }
            else
            {
                Directory.CreateDirectory(apiProjectPath);
            }

            await RunDotNetCommandAsync("new", "webapi", "-n", projectName, "-o", apiProjectPath);
            _logger.LogInformation($"API project created: {projectFile}");
        }

        private void ClearDirectory(string directoryPath)
        {
            foreach (var file in Directory.GetFiles(directoryPath))
            {
                File.Delete(file);
            }
            foreach (var dir in Directory.GetDirectories(directoryPath))
            {
                Directory.Delete(dir, true);
            }
        }

        public async Task AddApiProjectToSolutionAsync(string apiProjectPath)
        {
            _logger.LogInformation("Adding API project to solution");
            
            var solutionPath = Path.Combine(_outputPath, "src", $"{_solutionName}");
            var projectName = $"{_assemblyPrefix}.API";
            var projectFile = Path.Combine(apiProjectPath, $"{projectName}.csproj");

            await RunDotNetCommandAsync("sln", solutionPath, "add", projectFile);
            _logger.LogInformation($"API project added to solution: {solutionPath}");
        }

        public async Task AddProjectReferencesAsync(string apiProjectPath)
        {
            var apiProjectFile = Path.Combine(apiProjectPath, $"{_assemblyPrefix}.API.csproj");
            _logger.LogInformation($"Searching for DAL project files to add to {apiProjectFile}");

            var dalProjectsPath = Path.Combine(_outputPath, "src", "DAL");
            _logger.LogInformation($"DAL projects path: {dalProjectsPath}");

            if (!Directory.Exists(dalProjectsPath))
            {
                _logger.LogWarning($"DAL projects directory not found: {dalProjectsPath}");
                return;
            }

            var dalProjects = Directory.GetFiles(dalProjectsPath, "*.csproj", SearchOption.AllDirectories);

            _logger.LogInformation($"Found {dalProjects.Length} DAL project files:");
            foreach (var dalProject in dalProjects)
            {
                await RunDotNetCommandAsync("add", apiProjectFile, "reference", dalProject);
                _logger.LogInformation($"Added reference to {Path.GetFileName(dalProject)}");
            }
        }

        private async Task RunDotNetCommandAsync(params string[] args)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = string.Join(" ", args),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                throw new Exception($"dotnet command failed: {error}");
            }
        }
    }
}