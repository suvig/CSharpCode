using System.Diagnostics;
using System.Text;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("WalmartHW-XMLMerge code shell");
        Console.WriteLine($"Working directory: {Environment.CurrentDirectory}");
        Console.WriteLine();

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var outputXml = await UserCode.Run(args);

            var outputPath = Path.Combine(Environment.CurrentDirectory, "Output.xml");
            await File.WriteAllTextAsync(outputPath, outputXml, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Console.WriteLine($"Wrote: {outputPath}");
            Console.WriteLine();
            Console.WriteLine($"Done in {stopwatch.Elapsed}.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
