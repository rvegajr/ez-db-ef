using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

public static class JsonHelper
{
    public static string SerializeToKebabCase(object obj)
    {
        var settings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new KebabCaseNamingStrategy()
            }
        };

        return JsonConvert.SerializeObject(obj, settings);
    }
}

public class KebabCaseNamingStrategy : NamingStrategy
{
    protected override string ResolvePropertyName(string name)
    {
        return string.Concat(name.Select((x, i) => i > 0 && char.IsUpper(x) ? "-" + x.ToString() : x.ToString())).ToLower();
    }
}