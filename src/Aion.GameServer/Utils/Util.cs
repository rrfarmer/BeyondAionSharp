using Aion.GameServer.Configs.Main;

namespace Aion.GameServer.Utils;

/// <summary>Java parity: utils/Util (-Nemesiss-).</summary>
public class Util
{
    /// <summary>Converts name to valid pattern, e.g. "atracer" → "Atracer".</summary>
    public static string ConvertName(string name)
    {
        if (name.Length != 0)
        {
            if (NameConfig.ALLOW_CUSTOM_NAMES)
                return name;
            else
                return name.Substring(0, 1).ToUpper() + name.ToLower().Substring(1);
        }
        else
            return "";
    }
}
