using System;

namespace Aion.GameServer.Services.Mail;

/// <summary>Java parity: services/mail/AbyssSiegeLevel. id==ordinal→GetId()=(int)t.</summary>
public enum AbyssSiegeLevel
{
    NONE,
    HERO_DECORATION,
    MEDAL,
    ELITE_SOLDIER,
    VETERAN_SOLDIER
}

public static class AbyssSiegeLevelExtensions
{
    public static int GetId(this AbyssSiegeLevel l) => (int) l;

    public static AbyssSiegeLevel GetLevelById(int id)
    {
        foreach (AbyssSiegeLevel al in Enum.GetValues(typeof(AbyssSiegeLevel)))
        {
            if (al.GetId() == id)
            {
                return al;
            }
        }
        throw new ArgumentException("There is no AbyssSiegeLevel with ID " + id);
    }
}
