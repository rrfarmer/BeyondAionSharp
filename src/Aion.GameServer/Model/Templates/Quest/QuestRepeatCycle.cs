using System;
using System.Xml.Serialization;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Templates.Quest;

/// <summary>
/// Java parity: model/templates/quest/QuestRepeatCycle (vlog). Java enum implements L10n; C# enums can't implement
/// interfaces and @XmlEnum requires name-serialization, so L10n behavior is provided via extension methods.
/// weekDay values (0..7) match ordinals.
/// </summary>
[XmlType("QuestRepeatCycle")]
public enum QuestRepeatCycle
{
    ALL,
    MON,
    TUE,
    WED,
    THU,
    FRI,
    SAT,
    SUN
}

public static class QuestRepeatCycleExtensions
{
    public static int GetDay(this QuestRepeatCycle t) => (int) t;

    public static int GetL10nId(this QuestRepeatCycle t) => t switch
    {
        QuestRepeatCycle.ALL => 0,
        QuestRepeatCycle.MON => 900331,
        QuestRepeatCycle.TUE => 900332,
        QuestRepeatCycle.WED => 900333,
        QuestRepeatCycle.THU => 900334,
        QuestRepeatCycle.FRI => 900335,
        QuestRepeatCycle.SAT => 900336,
        QuestRepeatCycle.SUN => 900330,
        _ => throw new ArgumentOutOfRangeException(),
    };

    // Java parity: L10n::getL10n() default method.
    public static string? GetL10n(this QuestRepeatCycle t) => ChatUtil.L10n(t.GetL10nId());
}
