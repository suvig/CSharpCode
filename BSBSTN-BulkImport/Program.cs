using System;
using System.IO;

namespace BSBSTN_BulkImport
{
    /// <summary>
    /// Main program for testing the XML transformer
    /// This is for local testing only - DO NOT copy to CLM
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== XML Transformer - Testing Tool ===\n");
            
            // Get the project directory
            string projectDir = Directory.GetCurrentDirectory();
            string mappingConfigPath = Path.Combine(projectDir, "MappingConfig.xml");
            string csvPath = Path.Combine(projectDir, "input", "OCLMLoadsheet.csv");
            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                string providedPath = args[0].Trim();
                csvPath = Path.IsPathRooted(providedPath)
                    ? providedPath
                    : Path.GetFullPath(Path.Combine(projectDir, providedPath));
            }
            
            // Check if mapping config exists
            if (!File.Exists(mappingConfigPath))
            {
                Console.WriteLine($"ERROR: MappingConfig.xml not found at: {mappingConfigPath}");
                return;
            }
            
            // Load mapping configuration
            string mappingXml = File.ReadAllText(mappingConfigPath);
            Console.WriteLine($"✓ Loaded mapping configuration from: {mappingConfigPath}\n");
            
            // Check if CSV file exists
            if (!File.Exists(csvPath))
            {
                Console.WriteLine($"ERROR: CSV file not found at: {csvPath}");
                return;
            }
            
            // Process CSV file
            ProcessCsvFile(csvPath, mappingXml);
            
            Console.WriteLine("\n=== Processing Complete ===");
        }
        
        /// <summary>
        /// Process CSV file and generate output for each row
        /// </summary>
        static void ProcessCsvFile(string csvPath, string mappingXml)
        {
            Console.WriteLine($"\n--- Processing CSV File ---");
            Console.WriteLine($"CSV Path: {csvPath}\n");
            
            // Convert CSV rows to XML
            var xmlRows = CsvToXmlWrapper.ConvertCsvToXmlRows(csvPath);
            
            if (xmlRows.Count == 0)
            {
                Console.WriteLine("No data rows found in CSV file.");
                return;
            }
            
            Console.WriteLine($"Found {xmlRows.Count} data row(s) in CSV\n");
            
            // Process each row
            for (int i = 0; i < xmlRows.Count; i++)
            {
                Console.WriteLine($"Processing row {i + 1} of {xmlRows.Count}...");
                
                string inputXml = xmlRows[i];
                string outputXml = BulkImportCore.TransformXmlRow(inputXml, mappingXml);
                
                // Save to file
                string outputPath = $"Output_Row_{i + 1}.xml";
                File.WriteAllText(outputPath, outputXml);
                Console.WriteLine($"✓ Saved to: {Path.GetFullPath(outputPath)}");
            }
            
            Console.WriteLine($"\n✓ Processed all {xmlRows.Count} row(s)");
        }
    }
}
