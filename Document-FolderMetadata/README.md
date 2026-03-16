Document-FolderMetadata local structure

- Document-FolderMetadata.cs: manager-provided workflow script for local testing and CLM handoff.
- Document-FolderMetadata.Runner.csproj: local runner project that compiles only Document-FolderMetadata.cs.
- CsvToRowsXmlUtility.csproj: helper utility that converts the CSV manifest into the Row-based XML shape expected by Document-FolderMetadata.cs.
- SampleData/: local test inputs and generated outputs.

Required local files in SampleData

- SampleManifestWalmart.csv: source manifest you maintain.
- SampleManifestWalmart.xml: generated from the CSV with CsvToRowsXmlUtility.
- xWFData.xml: shared local source XML file used for both XML-path rows and simple-variable rows.

Run order

1. Generate the manifest XML:
   dotnet run --project CsvToRowsXmlUtility.csproj -- SampleData/SampleManifestWalmart.csv SampleData/SampleManifestWalmart.xml
2. Add or update SampleData/xWFData.xml.
3. Run the workflow script locally:
   dotnet run --project Document-FolderMetadata.Runner.csproj

Notes

- Run commands from the Document-FolderMetadata folder so the relative SampleData paths resolve correctly.
- The CSV-to-XML utility now emits Rows/Row to match the script's Row-based manifest query.