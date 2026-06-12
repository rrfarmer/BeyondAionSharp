using Aion.GameServer.Model;
using Aion.GameServer.Services.Panesterra.Ahserion;

namespace Aion.GameServer.Model.Base;

/// <summary>
/// Base/fortress occupier type — a race or Panesterra faction.
/// Java parity: model/base/BaseOccupier.
/// </summary>
public enum BaseOccupier
{
    // Java parity: ELYOS (no PanesterraFaction)
    ELYOS,
    // Java parity: ASMODIANS (no PanesterraFaction)
    ASMODIANS,
    // Java parity: BALAUR(PanesterraFaction.BALAUR)
    BALAUR,
    BELUS,
    IVY_TEMPLE,
    HIGHLAND_TEMPLE,
    ALPINE_TEMPLE,
    GRANDWEIR_TEMPLE,
    ASPIDA,
    NOERREN_TEMPLE,
    BOREALIS_TEMPLE,
    MYRKREN_TEMPLE,
    GLUMVEILEN_TEMPLE,
    ATANATOS,
    MEMORIA_TEMPLE,
    SYBILLINE_TEMPLE,
    AUSTERITY_TEMPLE,
    SERENITY_TEMPLE,
    DISILLON,
    NECROLUCE_TEMPLE,
    ESMERAUDUS_TEMPLE,
    VOLTAIC_TEMPLE,
    ILLUMINATUS_TEMPLE,
    // Java parity: PEACE(PanesterraFaction.PEACE)
    PEACE,
}

/// <summary>
/// Extension methods mirroring BaseOccupier's Java instance/static methods.
/// Java parity: model/base/BaseOccupier::getPanesterraFaction, ::findBy.
/// </summary>
public static class BaseOccupierExtensions
{
    // Java parity: BaseOccupier::getPanesterraFaction() — null for ELYOS/ASMODIANS
    public static PanesterraFaction? GetPanesterraFaction(this BaseOccupier occupier) => occupier switch
    {
        BaseOccupier.ELYOS or BaseOccupier.ASMODIANS => null,
        BaseOccupier.BALAUR => PanesterraFaction.BALAUR,
        BaseOccupier.BELUS => PanesterraFaction.BELUS,
        BaseOccupier.IVY_TEMPLE => PanesterraFaction.IVY_TEMPLE,
        BaseOccupier.HIGHLAND_TEMPLE => PanesterraFaction.HIGHLAND_TEMPLE,
        BaseOccupier.ALPINE_TEMPLE => PanesterraFaction.ALPINE_TEMPLE,
        BaseOccupier.GRANDWEIR_TEMPLE => PanesterraFaction.GRANDWEIR_TEMPLE,
        BaseOccupier.ASPIDA => PanesterraFaction.ASPIDA,
        BaseOccupier.NOERREN_TEMPLE => PanesterraFaction.NOERREN_TEMPLE,
        BaseOccupier.BOREALIS_TEMPLE => PanesterraFaction.BOREALIS_TEMPLE,
        BaseOccupier.MYRKREN_TEMPLE => PanesterraFaction.MYRKREN_TEMPLE,
        BaseOccupier.GLUMVEILEN_TEMPLE => PanesterraFaction.GLUMVEILEN_TEMPLE,
        BaseOccupier.ATANATOS => PanesterraFaction.ATANATOS,
        BaseOccupier.MEMORIA_TEMPLE => PanesterraFaction.MEMORIA_TEMPLE,
        BaseOccupier.SYBILLINE_TEMPLE => PanesterraFaction.SYBILLINE_TEMPLE,
        BaseOccupier.AUSTERITY_TEMPLE => PanesterraFaction.AUSTERITY_TEMPLE,
        BaseOccupier.SERENITY_TEMPLE => PanesterraFaction.SERENITY_TEMPLE,
        BaseOccupier.DISILLON => PanesterraFaction.DISILLON,
        BaseOccupier.NECROLUCE_TEMPLE => PanesterraFaction.NECROLUCE_TEMPLE,
        BaseOccupier.ESMERAUDUS_TEMPLE => PanesterraFaction.ESMERAUDUS_TEMPLE,
        BaseOccupier.VOLTAIC_TEMPLE => PanesterraFaction.VOLTAIC_TEMPLE,
        BaseOccupier.ILLUMINATUS_TEMPLE => PanesterraFaction.ILLUMINATUS_TEMPLE,
        BaseOccupier.PEACE => PanesterraFaction.PEACE,
        _ => null,
    };

    // Java parity: BaseOccupier::findBy(PanesterraFaction) — returns null if not found
    public static BaseOccupier? FindBy(PanesterraFaction panesterraFaction)
    {
        foreach (BaseOccupier occupier in Enum.GetValues<BaseOccupier>())
        {
            if (occupier.GetPanesterraFaction() == panesterraFaction)
                return occupier;
        }
        return null;
    }

    // Java parity: BaseOccupier::findBy(Race)
    public static BaseOccupier FindBy(Race race) => race switch
    {
        Race.ELYOS => BaseOccupier.ELYOS,
        Race.ASMODIANS => BaseOccupier.ASMODIANS,
        _ => BaseOccupier.BALAUR,
    };
}
