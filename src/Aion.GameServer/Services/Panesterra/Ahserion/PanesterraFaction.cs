using Aion.GameServer.Model;

namespace Aion.GameServer.Services.Panesterra.Ahserion;

/// <summary>
/// Panesterra faction identifiers, each mapped to a TribeClass.
/// Java parity: services/panesterra/ahserion/PanesterraFaction.
/// </summary>
public enum PanesterraFaction
{
    // Java parity: BALAUR(TribeClass.GAB1_MONSTER)
    BALAUR,
    // Java parity: BELUS(TribeClass.GAB1_01_POINT_01)
    BELUS,
    // Java parity: IVY_TEMPLE(TribeClass.GAB1_01_POINT_02)
    IVY_TEMPLE,
    // Java parity: HIGHLAND_TEMPLE(TribeClass.GAB1_01_POINT_03)
    HIGHLAND_TEMPLE,
    // Java parity: ALPINE_TEMPLE(TribeClass.GAB1_01_POINT_04)
    ALPINE_TEMPLE,
    // Java parity: GRANDWEIR_TEMPLE(TribeClass.GAB1_01_POINT_05)
    GRANDWEIR_TEMPLE,
    // Java parity: ASPIDA(TribeClass.GAB1_02_POINT_01)
    ASPIDA,
    // Java parity: NOERREN_TEMPLE(TribeClass.GAB1_02_POINT_02)
    NOERREN_TEMPLE,
    // Java parity: BOREALIS_TEMPLE(TribeClass.GAB1_02_POINT_03)
    BOREALIS_TEMPLE,
    // Java parity: MYRKREN_TEMPLE(TribeClass.GAB1_02_POINT_04)
    MYRKREN_TEMPLE,
    // Java parity: GLUMVEILEN_TEMPLE(TribeClass.GAB1_02_POINT_05)
    GLUMVEILEN_TEMPLE,
    // Java parity: ATANATOS(TribeClass.GAB1_03_POINT_01)
    ATANATOS,
    // Java parity: MEMORIA_TEMPLE(TribeClass.GAB1_03_POINT_02)
    MEMORIA_TEMPLE,
    // Java parity: SYBILLINE_TEMPLE(TribeClass.GAB1_03_POINT_03)
    SYBILLINE_TEMPLE,
    // Java parity: AUSTERITY_TEMPLE(TribeClass.GAB1_03_POINT_04)
    AUSTERITY_TEMPLE,
    // Java parity: SERENITY_TEMPLE(TribeClass.GAB1_03_POINT_05)
    SERENITY_TEMPLE,
    // Java parity: DISILLON(TribeClass.GAB1_04_POINT_01)
    DISILLON,
    // Java parity: NECROLUCE_TEMPLE(TribeClass.GAB1_04_POINT_02)
    NECROLUCE_TEMPLE,
    // Java parity: ESMERAUDUS_TEMPLE(TribeClass.GAB1_04_POINT_03)
    ESMERAUDUS_TEMPLE,
    // Java parity: VOLTAIC_TEMPLE(TribeClass.GAB1_04_POINT_04)
    VOLTAIC_TEMPLE,
    // Java parity: ILLUMINATUS_TEMPLE(TribeClass.GAB1_04_POINT_05)
    ILLUMINATUS_TEMPLE,
    // Java parity: PEACE(TribeClass.GAB1_PEACE)
    PEACE,
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
        PanesterraFaction.BALAUR => TribeClass.GAB1_MONSTER,
        PanesterraFaction.BELUS => TribeClass.GAB1_01_POINT_01,
        PanesterraFaction.IVY_TEMPLE => TribeClass.GAB1_01_POINT_02,
        PanesterraFaction.HIGHLAND_TEMPLE => TribeClass.GAB1_01_POINT_03,
        PanesterraFaction.ALPINE_TEMPLE => TribeClass.GAB1_01_POINT_04,
        PanesterraFaction.GRANDWEIR_TEMPLE => TribeClass.GAB1_01_POINT_05,
        PanesterraFaction.ASPIDA => TribeClass.GAB1_02_POINT_01,
        PanesterraFaction.NOERREN_TEMPLE => TribeClass.GAB1_02_POINT_02,
        PanesterraFaction.BOREALIS_TEMPLE => TribeClass.GAB1_02_POINT_03,
        PanesterraFaction.MYRKREN_TEMPLE => TribeClass.GAB1_02_POINT_04,
        PanesterraFaction.GLUMVEILEN_TEMPLE => TribeClass.GAB1_02_POINT_05,
        PanesterraFaction.ATANATOS => TribeClass.GAB1_03_POINT_01,
        PanesterraFaction.MEMORIA_TEMPLE => TribeClass.GAB1_03_POINT_02,
        PanesterraFaction.SYBILLINE_TEMPLE => TribeClass.GAB1_03_POINT_03,
        PanesterraFaction.AUSTERITY_TEMPLE => TribeClass.GAB1_03_POINT_04,
        PanesterraFaction.SERENITY_TEMPLE => TribeClass.GAB1_03_POINT_05,
        PanesterraFaction.DISILLON => TribeClass.GAB1_04_POINT_01,
        PanesterraFaction.NECROLUCE_TEMPLE => TribeClass.GAB1_04_POINT_02,
        PanesterraFaction.ESMERAUDUS_TEMPLE => TribeClass.GAB1_04_POINT_03,
        PanesterraFaction.VOLTAIC_TEMPLE => TribeClass.GAB1_04_POINT_04,
        PanesterraFaction.ILLUMINATUS_TEMPLE => TribeClass.GAB1_04_POINT_05,
        PanesterraFaction.PEACE => TribeClass.GAB1_PEACE,
        _ => TribeClass.GAB1_PEACE,
    };

    // Java parity: PanesterraFaction::getByFortressId(int fortressId)
    public static PanesterraFaction GetByFortressId(int fortressId) => fortressId switch
    {
        10111 => PanesterraFaction.BELUS,
        10211 => PanesterraFaction.ASPIDA,
        10311 => PanesterraFaction.ATANATOS,
        10411 => PanesterraFaction.DISILLON,
        _ => PanesterraFaction.PEACE,
    };
}
