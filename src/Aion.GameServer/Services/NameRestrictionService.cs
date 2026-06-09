using System;
using System.Text.RegularExpressions;
using Aion.GameServer.Configs.Main;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/NameRestrictionService (nrg, Neon).</summary>
public class NameRestrictionService
{
    public static bool IsValidName(string name)
    {
        return Matches(NameConfig.CHAR_NAME_PATTERN, name);
    }

    public static bool IsValidPetName(string name)
    {
        return Matches(NameConfig.PET_NAME_PATTERN, name);
    }

    public static bool IsValidLegionName(string name)
    {
        return Matches(LegionConfig.LEGION_NAME_PATTERN, name);
    }

    public static bool IsForbidden(string name)
    {
        return ContainsForbiddenSequence(name) || IsForbiddenWord(name);
    }

    private static bool ContainsForbiddenSequence(string name)
    {
        if (NameConfig.FORBIDDEN_SEQUENCE_PATTERN == null)
            return false;

        return NameConfig.FORBIDDEN_SEQUENCE_PATTERN.IsMatch(name);
    }

    public static bool IsForbiddenWord(string @string)
    {
        foreach (string s in NameConfig.FORBIDDEN_WORDS)
        {
            if (string.Equals(@string, s, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <returns>The filtered chat message (forbidden words are replaced by *'s)</returns>
    public static string FilterMessage(string message)
    {
        foreach (string word in message.Split(' '))
        {
            if (IsForbiddenWord(word))
                message = message.Replace(word, new string('*', word.Length));
        }
        return message;
    }

    // Java parity: Pattern.matcher(s).matches() requires the entire input to match (unlike Regex.IsMatch which is a partial find).
    private static bool Matches(Regex pattern, string input)
    {
        Match m = pattern.Match(input);
        return m.Success && m.Index == 0 && m.Length == input.Length;
    }
}
