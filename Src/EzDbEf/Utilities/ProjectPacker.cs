using System;
using System.IO;
using System.Text;

namespace EzDbEf.Utilities;

public class ProjectPacker
{
    private const string StartMarker = "<<START_FILE>>";
    private const string EndMarker = "<<END_FILE>>";

    public void PackProject(string projectDirectory, string outputFile)
    {
        using (var writer = new StreamWriter(outputFile, false, Encoding.UTF8))
        {
            foreach (var file in Directory.EnumerateFiles(projectDirectory, "*.*", SearchOption.AllDirectories))
            {
                if (ShouldIncludeFile(file))
                {
                    string relativePath = GetRelativePath(file, projectDirectory);
                    Console.WriteLine($"Processing file: {file}");

                    writer.WriteLine($"{StartMarker} {relativePath}");
                    writer.WriteLine(File.ReadAllText(file));
                    writer.WriteLine(EndMarker);
                }
            }
        }
        Console.WriteLine($"All files have been consolidated into: {outputFile}");
    }

    public void UnpackProject(string packedFile, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        using (var reader = new StreamReader(packedFile, Encoding.UTF8))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith(StartMarker))
                {
                    string relativePath = line.Substring(StartMarker.Length).Trim();
                    string fullPath = Path.Combine(outputDirectory, relativePath);
                    
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                    
                    using (var writer = new StreamWriter(fullPath, false, Encoding.UTF8))
                    {
                        while ((line = reader.ReadLine()) != null && line != EndMarker)
                        {
                            writer.WriteLine(line);
                        }
                    }
                    Console.WriteLine($"Unpacked file: {fullPath}");
                }
            }
        }
        Console.WriteLine($"All files have been unpacked to: {outputDirectory}");
    }

    private bool ShouldIncludeFile(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLower();
        return extension == ".csproj" || extension == ".cs" || extension == ".config" || extension == ".json";
    }

    private string GetRelativePath(string fullPath, string basePath)
    {
        return Path.GetRelativePath(basePath, fullPath).Replace('\\', '/');
    }
}