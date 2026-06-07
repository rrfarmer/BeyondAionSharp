using Aion.GameServer.Model;

namespace Aion.GameServer.Services.Panesterra.Ahserion;

/// <summary>
/// Panesterra faction identifiers, each mapped to a TribeClass.
/// Java parity: services/panesterra/ahserion/PanesterraFaction.
/// </summary>
public enum PanesterraFaction
{
    // Java parity: BALAUR(TribeClass.GAB1_MONSTER)
    Balaur,
    // Java parity: BELUS(TribeClass.GAB1_01_POINT_01)
    Belus,
    // Java parity: IVY_TEMPLE(TribeClass.GAB1_01_POINT_02)
    IvyTemple,
    // Java parity: HIGHLAND_TEMPLE(TribeClass.GAB1_01_POINT_03)
    HighlandTemple,
    // Java parity: ALPINE_TEMPLE(TribeClass.GAB1_01_POINT_04)
    AlpineTemple,
    // Java parity: GRANDWEIR_TEMPLE(TribeClass.GAB1_01_POINT_05)
    GrandweirTemple,
    // Java parity: ASPIDA(TribeClass.GAB1_02_POINT_01)
    Aspida,
    // Java parity: NOERREN_TEMPLE(TribeClass.GAB1_02_POINT_02)
    NoerrenTemple,
    // Java parity: BOREALIS_TEMPLE(TribeClass.GAB1_02_POINT_03)
    BorealisTemple,
    // Java parity: MYRKREN_TEMPLE(TribeClass.GAB1_02_POINT_04)
    MyrkrenTemple,
    // Java parity: GLUMVEILEN_TEMPLE(TribeClass.GAB1_02_POINT_05)
    GlumveilenTemple,
    // Java parity: ATANATOS(TribeClass.GAB1_03_POINT_01)
    Atanatos,
    // Java parity: MEMORIA_TEMPLE(TribeClass.GAB1_03_POINT_02)
    MemoriaTemple,
    // Java parity: SYBILLINE_TEMPLE(TribeClass.GAB1_03_POINT_03)
    SybillineTemple,
    // Java parity: AUSTERITY_TEMPLE(TribeClass.GAB1_03_POINT_04)
    AusterityTemple,
    // Java parity: SERENITY_TEMPLE(TribeClass.GAB1_03_POINT_05)
    SerenityTemple,
    // Java parity: DISILLON(TribeClass.GAB1_04_POINT_01)
    Disillon,
    // Java parity: NECROLUCE_TEMPLE(TribeClass.GAB1_04_POINT_02)
    NecrolluceTemple,
    // Java parity: ESMERAUDUS_TEMPLE(TribeClass.GAB1_04_POINT_03)
    EsmeraudusTemple,
    // Java parity: VOLTAIC_TEMPLE(TribeClass.GAB1_04_POINT_04)
    VoltaicTemple,
    // Java parity: ILLUMINATUS_TEMPLE(TribeClass.GAB1_04_POINT_05)
    IlluminatusTemple,
    // Java parity: PEACE(TribeClass.GAB1_PEACE)
    Peace,
}

/// <summary>
/// Extension methods mirroring PanesterraFaction's Java instance methods.
/// Java parity: services/panesterra/ahserion/PanesterraFaction::getTribe, ::getByFortressId.
/// </summary>
public static class PanesterraFactionExtensions
{
    // Java parity: PanesterraFaction::getTribe()
    public static TribeClass GetTribe(this PanesterraFaction faction) => faction switch
    {
        PanesterraFaction.Balaur => TribeClass.GAB1_MONSTER,
        PanesterraFaction.Belus => TribeClass.GAB1_01_POINT_01,
        PanesterraFaction.IvyTemple => TribeClass.GAB1_01_POINT_02,
        PanesterraFaction.HighlandTemple => TribeClass.GAB1_01_POINT_03,
        PanesterraFaction.AlpineTemple => TribeClass.GAB1_01_POINT_04,
        PanesterraFaction.GrandweirTemple => TribeClass.GAB1_01_POINT_05,
        PanesterraFaction.Aspida => TribeClass.GAB1_02_POINT_01,
        PanesterraFaction.NoerrenTemple => TribeClass.GAB1_02_POINT_02,
        PanesterraFaction.BorealisTemple => TribeClass.GAB1_02_POINT_03,
        PanesterraFaction.MyrkrenTemple => TribeClass.GAB1_02_POINT_04,
        PanesterraFaction.GlumveilenTemple => TribeClass.GAB1_02_POINT_05,
        PanesterraFaction.Atanatos => TribeClass.GAB1_03_POINT_01,
        PanesterraFaction.MemoriaTemple => TribeClass.GAB1_03_POINT_02,
        PanesterraFaction.SybillineTemple => TribeClass.GAB1_03_POINT_03,
        PanesterraFaction.AusterityTemple => TribeClass.GAB1_03_POINT_04,
        PanesterraFaction.SerenityTemple => TribeClass.GAB1_03_POINT_05,
        PanesterraFaction.Disillon => TribeClass.GAB1_04_POINT_01,
        PanesterraFaction.NecrolluceTemple => TribeClass.GAB1_04_POINT_02,
        PanesterraFaction.EsmeraudusTemple => TribeClass.GAB1_04_POINT_03,
        PanesterraFaction.VoltaicTemple => TribeClass.GAB1_04_POINT_04,
        PanesterraFaction.IlluminatusTemple => TribeClass.GAB1_04_POINT_05,
        PanesterraFaction.Peace => TribeClass.GAB1_PEACE,
        _ => TribeClass.GAB1_PEACE,
    };

    // Java parity: PanesterraFaction::getByFortressId(int fortressId)
    public static PanesterraFaction GetByFortressId(int fortressId) => fortressId switch
    {
        10111 => PanesterraFaction.Belus,
        10211 => PanesterraFaction.Aspida,
        10311 => PanesterraFaction.Atanatos,
        10411 => PanesterraFaction.Disillon,
        _ => PanesterraFaction.Peace,
    };
}
