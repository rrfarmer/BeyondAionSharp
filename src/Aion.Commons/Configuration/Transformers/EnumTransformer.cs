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
        // Enum.Parse also accepts numeric underlying values (and comma-separated flag values), while Java
        // Enum.valueOf accepts one exact declared name only. Guard the name before delegating to Parse.
        if (Array.IndexOf(Enum.GetNames(t), value) < 0)
            throw new ArgumentException($"No enum constant {t.FullName}.{value}");
        return Enum.Parse(t, value, ignoreCase: false);
    }
}
