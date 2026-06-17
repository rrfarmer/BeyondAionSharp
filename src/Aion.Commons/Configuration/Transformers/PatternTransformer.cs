using System;
using System.Text.RegularExpressions;

namespace Aion.Commons.Configuration.Transformers;

/// <summary>Java parity: transformers/PatternTransformer (SoulKeeper). Compiles the value to a Regex; empty -> null.</summary>
public sealed class PatternTransformer : PropertyTransformer
{
    public override bool Matches(Type targetType) => targetType == typeof(Regex);

    protected override object? ParseObject(string value, Type type)
        => value.Length == 0 ? null : new Regex(value);
}
