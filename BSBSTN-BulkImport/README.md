# XML Transformer for DocuSign CLM

This C# project transforms flat XML input from DocuSign CLM workflows into hierarchical MasterXml output format.

## Project Structure

```
XmlTransformer/
├── XmlTransformer.csproj          # Project file
├── XmlTransformerCore.cs          # ⭐ CLM-PORTABLE CORE LOGIC
├── CsvToXmlWrapper.cs             # CSV wrapper for testing only
├── Program.cs                     # Test harness (not for CLM)
├── MappingConfig.xml              # Mapping configuration
└── README.md                      # This file
```

## Key Components

### 1. **XmlTransformerCore.cs** (CLM-Portable)
- **This is the only file you copy to CLM**
- Contains the `TransformXmlRow()` method that CLM will call
- Self-contained with no external dependencies
- Uses only standard .NET XML libraries (System.Xml.Linq)

### 2. **MappingConfig.xml**
- Defines how flat input XML maps to hierarchical output
- Supports:
  - Simple fields (1:1 mapping)
  - Repeating groups (pipe-separated values)
  - Calculated fields (transformations)
  - Attributes on elements

### 3. **CsvToXmlWrapper.cs** (Testing Only)
- Converts CSV rows to flat XML format
- Simulates what DocuSign CLM will provide
- **Not used in production CLM**

### 4. **Program.cs** (Testing Only)
- Test harness for local development
- Can test with hardcoded sample or CSV file
- **Not used in production CLM**

## How It Works

### Input (from CLM):
```xml
<Row>
  <WKFID>OCLM-27292</WKFID>
  <ContractEntityName>TEST test</ContractEntityName>
  <Network>Network P|Network Q</Network>
  <Program>Specialty|General</Program>
  <!-- other fields -->
</Row>
```

### Output (MasterXml):
```xml
<MasterXml>
  <CLM_Agreement_ID>CLM-27292</CLM_Agreement_ID>
  <Params>
    <Params>
      <ContractEntityName>TEST test</ContractEntityName>
      <ContractingNetworks>
        <Networks>
          <Network>Network P</Network>
          <Program>Specialty</Program>
        </Networks>
        <Networks>
          <Network>Network Q</Network>
          <Program>General</Program>
        </Networks>
      </ContractingNetworks>
    </Params>
  </Params>
</MasterXml>
```

## Pipe-Separated Repeating Sections

When input fields have pipe-separated values (e.g., `"Network P|Network Q"`), the transformer:
1. Splits all related fields by pipe character
2. Combines values at the same index into repeating elements
3. Uses empty string for missing values if pipe counts don't match

**Example:**
```
Input:
  Network: "Network P|Network Q"
  Program: "Specialty|General"
  EffectiveStartDate: "2031-01-01|2032-01-01"

Output:
  <Networks>
    <Network>Network P</Network>
    <Program>Specialty</Program>
    <EffectiveStartDate>2031-01-01</EffectiveStartDate>
  </Networks>
  <Networks>
    <Network>Network Q</Network>
    <Program>General</Program>
    <EffectiveStartDate>2032-01-01</EffectiveStartDate>
  </Networks>
```

## Testing Locally

### Prerequisites
- .NET 9.0 SDK installed
- VS Code (recommended)

### Build the Project
```bash
cd XmlTransformer
dotnet build
```

### Run with Sample Data
```bash
dotnet run
# Choose option 1 for hardcoded sample
```

### Run with CSV File
```bash
dotnet run
# Choose option 2 and it will read from ../OCLMLoadsheet.csv
```

### Output
- Sample mode: Creates `Output_Sample.xml`
- CSV mode: Creates `Output_Row_1.xml`, `Output_Row_2.xml`, etc.

## Using in DocuSign CLM

### Step 1: Copy Core Logic to CLM
1. Open [XmlTransformerCore.cs](XmlTransformerCore.cs)
2. Copy the entire file content
3. Paste into CLM C# Expression editor

### Step 2: Store Mapping Configuration
The `MappingConfig.xml` needs to be available as a string in CLM. Options:
- Store in a CLM variable
- Embed as a string constant in the expression
- Load from CLM document storage

### Step 3: Call the Transformer
```csharp
// CLM will provide these variables:
string inputXmlRow = [YOUR_CLM_ROW_VARIABLE];  // From workflow
string mappingXml = [YOUR_MAPPING_CONFIG];      // From storage/variable

// Call the core method:
string outputXml = XmlTransformerCore.TransformXmlRow(inputXmlRow, mappingXml);

// Use outputXml in CLM workflow
return outputXml;
```

## Mapping Configuration

### Field Types

#### Simple Field
Maps one input element to one output element:
```xml
<Field>
  <InputNode>ContractEntityName</InputNode>
  <OutputPath>Params/Params/ContractEntityName</OutputPath>
  <FieldType>Simple</FieldType>
</Field>
```

#### Repeating Field
Part of a repeating group (handles pipe-separated values):
```xml
<Field>
  <InputNode>Network</InputNode>
  <OutputPath>Params/Params/ContractingNetworks/Networks/Network</OutputPath>
  <FieldType>Repeating</FieldType>
  <RepeatingGroupName>Networks</RepeatingGroupName>
  <RepeatingGroupPath>Params/Params/ContractingNetworks</RepeatingGroupPath>
  <RepeatingElementName>Networks</RepeatingElementName>
  <AttributeName>key</AttributeName> <!-- Optional: creates key="value" attribute -->
</Field>
```

#### Calculated Field
Transforms the value before output:
```xml
<Field>
  <InputNode>WKFID</InputNode>
  <OutputPath>CLM_Agreement_ID</OutputPath>
  <FieldType>Calculated</FieldType>
</Field>
```

### Transform Rules
- `FirstPipeValue` - Takes first value from pipe-separated list
- `AppendTime` - Adds `T00:00:00` to dates

## Extending the Solution

### Adding New Transform Rules
Edit [XmlTransformerCore.cs](XmlTransformerCore.cs), method `ApplyTransformRule()`:
```csharp
case "YourNewRule":
    return value.ToUpper(); // Your transformation
```

### Adding New Fields
Edit [MappingConfig.xml](MappingConfig.xml) and add new `<Field>` elements.

### Supporting New Repeating Groups
Add fields with the same `RepeatingGroupName` in [MappingConfig.xml](MappingConfig.xml).

## Limitations & Considerations

### CLM Constraints
- CLM only allows **one C# expression** per workflow step
- No file I/O in CLM (mappingXml must be a string variable)
- Limited debugging capabilities in CLM

### Current Limitations
1. **Attributes ignored**: `displayName=""`, `displayValue=""`, `optionName=""` not set (as per requirements)
2. **No validation**: Assumes input XML is well-formed
3. **Mismatched pipe counts**: Uses empty strings for missing values
4. **Static elements**: LogXml and Alternate_Language_Requests always created empty

## Troubleshooting

### Build Errors
```bash
dotnet --version  # Check .NET version
dotnet restore    # Restore dependencies
dotnet clean      # Clean build artifacts
dotnet build      # Rebuild
```

### Output Missing Fields
1. Check [MappingConfig.xml](MappingConfig.xml) has mapping for that field
2. Verify input XML contains the field
3. Check FieldType (Simple vs Repeating)

### Repeating Sections Not Working
1. Ensure all related fields have the same `RepeatingGroupName`
2. Verify `RepeatingGroupPath` matches the parent path
3. Check that input values are pipe-separated

### CLM Integration Issues
1. Ensure XmlTransformerCore.cs is copied completely
2. Verify mappingXml string is valid XML
3. Check CLM's C# version compatibility (method uses standard .NET features)

## File Inventory

| File | Purpose | Copy to CLM? |
|------|---------|--------------|
| XmlTransformerCore.cs | Core transformation logic | ✅ YES |
| MappingConfig.xml | Field mapping configuration | ✅ YES (as string) |
| Program.cs | Testing harness | ❌ NO |
| CsvToXmlWrapper.cs | CSV to XML conversion | ❌ NO |
| XmlTransformer.csproj | Project file | ❌ NO |

## Support & Customization

To customize the transformer:
1. Update [MappingConfig.xml](MappingConfig.xml) for field mappings
2. Modify [XmlTransformerCore.cs](XmlTransformerCore.cs) for logic changes
3. Test locally with `dotnet run`
4. Copy updated [XmlTransformerCore.cs](XmlTransformerCore.cs) to CLM

## Example Usage

### Testing with Sample Data
```bash
cd XmlTransformer
dotnet run
# Select option 1
# Check Output_Sample.xml
```

### Testing with CSV
```bash
cd XmlTransformer
dotnet run
# Select option 2
# Check Output_Row_*.xml files
```

### CLM Production Use
```csharp
// In CLM C# Expression:
string inputXml = workflowRowXml;  // From CLM
string config = mappingConfiguration; // From CLM variable

string result = XmlTransformerCore.TransformXmlRow(inputXml, config);
return result; // Use in CLM workflow
```

---

**Version:** 1.0  
**Last Updated:** December 18, 2025  
**Target Framework:** .NET 9.0 (compatible with .NET 6.0+)



Done Dec 31:
1) Update code to have de-dupe logic for the Core Networks.
2) Make sure all the csv is mapped and generate the proper xml structure.
3) Hardcode values
4) RepeatingGroupName & RepeatingElementName

