using System.Xml.Linq;

namespace DocumentFolderMetadata.CsvToRowsXmlUtility;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            string projectRoot = ResolveProjectRoot();
            string defaultInputPath = Path.Combine(projectRoot, "SampleData", "Update Metadata Mapping.csv");

            string inputPath = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
                ? ResolvePath(args[0], projectRoot)
                : defaultInputPath;

            string outputPath = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])
                ? ResolvePath(args[1], projectRoot)
                : Path.ChangeExtension(inputPath, ".xml");

            if (!File.Exists(inputPath))
            {
                Console.Error.WriteLine($"Input CSV not found: {inputPath}");
                PrintUsage(projectRoot);
                return 1;
            }

            XDocument xml = CsvToRowsXmlConverter.ConvertFile(inputPath);

            string? outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            xml.Save(outputPath);

            Console.WriteLine($"Input:  {inputPath}");
            Console.WriteLine($"Output: {outputPath}");
            Console.WriteLine($"Rows:   {xml.Root?.Elements("Row").Count() ?? 0}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Conversion failed: {ex.Message}");
            return 1;
        }
    }

    private static void PrintUsage(string projectRoot)
    {
        string sampleInputPath = Path.Combine(projectRoot, "SampleData", "Update Metadata Mapping.csv");
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project Document-FolderMetadata/CsvToRowsXmlUtility.csproj [inputCsvPath] [outputXmlPath]");
        Console.WriteLine();
        Console.WriteLine($"Default input:  {sampleInputPath}");
        Console.WriteLine($"Default output: {Path.ChangeExtension(sampleInputPath, ".xml")}");
    }

    private static string ResolveProjectRoot()
    {
        string? current = AppContext.BaseDirectory;

        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "CsvToRowsXmlUtility.csproj")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        return Directory.GetCurrentDirectory();
    }

    private static string ResolvePath(string path, string projectRoot)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(projectRoot, path));
    }
}