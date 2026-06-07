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
    Elyos,
    // Java parity: ASMODIANS (no PanesterraFaction)
    Asmodians,
    // Java parity: BALAUR(PanesterraFaction.BALAUR)
    Balaur,
    Belus,
    IvyTemple,
    HighlandTemple,
    AlpineTemple,
    GrandweirTemple,
    Aspida,
    NoerrenTemple,
    BorealisTemple,
    MyrkrenTemple,
    GlumveilenTemple,
    Atanatos,
    MemoriaTemple,
    SybillineTemple,
    AusterityTemple,
    SerenityTemple,
    Disillon,
    NecrolluceTemple,
    EsmeraudusTemple,
    VoltaicTemple,
    IlluminatusTemple,
    // Java parity: PEACE(PanesterraFaction.PEACE)
    Peace,
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
        BaseOccupier.Elyos or BaseOccupier.Asmodians => null,
        BaseOccupier.Balaur => PanesterraFaction.Balaur,
        BaseOccupier.Belus => PanesterraFaction.Belus,
        BaseOccupier.IvyTemple => PanesterraFaction.IvyTemple,
        BaseOccupier.HighlandTemple => PanesterraFaction.HighlandTemple,
        BaseOccupier.AlpineTemple => PanesterraFaction.AlpineTemple,
        BaseOccupier.GrandweirTemple => PanesterraFaction.GrandweirTemple,
        BaseOccupier.Aspida => PanesterraFaction.Aspida,
        BaseOccupier.NoerrenTemple => PanesterraFaction.NoerrenTemple,
        BaseOccupier.BorealisTemple => PanesterraFaction.BorealisTemple,
        BaseOccupier.MyrkrenTemple => PanesterraFaction.MyrkrenTemple,
        BaseOccupier.GlumveilenTemple => PanesterraFaction.GlumveilenTemple,
        BaseOccupier.Atanatos => PanesterraFaction.Atanatos,
        BaseOccupier.MemoriaTemple => PanesterraFaction.MemoriaTemple,
        BaseOccupier.SybillineTemple => PanesterraFaction.SybillineTemple,
        BaseOccupier.AusterityTemple => PanesterraFaction.AusterityTemple,
        BaseOccupier.SerenityTemple => PanesterraFaction.SerenityTemple,
        BaseOccupier.Disillon => PanesterraFaction.Disillon,
        BaseOccupier.NecrolluceTemple => PanesterraFaction.NecrolluceTemple,
        BaseOccupier.EsmeraudusTemple => PanesterraFaction.EsmeraudusTemple,
        BaseOccupier.VoltaicTemple => PanesterraFaction.VoltaicTemple,
        BaseOccupier.IlluminatusTemple => PanesterraFaction.IlluminatusTemple,
        BaseOccupier.Peace => PanesterraFaction.Peace,
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
        Race.Elyos => BaseOccupier.Elyos,
        Race.Asmodians => BaseOccupier.Asmodians,
        _ => BaseOccupier.Balaur,
    };
}
