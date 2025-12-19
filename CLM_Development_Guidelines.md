# DocuSign CLM C# Development Guidelines

## Project Overview
This project generates JSON patch operations to sync data between DocuSign CLM and Insight systems. The code must work in both local development and the restricted DocuSign CLM environment.

## Key Business Logic Principles

### 1. Patch Generation Rules
- **Only create "add" operations when CLM has meaningful (non-empty) values that Insight doesn't have**
- **Don't create "add" operations for empty/blank CLM values**
- **Required fields**: Always sync CLM values to Insight (replace or add as needed)
- **Optional fields**: Only sync when CLM has meaningful content
- **Remove operations**: Only when Insight has values but CLM doesn't (for optional fields)

### 2. Data Processing Logic
- Empty strings, null values, and whitespace-only values are considered "meaningless"
- Trim whitespace before processing values
- Case-insensitive field name matching for Insight XML
- Use escaped JSON for HTTP-ready output in CLM environment

### 3. File Architecture
- **CLMInsightPatch_v2.cs**: Pure CLM-compatible functions (deploy to CLM)
- **CLMInsightPatchV2Wrapper.cs**: Local testing wrapper with full C# features
- **Program.cs**: Simple console app using wrapper for local development
- **Maintain identical logic between CLM and wrapper files**

## DocuSign CLM Environment Restrictions

### ❌ **Cannot Use:**
- `using` statements at the top of files
- Classes (except for local testing wrappers)
- LINQ methods (`.Any()`, `.Where()`, `.Select()`, etc.)
- Modern C# features (var, null coalescing operators beyond `??`)
- Complex exception handling (use simple try/catch)
- Static method declarations in CLM environment
- Implicit type declarations
- Lambda expressions
- Extension methods
- Nullable reference type operators (`?.`, `??=`)

### ✅ **Must Use:**
- **Basic functions only** - simple method signatures
- **Fully qualified type names** - `System.Text.StringBuilder`, `System.Xml.XmlDocument`
- **Array-based collections** - `string[]`, manual array operations
- **Manual loops** - `for`, `foreach` (no LINQ)
- **Basic XML operations** - `XmlDocument.SelectNodes()`, `XmlNode.InnerText`
- **String concatenation/manipulation** - `Replace()`, `Trim()`, basic operations
- **Exception handling** - simple `try/catch` blocks

### 🔧 **CLM Variable Access Patterns:**
```csharp
// CLM variable access
string mappingXml = _context.XmlVariables["xCLMtoInsight"].GetXmlNode("//*").OuterXml;
string clmXml = _context.XmlVariables["dRowDocument"].GetXmlNode("//*").OuterXml;
string insightXml = _context.XmlVariables["xInsightDocMetadata"].GetXmlNode("//*").OuterXml;
```

## Development Workflow

### 1. Local Development
1. Edit `CLMInsightPatchV2Wrapper.cs` for testing
2. Run `Program.cs` to test with local XML files
3. Verify output in `output_patch.json`

### 2. CLM Deployment
1. Copy functions from `CLMInsightPatch_v2.cs` (lines 1-303)
2. Paste into CLM workflow script area
3. Update CLM variable names as needed
4. Test in CLM environment

### 3. Making Changes
1. **Always update both files**: `CLMInsightPatch_v2.cs` AND `CLMInsightPatchV2Wrapper.cs`
2. Keep logic identical between files
3. Test locally first, then deploy to CLM
4. Use CLM-compatible syntax in both files

## Technical Implementation Details

### JSON Patch Operations
```csharp
// Add operation
{"op":"add","path":"/values/0","value":{"value":"NewValue","origin":"SPRINGCM"}}

// Replace operation  
{"op":"replace","path":"/values/0/value","value":"UpdatedValue"}

// Remove operation
{"op":"remove","path":"/values/0"}
```

### XML Processing Patterns
```csharp
// CLM data extraction
XmlNodeList nodes = clmDoc.SelectNodes(xpath);
string value = nodes[i].InnerText?.Trim();

// Insight field matching
string.Equals(nameNode.InnerText, fieldName, System.StringComparison.OrdinalIgnoreCase)
```

### Empty Value Detection
```csharp
// Check for meaningful CLM values
bool hasClm = false;
for (int i = 0; i < clmValues.Length; i++)
{
    if (!string.IsNullOrEmpty(clmValues[i]) && !string.IsNullOrEmpty(clmValues[i].Trim()))
    {
        hasClm = true;
        break;
    }
}
```

## Performance Considerations
- Use `StringBuilder` for JSON construction
- Pre-allocate arrays with reasonable max sizes (e.g., `new string[100]`)
- Minimize XML document parsing - reuse parsed documents
- Avoid string concatenation in loops

## Testing Guidelines
- Test with various combinations of empty/populated fields
- Verify Required vs Optional field behavior
- Check Single vs Repeating field handling
- Validate JSON output format
- Test edge cases: null values, whitespace-only values
- Confirm CLM deployment works with exact same logic

## File Maintenance Rules
- When updating logic, modify BOTH CLM and wrapper files
- Keep wrapper file excluded from compilation via .csproj
- Maintain comments indicating CLM deployment boundaries
- Use version comments to track major changes
