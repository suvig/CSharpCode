// ========================================================================
// BULK IMPORT CORE - CLM COMPATIBLE FUNCTIONS
// ========================================================================
// For CLM Deployment: Copy lines 19-378 (all functions below) into CLM
// For Local Testing: This file wraps functions in a class for testing
// ========================================================================

using System;
using System.Collections.Generic;
using System.Xml;

namespace BSBSTN_BulkImport
{
    /// <summary>
    /// Wrapper class for local testing only
    /// DO NOT copy this class to CLM - copy only the functions inside
    /// </summary>
    public class BulkImportCore
    {
        public static string TransformXmlRow(string inputRowXml, string mappingXml)
        {
            return CLM_TransformXmlRow(inputRowXml, mappingXml);
        }

// ========================================================================
// START CLM FUNCTIONS - Copy from here to CLM
// ========================================================================

/// <summary>
/// Main transformation method - call this from CLM
/// </summary>
static string CLM_TransformXmlRow(string inputRowXml, string mappingXml)
{
    try
    {
        // Parse inputs
        System.Xml.XmlDocument inputRowDoc = new System.Xml.XmlDocument();
        inputRowDoc.LoadXml(inputRowXml);
        
        System.Xml.XmlDocument mappingDoc = new System.Xml.XmlDocument();
        mappingDoc.LoadXml(mappingXml);
        
        // Extract input values into dictionary
        System.Collections.Generic.Dictionary<string, string> inputData = ExtractInputData(inputRowDoc);
        
        // Load mapping configuration
        System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>> mappingConfig = LoadMappingConfig(mappingDoc);
        
        // Build output XML
        System.Xml.XmlDocument outputDoc = BuildOutputXml(inputData, mappingConfig);
        
        return outputDoc.OuterXml;
    }
    catch (System.Exception ex)
    {
        return "<Error>Transformation failed: " + ex.Message + "</Error>";
    }
}

/// <summary>
/// Extract all input elements into a dictionary
/// </summary>
static System.Collections.Generic.Dictionary<string, string> ExtractInputData(System.Xml.XmlDocument inputRowDoc)
{
    System.Collections.Generic.Dictionary<string, string> data = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
    
    System.Xml.XmlNode rowElement = inputRowDoc.DocumentElement;
    if (rowElement != null)
    {
        System.Xml.XmlNodeList children = rowElement.ChildNodes;
        for (int i = 0; i < children.Count; i++)
        {
            System.Xml.XmlNode element = children[i];
            if (element.NodeType == System.Xml.XmlNodeType.Element)
            {
                string key = element.LocalName;
                string value = element.InnerText;
                if (value == null)
                {
                    value = string.Empty;
                }
                data[key] = value;
            }
        }
    }
    
    return data;
}

/// <summary>
/// Load mapping configuration from XML
/// </summary>
static System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>> LoadMappingConfig(System.Xml.XmlDocument mappingDoc)
{
    System.Xml.XmlNodeList fieldNodes = mappingDoc.SelectNodes("//Row");
    System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>> mappings = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>>();
    
    for (int i = 0; i < fieldNodes.Count; i++)
    {
        System.Xml.XmlNode fieldElement = fieldNodes[i];
        
        System.Collections.Generic.Dictionary<string, string> mapping = new System.Collections.Generic.Dictionary<string, string>();
        mapping["InputNode"] = GetElementValue(fieldElement, "InputNode");
        mapping["OutputPath"] = GetElementValue(fieldElement, "OutputPath");
        mapping["FieldType"] = GetElementValue(fieldElement, "FieldType");
        mapping["RepeatingGroupName"] = GetElementValue(fieldElement, "RepeatingGroupName");
        mapping["RepeatingGroupPath"] = GetElementValue(fieldElement, "RepeatingGroupPath");
        mapping["RepeatingElementName"] = GetElementValue(fieldElement, "RepeatingElementName");
        mapping["TransformRule"] = GetElementValue(fieldElement, "TransformRule");
        
        mappings.Add(mapping);
    }
    
    return mappings;
}

/// <summary>
/// Build the output XML structure
/// </summary>
static System.Xml.XmlDocument BuildOutputXml(System.Collections.Generic.Dictionary<string, string> inputData, System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>> mappings)
{
    System.Xml.XmlDocument doc = new System.Xml.XmlDocument();
    System.Xml.XmlElement root = doc.CreateElement("MasterXml");
    doc.AppendChild(root);
    
    // Process simple and calculated fields first
    for (int i = 0; i < mappings.Count; i++)
    {
        if (mappings[i]["FieldType"] == "Simple" || mappings[i]["FieldType"] == "Calculated")
        {
            ProcessSimpleField(doc, root, inputData, mappings[i]);
        }
    }
    
    // Process static fields
    for (int i = 0; i < mappings.Count; i++)
    {
        if (mappings[i]["FieldType"] == "Static")
        {
            ProcessStaticField(doc, root, mappings[i]);
        }
    }
    
    // Process repeating groups - group by RepeatingGroupName
    System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>>> groups = 
        new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>>>();
    
    for (int i = 0; i < mappings.Count; i++)
    {
        if (mappings[i]["FieldType"] == "Repeating")
        {
            string groupName = mappings[i]["RepeatingGroupName"];
            if (!groups.ContainsKey(groupName))
            {
                groups[groupName] = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>>();
            }
            groups[groupName].Add(mappings[i]);
        }
    }
    
    // Process each repeating group
    System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>>>.KeyCollection keys = groups.Keys;
    foreach (string groupName in keys)
    {
        System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>> groupMappings = groups[groupName];
        ProcessRepeatingGroup(doc, root, inputData, groupMappings);
    }
    
    return doc;
}

/// <summary>
/// Process a static field (always creates empty element)
/// </summary>
static void ProcessStaticField(System.Xml.XmlDocument doc, System.Xml.XmlElement root, System.Collections.Generic.Dictionary<string, string> mapping)
{
    // Create the path in output XML with empty value
    System.Xml.XmlElement targetElement = EnsurePathExists(doc, root, mapping["OutputPath"]);
    // Leave it empty (no value assignment)
}

/// <summary>
/// Process a simple field mapping
/// </summary>
static void ProcessSimpleField(System.Xml.XmlDocument doc, System.Xml.XmlElement root, 
    System.Collections.Generic.Dictionary<string, string> inputData, System.Collections.Generic.Dictionary<string, string> mapping)
{
    if (!inputData.ContainsKey(mapping["InputNode"]))
        return;
    
    string value = inputData[mapping["InputNode"]];
    
    // Apply transformations
    value = ApplyTransformRule(value, mapping["TransformRule"]);
    
    // Create the path in output XML
    System.Xml.XmlElement targetElement = EnsurePathExists(doc, root, mapping["OutputPath"]);
    targetElement.InnerText = value;
}

/// <summary>
/// Process a repeating group (e.g., Networks, RelatedProviders)
/// </summary>
static void ProcessRepeatingGroup(System.Xml.XmlDocument doc, System.Xml.XmlElement root, 
    System.Collections.Generic.Dictionary<string, string> inputData, System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>> groupMappings)
{
    if (groupMappings.Count == 0)
        return;
    
    // Get the first mapping to determine group structure
    System.Collections.Generic.Dictionary<string, string> firstMapping = groupMappings[0];
    
    // Collect all pipe-separated values for this group
    System.Collections.Generic.Dictionary<string, string[]> allValues = 
        new System.Collections.Generic.Dictionary<string, string[]>();
    int maxCount = 0;
    
    for (int i = 0; i < groupMappings.Count; i++)
    {
        System.Collections.Generic.Dictionary<string, string> mapping = groupMappings[i];
        if (inputData.ContainsKey(mapping["InputNode"]))
        {
            string value = inputData[mapping["InputNode"]];
            string[] parts = value.Split('|');
            allValues[mapping["InputNode"]] = parts;
            if (parts.Length > maxCount)
            {
                maxCount = parts.Length;
            }
        }
    }
    
    if (maxCount == 0)
        return;
    
    // Create parent path (e.g., Params/Params/ContractingNetworks)
    System.Xml.XmlElement parentElement = EnsurePathExists(doc, root, firstMapping["RepeatingGroupPath"]);
    
    // Create repeating elements
    for (int i = 0; i < maxCount; i++)
    {
        System.Xml.XmlElement repeatingElement = doc.CreateElement(firstMapping["RepeatingElementName"]);
        
        // Add all fields for this instance
        for (int j = 0; j < groupMappings.Count; j++)
        {
            System.Collections.Generic.Dictionary<string, string> mapping = groupMappings[j];
            string value = string.Empty;
            
            if (allValues.ContainsKey(mapping["InputNode"]))
            {
                string[] values = allValues[mapping["InputNode"]];
                if (i < values.Length)
                {
                    value = values[i].Trim();
                }
            }
            
            // Apply transformations
            value = ApplyTransformRule(value, mapping["TransformRule"]);
            
            // Extract the child element name from output path
            string childElementName = ExtractChildElementName(mapping["OutputPath"]);
            
            System.Xml.XmlElement childElement = doc.CreateElement(childElementName);
            childElement.InnerText = value;
            
            repeatingElement.AppendChild(childElement);
        }
        
        parentElement.AppendChild(repeatingElement);
    }
}

/// <summary>
/// Ensure the full path exists in the XML tree, creating nodes as needed
/// </summary>
static System.Xml.XmlElement EnsurePathExists(System.Xml.XmlDocument doc, System.Xml.XmlElement root, string path)
{
    string[] parts = path.Split('/');
    System.Xml.XmlElement current = root;
    
    for (int i = 0; i < parts.Length; i++)
    {
        string part = parts[i];
        if (string.IsNullOrEmpty(part))
            continue;
        
        System.Xml.XmlElement child = null;
        System.Xml.XmlNodeList children = current.ChildNodes;
        for (int j = 0; j < children.Count; j++)
        {
            if (children[j].NodeType == System.Xml.XmlNodeType.Element && children[j].LocalName == part)
            {
                child = (System.Xml.XmlElement)children[j];
                break;
            }
        }
        
        if (child == null)
        {
            child = doc.CreateElement(part);
            current.AppendChild(child);
        }
        current = child;
    }
    
    return current;
}

/// <summary>
/// Extract the last element name from a path
/// </summary>
static string ExtractChildElementName(string outputPath)
{
    string[] parts = outputPath.Split('/');
    return parts[parts.Length - 1];
}

/// <summary>
/// Apply transformation rules to values
/// </summary>
static string ApplyTransformRule(string value, string rule)
{
    if (string.IsNullOrEmpty(rule))
        return value;
    
    if (rule == "Replace 'OCLM' with 'CLM'")
    {
        return value.Replace("OCLM", "CLM");
    }
    else if (rule == "FirstPipeValue")
    {
        string[] parts = value.Split('|');
        if (parts.Length > 0)
        {
            return parts[0].Trim();
        }
        return value;
    }
    else if (rule == "AppendTime")
    {
        // If value is a date (YYYY-MM-DD), append time
        if (!string.IsNullOrEmpty(value) && value.Length >= 10)
        {
            return value + "T00:00:00";
        }
        return value;
    }
    
    return value;
}

/// <summary>
/// Helper to get element value or empty string
/// </summary>
static string GetElementValue(System.Xml.XmlNode parent, string elementName)
{
    System.Xml.XmlNodeList nodes = parent.ChildNodes;
    for (int i = 0; i < nodes.Count; i++)
    {
        if (nodes[i].NodeType == System.Xml.XmlNodeType.Element && nodes[i].LocalName == elementName)
        {
            string value = nodes[i].InnerText;
            if (value == null)
            {
                return string.Empty;
            }
            return value;
        }
    }
    return string.Empty;
}

// ========================================================================
// END CLM FUNCTIONS - Copy to here for CLM deployment
// ========================================================================

    } // End BulkImportCore class
} // End namespace

// ========================================================================
// END OF CLM COMPATIBLE CODE
// ========================================================================
