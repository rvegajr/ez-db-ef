using System.Text.RegularExpressions;

namespace EzDbEf;

public class DatabaseMaskParser
{
    public record DatabaseObject(string Database, string Schema, string Table, bool IsExcluded)
    {
        public override string ToString() => $"{Database}.{Schema}.{Table}";

        public Regex Regex { get; } = new Regex(WildcardToRegex(Database), RegexOptions.IgnoreCase);

        private static string WildcardToRegex(string pattern)
        {
            return "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        }
    }

    public static List<DatabaseObject> ParseMasks(string[] masks)
    {
        return masks.Select(mask =>
        {
            var isExcluded = mask.StartsWith('-');
            var cleanMask = isExcluded ? mask[1..] : mask;
            var parts = cleanMask.Split('.');

            return parts.Length switch
            {
                1 => new DatabaseObject(parts[0], "*", "*", isExcluded),
                2 => new DatabaseObject(parts[0], parts[1], "*", isExcluded),
                3 => new DatabaseObject(parts[0], parts[1], parts[2], isExcluded),
                _ => throw new ArgumentException($"Invalid mask format: {mask}")
            };
        }).ToList();
    }

    public static bool IsMatch(DatabaseObject dbObject, string database, string schema, string table)
    {
        return IsWildcardMatch(dbObject.Database, database) &&
               IsWildcardMatch(dbObject.Schema, schema) &&
               IsWildcardMatch(dbObject.Table, table);
    }

    private static bool IsWildcardMatch(string pattern, string input)
    {
        return Regex.IsMatch(input, $"^{Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".")}$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
    }
}