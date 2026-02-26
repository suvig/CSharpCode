using System.Xml;

internal static class UserCode
{
    // Paste your code into this method (or call into other files/classes you add).
    // Run it from the WalmartHW-XMLMerge folder with:
    //   dotnet run
    public static Task<string> Run(string[] args)
    {
        // Example: load the input XML files using paths relative to WalmartHW-XMLMerge/.
        var inputDir = Path.Combine("Input");

        var mergeRulesPath = Path.Combine(inputDir, "dMergeRules.xml");
        var validationPath = Path.Combine(inputDir, "dDataValidation.xml");
        var wfDataPath = Path.Combine(inputDir, "xWFData.xml");

        Console.WriteLine($"Merge rules:      {mergeRulesPath}");
        Console.WriteLine($"Data validation:  {validationPath}");
        Console.WriteLine($"WF data:          {wfDataPath}");
        Console.WriteLine();

        var mergeRules = new XmlDocument { PreserveWhitespace = true };
        mergeRules.Load(mergeRulesPath);

        var sourceRawDoc = new XmlDocument { PreserveWhitespace = true };
        sourceRawDoc.Load(validationPath);

        var targetDoc = new XmlDocument { PreserveWhitespace = true };
        targetDoc.Load(wfDataPath);

        XmlNodeList mergeRulesList = mergeRules.SelectNodes("//Row");

        /*
        //
        // TARGET (master) = xWFData
        //
        var xWFData = _context.XmlVariables["xWFData"].GetXmlNode("//*").OuterXml;

        //
        // SOURCE is the output of the DocGen Step
        //
        var xMergeSourceRaw = _context.XmlVariables["dDataValidation"].GetXmlNode("//*").OuterXml;

        //
        // MERGE RULES (CSV -> Rows/Row)
        //
        XmlNodeList mergeRulesList = _context.XmlVariables["dMergeRules"].GetXmlNodes("//Row");

        var targetDoc = new XmlDocument { PreserveWhitespace = true };
        targetDoc.LoadXml(xWFData);

        var sourceRawDoc = new XmlDocument { PreserveWhitespace = true };
        sourceRawDoc.LoadXml(xMergeSourceRaw);
        */
        // Only Params/TemplateFieldData is in-scope
        XmlNode sourceTemplateFieldData = sourceRawDoc.SelectSingleNode("//TemplateFieldData");
        if (sourceTemplateFieldData == null)
            throw new Exception("Source XML does not contain //TemplateFieldData.");

        var sourceDoc = new XmlDocument { PreserveWhitespace = true };
        sourceDoc.LoadXml(sourceTemplateFieldData.OuterXml);

        string GetRowValue(XmlNode row, string nodeName)
        {
            XmlNode n = row.SelectSingleNode(nodeName);
            return n == null ? "" : (n.InnerText ?? "").Trim();
        }

        XmlNode SelectSingleOrError(XmlDocument doc, string xpath, string label, bool skipIfMissing)
        {
            if (string.IsNullOrWhiteSpace(xpath))
                throw new Exception(label + " XPath is blank.");

            XmlNodeList nodes = doc.SelectNodes(xpath);
            int count = (nodes == null) ? 0 : nodes.Count;

            if (count == 0)
            {
                if (skipIfMissing) return null;
                throw new Exception(label + " XPath matched 0 nodes: " + xpath);
            }

            if (count > 1)
                throw new Exception(label + " XPath matched " + count + " nodes (must be exactly 1): " + xpath);

            return nodes[0];
        }

        XmlNode SelectZeroOrOneOrError(XmlDocument doc, string xpath, string label)
        {
            if (string.IsNullOrWhiteSpace(xpath))
                throw new Exception(label + " XPath is blank.");

            XmlNodeList nodes = doc.SelectNodes(xpath);
            int count = (nodes == null) ? 0 : nodes.Count;

            if (count == 0) return null;

            if (count > 1)
                throw new Exception(label + " XPath matched " + count + " nodes (must be 0 or 1): " + xpath);

            return nodes[0];
        }

        bool EvaluateCondition(string condition)
        {
            if (string.IsNullOrWhiteSpace(condition)) return true;
            var nav = targetDoc.CreateNavigator();
            var expr = System.Xml.XPath.XPathExpression.Compile(condition.Trim());
            var result = nav.Evaluate(expr);
            if (result is bool b) return b;

            throw new Exception("Condition must evaluate to a boolean.");
        }

        // =====================
        // Merge Execution (top-down)
        // =====================
        foreach (XmlNode rule in mergeRulesList)
        {
            string condition = GetRowValue(rule, "Condition");
            if (!EvaluateCondition(condition))
                continue;

            string actionRaw = GetRowValue(rule, "Action");
            string sourceXPath = GetRowValue(rule, "SourceNode");
            string targetXPath = GetRowValue(rule, "TargetNode");

            if (string.IsNullOrWhiteSpace(actionRaw))
                throw new Exception("Merge rule Action is blank.");

            string action = actionRaw.Trim();

            // Backward-compatible aliases
            if (action.Equals("UpdateTree", StringComparison.OrdinalIgnoreCase)) action = "ReplaceTree";
            if (action.Equals("Update", StringComparison.OrdinalIgnoreCase)) action = "ReplaceTree";

            if (action.Equals("Add", StringComparison.OrdinalIgnoreCase))
            {
                // Source missing -> skip
                XmlNode src = SelectSingleOrError(sourceDoc, sourceXPath, "SourceNode", skipIfMissing: true);
                if (src == null) continue;

                // Target parent must exist (unique)
                XmlNode tgtParent = SelectSingleOrError(targetDoc, targetXPath, "TargetNode", skipIfMissing: false);

                XmlNode imported = targetDoc.ImportNode(src, true);
                tgtParent.AppendChild(imported); // add to end
            }
            else if (action.Equals("UpsertTree", StringComparison.OrdinalIgnoreCase))
            {
                // Source missing -> skip
                XmlNode src = SelectSingleOrError(sourceDoc, sourceXPath, "SourceNode", skipIfMissing: true);
                if (src == null) continue;

                XmlNode existing = SelectZeroOrOneOrError(targetDoc, targetXPath, "TargetNode");
                XmlNode imported = targetDoc.ImportNode(src, true);

                if (existing != null)
                {
                    XmlNode parent = existing.ParentNode;
                    if (parent == null)
                        throw new Exception("TargetNode has no parent; cannot UpsertTree replace.");

                    parent.ReplaceChild(imported, existing);
                }
                else
                {
                    string targetParentXPath = GetRowValue(rule, "TargetParent");
                    if (string.IsNullOrWhiteSpace(targetParentXPath))
                        throw new Exception("UpsertTree insert requires TargetParent when TargetNode does not exist.");

                    XmlNode tgtParent = SelectSingleOrError(targetDoc, targetParentXPath, "TargetParent", skipIfMissing: false);

                    string insertBeforeXPath = GetRowValue(rule, "InsertBefore");
                    if (!string.IsNullOrWhiteSpace(insertBeforeXPath))
                    {
                        XmlNode anchor = SelectSingleOrError(targetDoc, insertBeforeXPath, "InsertBefore", skipIfMissing: false);
                        if (!ReferenceEquals(anchor.ParentNode, tgtParent))
                            throw new Exception("InsertBefore node is not a child of TargetParent.");

                        tgtParent.InsertBefore(imported, anchor);
                    }
                    else
                    {
                        tgtParent.AppendChild(imported);
                    }
                }
            }
            else if (action.Equals("ReplaceTree", StringComparison.OrdinalIgnoreCase))
            {
                // Source missing -> skip
                XmlNode src = SelectSingleOrError(sourceDoc, sourceXPath, "SourceNode", skipIfMissing: true);
                if (src == null) continue;

                // Target missing -> skip (only Add creates)
                XmlNode tgt = SelectSingleOrError(targetDoc, targetXPath, "TargetNode", skipIfMissing: true);
                if (tgt == null) continue;

                XmlNode imported = targetDoc.ImportNode(src, true);

                XmlNode parent = tgt.ParentNode;
                if (parent == null)
                    throw new Exception("TargetNode has no parent; cannot ReplaceTree.");

                parent.ReplaceChild(imported, tgt);
            }
            else if (action.Equals("SetValue", StringComparison.OrdinalIgnoreCase))
            {
                // Source missing -> skip
                XmlNode src = SelectSingleOrError(sourceDoc, sourceXPath, "SourceNode", skipIfMissing: true);
                if (src == null) continue;

                // Target missing -> skip
                XmlNode tgt = SelectSingleOrError(targetDoc, targetXPath, "TargetNode", skipIfMissing: true);
                if (tgt == null) continue;

                tgt.InnerText = src.InnerText;
            }
            else if (action.Equals("Delete", StringComparison.OrdinalIgnoreCase))
            {
                // Delete uses TargetNode if provided, else SourceNode as the path-to-delete in target
                string deleteXPath = !string.IsNullOrWhiteSpace(targetXPath) ? targetXPath : sourceXPath;

                // Missing -> skip
                XmlNode tgt = SelectSingleOrError(targetDoc, deleteXPath, "Delete XPath", skipIfMissing: true);
                if (tgt == null) continue;

                XmlNode parent = tgt.ParentNode;
                if (parent != null)
                    parent.RemoveChild(tgt);
            }
            else
            {
                throw new Exception("Unsupported merge Action: " + actionRaw);
            }
        }

        // Return merged xWFData XML
        return Task.FromResult(targetDoc.OuterXml);
    }
}
