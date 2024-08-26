
using System.Reflection;

class Program
{
    private static string LogFilePath => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, $"ezdbef_log_{DateTime.Now:yyyy-MM-dd}.txt");

    static async Task<int> Main(string[] args)
    {
        var serviceProvider = new ServiceCollection()
             .AddLogging(builder =>
             {
                 builder.AddConfiguration(new ConfigurationBuilder().Build())
                        .AddConsole()
                        .AddDebug() // This is the correct way to add Debug logger
                        .AddProvider(new FileLoggerProvider(LogFilePath))
                        .SetMinimumLevel(LogLevel.Debug);
             })
             .BuildServiceProvider();

        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("EzDbEf starting...");

        try
        {
            var rootCommand = CommandLineSetup.CreateRootCommand(serviceProvider);
            int result = await rootCommand.InvokeAsync(args);

            logger.LogInformation("EzDbEf completed with result code: {ResultCode}", result);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while executing EzDbEf");
            return 1;
        }
        finally
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                Console.WriteLine("Press any key to exit...");
                Console.Read();
            }
        }
    }
}
