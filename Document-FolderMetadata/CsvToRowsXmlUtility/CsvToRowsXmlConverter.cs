using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace DocumentFolderMetadata.CsvToRowsXmlUtility;

internal static class CsvToRowsXmlConverter
{
    public static XDocument ConvertFile(string csvFilePath)
    {
        if (!File.Exists(csvFilePath))
        {
            throw new FileNotFoundException("CSV file not found.", csvFilePath);
        }

        string[] lines = File.ReadAllLines(csvFilePath);
        if (lines.Length == 0)
        {
            throw new InvalidOperationException("CSV file is empty.");
        }

        List<string> headers = ParseCsvLine(lines[0]);
        XElement root = new("Rows");

        for (int index = 1; index < lines.Length; index++)
        {
            List<string> values = ParseCsvLine(lines[index]);
            if (values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            root.Add(BuildRow(headers, values));
        }

        return new XDocument(root);
    }

    private static XElement BuildRow(IReadOnlyList<string> headers, IReadOnlyList<string> values)
    {
        XElement row = new("Row");

        for (int index = 0; index < headers.Count; index++)
        {
            string header = headers[index].Trim();
            if (string.IsNullOrWhiteSpace(header))
            {
                continue;
            }

            string value = index < values.Count ? values[index].Trim() : string.Empty;
            row.Add(new XElement(SanitizeElementName(header), value));
        }

        return row;
    }

    private static List<string> ParseCsvLine(string line)
    {
        List<string> values = new();
        StringBuilder currentValue = new();
        bool inQuotes = false;

        for (int index = 0; index < line.Length; index++)
        {
            char currentChar = line[index];

            if (currentChar == '"')
            {
                bool isEscapedQuote = inQuotes
                    && index + 1 < line.Length
                    && line[index + 1] == '"';

                if (isEscapedQuote)
                {
                    currentValue.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (currentChar == ',' && !inQuotes)
            {
                values.Add(currentValue.ToString());
                currentValue.Clear();
                continue;
            }

            currentValue.Append(currentChar);
        }

        values.Add(currentValue.ToString());
        return values;
    }

    private static string SanitizeElementName(string header)
    {
        StringBuilder sanitized = new();

        foreach (char currentChar in header)
        {
            if (char.IsLetterOrDigit(currentChar) || currentChar == '_' || currentChar == '-')
            {
                sanitized.Append(currentChar);
            }
            else if (currentChar == ' ')
            {
                sanitized.Append('_');
            }
        }

        string candidate = sanitized.Length == 0 ? "Column" : sanitized.ToString();

        if (!XmlConvert.IsStartNCNameChar(candidate[0]))
        {
            candidate = $"_{candidate}";
        }

        return candidate;
    }
}