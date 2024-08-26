namespace EzDbEf;
class ConfigurationProvider
{
    public static IConfigurationRoot LoadConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("ezdbef-settings.json", optional: true, reloadOnChange: true)
            .Build();
    }
}