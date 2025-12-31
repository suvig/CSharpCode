# Quick CLM Integration Guide

## What You Need to Copy to CLM

### 1. XmlTransformerCore.cs (Required)
**Copy the entire file content** from [XmlTransformerCore.cs](XmlTransformerCore.cs) into your CLM C# Expression. 

### 2. MappingConfig.xml as String (Required)
Convert the [MappingConfig.xml](MappingConfig.xml) to a string variable in CLM.

**Option A: Store in CLM Variable**
```csharp
string mappingXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<MappingConfiguration>
  <Field>
    <InputNode>WKFID</InputNode>
    <OutputPath>CLM_Agreement_ID</OutputPath>
    <FieldType>Calculated</FieldType>
  </Field>
  <!-- ... rest of mapping config ... -->
</MappingConfiguration>";
```

**Option B: Load from CLM Document Storage**
```csharp
string mappingXml = LoadFromClmStorage("MappingConfig");
```

## CLM C# Expression Template

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// ============================================
// PASTE XmlTransformerCore.cs CONTENT HERE
// ============================================

namespace XmlTransformer
{
    public class XmlTransformerCore
    {
        // ... entire XmlTransformerCore class code ...
    }
}

// ============================================
// YOUR CLM WORKFLOW CODE
// ============================================

public class ClmWorkflow
{
    public string Execute()
    {
        // Step 1: Get input XML from CLM workflow
        // This comes from DocuSign CLM as a flat Row XML
        string inputXmlRow = [YOUR_CLM_ROW_VARIABLE];
        
        // Step 2: Get mapping configuration
        // Store this in a CLM variable or load from storage
        string mappingXml = [YOUR_MAPPING_CONFIG_VARIABLE];
        
        // Step 3: Transform
        string outputXml = XmlTransformerCore.TransformXmlRow(inputXmlRow, mappingXml);
        
        // Step 4: Return or use in workflow
        return outputXml;
    }
}
```

## Input Format from CLM

CLM will provide XML in this format:
```xml
<Row>
  <WKFID>OCLM-27292</WKFID>
  <ContractEntityName>TEST test</ContractEntityName>
  <ProviderType>Provider</ProviderType>
  <Network>Network P|Network Q</Network>
  <Program>Specialty|General</Program>
  <!-- All CSV columns as flat XML elements -->
</Row>
```

## Expected Output

The transformer returns hierarchical MasterXml:
```xml
<MasterXml>
  <CLM_Agreement_ID>CLM-27292</CLM_Agreement_ID>
  <CORE_NetworkNames>
    <NetworkName>Network P</NetworkName>
  </CORE_NetworkNames>
  <Params>
    <Params>
      <ContractEntityName>TEST test</ContractEntityName>
      <ContractingNetworks>
        <Networks>
          <Network key="Network P">Network P</Network>
          <Program>Specialty</Program>
        </Networks>
        <Networks>
          <Network key="Network Q">Network Q</Network>
          <Program>General</Program>
        </Networks>
      </ContractingNetworks>
    </Params>
  </Params>
  <!-- ... other elements ... -->
</MasterXml>
```

## Testing Before CLM Deployment

1. **Test locally first:**
   ```bash
   cd XmlTransformer
   dotnet run
   ```

2. **Verify output matches expectations:**
   - Check `Output_Sample.xml` or `Output_Row_*.xml`
   - Compare with your target XML structure

3. **Modify mapping if needed:**
   - Edit [MappingConfig.xml](MappingConfig.xml)
   - Rebuild and test again

4. **Once verified, copy to CLM**

## Common CLM Variables

Assuming CLM provides these variables:

```csharp
// Example CLM variable names (adjust to your CLM setup)
string inputXmlRow = clmWorkflowRow;          // From CLM workflow
string mappingXml = clmMappingConfiguration;  // Stored in CLM
string agreementId = clmAgreementId;          // CLM Agreement ID
```

## Troubleshooting in CLM

### If transformation fails:
1. Check error message in returned XML: `<Error>...</Error>`
2. Verify input XML is well-formed
3. Ensure mapping XML is valid
4. Check CLM's C# version supports System.Xml.Linq

### If output is incomplete:
1. Add missing fields to [MappingConfig.xml](MappingConfig.xml)
2. Test locally first
3. Update CLM expression with new mapping config

### If repeating sections don't work:
1. Verify pipe-separated values in input
2. Check all related fields have same `RepeatingGroupName`
3. Ensure `RepeatingGroupPath` is correct

## Step-by-Step CLM Integration

### Step 1: Prepare Files
- ✅ [XmlTransformerCore.cs](XmlTransformerCore.cs) - Copy entire content
- ✅ [MappingConfig.xml](MappingConfig.xml) - Convert to string

### Step 2: Test Locally
```bash
dotnet run
# Verify output is correct
```

### Step 3: Create CLM C# Expression
- Open CLM workflow editor
- Create new C# Expression step
- Paste XmlTransformerCore.cs code
- Add your workflow code below it

### Step 4: Configure Variables
- Input: `inputXmlRow` from CLM workflow
- Config: `mappingXml` from CLM variable/storage
- Output: Store result in CLM variable

### Step 5: Test in CLM
- Run workflow with test data
- Verify output XML structure
- Debug if needed

### Step 6: Deploy
- Move to production CLM environment
- Monitor first few runs
- Adjust mapping if needed

## Quick Reference: Method Signature

```csharp
public static string TransformXmlRow(string inputXmlRow, string mappingXml)
```

**Parameters:**
- `inputXmlRow`: Flat XML from CLM workflow (e.g., `<Row>...</Row>`)
- `mappingXml`: Mapping configuration as XML string

**Returns:**
- Hierarchical MasterXml as string
- Or `<Error>...</Error>` if transformation fails

## Support Checklist

Before deploying to CLM, verify:
- [ ] XmlTransformerCore.cs copied completely
- [ ] MappingConfig.xml converted to string
- [ ] Test data validated locally
- [ ] Output matches expected format
- [ ] All required fields mapped
- [ ] Repeating sections working correctly
- [ ] Error handling tested
- [ ] CLM variables configured
- [ ] Namespace conflicts resolved (if any)

---

Need help? Review the main [README.md](README.md) or test locally with `dotnet run`.
