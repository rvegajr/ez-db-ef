using Newtonsoft.Json;

namespace EzDbEf.Utilities;

public class CodeGeneration
{
    [JsonProperty("enable-on-configuring")]
    public bool EnableOnConfiguring { get; set; }

    [JsonProperty("generate-mermaid-diagram")]
    public bool GenerateMermaidDiagram { get; set; }

    [JsonProperty("merge-dacpacs")]
    public bool MergeDacpacs { get; set; } = false;

    [JsonProperty("refresh-object-lists")]
    public bool RefreshObjectLists { get; set; }

    [JsonProperty("remove-defaultsql-from-bool-properties")]
    public bool RemoveDefaultSqlFromBoolProperties { get; set; }

    [JsonProperty("soft-delete-obsolete-files")]
    public bool SoftDeleteObsoleteFiles { get; set; }

    [JsonProperty("t4-template-path")]
    public object T4TemplatePath { get; set; }

    public string Type { get; set; } = "all";

    [JsonProperty("use-alternate-stored-procedure-resultset-discovery")]
    public bool UseAlternateStoredProcedureResultSetDiscovery { get; set; }

    [JsonProperty("use-data-annotations")]
    public bool UseDataAnnotations { get; set; }

    [JsonProperty("use-database-names")]
    public bool UseDatabaseNames { get; set; }

    [JsonProperty("use-decimal-data-annotation-for-sproc-results")]
    public bool UseDecimalDataAnnotationForSprocResults { get; set; }

    [JsonProperty("use-inflector")]
    public bool UseInflector { get; set; }

    [JsonProperty("use-legacy-inflector")]
    public bool UseLegacyInflector { get; set; }

    [JsonProperty("use-many-to-many-entity")]
    public bool UseManyToManyEntity { get; set; }

    [JsonProperty("use-nullable-reference-types")]
    public bool UseNullableReferenceTypes { get; set; }

    [JsonProperty("use-prefix-navigation-naming")]
    public bool UsePrefixNavigationNaming { get; set; }

    [JsonProperty("use-t4")]
    public bool UseT4 { get; set; }
}

public class FileLayout
{
    [JsonProperty("output-dbcontext-path")]
    public object OutputDbContextPath { get; set; }

    [JsonProperty("output-path")]
    public string OutputPath { get; set; }
}

public class Function
{
    public string Name { get; set; }
}

public class Names
{
    [JsonProperty("dbcontext-name")]
    public object DbContextName { get; set; }

    [JsonProperty("dbcontext-namespace")]
    public object DbContextNamespace { get; set; }

    [JsonProperty("model-namespace")]
    public object ModelNamespace { get; set; }

    [JsonProperty("root-namespace")]
    public object RootNamespace { get; set; }
}

public class EfcptConfig
{
    [JsonProperty("$schema")]
    public string Schema { get; set; }

    [JsonProperty("code-generation")]
    public CodeGeneration CodeGeneration { get; set; }

    [JsonProperty("file-layout")]
    public FileLayout FileLayout { get; set; }
    public List<Function> Functions { get; set; }
    public Names Names { get; set; }
    public List<Table> Tables { get; set; }
    public List<View> Views { get; set; }
}

public class Table
{
    public string Name { get; set; }
}

public class View
{
    public string Name { get; set; }
}

public class EFPTRenaming : List<EFPTRenamingItem> {}

public class EFPTRenamingColumn
{
    public string Name { get; set; }
    public string NewName { get; set; }
}

public class EFPTRenamingItem
{
    public string SchemaName { get; set; }
    public List<EFPTRenamingTable> Tables { get; set; }
    public bool UseSchemaName { get; set; }
}

public class EFPTRenamingTable
{
    public List<EFPTRenamingColumn> Columns { get; set; }
    public string Name { get; set; }
    public string NewName { get; set; }
}

public static class EfcptConfigInstance
{
    public static EfcptConfig Create(string contextName, string codeNameSpace) => new EfcptConfig
    {
        Schema = "https://raw.githubusercontent.com/ErikEJ/EFCorePowerTools/master/samples/efcpt-schema.json",
        CodeGeneration = new CodeGeneration
        {
            EnableOnConfiguring = false,
            GenerateMermaidDiagram = false,
            MergeDacpacs = false,
            RefreshObjectLists = true,
            RemoveDefaultSqlFromBoolProperties = false,
            SoftDeleteObsoleteFiles = true,
            T4TemplatePath = null,
            Type = "all",
            UseAlternateStoredProcedureResultSetDiscovery = true,
            UseDataAnnotations = true,
            UseDatabaseNames = true,
            UseDecimalDataAnnotationForSprocResults = true,
            UseInflector = true,
            UseLegacyInflector = false,
            UseManyToManyEntity = false,
            UseNullableReferenceTypes = true,
            UsePrefixNavigationNaming = true,
            UseT4 = false
        },
        FileLayout = new FileLayout
        {
            OutputDbContextPath = "Models",
            OutputPath = "Models"
        },
        //Functions = new List<Function>
        //{
        //    new Function { Name = "[dbo].[udfBuildISO8601Date]" },
        //    new Function { Name = "[dbo].[udfMinimumDate]" },
        //    new Function { Name = "[dbo].[udfTwoDigitZeroFill]" }
        //},
        Names = new Names
        {
            DbContextName = contextName,
            DbContextNamespace = codeNameSpace,
            ModelNamespace = codeNameSpace,
            RootNamespace = null
        }
        //Tables = new List<Table>
        //{
        //    new Table { Name = "[dbo].[AdventureWorksDWBuildVersion]" },
        //    // ... add other tables ...
        //},
        //Views = new List<View>
        //{
        //    new View { Name = "[dbo].[vAssocSeqLineItems]" },
        //    // ... add other views ...
        //}
    };
}

