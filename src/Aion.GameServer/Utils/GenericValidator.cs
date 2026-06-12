using System.Collections;

namespace Aion.GameServer.Utils;

/// <summary>
/// Faithful-infra shim for Apache commons-validator GenericValidator's blank/null checks.
/// Java parity: org.apache.commons.validator.GenericValidator.isBlankOrNull; the reworked call sites
/// also use it on collections, so an IEnumerable overload is provided (empty-or-null).
/// </summary>
public static class GenericValidator
{
    public static bool IsBlankOrNull(string? value) => string.IsNullOrWhiteSpace(value);

    public static bool IsBlankOrNull(IEnumerable? value)
    {
        if (value == null)
            return true;
        foreach (var _ in value)
            return false;
        return true;
    }

    // camelCase aliases for call sites transcribed literally from Java.
    public static bool isBlankOrNull(string? value) => IsBlankOrNull(value);
    public static bool isBlankOrNull(IEnumerable? value) => IsBlankOrNull(value);
}
