namespace AiPromptEvaluator;

/// <summary>Minimal RFC-4180 CSV parser that handles quoted multi-line fields.</summary>
public static class CsvParser
{
    public static List<List<string>> Parse(string text)
    {
        var rows = new List<List<string>>();
        var fields = new List<string>();
        var field = new System.Text.StringBuilder();
        bool inQuotes = false;
        int i = 0;

        while (i < text.Length)
        {
            var ch = text[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    // Escaped quote ""
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                        continue;
                    }
                    inQuotes = false;
                    i++;
                    continue;
                }
                field.Append(ch);
                i++;
                continue;
            }

            if (ch == '"')
            {
                inQuotes = true;
                i++;
                continue;
            }

            if (ch == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
                i++;
                continue;
            }

            if (ch == '\r' || ch == '\n')
            {
                fields.Add(field.ToString());
                field.Clear();
                rows.Add(fields);
                fields = new List<string>();
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }
                i++;
                continue;
            }

            field.Append(ch);
            i++;
        }

        // last field / row
        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            rows.Add(fields);
        }

        return rows;
    }
}
