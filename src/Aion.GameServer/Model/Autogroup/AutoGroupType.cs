using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Templates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Model.Autogroup;

/// <summary>
/// Java parity: model/autogroup/AutoGroupType (xTz, Estrayl). Java enum w/ per-instance fields + per-constant
/// abstract newAutoInstance() override + implements L10n→C# enum + AutoGroupTypeExtensions: static Data dict
/// (template/maximumJoinTime/difficultId, resolved in static ctor like Java class-load); per-constant factory→
/// NewAutoInstance switch dispatching to AutoPvp/AutoHarmony/AutoPvPFFA; is* switch-exprs preserved; (byte)→sbyte.
/// NOTE: C# enum can't implement the L10n interface — GetL10nId provided as extension (red-tolerated polymorphism gap).
/// </summary>
public enum AutoGroupType
{
    BARANATH_DREDGION, CHANTRA_DREDGION, TERATH_DREDGION,
    ARENA_OF_CHAOS_1, ARENA_OF_CHAOS_2, ARENA_OF_CHAOS_3,
    ARENA_OF_DISCIPLINE_1, ARENA_OF_DISCIPLINE_2, ARENA_OF_DISCIPLINE_3,
    CHAOS_TRAINING_GROUNDS_1, CHAOS_TRAINING_GROUNDS_2, CHAOS_TRAINING_GROUNDS_3,
    DISCIPLINE_TRAINING_GROUNDS_1, DISCIPLINE_TRAINING_GROUNDS_2, DISCIPLINE_TRAINING_GROUNDS_3,
    ARENA_OF_HARMONY_1, ARENA_OF_HARMONY_2, ARENA_OF_HARMONY_3,
    ARENA_OF_GLORY_1, ARENA_OF_CHAOS_4, ARENA_OF_DISCIPLINE_4, ARENA_OF_HARMONY_4, ARENA_OF_GLORY_2,
    CHAOS_TRAINING_GROUNDS_4, DISCIPLINE_TRAINING_GROUNDS_4, HARAMONIOUS_TRAINING_CENTER_4,
    HARAMONIOUS_TRAINING_CENTER_1, HARAMONIOUS_TRAINING_CENTER_2, HARAMONIOUS_TRAINING_CENTER_3,
    KAMAR_BATTLEFIELD, ENGULFED_OPHIDIAN_BRIDGE, IRON_WALL_WARFRONT, IDGEL_DOME
}

public static class AutoGroupTypeExtensions
{
    private static readonly ILogger log = NullLogger.Instance;

    private readonly struct Data
    {
        public readonly AutoGroup Template;
        public readonly int MaximumJoinTime;
        public readonly sbyte DifficultId;

        public Data(AutoGroup template, int maximumJoinTime, sbyte difficultId)
        {
            Template = template;
            MaximumJoinTime = maximumJoinTime;
            DifficultId = difficultId;
        }
    }

    private static readonly Dictionary<AutoGroupType, Data> data = new();

    static AutoGroupTypeExtensions()
    {
        Add(AutoGroupType.BARANATH_DREDGION, 1, 420000, 0);
        Add(AutoGroupType.CHANTRA_DREDGION, 2, 420000, 0);
        Add(AutoGroupType.TERATH_DREDGION, 3, 420000, 0);
        Add(AutoGroupType.ARENA_OF_CHAOS_1, 21, 110000, 1);
        Add(AutoGroupType.ARENA_OF_CHAOS_2, 22, 110000, 2);
        Add(AutoGroupType.ARENA_OF_CHAOS_3, 23, 110000, 3);
        Add(AutoGroupType.ARENA_OF_DISCIPLINE_1, 24, 110000, 1);
        Add(AutoGroupType.ARENA_OF_DISCIPLINE_2, 25, 110000, 2);
        Add(AutoGroupType.ARENA_OF_DISCIPLINE_3, 26, 110000, 3);
        Add(AutoGroupType.CHAOS_TRAINING_GROUNDS_1, 27, 110000, 1);
        Add(AutoGroupType.CHAOS_TRAINING_GROUNDS_2, 28, 110000, 2);
        Add(AutoGroupType.CHAOS_TRAINING_GROUNDS_3, 29, 110000, 3);
        Add(AutoGroupType.DISCIPLINE_TRAINING_GROUNDS_1, 30, 110000, 1);
        Add(AutoGroupType.DISCIPLINE_TRAINING_GROUNDS_2, 31, 110000, 2);
        Add(AutoGroupType.DISCIPLINE_TRAINING_GROUNDS_3, 32, 110000, 3);
        Add(AutoGroupType.ARENA_OF_HARMONY_1, 33, 110000, 1);
        Add(AutoGroupType.ARENA_OF_HARMONY_2, 34, 110000, 2);
        Add(AutoGroupType.ARENA_OF_HARMONY_3, 35, 110000, 3);
        Add(AutoGroupType.ARENA_OF_GLORY_1, 38, 110000, 1);
        Add(AutoGroupType.ARENA_OF_CHAOS_4, 39, 110000, 4);
        Add(AutoGroupType.ARENA_OF_DISCIPLINE_4, 40, 110000, 4);
        Add(AutoGroupType.ARENA_OF_HARMONY_4, 41, 110000, 4);
        Add(AutoGroupType.ARENA_OF_GLORY_2, 42, 110000, 2);
        Add(AutoGroupType.CHAOS_TRAINING_GROUNDS_4, 43, 110000, 4);
        Add(AutoGroupType.DISCIPLINE_TRAINING_GROUNDS_4, 44, 110000, 4);
        Add(AutoGroupType.HARAMONIOUS_TRAINING_CENTER_4, 45, 110000, 4);
        Add(AutoGroupType.HARAMONIOUS_TRAINING_CENTER_1, 101, 110000, 1);
        Add(AutoGroupType.HARAMONIOUS_TRAINING_CENTER_2, 102, 110000, 2);
        Add(AutoGroupType.HARAMONIOUS_TRAINING_CENTER_3, 103, 110000, 3);
        Add(AutoGroupType.KAMAR_BATTLEFIELD, 107, 230000, 0);
        Add(AutoGroupType.ENGULFED_OPHIDIAN_BRIDGE, 108, 230000, 0);
        Add(AutoGroupType.IRON_WALL_WARFRONT, 109, 230000, 0);
        Add(AutoGroupType.IDGEL_DOME, 111, 170000, 0);
    }

    private static void Add(AutoGroupType t, int instanceMaskId, int maximumJoinTime, sbyte difficultId)
    {
        AutoGroup template = DataManager.AUTO_GROUP.GetTemplateByInstanceMaskId(instanceMaskId);
        if (template == null)
            log.LogWarning("No auto group template for instanceMaskId={InstanceMaskId} found.", instanceMaskId);
        data[t] = new Data(template, maximumJoinTime, difficultId);
    }

    public static AutoGroupType[] Values()
    {
        return System.Enum.GetValues<AutoGroupType>();
    }

    public static AutoGroup GetTemplate(this AutoGroupType self)
    {
        return data[self].Template;
    }

    public static int GetL10nId(this AutoGroupType self)
    {
        return self.GetTemplate().GetL10nId();
    }

    // Java parity: L10n::getL10n() - C# enum can't implement IL10n, so provide GetL10n directly.
    public static string? GetL10n(this AutoGroupType self) => Utils.ChatUtil.L10n(self.GetL10nId());

    public static int GetMaximumJoinTime(this AutoGroupType self)
    {
        return data[self].MaximumJoinTime;
    }

    public static bool ContainNpcId(this AutoGroupType self, int npcId)
    {
        return self.GetTemplate().GetNpcIds().Contains(npcId);
    }

    public static bool IsPeriodicInstance(this AutoGroupType self)
    {
        return self == AutoGroupType.KAMAR_BATTLEFIELD || self == AutoGroupType.ENGULFED_OPHIDIAN_BRIDGE || self == AutoGroupType.IDGEL_DOME || self == AutoGroupType.IRON_WALL_WARFRONT
            || self == AutoGroupType.CHANTRA_DREDGION || self == AutoGroupType.BARANATH_DREDGION || self == AutoGroupType.TERATH_DREDGION;
    }

    public static AutoGroupType? GetAGTByMaskId(int instanceMaskId)
    {
        foreach (AutoGroupType autoGroupType in Values())
        {
            if (autoGroupType.GetTemplate().GetMaskId() == instanceMaskId)
            {
                return autoGroupType;
            }
        }
        return null;
    }

    public static AutoGroupType? GetAutoGroup(int level, int npcId)
    {
        foreach (AutoGroupType agt in Values())
        {
            if (agt.IsInLvlRange(level) && agt.ContainNpcId(npcId))
            {
                return agt;
            }
        }
        return null;
    }

    public static AutoGroupType? GetAutoGroupByWorld(int level, int worldId)
    {
        foreach (AutoGroupType agt in Values())
        {
            if (agt.GetTemplate().GetInstanceMapId() == worldId && agt.IsInLvlRange(level))
            {
                return agt;
            }
        }
        return null;
    }

    public static AutoGroupType? GetAutoGroup(int npcId)
    {
        foreach (AutoGroupType agt in Values())
        {
            if (agt.ContainNpcId(npcId))
            {
                return agt;
            }
        }
        return null;
    }

    public static bool IsPvPSoloArena(this AutoGroupType self)
    {
        return self switch
        {
            AutoGroupType.ARENA_OF_DISCIPLINE_1 or AutoGroupType.ARENA_OF_DISCIPLINE_2 or AutoGroupType.ARENA_OF_DISCIPLINE_3 or AutoGroupType.ARENA_OF_DISCIPLINE_4 => true,
            _ => false,
        };
    }

    public static bool IsTrainingPvPSoloArena(this AutoGroupType self)
    {
        return self switch
        {
            AutoGroupType.DISCIPLINE_TRAINING_GROUNDS_1 or AutoGroupType.DISCIPLINE_TRAINING_GROUNDS_2 or AutoGroupType.DISCIPLINE_TRAINING_GROUNDS_3 or AutoGroupType.DISCIPLINE_TRAINING_GROUNDS_4 => true,
            _ => false,
        };
    }

    public static bool IsPvPFFAArena(this AutoGroupType self)
    {
        return self switch
        {
            AutoGroupType.ARENA_OF_CHAOS_1 or AutoGroupType.ARENA_OF_CHAOS_2 or AutoGroupType.ARENA_OF_CHAOS_3 or AutoGroupType.ARENA_OF_CHAOS_4 => true,
            _ => false,
        };
    }

    public static bool IsTrainingPvPFFAArena(this AutoGroupType self)
    {
        return self switch
        {
            AutoGroupType.CHAOS_TRAINING_GROUNDS_1 or AutoGroupType.CHAOS_TRAINING_GROUNDS_2 or AutoGroupType.CHAOS_TRAINING_GROUNDS_3 or AutoGroupType.CHAOS_TRAINING_GROUNDS_4 => true,
            _ => false,
        };
    }

    public static bool IsTrainingHarmonyArena(this AutoGroupType self)
    {
        return self switch
        {
            AutoGroupType.HARAMONIOUS_TRAINING_CENTER_1 or AutoGroupType.HARAMONIOUS_TRAINING_CENTER_2 or AutoGroupType.HARAMONIOUS_TRAINING_CENTER_3 or AutoGroupType.HARAMONIOUS_TRAINING_CENTER_4 => true,
            _ => false,
        };
    }

    public static bool IsHarmonyArena(this AutoGroupType self)
    {
        return self switch
        {
            AutoGroupType.ARENA_OF_HARMONY_1 or AutoGroupType.ARENA_OF_HARMONY_2 or AutoGroupType.ARENA_OF_HARMONY_3 or AutoGroupType.ARENA_OF_HARMONY_4 => true,
            _ => false,
        };
    }

    public static bool IsGloryArena(this AutoGroupType self)
    {
        return self switch
        {
            AutoGroupType.ARENA_OF_GLORY_1 or AutoGroupType.ARENA_OF_GLORY_2 => true,
            _ => false,
        };
    }

    public static bool IsPvpArena(this AutoGroupType self)
    {
        return self.IsTrainingPvPFFAArena() || self.IsPvPFFAArena() || self.IsTrainingPvPSoloArena() || self.IsPvPSoloArena();
    }

    public static bool IsInLvlRange(this AutoGroupType self, int level)
    {
        return level >= self.GetTemplate().GetMinLvl() && level <= self.GetTemplate().GetMaxLvl();
    }

    public static sbyte GetDifficultId(this AutoGroupType self)
    {
        return data[self].DifficultId;
    }

    public static AutoInstance CreateAutoInstance(this AutoGroupType self)
    {
        return self.NewAutoInstance();
    }

    /// <summary>Java per-constant abstract newAutoInstance() override → factory switch by member.</summary>
    private static AutoInstance NewAutoInstance(this AutoGroupType self)
    {
        return self switch
        {
            AutoGroupType.BARANATH_DREDGION or AutoGroupType.CHANTRA_DREDGION or AutoGroupType.TERATH_DREDGION
                or AutoGroupType.KAMAR_BATTLEFIELD or AutoGroupType.ENGULFED_OPHIDIAN_BRIDGE or AutoGroupType.IRON_WALL_WARFRONT or AutoGroupType.IDGEL_DOME
                => new AutoPvpInstance(self),
            AutoGroupType.ARENA_OF_HARMONY_1 or AutoGroupType.ARENA_OF_HARMONY_2 or AutoGroupType.ARENA_OF_HARMONY_3 or AutoGroupType.ARENA_OF_HARMONY_4
                or AutoGroupType.HARAMONIOUS_TRAINING_CENTER_1 or AutoGroupType.HARAMONIOUS_TRAINING_CENTER_2 or AutoGroupType.HARAMONIOUS_TRAINING_CENTER_3 or AutoGroupType.HARAMONIOUS_TRAINING_CENTER_4
                => new AutoHarmonyInstance(self),
            _ => new AutoPvPFFAInstance(self),
        };
    }
}
