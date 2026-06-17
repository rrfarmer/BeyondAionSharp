using System;

namespace Aion.Commons.Configuration.Transformers;

/// <summary>
/// Java parity: transformers/BooleanTransformer (SoulKeeper). Boolean is true/false (case-insensitive) or 1/0;
/// anything else throws (mirrors Java's deliberate avoidance of Boolean.parseBoolean's silent false).
/// </summary>
public sealed class BooleanTransformer : PropertyTransformer
{
    public override bool Matches(Type targetType) => targetType == typeof(bool);

    protected override object? ParseObject(string value, Type type)
    {
        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1")
            return true;
        if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) || value == "0")
            return false;
        throw new ArgumentException("Only true, false, 1 and 0 are allowed.");
    }
}
