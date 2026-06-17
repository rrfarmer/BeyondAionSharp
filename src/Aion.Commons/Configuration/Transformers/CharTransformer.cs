using System;

namespace Aion.Commons.Configuration.Transformers;

/// <summary>Java parity: transformers/CharTransformer. Single-character value; empty or multi-char throws.</summary>
public sealed class CharTransformer : PropertyTransformer
{
    public override bool Matches(Type targetType) => targetType == typeof(char);

    protected override object? ParseObject(string value, Type type)
    {
        if (value.Length == 0)
            throw new ArgumentException("Cannot convert empty string to character.");
        if (value.Length > 1)
            throw new ArgumentException("Too many characters in the value.");
        return value[0];
    }
}
