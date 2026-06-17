using System;
using System.Collections.Generic;
using System.Text;

namespace Aion.Commons.Configuration.Transformers;

/// <summary>
/// Java parity: transformers/CommaSeparatedValueTransformer (Neon). Splits on commas outside double-quotes,
/// trims each token, and strips a surrounding quote pair. Subclasses convert the resulting token list.
/// </summary>
public abstract class CommaSeparatedValueTransformer : PropertyTransformer
{
    protected sealed override object? ParseObject(string value, Type type)
    {
        return ParseObject(SplitAndTrimValues(value), type);
    }

    protected abstract object? ParseObject(List<string> values, Type type);

    /// <summary>
    /// Java parity: splitAndTrimValues. Splits on every comma outside quotes; trims; removes a matching
    /// leading+trailing quote pair. Empty final token is dropped.
    /// </summary>
    protected List<string> SplitAndTrimValues(string value)
    {
        var tokensList = new List<string>();
        bool inQuotes = false;
        var b = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            switch (c)
            {
                case ',':
                    if (inQuotes)
                        break;
                    tokensList.Add(Trim(b));
                    b.Length = 0;
                    continue;
                case '"':
                    inQuotes = !inQuotes;
                    break;
            }
            b.Append(c);
        }
        string lastValue = Trim(b);
        if (lastValue.Length != 0)
            tokensList.Add(lastValue);
        return tokensList;
    }

    private static string Trim(StringBuilder input)
    {
        string output = input.ToString().Trim();
        if (output.Length > 1 && output[0] == '"' && output[output.Length - 1] == '"')
            output = output.Substring(1, output.Length - 2);
        return output;
    }
}
