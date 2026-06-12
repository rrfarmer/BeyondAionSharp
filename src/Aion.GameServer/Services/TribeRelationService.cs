using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Spawns.Basespawns;
using Aion.GameServer.Services.Panesterra.Ahserion;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/TribeRelationService (Cheatkiller). Tribe-relation predicates. Java enum switch (bare case)→C# enum-qualified case labels (TribeClass.*); instanceof X x→is X x; enum.name().startsWith→ToString().StartsWith; getTribe()→GetTribe(). TribeClass/Creature/DataManager red-tolerated.</summary>
public class TribeRelationService
{
    public static bool IsAggressive(Creature creature1, Creature creature2)
    {
        switch (creature1.GetTribe())
        {
            case TribeClass.AGGRESSIVESINGLEMONSTER:
                if (creature2.GetTribe() == TribeClass.YUN_GUARD)
                    return true;
                break;
            case TribeClass.IDF5U2_SHULACK:
                if (creature2.GetTribe() == TribeClass.FIELD_OBJECT_ALL_HOSTILEMONSTER)
                    return false;
                break;
        }
        switch (creature1.GetBaseTribe())
        {
            case TribeClass.GUARD_DARK:
                switch (creature2.GetBaseTribe())
                {
                    case TribeClass.PC:
                    case TribeClass.GUARD:
                    case TribeClass.GENERAL:
                    case TribeClass.GUARD_DRAGON:
                        return true;
                }
                break;
            case TribeClass.GUARD:
                switch (creature2.GetBaseTribe())
                {
                    case TribeClass.PC_DARK:
                    case TribeClass.GUARD_DARK:
                    case TribeClass.GENERAL_DARK:
                    case TribeClass.GUARD_DRAGON:
                        return true;
                }
                break;
            case TribeClass.GUARD_DRAGON:
                switch (creature2.GetBaseTribe())
                {
                    case TribeClass.PC_DARK:
                    case TribeClass.PC:
                    case TribeClass.GUARD:
                    case TribeClass.GUARD_DARK:
                    case TribeClass.GENERAL_DARK:
                    case TribeClass.GENERAL:
                        return true;
                }
                break;
        }
        if (creature2 is Player p && p.GetPanesterraFaction() != null && creature1.GetTribe().ToString().StartsWith("GAB1_"))
        {
            if (creature1.GetTribe() == p.GetPanesterraFaction().GetTribe())
                return false;
            return DataManager.TRIBE_RELATIONS_DATA.IsAggressiveRelation(creature1.GetTribe(), p.GetPanesterraFaction().GetTribe());
        }

        return DataManager.TRIBE_RELATIONS_DATA.IsAggressiveRelation(creature1.GetTribe(), creature2.GetTribe());
    }

    public static bool IsFriend(Creature creature1, Creature creature2)
    {
        if (creature1.GetTribe() == creature2.GetTribe()) // OR BASE ????
            return true;
        if (creature1.GetTribe() == TribeClass.IDF5U2_SHULACK && creature2.GetTribe() == TribeClass.FIELD_OBJECT_ALL_HOSTILEMONSTER)
            return true;
        switch (creature1.GetBaseTribe())
        {
            case TribeClass.USEALL:
            case TribeClass.FIELD_OBJECT_ALL:
                return true;
            case TribeClass.GENERAL_DARK:
                if (creature1.GetTribe() != TribeClass.DRAMA_EVE_NONPC_DARKA && creature1.GetTribe() != TribeClass.DRAMA_EVE_NONPC_DARKB)
                {
                    switch (creature2.GetBaseTribe())
                    {
                        case TribeClass.PC_DARK:
                        case TribeClass.GUARD_DARK:
                            return true;
                    }
                }
                break;
            case TribeClass.GENERAL:
                if (creature1.GetTribe() != TribeClass.DRAMA_EVE_NONPC_A && creature1.GetTribe() != TribeClass.DRAMA_EVE_NONPC_B)
                {
                    switch (creature2.GetBaseTribe())
                    {
                        case TribeClass.PC:
                        case TribeClass.GUARD:
                            return true;
                    }
                }
                break;
            case TribeClass.FIELD_OBJECT_LIGHT:
                if (creature2.GetBaseTribe() == TribeClass.PC)
                    return true;
                break;
            case TribeClass.FIELD_OBJECT_DARK:
                if (creature2.GetBaseTribe() == TribeClass.PC_DARK)
                    return true;
                break;
        }
        if (creature2 is Player p && p.GetPanesterraFaction() != null && creature1.GetTribe().ToString().StartsWith("GAB1_"))
        {
            if (creature1.GetTribe() == p.GetPanesterraFaction().GetTribe())
                return true;
            return DataManager.TRIBE_RELATIONS_DATA.IsFriendlyRelation(creature1.GetTribe(), p.GetPanesterraFaction().GetTribe());
        }

        return DataManager.TRIBE_RELATIONS_DATA.IsFriendlyRelation(creature1.GetTribe(), creature2.GetTribe());
    }

    public static bool IsSupport(Creature creature1, Creature creature2)
    {
        if (creature1.GetTribe() == creature2.GetTribe() || creature1.GetBaseTribe() == creature2.GetTribe()
            || creature1.GetTribe() == creature2.GetBaseTribe() || creature1.GetBaseTribe() == creature2.GetBaseTribe())
        {
            return true;
        }
        switch (creature1.GetBaseTribe())
        {
            case TribeClass.GUARD_DARK:
                if (creature2.GetBaseTribe() == TribeClass.PC_DARK)
                    return true;
                break;
            case TribeClass.GUARD:
                if (creature2.GetBaseTribe() == TribeClass.PC)
                    return true;
                break;
        }
        if (creature2 is Player p && p.GetPanesterraFaction() != null && creature1.GetTribe().ToString().StartsWith("GAB1_"))
        {
            if (creature1.GetTribe() == p.GetPanesterraFaction().GetTribe())
                return true;
            return DataManager.TRIBE_RELATIONS_DATA.IsSupportRelation(creature1.GetTribe(), p.GetPanesterraFaction().GetTribe());
        }

        return DataManager.TRIBE_RELATIONS_DATA.IsSupportRelation(creature1.GetTribe(), creature2.GetTribe());
    }

    public static bool IsNone(Creature creature1, Creature creature2)
    {
        if (DataManager.TRIBE_RELATIONS_DATA.IsAggressiveRelation(creature1.GetTribe(), creature2.GetTribe())
            || creature1 is Npc && CheckSiegeRelation((Npc)creature1, creature2)
            || DataManager.TRIBE_RELATIONS_DATA.IsHostileRelation(creature1.GetTribe(), creature2.GetTribe())
            || DataManager.TRIBE_RELATIONS_DATA.IsNeutralRelation(creature1.GetTribe(), creature2.GetTribe()))
        {
            return false;
        }
        switch (creature1.GetBaseTribe())
        {
            case TribeClass.GAB1_PEACE:
            case TribeClass.GENERAL_DRAGON:
                return true;
            case TribeClass.GENERAL:
            case TribeClass.FIELD_OBJECT_LIGHT:
                if (creature2.GetBaseTribe() == TribeClass.PC_DARK)
                    return true;

                break;
            case TribeClass.GENERAL_DARK:
            case TribeClass.FIELD_OBJECT_DARK:
                if (creature2.GetBaseTribe() == TribeClass.PC)
                    return true;
                break;
        }
        return DataManager.TRIBE_RELATIONS_DATA.IsNoneRelation(creature1.GetTribe(), creature2.GetTribe());
    }

    public static bool IsNeutral(Creature creature1, Creature creature2)
    {
        return DataManager.TRIBE_RELATIONS_DATA.IsNeutralRelation(creature1.GetTribe(), creature2.GetTribe());
    }

    public static bool IsHostile(Creature creature1, Creature creature2)
    {
        if (creature1 is Npc && CheckSiegeRelation((Npc)creature1, creature2))
            return true;
        if (creature1.GetTribe() == TribeClass.IDF5U2_SHULACK && creature2.GetTribe() == TribeClass.FIELD_OBJECT_ALL_HOSTILEMONSTER)
            return false;
        if (creature1.GetBaseTribe() == TribeClass.MONSTER)
        {
            switch (creature2.GetBaseTribe())
            {
                case TribeClass.PC_DARK:
                case TribeClass.PC:
                    return true;
            }
        }

        if (creature2 is Player p && p.GetPanesterraFaction() != null && creature1.GetTribe().ToString().StartsWith("GAB1_"))
        {
            if (creature1.GetTribe() == p.GetPanesterraFaction().GetTribe())
                return false;
            return DataManager.TRIBE_RELATIONS_DATA.IsHostileRelation(creature1.GetTribe(), p.GetPanesterraFaction().GetTribe());
        }

        return DataManager.TRIBE_RELATIONS_DATA.IsHostileRelation(creature1.GetTribe(), creature2.GetTribe());
    }

    public static bool CheckSiegeRelation(Npc npc, Creature creature)
    {
        return ((npc.GetObjectTemplate().GetAbyssNpcType() != AbyssNpcType.ARTIFACT && npc.GetObjectTemplate().GetAbyssNpcType() != AbyssNpcType.NONE)
            || npc.GetSpawn() is BaseSpawnTemplate)
            && ((npc.GetBaseTribe() == TribeClass.GENERAL && creature.GetTribe() == TribeClass.PC_DARK)
                || (npc.GetBaseTribe() == TribeClass.GENERAL_DARK && creature.GetTribe() == TribeClass.PC))
            || npc.GetBaseTribe() == TribeClass.GENERAL_DRAGON && npc.GetObjectTemplate().GetAbyssNpcType() != AbyssNpcType.ARTIFACT;
    }

    public static bool CanHelpCreature(Creature creature, Creature creatureAskingForSupport)
    {
        return DataManager.TRIBE_RELATIONS_DATA.CanSupport(creature.GetTribe(), creatureAskingForSupport.GetTribe());
    }
}
