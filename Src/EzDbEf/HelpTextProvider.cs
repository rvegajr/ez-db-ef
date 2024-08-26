
namespace EzDbEf;

class HelpTextProvider
{
    public static string GetDetailedHelp()
    {
        return @"
EzDbEf - Easy Database Entity Framework Generator

Description:
  EzDbEf is a tool for generating Entity Framework Core classes based on your database schema.
  It allows you to specify which database objects to include or exclude using mask patterns.

Usage:
  ezdbef --connection-string <connection_string> --db-masks <mask1> [<mask2>...] [--output-path <path>]

Options:
  --connection-string    (Required) The connection string to your database.
  --db-masks             (Required) One or more database mask patterns.
  --output-path          (Optional) The output directory for generated files. Defaults to current directory.

Mask Pattern Examples:
  - 'dbname.*.*'              : Include all schemas and tables in the 'dbname' database
  - 'dbname.dbo.*'            : Include all tables in the 'dbo' schema of 'dbname' database
  - 'dbname.dbo.table1'       : Include only 'table1' in the 'dbo' schema of 'dbname' database
  - 'dbname.dbo.customer*'    : Include all tables starting with 'customer' in 'dbo' schema
  - '-dbname.dbo.system*'     : Exclude all tables starting with 'system' in 'dbo' schema

Usage Examples:
  1. Generate classes for all tables in the 'MyDatabase' database:
     ezdbef --connection-string ""Server=myserver;Database=MyDatabase;User Id=myuser;Password=mypassword;"" --db-masks ""MyDatabase.*.*""

  2. Generate classes for specific schemas and exclude system tables:
     ezdbef --connection-string ""Server=myserver;Database=MyDatabase;User Id=myuser;Password=mypassword;"" 
            --db-masks ""MyDatabase.dbo.*"" ""MyDatabase.sales.*"" ""-MyDatabase.dbo.system*""
            --output-path ""./output""

  3. Generate classes for specific tables:
     ezdbef --connection-string ""Server=myserver;Database=MyDatabase;User Id=myuser;Password=mypassword;""
            --db-masks ""MyDatabase.dbo.Customers"" ""MyDatabase.dbo.Orders"" ""MyDatabase.dbo.Products""

Note: Make sure to enclose your connection string in quotes if it contains spaces or special characters.
";
    }
}
