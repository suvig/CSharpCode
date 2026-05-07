// =========================================================
// DATA LOADING BLOCK
// =========================================================
System.Xml.XmlDocument manifest = new System.Xml.XmlDocument();
System.Xml.XmlDocument source = new System.Xml.XmlDocument();

// --- !!!! CLM USE (Uncomment for CLM USE) !!!!! ---
string manifestData = _context.XmlVariables["AttributeMappingCSV"].GetXmlNode("/*").OuterXml;
string sourceData = _context.XmlVariables["xWFData"].GetXmlNode("/*").OuterXml;
manifest.LoadXml(manifestData);
source.LoadXml(sourceData);

// =========================================================
// SETUP OUTPUT DOCUMENT
// =========================================================
System.Xml.XmlDocument dataToWrite = new System.Xml.XmlDocument();

System.Xml.XmlElement rootNode = dataToWrite.CreateElement("UpdateMetadata");
rootNode.SetAttribute("UpdateType", "Patch");
dataToWrite.AppendChild(rootNode);

System.Xml.XmlElement debugLogNode = dataToWrite.CreateElement("DebugLog");
// rootNode.AppendChild(debugLogNode);

System.Xml.XmlElement metadataGroupsNode = dataToWrite.CreateElement("MetadataGroups");
rootNode.AppendChild(metadataGroupsNode);

System.Text.StringBuilder debugLog = new System.Text.StringBuilder();
debugLog.AppendLine("Starting processing...");

// Keep track of how many times we've seen a specific field to increment SetNumber for repeating attributes
System.Collections.Generic.Dictionary<string, int> fieldOccurrenceTracker = new System.Collections.Generic.Dictionary<string, int>();

// =========================================================
// HELPER METHODS AS DELEGATES
// =========================================================
System.Func<System.Xml.XmlNode, string, bool> ShouldSkipRow = (rowConfig, sourceNodeData) =>
{
    string ignoreNullStr = rowConfig.SelectSingleNode("IgnoreNull")?.InnerText;
    bool ignoreNull = string.Equals(ignoreNullStr, "true", System.StringComparison.OrdinalIgnoreCase);

    if (ignoreNull && string.IsNullOrEmpty(sourceNodeData)) return true;

    string valuesToIgnore = rowConfig.SelectSingleNode("ValuesToIgnore")?.InnerText;
    if (!string.IsNullOrEmpty(valuesToIgnore))
    {
        string[] ignoreList = valuesToIgnore.Split(new char[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (string ignoreValue in ignoreList)
        {
            if (sourceNodeData.Trim().Equals(ignoreValue.Trim(), System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
    }

    string mustHaveKeywords = rowConfig.SelectSingleNode("MustHaveKeywords")?.InnerText;
    if (!string.IsNullOrEmpty(mustHaveKeywords))
    {
        string[] keywordList = mustHaveKeywords.Split(new char[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (string keyword in keywordList)
        {
            if (sourceNodeData.Contains(keyword.Trim()))
                return false; 
        }
        return true; 
    }

    return false;
};

System.Func<string, System.Xml.XmlNode, string> FormatDataValue = (val, rowConfig) =>
{
    if (rowConfig == null || string.IsNullOrEmpty(val)) return val ?? "";

    bool isDate = rowConfig.SelectSingleNode("IsDate")?.InnerText?.ToUpper() == "TRUE";
    bool isNumber = rowConfig.SelectSingleNode("IsNumber")?.InnerText?.ToUpper() == "TRUE";
    bool isDecimal = rowConfig.SelectSingleNode("IsDecimal")?.InnerText?.ToUpper() == "TRUE";
    string transformations = rowConfig.SelectSingleNode("Transformations")?.InnerText;

    if (isDate)
    {
        System.DateTime parsedDate;
        string[] dateFormats = { "yyyyMMddHHmmss", "MM/dd/yyyy", "yyyy-MM-dd", "MM/dd/yyyy HH:mm:ss", "yyyy-MM-ddTHH:mm:ss" };
        if (System.DateTime.TryParseExact(val, dateFormats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out parsedDate) ||
            System.DateTime.TryParse(val, out parsedDate))
        {
            return parsedDate.ToString("MM/dd/yyyy");
        }
        return ""; 
    }
    else if (isNumber)
    {
        double parsedNumber;
        if (double.TryParse(val, out parsedNumber)) return parsedNumber.ToString("0");
        return "";
    }
    else if (isDecimal)
    {
        decimal parsedDecimal;
        if (decimal.TryParse(val, out parsedDecimal)) return parsedDecimal.ToString();
        return "";
    }

    if (!string.IsNullOrEmpty(transformations))
    {
        string[] transformationList = transformations.Split(new char[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (string transformation in transformationList)
        {
            string[] parts = transformation.Split(':');
            if (parts.Length == 2 && val.Equals(parts[0].Trim(), System.StringComparison.OrdinalIgnoreCase))
            {
                return parts[1].Trim();
            }
        }
    }
    return val;
};

// =========================================================
// CORE PROCESSING
// =========================================================
foreach (System.Xml.XmlNode row in manifest.SelectNodes("//Row"))
{
    string targetGroup = row.SelectSingleNode("TargetGroup")?.InnerText;
    string targetField = row.SelectSingleNode("TargetField")?.InnerText;
    string xmlPath = row.SelectSingleNode("XmlSourcePath")?.InnerText;
    string setName = row.SelectSingleNode("SetName")?.InnerText;
    string defaultValue = row.SelectSingleNode("DefaultValue")?.InnerText;

    if (string.IsNullOrEmpty(targetGroup) || string.IsNullOrEmpty(targetField)) continue;

    System.Collections.Generic.List<string> rawValuesFound = new System.Collections.Generic.List<string>();

    if (xmlPath != "PLACEHOLDER" && !string.IsNullOrEmpty(xmlPath))
    {
        string xpath = xmlPath.StartsWith("//") ? xmlPath : "//" + xmlPath;
        try
        {
            System.Xml.XmlNodeList sourceNodes = source.SelectNodes(xpath);
            if (sourceNodes != null)
            {
                foreach (System.Xml.XmlNode node in sourceNodes)
                {
                    if (!string.IsNullOrEmpty(node.InnerText)) rawValuesFound.Add(node.InnerText);
                }
            }
        }
        catch (System.Exception ex)
        {
            debugLog.AppendLine($"Failed XPath Evaluation for '{xpath}': {ex.Message}");
        }
    }

    if (rawValuesFound.Count == 0 && !string.IsNullOrEmpty(defaultValue)) rawValuesFound.Add(defaultValue);
    if (rawValuesFound.Count == 0) continue;

    bool isRepeatingSet = !string.IsNullOrEmpty(setName);

    for (int i = 0; i < rawValuesFound.Count; i++)
    {
        string rawVal = rawValuesFound[i];
        string finalVal = FormatDataValue(rawVal, row);
        
        if (string.IsNullOrEmpty(finalVal) || ShouldSkipRow(row, finalVal)) continue;

        // Determine SetNumber
        int setNum = 0;
        if (isRepeatingSet)
        {
            // Standard Repeating Set behavior (1, 2, 3...)
            setNum = i + 1;
        }
        else
        {
            // Repeating Attribute behavior (0 for first, +1 for subsequent)
            string fieldKey = targetGroup + "_" + targetField;
            if (!fieldOccurrenceTracker.ContainsKey(fieldKey))
            {
                fieldOccurrenceTracker[fieldKey] = 0; // First instance
            }
            else
            {
                fieldOccurrenceTracker[fieldKey]++; // Increment for subsequent instances
            }
            setNum = fieldOccurrenceTracker[fieldKey];
        }

        // 1. Find or create MetadataGroup
        System.Xml.XmlNode groupNode = metadataGroupsNode.SelectSingleNode($"MetadataGroup[Name='{targetGroup}']");
        if (groupNode == null)
        {
            groupNode = dataToWrite.CreateElement("MetadataGroup");
            System.Xml.XmlElement groupNameElement = dataToWrite.CreateElement("Name");
            groupNameElement.InnerText = targetGroup;
            groupNode.AppendChild(groupNameElement);
            metadataGroupsNode.AppendChild(groupNode);
        }

        // 2. Find or create Set within that group
        // Note: Query includes SetNumber to ensure we create new sets for incremented numbers
        bool isNonRepeatingSet = !isRepeatingSet && setNum == 0;
        string setQuery = isRepeatingSet
            ? $"Set[SetNumber='{setNum}' and Name='{setName}']"
            : isNonRepeatingSet
                ? "Set[Name='Set' or SetNumber='0']"
                : $"Set[SetNumber='{setNum}']";
        System.Xml.XmlNode setNode = groupNode.SelectSingleNode(setQuery);
        
        if (setNode == null)
        {
            setNode = dataToWrite.CreateElement("Set");
            groupNode.AppendChild(setNode);
        }

        // 3. Find or create Field within that Set
        // Since we are incrementing the SetNumber for every repeated value, 
        // each field will effectively be unique within its specific Set block.
        System.Xml.XmlNode existingField = setNode.SelectSingleNode($"Field[Field='{targetField}']");

        if (existingField != null)
        {
            System.Xml.XmlNode valNode = existingField.SelectSingleNode("Value");
            if (valNode != null) valNode.InnerText = finalVal;
        }
        else
        {
            System.Xml.XmlElement fieldContainer = dataToWrite.CreateElement("Field");

            System.Xml.XmlElement fGroup = dataToWrite.CreateElement("Group");
            fGroup.InnerText = targetGroup;
            fieldContainer.AppendChild(fGroup);

            System.Xml.XmlElement fName = dataToWrite.CreateElement("Field");
            fName.InnerText = targetField;
            fieldContainer.AppendChild(fName);

            System.Xml.XmlElement fVal = dataToWrite.CreateElement("Value");
            fVal.InnerText = finalVal;
            fieldContainer.AppendChild(fVal);

            if (isRepeatingSet)
            {
                System.Xml.XmlElement fSetName = dataToWrite.CreateElement("SetName");
                fSetName.InnerText = setName;
                fieldContainer.AppendChild(fSetName);
            }

            if (!isNonRepeatingSet)
            {
                System.Xml.XmlElement fSetNum = dataToWrite.CreateElement("SetNumber");
                fSetNum.InnerText = setNum.ToString();
                fieldContainer.AppendChild(fSetNum);
            }

            setNode.AppendChild(fieldContainer);
        }

        string resolvedSetName = isRepeatingSet ? setName : "Set";
        System.Xml.XmlElement setNameNode = setNode.SelectSingleNode("Name") as System.Xml.XmlElement;
        if (setNameNode == null)
        {
            setNameNode = dataToWrite.CreateElement("Name");
        }
        else
        {
            setNode.RemoveChild(setNameNode);
        }
        setNameNode.InnerText = resolvedSetName;
        setNode.AppendChild(setNameNode);

        System.Xml.XmlElement setNumberNode = setNode.SelectSingleNode("SetNumber") as System.Xml.XmlElement;
        if (isNonRepeatingSet)
        {
            if (setNumberNode != null)
            {
                setNode.RemoveChild(setNumberNode);
            }
        }
        else if (setNumberNode == null)
        {
            setNumberNode = dataToWrite.CreateElement("SetNumber");
            setNumberNode.InnerText = setNum.ToString();
            setNode.AppendChild(setNumberNode);
        }
        else
        {
            setNode.RemoveChild(setNumberNode);
            setNumberNode.InnerText = setNum.ToString();
            setNode.AppendChild(setNumberNode);
        }
    }
}

System.Xml.XmlComment commentLog = dataToWrite.CreateComment(debugLog.ToString());
dataToWrite.InsertBefore(commentLog, rootNode);


return dataToWrite.OuterXml;
