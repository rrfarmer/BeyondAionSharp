using System;

namespace Aion.Commons.Configuration.Transformers;

/// <summary>Java parity: transformers/StringTransformer (SoulKeeper). Identity transform for string fields.</summary>
public sealed class StringTransformer : PropertyTransformer
{
    public override bool Matches(Type targetType) => targetType == typeof(string);

    protected override object? ParseObject(string value, Type type) => value;
}
