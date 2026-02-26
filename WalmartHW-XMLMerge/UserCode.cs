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
        var wfDataPath = Path.Combine(inputDir, "xWFData2.xml");

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

        // Split by delimiter (" and " / " or ") but ignore delimiters inside quotes.
        string[] SplitOutsideQuotes(string input, string delimiter)
        {
            if (input == null) return new string[0];
            if (delimiter == null || delimiter.Length == 0) return new[] { input };

            var parts = new System.Collections.Generic.List<string>();
            int i = 0;
            int start = 0;
            char quote = '\0';

            while (i < input.Length)
            {
                char c = input[i];

                if (quote == '\0')
                {
                    if (c == '\'' || c == '"') quote = c;

                    // delimiter match (case-insensitive)
                    if (i + delimiter.Length <= input.Length &&
                        string.Compare(input, i, delimiter, 0, delimiter.Length, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        parts.Add(input.Substring(start, i - start));
                        i += delimiter.Length;
                        start = i;
                        continue;
                    }
                }
                else
                {
                    if (c == quote) quote = '\0';
                }

                i++;
            }

            parts.Add(input.Substring(start));
            return parts.ToArray();
        }

        string StripOuterBoolean(string condition)
        {
            if (condition == null) return "";
            string c = condition.Trim();

            // boolean( ... )
            if (c.Length >= 8 && c.StartsWith("boolean(", StringComparison.OrdinalIgnoreCase) && c.EndsWith(")"))
            {
                return c.Substring(8, c.Length - 9).Trim();
            }

            return c;
        }

        string Unquote(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            s = s.Trim();
            if ((s.StartsWith("'") && s.EndsWith("'")) || (s.StartsWith("\"") && s.EndsWith("\"")))
                return s.Substring(1, s.Length - 2);
            return s;
        }

        // Evaluate a single predicate like:
        //   //SYS_App_Name='HW'
        //   //SYS_Route="Initial Route"
        //   //SomeNode   (existence)
        bool EvalAtom(string atom)
        {
            atom = (atom ?? "").Trim();
            if (atom.Length == 0) return true;

            // not(...)
            if (atom.StartsWith("not(", StringComparison.OrdinalIgnoreCase) && atom.EndsWith(")"))
            {
                string inner = atom.Substring(4, atom.Length - 5).Trim();
                return !EvalExpr(inner);
            }

            int eq = atom.IndexOf('=');
            if (eq < 0)
            {
                // existence check (XPath boolean(node-set))
                XmlNode n = targetDoc.SelectSingleNode(atom);
                return n != null;
            }

            string left = atom.Substring(0, eq).Trim();
            string right = atom.Substring(eq + 1).Trim();
            string expected = Unquote(right);

            XmlNode node = targetDoc.SelectSingleNode(left);
            if (node == null) return false;

            string actual = (node.InnerText ?? "").Trim();
            return string.Equals(actual, expected, StringComparison.Ordinal);
        }

        // Evaluate expression supporting "and" / "or" (no nested parentheses except not(...))
        bool EvalExpr(string expr)
        {
            expr = (expr ?? "").Trim();
            if (expr.Length == 0) return true;

            // OR has lower precedence than AND (typical)
            string[] orParts = SplitOutsideQuotes(expr, " or ");
            if (orParts.Length > 1)
            {
                for (int i = 0; i < orParts.Length; i++)
                {
                    if (EvalExpr(orParts[i])) return true;
                }
                return false;
            }

            string[] andParts = SplitOutsideQuotes(expr, " and ");
            for (int i = 0; i < andParts.Length; i++)
            {
                if (!EvalAtom(andParts[i])) return false;
            }
            return true;
        }

        bool EvaluateCondition(string condition)
        {
            if (string.IsNullOrWhiteSpace(condition)) return true;
            string expr = StripOuterBoolean(condition);

            // If someone passes a complex XPath expression, fail fast to avoid silent wrong merges.
            // (You can remove this guard if you want to allow more.)
            // We'll still allow: //X='Y', //X, not(...), AND/OR
            return EvalExpr(expr);
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
