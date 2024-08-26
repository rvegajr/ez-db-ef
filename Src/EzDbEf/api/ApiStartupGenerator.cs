using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace EzDbEf
{
    public class ApiStartupGenerator
    {
        private readonly string _outputPath;
        private readonly string _assemblyPrefix;
        private readonly ILogger _logger;

        public ApiStartupGenerator(string outputPath, string assemblyPrefix, ILogger logger)
        {
            _outputPath = outputPath;
            _assemblyPrefix = assemblyPrefix;
            _logger = logger;
        }

        public async Task GenerateStartupFilesAsync(string apiProjectPath)
        {
            await ConfigureStartupAsync(apiProjectPath);
            await CreateODataConfigurationAsync(apiProjectPath);
        }

        private async Task ConfigureStartupAsync(string apiProjectPath)
        {
            _logger.LogInformation("Configuring Startup class");

            var startupFilePath = Path.Combine(apiProjectPath, "Startup.cs");
            var apiProjectName = $"{_assemblyPrefix}.API";

            var startupContent = $@"
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.OData;
using {apiProjectName}.Configuration;

namespace {apiProjectName}
{{
    public class Startup
    {{
        public IConfiguration Configuration {{ get; }}

        public Startup(IConfiguration configuration)
        {{
            Configuration = configuration;
        }}

        public void ConfigureServices(IServiceCollection services)
        {{
            services.AddControllers().AddOData(opt => opt.Count().Filter().Expand().Select().OrderBy().SetMaxTop(100));

            services.AddSwaggerGen(c =>
            {{
                c.SwaggerDoc(""v1"", new OpenApiInfo {{ Title = ""{_assemblyPrefix} API"", Version = ""v1"" }});
            }});

            ODataConfiguration.ConfigureOData(services);
        }}

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {{
            if (env.IsDevelopment())
            {{
                app.UseDeveloperExceptionPage();
            }}

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {{
                c.SwaggerEndpoint(""/swagger/v1/swagger.json"", ""{_assemblyPrefix} API V1"");
            }});

            app.UseRouting();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {{
                endpoints.MapControllers();
            }});
        }}
    }}
}}";

            await File.WriteAllTextAsync(startupFilePath, startupContent);
            _logger.LogInformation($"Startup.cs file created: {startupFilePath}");
        }

        private async Task CreateODataConfigurationAsync(string apiProjectPath)
        {
            _logger.LogInformation("Creating OData configuration");

            var configFilePath = Path.Combine(apiProjectPath, "Configuration", "ODataConfiguration.cs");
            Directory.CreateDirectory(Path.GetDirectoryName(configFilePath));

            var apiProjectName = $"{_assemblyPrefix}.API";
            var dalProjectsPath = Path.Combine(_outputPath, "src", "DAL");
            var databases = Directory.GetDirectories(dalProjectsPath).Select(Path.GetFileName).ToList();

            var configContent = $@"
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace {apiProjectName}.Configuration
{{
    public static class ODataConfiguration
    {{
        public static void ConfigureOData(IServiceCollection services)
        {{
            {string.Join("\n            ", databases.Select(db => $"services.AddOData(opt => opt.AddRouteComponents(\"{db}\", Get{db}EdmModel()));"))}
        }}

        {string.Join("\n\n        ", databases.Select(db => $@"private static IEdmModel Get{db}EdmModel()
        {{
            var builder = new ODataConventionModelBuilder();
            // TODO: Add entity sets for {db}
            // Example: builder.EntitySet<YourEntity>(""{db}Entities"");
            return builder.GetEdmModel();
        }}"))}
    }}
}}";

            await File.WriteAllTextAsync(configFilePath, configContent);
            _logger.LogInformation($"ODataConfiguration.cs file created: {configFilePath}");
        }
    }
}