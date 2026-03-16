// ---------------------------------------------------------
// Created by:            Kyle Bouquet
// Last Modified:         2026-02-03
// Description:           This program processes a manifest XML to extract metadata from source XML or string variables,
//                        formats the data according to specified rules, and constructs an output XML document for metadata updates.
//                        It supports both single data points and sets of data points, with detailed debug logging throughout the process.
//                        The output XML is structured to indicate whether a PUT or PATCH method should be used for updates.
//                        The program includes helper functions for logging, data formatting, and row skipping based on manifest settings.
// Usage:                 This code is intended to be run in a workflow environment where manifest and source data are provided as XML variables (strings 
//                        accepted for source as well).
// Notes:                 Modify the source data loading section to fit your specific workflow variable retrieval method. This code has been prepared
//                        for testing in a local environment with simulated data files as well as in CLM by commenting and uncommenting the appropriate lines.
//                        Additionally, the logging is extremely heavy for debug purposes on initial build. Once stable, CONSIDER REDUCING the logging to only key events or errors
//                        to maintain an output within the 1MB limit for expression output in CLM, or to disable entirely by setting 'disableDebugLogging' to TRUE at the start of code.
// Manifest Requirements: The manifest XML must contain rows with specific fields (names can be modified when loading the manifest if your structure or manifest is different):
//                        NameOfSourceVariable (string, required): the name of the variable in the workflow from which data should be sourced
//                        XmlSourcePath (string, required for XML sources, should NOT be filled for string sources): the xpath (simple or with predicate) where data is expected in the XML source
//                        DefaultValue (string, optional): If the value for the target field is a static default value, populate it here. When provided, all other row parameters will be ignored.
//                        TargetGroup (string, required, case and space sensitive): The name of the target group to create
//                        TargetField (string, required, case and space sensitive): The name of the target field to create
//                        SetName (string, required for fields that are part of the same set within a group): Used to identify manifest rows that belong together for repeating and non-repeating sets
//                        IgnoreNull (bool, optional): default FALSE if not specified. When TRUE, will skip attempting to write rows where source is NULL. Note: if null value is in a group where other fields are written, field may still appear
//                        Condition (string, optional): XPath boolean expression evaluated against xWFData. Row is skipped unless the expression returns TRUE.
//                        Transformations (string, pipe delimited sets, colon delimited transformations): takes a found value and translates it to its corresponding transformed value. Format as 'Value1:Value2|ValueA:ValueB' where the comparison value is left of colon, transformed result is right of colon. Possibilities separated by pipe.
//                        ValuesToIgnore (string, pipe delimited, case insensitive): If the source data contains any of the pipe separated words, it will be ignored/skipped
//                        MustHaveKeywords (string, pipe delimited, case insensitive): If the source data does NOT contain any of the pipe separated words, it will be ignored/skipped.
//
//                        ***Manifest can be in the form of CSV file with above named fields/columns, or a manually created XML variable where the above fields are child elements of repeating <Row> elements
// --------------------------------------------------------
using System.Diagnostics;
using System;
using System.Xml;
using System.Dynamic;
using System.Xml.XPath;

class Program
{
    static void Main()
    {
        //IF USING IN CLM, REMOVE EVERTHING ABOVE THIS COMMENT LINE AND EVERYTHING BELOW THE LAST LINE OF THE FILE
        //AND UNCOMMENT THE RETURN STATEMENT AT THE END OF THE FILE TO RETURN THE OUTPUT VARIABLE TO THE WORKFLOW
        //ALSO ENSURE TO ADJUST THE DATA LOADING BLOCKS TO LOAD FROM CONTEXT VARIABLES RATHER THAN LOCAL FILES (Ctrl+F "DATA LOADING BLOCK" TO FIND THEM; There are 4)

        // ---------------------------------------------------------
        // INITIAL SETUP to create variables and load manifest/source data
        // ---------------------------------------------------------
        // Create an index tracker and message variable for processed rows for debug/logging purposes
        string minLevelToInclude = "NONE"; // Set the minimum debug level to include in output (0-4, 0 being most detailed, or NONE to disable logging entirely)
        string logMessage;
        string processedIndexes = "";     
        bool putMethod = false; // Determine if you want to use PUT method. If 'true', nothing is specified, or this line is commented out, PATCH will be used (this is the step default, so only need to specify PUT when desired).

        // =========================================================
        // DATA LOADING BLOCK
        // =========================================================
        // Load the Manifest File Data. Ensure you have a manifest xml variable loaded in workflow named "manifestCsv" or modify the code below to load from your desired source. 
        // Column names are also identified here and pre set, but can be changed. They are case and space sensitive
        System.Xml.XmlDocument manifest = new System.Xml.XmlDocument();
        // ---!!!! LOCAL TESTING (Comment out for CLM USE) !!!!---
        string manifestData = System.IO.File.ReadAllText("SampleData/SampleManifestWalmart.xml");//<---COMMENT ME OUT WHEN TESTING IN CLM!

        // --- !!!!CLM USE (Uncomment for CLM USE) !!!!!---
        //string manifestData = "";
        //manifestData = _context.XmlVariables["manifestCsv"].GetXmlNode("/*").OuterXml;
        // =========================================================
        manifest.LoadXml(manifestData);

        // Set Column Header Names
        string colRowIterator = "Row";
        string colSourceVariableName = "NameOfSourceVariable";
        string colXmlSourcePath = "XmlSourcePath";
        string colDefaultValue = "DefaultValue";
        string colTargetGroup = "TargetGroup";
        string colTargetField = "TargetField";
        string colIsDate = "IsDate";
        string colIsNumber = "IsNumber";
        string colIsDecimal = "IsDecimal";
        string colSetName = "SetName";
        string colIgnoreNull = "IgnoreNull";
        string colCondition = "Condition";
        string colTransformations = "Transformations";
        string colValuesToIgnore = "ValuesToIgnore";
        string colMustHaveKeywords = "MustHaveKeywords";


        // Create a source xml variable, initialized now and to be used later to load source data from workflow variables as needed
        System.Xml.XmlDocument source = new System.Xml.XmlDocument();


        // ---------------------------------------------------------
        /* Create return variable for end of expression, and establish the root node and method attribute.
         * Also establishes a node for Debug Logging.*/
        // ---------------------------------------------------------

        // Create the output XML document with root element that defines operation type
        System.Xml.XmlDocument dataToWrite = new System.Xml.XmlDocument();
        XmlElement writeRoot = dataToWrite.CreateElement("UpdateMetadata");
        string method = putMethod == true ? "Put" : "Patch";
        writeRoot.SetAttribute("UpdateType", method);

        // Establish a node for error logging
        XmlElement errorLogNode = dataToWrite.CreateElement("DebugLog");
        writeRoot.AppendChild(errorLogNode);

        // Establish a node for metadata groups (the data to be written to attributes)
        XmlElement outerGroupNode = dataToWrite.CreateElement("MetadataGroups");
        writeRoot.AppendChild(outerGroupNode);
        dataToWrite.AppendChild(writeRoot);

        // Load xWFData once for manifest Condition XPath evaluation
        System.Xml.XmlDocument conditionSource = new System.Xml.XmlDocument();
        System.Xml.XPath.XPathNavigator requestNavigator = null;
        try
        {
                // =========================================================
                // DATA LOADING BLOCK
                // =========================================================
                // --- LOCAL TESTING (Comment out for CLM USE) ---
                string conditionSourceData = System.IO.File.ReadAllText("SampleData/xWFData.xml");

                // --- CLM USE (Uncomment for CLM USE) ---
                //string conditionSourceData = _context.XmlVariables["xWFData"].GetXmlNode("/*").OuterXml;
                // =========================================================

                conditionSource.LoadXml(conditionSourceData);
                requestNavigator = conditionSource.CreateNavigator();
                logMessage = "Loaded xWFData for manifest condition evaluation.";
                WriteDebugLog(null, logMessage, null, false, 1);
        }
        catch (Exception ex)
        {
                logMessage = "----|***ERROR LOADING xWFData FOR CONDITION EVALUATION*** - " + ex.Message;
                WriteDebugLog(null, logMessage, "Failed", false, 4);
        }

        // Create a set tracking variable to prevent duplicate processing of any Set type rows (since those in a set need to be processed together)
        string processedSetNames = "";

        //Set a variable for tracking the source variable from workflow to use for each iteration
        string currentSourceVariableName = "";

        // Initial Logging
        logMessage = "Using Method: " + (putMethod == true ? "PUT |" : "PATCH | ") + "Beginning Manifest Processing...";
        WriteDebugLog(null ,logMessage, null, true, 2);

        // ---------------------------------------------------------
        /* Begin Iteration of Manifest NOTE: you may opt to use a single manifest across multiple document types. 
         * If so, be sure to add your filter on //Rows as a predicate in the below xpath operation. 
         * For example, you might want a column in your manifest for "AgreementType", in which case you'd add the predicate '[AgreementType='{DesiredType}']' 
         * to the xpath below to only select rows relevant to the current document type being processed. Alternatively, you can filter this when you load the 
         * manifest in workflow prior to this step. There is a commented alternative of the foreach below showing how to do this.*/
        // ---------------------------------------------------------

        //foreach (XmlNode currentRowNode in manifest.SelectNodes("//" + colRowIterator + "[AgreementType='TypeA']")){
        foreach (XmlNode currentRowNode in manifest.SelectNodes("//" + colRowIterator)){
                // Get Source Variable Info to determine source type (xml, string, or default value), and determine if there is an XmlSourcePath (which helps identify the source variable as XML)
                XmlNode nameOfSourceVariableNode = currentRowNode.SelectSingleNode(colSourceVariableName);
                string nameOfSourceVariable = nameOfSourceVariableNode?.InnerText;

                XmlNode sourcePathNode = currentRowNode.SelectSingleNode(colXmlSourcePath);
                string currentSourcePathValue = sourcePathNode?.InnerText;

                XmlNode defaultValueNode = currentRowNode.SelectSingleNode(colDefaultValue);
                string defaultValue = defaultValueNode?.InnerText;

                // Log the beginning of processing for this row
                WriteDebugLog(currentRowNode, null, null, false, 0);

                // Check if this row has already been processed (important for sets since they are processed together)
                if (processedIndexes.Contains(GetRowIndexNumber(currentRowNode))){
                        // Log that we are skipping this row since it has already been processed
                        logMessage = "Row already processed, skipping.";
                        WriteDebugLog(null, logMessage, "Already Processed", false, 1);
                        continue; // Skip to next iteration
                }

                if (!EvaluateManifestCondition(currentRowNode))
                {
                        logMessage = "Skipping row because manifest condition evaluated to FALSE.";
                        WriteDebugLog(null, logMessage, "Skipped", false, 2);
                        MarkRowProcessed(currentRowNode);
                        continue;
                }

                // Handle the source as an XML Variable
                if (!string.IsNullOrEmpty(nameOfSourceVariable) && nameOfSourceVariable != currentSourceVariableName && !string.IsNullOrEmpty(currentSourcePathValue)){
                        try{
                                // Update the currentSourceVariable so we don't unnecessarily do this operation the next time around unless the source variable changes
                                currentSourceVariableName = nameOfSourceVariable;

                                // Clear the variable just to be safe in case the load operation fails
                                source = new System.Xml.XmlDocument();
                                string sourceData = "";

                                // =========================================================
                                // DATA LOADING BLOCK
                                // =========================================================
                                // --- !!!!! LOCAL TESTING (Comment out for CLM USE) !!!!---
                                sourceData = System.IO.File.ReadAllText("SampleData/" + nameOfSourceVariable + ".xml");

                                // --- !!!!CLM USE (Uncomment for CLM USE) !!!!!---
                                /*
                                if (_context.XmlVariables.ContainsKey(nameOfSourceVariable))
                                {
                                        // Use "/*" to get the root element efficiently
                                        sourceData = _context.XmlVariables[nameOfSourceVariable].GetXmlNode("/*").OuterXml;
                                }
                                else
                                {
                                        // Force an error we can catch if the variable doesn't exist
                                        throw new Exception($"Variable '{nameOfSourceVariable}' not found in Context.");
                                }
                                */
                                // =========================================================

                                if (string.IsNullOrEmpty(sourceData)) throw new Exception("Loaded source data is empty or invalid.");
                                source.LoadXml(sourceData);

                                // Log the loading of the XML source variable
                                logMessage = "Loaded XML Source Variable.";
                                WriteDebugLog(null, logMessage, null, false, 1);
                        }
                        catch (Exception ex){
                                // Log the error encountered while loading the XML source variable
                                logMessage = "----|***ERROR LOADING XML SOURCE VARIABLE*** - " + ex.Message;
                                WriteDebugLog(null, logMessage, "Failed", false, 4);
                                // Skip to the next iteration
                                continue;
                        }
                }
                
                // If not an Xml source, handle as string variable, but we'll put it in XML format for consistent processing downstream
                else if ((!string.IsNullOrEmpty(nameOfSourceVariable) && nameOfSourceVariable != currentSourceVariableName && string.IsNullOrEmpty(currentSourcePathValue))){
                        try
                        {
                                // Update the currentSourceVariable tracker
                                currentSourceVariableName = nameOfSourceVariable;
                                string sourceData = "";

                                // =========================================================
                                // DATA LOADING BLOCK
                                // =========================================================
                                // --- LOCAL TESTING (Comment out for CLM USE) ---
                                System.Xml.XmlDocument testStringXml = new System.Xml.XmlDocument();
                                string testStringData = System.IO.File.ReadAllText("SampleData/xWFData.xml");
                                testStringXml.LoadXml(testStringData);
                                sourceData = testStringXml.SelectSingleNode("//" + nameOfSourceVariable)?.InnerText;
                                

                                // --- CLM USE (Uncomment for CLM USE) ---
                                // sourceData = GetVariableValue(nameOfSourceVariable);
                                
                                // =========================================================

                                // Ensure we actually got data back
                                if (sourceData == null) throw new Exception($"Source Data for '{nameOfSourceVariable}' returned null.");

                                // Initialize source safely
                                source = new System.Xml.XmlDocument();
                                
                                // Manually construct the XML structure safely
                                // SecurityElement.Escape is critical here to handle special chars like <, >, &
                                string safeData = System.Security.SecurityElement.Escape(sourceData);
                                source.LoadXml($"<Root><{nameOfSourceVariable}>{safeData}</{nameOfSourceVariable}></Root>");

                                // Log Success
                                logMessage = $"Loaded String Source Variable: {nameOfSourceVariable}";
                                WriteDebugLog(null, logMessage, null, false,1);
                        }
                        catch (Exception ex)
                        {
                                // 1. Log the crash
                                string errorMsg = $"***ERROR LOADING STRING SOURCE VARIABLE*** '{nameOfSourceVariable}'. Error: {ex.Message}";
                                WriteDebugLog(null, errorMsg, "Failed", false, 4);

                                // 2. Reset the tracker
                                currentSourceVariableName = "";

                                // 3. Skip to next iteration
                                continue;
                        }
                }
                else if (!string.IsNullOrEmpty(defaultValue)){
                        try
                        {
                                // Update the currentSourceVariable tracker
                                currentSourceVariableName = "DefaultValue";

                                // Initialize source safely
                                source = new System.Xml.XmlDocument();

                                // Manually construct the XML structure safely
                                // SecurityElement.Escape is critical here to handle special chars like <, >, &
                                string safeData = System.Security.SecurityElement.Escape(defaultValue);
                                source.LoadXml($"<Root><DefaultValue>{safeData}</DefaultValue></Root>");

                                // Log Success
                                logMessage = $"Using Default Value for for this row: {nameOfSourceVariable}: {defaultValue}";
                                WriteDebugLog(null, logMessage, null, false,1);
                        }
                        catch (Exception ex)
                        {
                                // 1. Log the crash
                                string errorMsg = $"***ERROR LOADING DEFAULT VALUE FOR ROW*** '{nameOfSourceVariable}'. Error: {ex.Message}";
                                WriteDebugLog(null, errorMsg, "Failed", false, 4);

                                // 2. Reset the tracker
                                currentSourceVariableName = "";

                                // 3. Skip to next iteration
                                continue;
                        }
                }
                else{
                        // Log that no source variable was specified
                        logMessage = "Source variable was either not specified, invalid, or was ALREADY loaded. The only case where this is not an issue is if the previous row loaded the same value. This message indicates that reloading the variable was unnecessary. But if that is not the case, it is likely a manifest configuration issue and you should check to ensure the NameOfSourceVariable is populated.";
                        WriteDebugLog(null, logMessage, null, false, 1);
                }


                // ---------------------------------------------------------
                /* Check first if the data point is part of a set
                 * IMPORTANT NOTE ABOUT REPEATING SETS: This code assumes that sets are defined by a common SetName in the manifest, and that each data point in the set has its own row in the manifest.
                 * If each data point in the SOURCE DATA has a SetNumber, then this code will adopt that SetNumber for the data point in the output. <--THIS IS THE IDEAL scenario and structure of source data!
                 * If the data point in the SOURCE DATA does not have a SetNumber, then this code will assign the data point a SetNumber based on the order in which it was found in the source 
                 * data for that SourcePath and availability of that SetNumber (i.e., it checks to make sure a subsequent data point is not already assigned that SetNumber), ensuring no duplication of SetNumbers 
                 * within that SetName. HOWEVER, when assigning the SetNumber, the next available number will be used for each data point individually.
                 * Therefore, and for example, if you have a set of 4 data points, and there are 4 sets in the source data, but one of the data points in the 2nd set is missing from the source data,
                 * you may experience a misalignment of SetNumbers (e.g., since the SECOND set is missing [DataPointA], the THIRD set's [DataPointA] will be assigned SetNumber 2, and all subsequent data points
                 * in that set will be off by one). This is unavoidable because it will otherwise be impossible to tell which data points go together in a set due to lack of information.
                 * To avoid this misalignment, ensure that all data points in a set are present in the source data, EVEN IF THEY ARE EMPTY!
                 * Sets are proccessed separately for two reasons:
                 *      > First, all data points in a set are processed together making the output more human readable (but is not necessary for the attribute assignment)
                 *      > Second, the operation knows to look for more than one of a data point in the source data, whereas non-set data points are only looking for a single instance.*/
                // ---------------------------------------------------------
                // Check 1: Does the row have a SetName?  
                XmlNode setNameNode = currentRowNode.SelectSingleNode(colSetName);
                string currentSetName = setNameNode?.InnerText;
                if (!string.IsNullOrEmpty(currentSetName)){

                        // Log that we are processing a set
                        logMessage = "Processing as Set: " + currentSetName;
                        WriteDebugLog(null, logMessage, null, false, 2);

                        // Check 2: Have we already processed this SetName Group?
                        if (!processedSetNames.Contains(currentSetName)){

                                // Log that we are proceeding with processing this set
                                logMessage = "Set not yet processed, proceeding. Will reprocess with sibling rows in manifest.";
                                WriteDebugLog(null, logMessage, null, false, 1);

                                // Build the xpath to find all rows in the manifest with this SetName
                                string manifestSetQuery = "//" + colRowIterator + "[SetName='" + currentSetName + "']";
                                XmlNodeList matchingRows = manifest.SelectNodes(manifestSetQuery);

                                // This variable is helpful for debug only and is not used in a material way in processing, though it is being written to in the nested foreach below.
                                string createdSetNumbers = "";

                                // Log how many rows were found in this set group
                                logMessage = "Found " + matchingRows.Count.ToString() + " Rows in Set Group.";
                                WriteDebugLog(null, logMessage, "Reprocessing as Set", false, 2);

                                // Iterate through each row in the set group
                                foreach (XmlNode groupRow in matchingRows){
                                        int foundItemCount = 0;

                                        if (processedIndexes.Contains(GetRowIndexNumber(groupRow))){
                                                logMessage = "Set row already processed, skipping.";
                                                WriteDebugLog(null, logMessage, "Already Processed", false, 1);
                                                continue;
                                        }

                                        if (!EvaluateManifestCondition(groupRow))
                                        {
                                                logMessage = "Skipping set row because manifest condition evaluated to FALSE.";
                                                WriteDebugLog(null, logMessage, "Skipped", false, 2);
                                                MarkRowProcessed(groupRow);
                                                continue;
                                        }

                                        // Find all nodes in source data that match the current row's SourcePath and make a list out of it since it is possibly a set
                                        string currentSourcePath = groupRow.SelectSingleNode(colXmlSourcePath).InnerText;
                                        string sourceDataQuery = "//" + currentSourcePath;
                                        XmlNodeList sourceNodes = source.SelectNodes(sourceDataQuery);

                                        // We reset this counter for each source fieldName in the set (not for each found instance in the source data) otherwise later data points 
                                        // in the set would be misaligned. Values not found in the source data will get an empty node with the SetNumber
                                        int createdSetNumber = 1;
                                        
                                        // Create new log entry for this row in the set and indicate how many were found
                                        logMessage = "Found " + sourceNodes.Count.ToString() + " Source Items for this Row.";
                                        WriteDebugLog(groupRow, logMessage, null, false, 1);

                                         // Call the helper function to validate target names
                                        // IF it returns false (validation failed), skip this loop iteration.
                                        string targetGroup;
                                        string targetField;
                                        if (!GetTargetInfo(groupRow, colTargetGroup, colTargetField, out targetGroup, out targetField))
                                        {
                                                continue; 
                                        }
                                        
                                        XmlElement newGroupNode = null;
                                        if (sourceNodes.Count > 0)
                                        {
                                        // Since we passed above checks and there is at least one item from the source, determine if group already exists, if not create it (pass to helper function for this)
                                                newGroupNode = GetOrCreateGroupNode(outerGroupNode, targetGroup);        
                                        }

                                        // Write each found node for this SourcePath to the new output variable 'dataToWrite'
                                        foreach (XmlNode sourceNode in sourceNodes){

                                                // Increment found item count
                                                foundItemCount++;

                                                // ---------------------------------------------------------
                                                /* Get the data and check if data needs special handling (date, number, or transformation), and then write it to the new node
                                                 * We do this first because if there is no data, we may need to skip writing the node entirely based on manifest settings*/
                                                // ---------------------------------------------------------
                                                // Pass data to Helper Function to format properly
                                                string sourceNodeData = FormatDataValue(sourceNode.InnerText, groupRow);

                                                //Log number of source elements found
                                                logMessage = "Processing Found Source Item #" + foundItemCount.ToString() + " with Data: " + sourceNodeData;
                                                WriteDebugLog(null, logMessage, null, false, 1);

                                                // Use Helper function to check if we should skip this row based on manifest settings
                                                if (ShouldSkipRow(groupRow, sourceNodeData))
                                                {
                                                        // Log that we are skipping this row based on manifest settings
                                                        logMessage = "----|***INVALID MANIFEST CONFIGURATION*** - Cannot skip individual items in a set. All items in a set must be processed to maintain SetNumber integrity. Writing empty node for this item.";
                                                        WriteDebugLog(null, logMessage, null, false, 3);
                                                }

                                                // Check SetNumber existence in source, and if not in source, ensure that the set number is not already in use by any attribute within the group by the same SetName
                                                string setNumberAttr = sourceNode.Attributes["SetNumber"]?.Value;
                                                bool sourceSetNumberExists = !string.IsNullOrEmpty(setNumberAttr);
                                                string setNumber = !string.IsNullOrEmpty(setNumberAttr) ? setNumberAttr : createdSetNumber.ToString();
                                                int currentSetNumber = int.Parse(setNumber);
                                                if(!sourceSetNumberExists){  
                                                        // Log SetNumber not found
                                                        logMessage = "----|SetNumber not identified in source. Searching for next available number based on both source and target xml...";
                                                        WriteDebugLog(null, logMessage, null, false, 1);

                                                        // If the set number is already in use with that set name in the SOURCE data, OR there is an element in the newGroupNode that already has this setNumber, increment until we find one that is not in use      
                                                        while ((source.SelectNodes("//*[@SetName='" + currentSetName + "' and @SetNumber='" + currentSetNumber + "']").Count > 0) || (newGroupNode.SelectNodes("//MetadataGroup[Name/text()='" + targetGroup + "']/Set/Field[Field/text()='" + targetField + "' and SetNumber/text()='" + currentSetNumber.ToString() + "']").Count > 0))
                                                        {
                                                                createdSetNumber++;
                                                                // Log SetNumber
                                                                logMessage = "----|Trying SetNumber: " + createdSetNumber.ToString();
                                                                WriteDebugLog(null, logMessage, null, false);
                                                                logMessage = "----|FOUND AVAILABLE SETNUMBER: " + createdSetNumber.ToString();
                                                                currentSetNumber = createdSetNumber;
                                                        }
                                                        if(!createdSetNumbers.Contains(currentSetNumber.ToString())){
                                                                createdSetNumbers += "|" + currentSetNumber.ToString() + "|";
                                                        }
                                                }
                                                else{
                                                        // Log that we are using the source SetNumber
                                                        logMessage = "----|FOUND SETNUMBER IN SOURCE " + currentSetNumber.ToString();
                                                }
      
                                                // Log found available SetNumber
                                                WriteDebugLog(null, logMessage, null, false, 1);

                                                // Now that we have an available of found set number, we add the data to the group node, but first we have to check if the set has already been established in this group
                                                CreateFieldSet(newGroupNode, currentSetNumber, targetGroup, targetField, sourceNodeData, currentSetName);                                    
                                        }
                                        // Track completion of processing for this row
                                        string currentGroupRowIndex = GetRowIndexNumber(groupRow);
                                        if (!processedIndexes.Contains(currentGroupRowIndex)){        
                                                processedIndexes += currentGroupRowIndex + "|"; 
                                        }
                                             
                                        // Log outcome of row processing
                                        logMessage = "Completed processing of this row in set.";
                                        WriteDebugLog(null, logMessage, "Success", false, 2);
                                }
                                // Mark this set as processed
                                processedSetNames += "|" + currentSetName + "|";
                        }
                        else{
                                // Log that we are skipping processing this set
                                logMessage = "Set already processed, skipping.";
                                WriteDebugLog(null, logMessage, "Already Processed", false);   
                        }
                }
                // If NOT part of a set, process normally
                else{                        
                        // Log that we are processing a normal (non-set) row
                        logMessage = "Processing as a non-set row.";
                        WriteDebugLog(null, logMessage, null, false, 2); 

                        // Build the source data query, checking if there is a specific XmlSourcePath to use or just the variable name
                        
                        string sourceDataQuery = "//";
                        if (!string.IsNullOrEmpty(currentSourcePathValue)){
                                // it's XML, so use the specific path
                                sourceDataQuery += currentSourcePathValue;

                                // Log that it is an XML source path
                                logMessage = "Determined that this is an XML based row";
                                WriteDebugLog(null, logMessage, null, false, 1);
                        }
                        else if (!string.IsNullOrEmpty(nameOfSourceVariable)){
                                // it's a simple variable, so just use the variable name
                                sourceDataQuery += nameOfSourceVariable;

                                // Log that it is a simple variable
                                logMessage = "Determined that this is a Simple Variable based row";
                                WriteDebugLog(null, logMessage, null, false, 1);
                        }
                        else if(!string.IsNullOrEmpty(defaultValue)){
                                // it's a default value, so just use the value given
                                sourceDataQuery += "DefaultValue";

                                // Log that it is a default value
                                logMessage = "Determined that this is a Default Value row";
                                WriteDebugLog(null, logMessage, null, false, 1);
                        }
                        else{
                                // Log that no source variable was specified
                                logMessage = "No Source data specified, skipping row.";
                                WriteDebugLog(null, logMessage, "Failed", false, 4);
                                continue; // Skip to next iteration
                        }
                        string targetGroup;
                        string targetField;
                        // Call the helper function to validate target names
                        // IF it returns false (validation failed), skip this loop iteration.
                        if (!GetTargetInfo(currentRowNode, colTargetGroup, colTargetField, out targetGroup, out targetField))
                        {
                                continue; 
                        }

                        XmlNode sourceNode = source.SelectSingleNode(sourceDataQuery);

                        // Log found data point
                        logMessage = "Found source data: " + (sourceNode != null ? sourceNode.InnerText : "NULL");
                        WriteDebugLog(null, logMessage, null, false, 1);

                        if (sourceNode != null){
                                // ---------------------------------------------------------
                                // * Get the data and check if data needs special handling (date, number, or transformation), and then write it to the new node
                                // * We do this first because if there is no data, we may need to skip writing the node entirely based on manifest settings
                                // ---------------------------------------------------------
                                // Pass data to Helper Function to format properly
                                string sourceNodeData = FormatDataValue(sourceNode.InnerText, currentRowNode);
                                // Use Helper function to check if we should skip this row based on manifest settings
                                if (ShouldSkipRow(currentRowNode, sourceNodeData))
                                {
                                        // Log that we are skipping this row based on manifest settings
                                        logMessage = "Skipping row based on manifest settings.";
                                        WriteDebugLog(null, logMessage, "Skipped", false, 2);
                                        // Track completion of processing for this row
                                        string skippedRowIndex = GetRowIndexNumber(currentRowNode);
                                        if (!processedIndexes.Contains(skippedRowIndex)){        
                                                processedIndexes += skippedRowIndex + "|"; 
                                        }

                                        // Skip to the next iteration of the loop
                                        continue;
                                }
                                
                                // Since we passed above checks, first Determine if group already exists, if not create it
                                XmlElement newGroupNode = GetOrCreateGroupNode(outerGroupNode, targetGroup);

                                 // Since we passed above checks, first Determine if FIELD SET already exists, if not create it
                                XmlElement newFieldNode = newGroupNode.SelectSingleNode("Set/Field[Field/text()='" + targetField + "']") as XmlElement;
                                if (newFieldNode == null)
                                {
                                        CreateFieldSet(newGroupNode, 0, targetGroup, targetField, sourceNodeData, null);
                                }
                                else{
                                        newFieldNode.SelectSingleNode("Value").InnerText = sourceNodeData;
                                        // Log that the field node already exists and that it was overwritten with new data
                                        logMessage = "----|Field Node " + targetField + " already exists in target XML and was overwritten with new data";
                                        WriteDebugLog(null, logMessage, null, false, 3);
                                }
                        }
                        // Track completion of processing for this row
                        string currentRowIndex = GetRowIndexNumber(currentRowNode);
                        if (!processedIndexes.Contains(currentRowIndex)){        
                                processedIndexes += currentRowIndex + "|"; 
                        }
                        // Log outcome of row processing
                        logMessage = "Completed processing of this row.";
                        WriteDebugLog(null, logMessage, "Success", false, 2);
                }
        }
        // Log final completion message
        logMessage = "Manifest Processing Complete. Processed " + processedIndexes.Split(new char[] {'|'}, StringSplitOptions.RemoveEmptyEntries).Length.ToString() + " Rows Successfully.";
        WriteDebugLog(null, logMessage, null, true, 2);

        
        // ---------------------------------------------------------
        // Helper Function to get row index number
        // ---------------------------------------------------------
        // Calculate Row Number
        string GetRowIndexNumber(XmlNode rowToSearch)
        {
                XmlElement rowNumNode = dataToWrite.CreateElement("RowIndexNumber");
                int index = -1;
                XmlNodeList allRows = manifest.SelectNodes("//" + colRowIterator); 
                for (int i = 0; i < allRows.Count; i++)
                {
                if (allRows[i] == rowToSearch)
                        {
                                index = i;
                                break;
                        }
                }
                return index.ToString();
        }

        // ---------------------------------------------------------
        // Helper Function to mark a row as processed
        // ---------------------------------------------------------
        void MarkRowProcessed(XmlNode rowToMark)
        {
                string rowIndex = GetRowIndexNumber(rowToMark);
                if (!processedIndexes.Contains(rowIndex))
                {
                        processedIndexes += rowIndex + "|";
                }
        }

        // ---------------------------------------------------------
        // Helper Function to evaluate manifest row condition against xWFData
        // ---------------------------------------------------------
        bool EvaluateManifestCondition(XmlNode manifestRow)
        {
                string condition = manifestRow.SelectSingleNode(colCondition)?.InnerText?.Trim();

                if (string.IsNullOrEmpty(condition))
                {
                        return true;
                }

                if (requestNavigator == null)
                {
                        logMessage = "Condition evaluator is unavailable. Skipping row. Condition: " + condition;
                        WriteDebugLog(null, logMessage, null, false, 3);
                        return false;
                }

                try
                {
                        System.Xml.XPath.XPathExpression expression = System.Xml.XPath.XPathExpression.Compile(condition);
                        if (expression.ReturnType != System.Xml.XPath.XPathResultType.Boolean)
                        {
                                logMessage = "Manifest condition must return a boolean. Skipping row. Condition: " + condition;
                                WriteDebugLog(null, logMessage, null, false, 3);
                                return false;
                        }

                        bool conditionPassed = (bool)requestNavigator.Evaluate(expression);
                        logMessage = "Manifest condition evaluated to " + (conditionPassed ? "TRUE" : "FALSE") + ": " + condition;
                        WriteDebugLog(null, logMessage, null, false, 1);
                        return conditionPassed;
                }
                catch (Exception ex)
                {
                        logMessage = "***ERROR EVALUATING MANIFEST CONDITION*** - " + ex.Message + " | Condition: " + condition;
                        WriteDebugLog(null, logMessage, null, false, 4);
                        return false;
                }
        }

        // ---------------------------------------------------------
        // Helper Function to determine if a row should be skipped based on manifest settings
        // ---------------------------------------------------------
        bool ShouldSkipRow(XmlNode Row, string sourceNodeData)
        {
                // 1. Check IgnoreNull
                string ignoreNullStr = Row.SelectSingleNode(colIgnoreNull)?.InnerText;
                bool ignoreNull = string.Equals(ignoreNullStr, "true", StringComparison.OrdinalIgnoreCase);


                if (ignoreNull && string.IsNullOrEmpty(sourceNodeData))
                {
                        // Log that it is skipped due to null
                        logMessage = "Manifest dictates this row be skipped due to null value";
                        WriteDebugLog(null, logMessage, null, false, 2);
                        return true; 
                }

                // 2. Check ValuesToIgnore
                string valuesToIgnore = Row.SelectSingleNode(colValuesToIgnore)?.InnerText;
                if (!string.IsNullOrEmpty(valuesToIgnore))
                {
                        string[] ignoreList = valuesToIgnore.Split(new char[] {'|'}, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string ignoreValue in ignoreList)
                        {
                        // Trim() is safer in case config has spaces like "ValueA | ValueB"
                        if (sourceNodeData.Trim().Equals(ignoreValue.Trim(), StringComparison.OrdinalIgnoreCase))
                                {
                                        // Log that we're skipping due to ignore value match
                                        logMessage = "Manifest dictates this row be skipped due to ignore value (" +ignoreValue.Trim() + ") being present in source data (" + sourceNodeData.Trim() + ")";
                                        WriteDebugLog(null, logMessage, null, false, 2);
                                        return true; 
                                }
                        }
                }

                // 3. Check MustHaveKeywords
                string mustHaveKeywords = Row.SelectSingleNode(colMustHaveKeywords)?.InnerText;
                if (!string.IsNullOrEmpty(mustHaveKeywords))
                {
                        string[] keywordList = mustHaveKeywords.Split(new char[] {'|'}, StringSplitOptions.RemoveEmptyEntries);
                        
                        // If we match ANY one keyword, we are good. If we miss ALL keywords, we skip.
                        foreach (string keyword in keywordList)
                        {
                                if (sourceNodeData.Contains(keyword.Trim()))
                                {
                                        // Log that we found a required keyword
                                        logMessage = "Manifest required keyword (" + keyword.Trim() + ") found in source data (" + sourceNodeData + ")";
                                        WriteDebugLog(null, logMessage, null, false, 2);
                                        return false; 
                                }
                        }
                        // Log that we're skipping because required keywords were not found
                        logMessage = "Manifest dictates this row be skipped due to not matching any required keywords";
                        WriteDebugLog(null, logMessage, null, false, 2);
                        return true; 
                        
                }
                // Log that we are not skipping this row
                logMessage = "Row passed all manifest checks, will not be skipped";;
                WriteDebugLog(null, logMessage, null, false, 1); 
                
                // If we survived all checks, do not skip.
                return false;
        }

        // ---------------------------------------------------------
        // Helper Function to format data values based on manifest row settings
        // ---------------------------------------------------------
        string FormatDataValue(string value, XmlNode node)
        {
                // Safety checks
                if (node == null)
                {
                        logMessage = "----|Manifest rules for formatting is missing. No data formatting performed";
                        WriteDebugLog(null, logMessage, null, false, 3);   
                        return "";
                }
                if (string.IsNullOrEmpty(value)){
                        logMessage = "----|Source string is null. No data formatting performed";
                        WriteDebugLog(null, logMessage, null, false, 1);   
                        return "";
                }

             
                bool isDate = node.SelectSingleNode(colIsDate)?.InnerText?.ToUpper() == "TRUE";
                bool isNumber = node.SelectSingleNode(colIsNumber)?.InnerText?.ToUpper() == "TRUE";
                bool isDecimal = node.SelectSingleNode(colIsDecimal)?.InnerText?.ToUpper() == "TRUE";
                string transformations = node.SelectSingleNode(colTransformations)?.InnerText;  

                // Logic: Date Formatting
                if (isDate)
                {
                        DateTime parsedDate;
                        string[] dateFormats = { "yyyyMMddHHmmss", "MM/dd/yyyy", "yyyy-MM-dd", "MM/dd/yyyy HH:mm:ss" };

                        if (DateTime.TryParseExact(value, dateFormats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out parsedDate))
                        {
                                logMessage = "Successfully parsed and transformed date (" + value + ") to the necessary format (" + parsedDate.ToString("yyyyMMdd") + ").";
                                WriteDebugLog(null, logMessage, null, false, 1);
                                return parsedDate.ToString("MM/dd/yyyy");
                        }
                        else if (DateTime.TryParse(value, out parsedDate))
                        {
                                logMessage = "Successfully parsed and transformed date (" + value + ") to the necessary format (" + parsedDate.ToString("yyyyMMdd") + ").";
                                WriteDebugLog(null, logMessage, null, false, 1);
                                return parsedDate.ToString("MM/dd/yyyy");
                        }
                        else
                        {
                                // Log date transformation failed
                                logMessage = "Failed to parse and transform date (" + value + ") to the necessary format. Write will only be skipped if IgnoreNull = TRUE in manifest.";
                                WriteDebugLog(null, logMessage, null, false, 3);
                                return ""; // Return empty if date parsing failed
                        }
                }
                // Logic: Number Formatting
                else if (isNumber)
                {
                        double parsedNumber;
                        if (double.TryParse(value, out parsedNumber))
                        {
                                 logMessage = "Successfully parsed and transformed number (" + value + ") to the necessary format (" + parsedNumber.ToString("0") + ").";
                                WriteDebugLog(null, logMessage, null, false, 1);
                                return parsedNumber.ToString("0");
                        }
                        else
                        {
                                // Log date transformation failed
                                logMessage = "Failed to parse and transform number (" + value + ") to the necessary format. Write will only be skipped if IgnoreNull = TRUE in manifest.";
                                WriteDebugLog(null, logMessage, null, false, 3);
                                return ""; // Return empty if number parsing failed
                        }
                }
                else if (isDecimal)
                {
                        decimal parsedDecimal;
                        if (decimal.TryParse(value, out parsedDecimal))
                        {
                                logMessage = "Successfully parsed and transformed decimal (" + value + ") to the necessary format (" + parsedDecimal.ToString() + ").";
                                WriteDebugLog(null, logMessage, null, false, 1);
                                return parsedDecimal.ToString();
                        }
                        else
                        {
                                // Log date transformation failed 
                                logMessage = "Failed to parse and transform decimal (" + value + ") to the necessary format. Write will only be skipped if IgnoreNull = TRUE in manifest.";
                                WriteDebugLog(null, logMessage, null, false, 3);
                                return ""; // Return empty if decimal parsing failed
                                
                        }
                }
                // Logic: Transformations
                if (!string.IsNullOrEmpty(transformations))
                {
                        string[] transformationList = transformations.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string transformation in transformationList)
                        {
                           string[] parts = transformation.Split(':');
                                // Ensure we have exactly a Key and a Value
                                if (parts.Length == 2)
                                {
                                        // Trim whitespace just in case the string was "ValueA : ValueB"
                                        string sourceKey = parts[0].Trim();
                                        string targetValue = parts[1].Trim();

                                        // Check if our current data matches the source key
                                        // Using "Equals" allows for Case Insensitive checks
                                        if (value.Equals(sourceKey, StringComparison.OrdinalIgnoreCase))
                                        {
                                                // Log Transformation Occurred
                                                logMessage = "Transformed original value (" + value + ") to new value (" + targetValue + ")";
                                                WriteDebugLog(null, logMessage, null, false, 1);
                                                value = targetValue;
                                                break; // Stop looping once we find the match
                                        }
                                }
                                else
                                {
                                        // Log malformed transformation entry
                                        logMessage = "Malformed transformation entry (" + transformation + ") - skipping this transformation.";
                                        WriteDebugLog(null, logMessage, null, false, 3);
                                }
                        }
                }
                return value;
        }

        // ---------------------------------------------------------
        // Helper Function to validate target group and field names
        // ---------------------------------------------------------
        bool GetTargetInfo(XmlNode targetInfo, string colTargetGroup, string colTargetField, out string targetGroup, out string targetField)
        {
                // 1. Initialize "out" variables to empty
                targetGroup = string.Empty;
                targetField = string.Empty;

                // 2. Extract and clean data
                // (Using ?. to be safe if the node is missing entirely)
                string rawGroup = targetInfo.SelectSingleNode(colTargetGroup)?.InnerText;//.Replace(" ", "_");
                string rawField = targetInfo.SelectSingleNode(colTargetField)?.InnerText;//.Replace(" ", "_");

                // 3. Define Validation Regex
                System.Text.RegularExpressions.Regex validXmlTag = new System.Text.RegularExpressions.Regex(@"^[a-zA-Z_][a-zA-Z0-9_\-\.]*$");

                // 4. Validate
                if (string.IsNullOrEmpty(rawGroup) || 
                        string.IsNullOrEmpty(rawField))
                {
                        // Log the failure immediately
                        logMessage = $"TargetGroup '{rawGroup}' or TargetField '{rawField}' is empty or invalid. Skipping row.";
                        WriteDebugLog(null, logMessage, "Failed", false, 4);
                        
                        return false; // FAIL: Tell the loop to 'continue'
                }

                // 5. Success! Assign values to the "out" parameters
                targetGroup = rawGroup;
                targetField = rawField;
                logMessage = $"TargetGroup '{rawGroup}' and TargetField '{rawField}' are well formed (but cannot check that they actually exist as attributes).";
                WriteDebugLog(null, logMessage, null, false, 1);
                return true; // SUCCESS: Tell the loop to proceed
        }

        // ---------------------------------------------------------
        // Helper Function to Get or Create a MetadataGroup Node
        // ---------------------------------------------------------
        XmlElement GetOrCreateGroupNode(XmlElement outerGroupNode, string targetGroup)
        {
                // 1. Try to find an existing MetadataGroup that has a child <Name> matching our targetGroup
                // We search for the <Name> node, then navigate up to its parent <MetadataGroup>
                XmlNode nameNode = outerGroupNode.SelectSingleNode("MetadataGroup/Name[text()='" + targetGroup + "']");
                
                XmlElement groupNode;

                if (nameNode != null)
                {
                        // FOUND: Get the parent <MetadataGroup> of the found <Name> node
                        groupNode = nameNode.ParentNode as XmlElement;

                        // Log that the group already exists
                        string logMessage = "----|Group already exists: " + targetGroup;
                        WriteDebugLog(null, logMessage, null, false, 1);
                }
                else
                {
                        // NOT FOUND: Create the structure <MetadataGroup><Name>TargetGroup</Name></MetadataGroup>
                        groupNode = outerGroupNode.OwnerDocument.CreateElement("MetadataGroup");
                        outerGroupNode.AppendChild(groupNode);

                        XmlElement nameElement = outerGroupNode.OwnerDocument.CreateElement("Name");
                        nameElement.InnerText = targetGroup;
                        groupNode.AppendChild(nameElement);

                        // Log that we created the group node
                        string logMessage = "----|Created new Group for: " + targetGroup;
                        WriteDebugLog(null, logMessage, null, false, 1);
                }
                return groupNode;
        }


        //----------------------------------------------------------
        // Helper Function to create the field set nodes
        //----------------------------------------------------------
        //Create the <Set> node if it doesn't already exist, and add the set name and set number
        void CreateFieldSet(XmlElement newGroupNode, int currentSetNumber,  string targetGroup, string targetField, string sourceNodeData, string currentSetName)
        {
                XmlElement newSetNode = null;
                if (currentSetName != null){
                        newSetNode = newGroupNode.SelectSingleNode("Set[Name/text()='" + currentSetName + "' and SetNumber/text()='" + currentSetNumber.ToString() + "']") as XmlElement;
                }
                else{
                        newSetNode = newGroupNode.SelectSingleNode("Set[SetNumber/text()='" + currentSetNumber.ToString() + "']") as XmlElement;
                }
                if (newSetNode == null)
                {
                        newSetNode = newGroupNode.OwnerDocument.CreateElement("Set");

                        // Check to see if there is a SetName before creating the node (non repeating fields will not have one)
                        if (!string.IsNullOrEmpty(currentSetName)){
                                XmlElement newSetNameNode = newSetNode.OwnerDocument.CreateElement("Name");
                                newSetNameNode.InnerText = currentSetName;
                                newSetNode.AppendChild(newSetNameNode);

                                // Log that we created the set node
                                logMessage = "----|CREATED new SetName Node for SetName: " + currentSetName;
                                WriteDebugLog(null, logMessage, null, false, 1);
                        }

                        //Create the set number node (which will be "0" if non repeating)
                        XmlElement newSetNumberNode = newSetNode.OwnerDocument.CreateElement("SetNumber");
                        newSetNumberNode.InnerText = currentSetNumber.ToString();
                        newSetNode.AppendChild(newSetNumberNode);

                        // Log that we created the set node
                        logMessage = "----|CREATED new SetNumber node for Set Number: " + currentSetNumber.ToString();
                        WriteDebugLog(null, logMessage, null, false, 1);
                }
                else{
                        // Log that the set node already exists
                        logMessage = "----|Set Node for SetName: " + currentSetName + " and SetNumber: " + currentSetNumber.ToString() + " ALREADY EXISTS in target XML. Using that.";
                        WriteDebugLog(null, logMessage, null, false, 1);
                }
                //If this group isn't already appended to the outerGroupNode, do so now
                if (newSetNode.ParentNode == null)
                {
                        newGroupNode.AppendChild(newSetNode);
                }
                

                // Then create the <Field> to append to the set group by adding the Group, Field Name, Value, and Set Number information
                XmlElement newFieldNode = newSetNode.OwnerDocument.CreateElement("Field");
                XmlElement newFieldNameNode = newFieldNode.OwnerDocument.CreateElement("Field");
                newFieldNameNode.InnerText = targetField;
                newFieldNode.AppendChild(newFieldNameNode);
                XmlElement GroupNameNode = newFieldNode.OwnerDocument.CreateElement("Group");
                GroupNameNode.InnerText = targetGroup;
                newFieldNode.AppendChild(GroupNameNode);
                XmlElement newFieldValueNode = newFieldNode.OwnerDocument.CreateElement("Value");
                newFieldValueNode.InnerText = sourceNodeData;
                newFieldNode.AppendChild(newFieldValueNode);
                if(!String.IsNullOrEmpty(currentSetName)){
                        XmlElement newSetNameFieldNode = newFieldNode.OwnerDocument.CreateElement("SetName");
                        newSetNameFieldNode.InnerText = currentSetName;
                        newFieldNode.AppendChild(newSetNameFieldNode);
                }
                XmlElement newSetNumberFieldNode = newFieldNode.OwnerDocument.CreateElement("SetNumber");
                newSetNumberFieldNode.InnerText = currentSetNumber.ToString();
                newFieldNode.AppendChild(newSetNumberFieldNode);
                newSetNode.AppendChild(newFieldNode);                                             
                newGroupNode.AppendChild(newSetNode);

                // Log that we created the field node
                logMessage = "----|Created new Field Set Node for: " + targetField;
                WriteDebugLog(null, logMessage, null, false, 1);
        }
         
        // ---------------------------------------------------------
        // Helper Function for debug messages
        // ---------------------------------------------------------
        void WriteDebugLog(XmlNode rowToStart = null, string message = null, string outcome = null, bool isAdHoc = false, int level = 0)
        {
                if (minLevelToInclude == "NONE" || string.IsNullOrEmpty(minLevelToInclude) || minLevelToInclude=="") return;
                
                int threshold = int.Parse(minLevelToInclude);

                string logLevel;
                // ---------------------------------------------------------
                // 1. DETERMINE IF WE NEED A NEW ENTRY
                // ---------------------------------------------------------
                // Scenario A: Starting a standard Row Context
                if (rowToStart != null)
                {
                        XmlElement logEntry = dataToWrite.CreateElement("LogEntry");

                        // Import the Row structure
                        XmlNode importedRow = dataToWrite.ImportNode(rowToStart, true);
                        logEntry.AppendChild(importedRow);

                        // Calculate Row Number
                        string rowIndexNumber = GetRowIndexNumber(rowToStart);
                        XmlElement rowNumNode = dataToWrite.CreateElement("RowIndexNumber");
                        rowNumNode.InnerText = rowIndexNumber;
                        logEntry.AppendChild(rowNumNode);

                        // Attach to main log
                        errorLogNode.AppendChild(logEntry);
                }
                // Scenario B: Starting an Ad Hoc (Standalone) Context
                else if (isAdHoc)
                {
                        // Just create a blank container so the message has somewhere to live
                        XmlElement logEntry = dataToWrite.CreateElement("LogEntry");
                        errorLogNode.AppendChild(logEntry);
                }
                // ---------------------------------------------------------
                // 2. ADD MESSAGE
                // ---------------------------------------------------------
                if (!string.IsNullOrEmpty(message))
                {
                        if (level >= threshold)
                        {
                                // Safety: If you try to log a message but no entry exists at all yet, create one
                                if (errorLogNode.LastChild == null)
                                {
                                        XmlElement safetyEntry = dataToWrite.CreateElement("LogEntry");
                                        errorLogNode.AppendChild(safetyEntry);
                                }
                                // Always write to the most recent entry (whether it's the Row we just made, the AdHoc we just made, or an existing one)
                                switch (level)
                                {
                                        case 1:
                                                logLevel = "DEBUG";
                                                break;
                                        case 2:
                                                logLevel = "INFO";
                                                break;
                                        case 3:
                                                logLevel = "WARNING";
                                                break;
                                        case 4:
                                                logLevel = "ERROR";
                                                break;
                                        default:
                                                logLevel = "TRACE";
                                                break;
                                }
                                
                                XmlElement errorNode = dataToWrite.CreateElement(logLevel);
                                errorNode.InnerText = message;
                                errorLogNode.LastChild.AppendChild(errorNode);
                        }
                }
                // ---------------------------------------------------------
                // 3. ADD OUTCOME
                // ---------------------------------------------------------
                if (!string.IsNullOrEmpty(outcome))
                {
                        if (errorLogNode.LastChild != null)
                        {
                        XmlElement outcomeNode = dataToWrite.CreateElement("Outcome");
                        outcomeNode.InnerText = outcome;
                        errorLogNode.LastChild.AppendChild(outcomeNode);
                        }
                } 
        }

        // =========================================================
        // DATA LOADING BLOCK
        // =========================================================
        // --- LOCAL TESTING (Comment out for CLM USE) ---
        Console.WriteLine(dataToWrite.OuterXml);
        
        // Save to XML file in SampleData folder
        string outputPath = "SampleData/OutputMetadataXml.xml";
        dataToWrite.Save(outputPath);
        Console.WriteLine($"\nXML saved to: {outputPath}");
        

        // --- CLM USE (Uncomment for CLM USE) ---
        //return dataToWrite.OuterXml;
        
        // =========================================================
    }
}