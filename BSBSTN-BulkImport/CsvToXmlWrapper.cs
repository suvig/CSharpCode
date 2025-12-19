using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace BSBSTN_BulkImport
{
    /// <summary>
    /// CSV to XML wrapper for testing purposes
    /// This simulates what CLM will provide as flat XML
    /// DO NOT copy this to CLM - for local testing only
    /// </summary>
    public class CsvToXmlWrapper
    {
        /// <summary>
        /// Read CSV file and convert each row to flat XML format
        /// </summary>
        public static List<string> ConvertCsvToXmlRows(string csvFilePath)
        {
            var xmlRows = new List<string>();
            
            if (!File.Exists(csvFilePath))
            {
                Console.WriteLine($"CSV file not found: {csvFilePath}");
                return xmlRows;
            }
            
            var lines = File.ReadAllLines(csvFilePath);
            if (lines.Length < 2)
            {
                Console.WriteLine("CSV file must have at least a header row and one data row");
                return xmlRows;
            }
            
            // Parse header
            var headers = ParseCsvLine(lines[0]);
            
            // Parse data rows
            for (int i = 1; i < lines.Length; i++)
            {
                var values = ParseCsvLine(lines[i]);
                
                // Skip empty rows
                if (values.All(string.IsNullOrWhiteSpace))
                    continue;
                
                // Build XML row
                var xmlRow = BuildXmlRow(headers, values);
                xmlRows.Add(xmlRow);
            }
            
            return xmlRows;
        }
        
        /// <summary>
        /// Parse a CSV line, handling quoted values
        /// </summary>
        private static List<string> ParseCsvLine(string line)
        {
            var values = new List<string>();
            var currentValue = new StringBuilder();
            bool inQuotes = false;
            
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    values.Add(currentValue.ToString());
                    currentValue.Clear();
                }
                else
                {
                    currentValue.Append(c);
                }
            }
            
            // Add the last value
            values.Add(currentValue.ToString());
            
            return values;
        }
        
        /// <summary>
        /// Build a flat XML row from headers and values
        /// </summary>
        private static string BuildXmlRow(List<string> headers, List<string> values)
        {
            var row = new XElement("Row");
            
            for (int i = 0; i < headers.Count && i < values.Count; i++)
            {
                string header = headers[i].Trim();
                string value = values[i].Trim();
                
                // Skip empty headers
                if (string.IsNullOrWhiteSpace(header))
                    continue;
                
                // Create element
                var element = new XElement(SanitizeElementName(header), value);
                row.Add(element);
            }
            
            return row.ToString();
        }
        
        /// <summary>
        /// Sanitize header names to be valid XML element names
        /// </summary>
        private static string SanitizeElementName(string name)
        {
            // Remove invalid characters and replace spaces with underscores
            var sanitized = new StringBuilder();
            
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                {
                    sanitized.Append(c);
                }
                else if (c == ' ')
                {
                    sanitized.Append('_');
                }
            }
            
            return sanitized.ToString();
        }
    }
}
