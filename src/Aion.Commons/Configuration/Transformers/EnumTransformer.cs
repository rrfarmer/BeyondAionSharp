using System;

namespace Aion.Commons.Configuration.Transformers;

/// <summary>
/// Java parity: transformers/EnumTransformer (SoulKeeper). Case-sensitive name match (Java Enum.valueOf); empty
/// value yields null (only meaningful for nullable enum members).
/// </summary>
public sealed class EnumTransformer : PropertyTransformer
{
    public override bool Matches(Type targetType)
    {
        var t = Nullable.GetUnderlyingType(targetType) ?? targetType;
        return t.IsEnum;
    }

    protected override object? ParseObject(string value, Type type)
    {
        if (value.Length == 0)
            return null;
        var t = Nullable.GetUnderlyingType(type) ?? type;
        // caseSensitive: true mirrors Java Enum.valueOf (throws if the name does not match exactly).
        return Enum.Parse(t, value, ignoreCase: false);
    }
}
