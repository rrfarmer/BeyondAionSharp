namespace Aion.GameServer.Utils;

/// <summary>
/// Java <c>Enum.valueOf</c> semantics: exact, case-sensitive constant names only.
/// Unlike .NET <see cref="Enum.Parse{TEnum}(string)"/>, numeric strings and comma-separated
/// combinations are never accepted.
/// </summary>
internal static class JavaEnum
{
    internal static TEnum ValueOf<TEnum>(string value) where TEnum : struct, Enum
    {
        if (value is null)
            throw new NullReferenceException("Name is null");

        if (TryValueOf(value, out TEnum result))
            return result;

        throw new ArgumentException($"No enum constant {typeof(TEnum).FullName}.{value}");
    }

    internal static bool TryValueOf<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum
    {
        if (value is not null)
        {
            foreach (string name in Enum.GetNames<TEnum>())
            {
                if (string.Equals(name, value, StringComparison.Ordinal))
                {
                    result = Enum.Parse<TEnum>(name);
                    return true;
                }
            }
        }

        result = default;
        return false;
    }
}
