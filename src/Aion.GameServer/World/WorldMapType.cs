namespace Aion.GameServer.World;

/// <summary>Java parity: world/WorldMapType — named world-map IDs.</summary>
public enum WorldMapType
{
    // Asmodae
    Pandaemonium       = 120010000,
    Marchutan          = 120020000,
    Ishalgen           = 220010000,
    Morheim            = 220020000,
    Altgard            = 220030000,
    Beluslan           = 220040000,
    Brusthonin         = 220050000,

    // Elysea
    Sanctum            = 110010000,
    Kaisinel           = 110020000,
    Poeta              = 210010000,
    Eltnen             = 210020000,
    Verteron           = 210030000,
    Heiron             = 210040000,
    Theobomos          = 210060000,

    // Balaurea
    Inggison           = 210050000,
    Gelkmaros          = 220070000,
    SilenteraCanyon    = 600010000,

    // Prison
    LfPrison           = 510010000,
    DfPrison           = 520010000,

    Reshanta           = 400010000,

    // Instances
    NoZoneName         = 300010000,
    IdTestDungeon      = 300020000,
    NochsanaTrainingCamp = 300030000,
    DarkPoeta          = 300040000,
    AsteriaChamber     = 300050000,
    SulfurTreeNest     = 300060000,
    ChamberOfRoah      = 300070000,
    LeftWingChamber    = 300080000,
    RightWingChamber   = 300090000,
    SteelRake          = 300100000,
    Dredgion           = 300110000,
    KysisChamber       = 300120000,
    MirenChamber       = 300130000,
    KrotanChamber      = 300140000,
    UdasTemple         = 300150000,
    UdasTempleLower    = 300160000,
    BeshundirTemple    = 300170000,
    TalocsHollow       = 300190000,
    Haramel            = 300200000,
    DredgionOfChantra  = 300210000,
    AbyssalSplinter    = 300220000,
    KromdesTrial       = 300230000,
    Karamatis          = 310010000,
    KaramatisB         = 310020000,
    Aerdina            = 310030000,
    Geranaia           = 310040000,
    AetherogeneticsLab = 310050000,
    FragmentOfDarkness = 310060000,
    IdlfOneBStigma     = 310070000,
    SanctumUndergroundArena  = 310080000,
    TrinielUndergroundArena  = 320090000,
    IndratuFortress    = 310090000,
    AzoturanFortress   = 310100000,
    TheobomoLab        = 310110000,
    IdabProL3          = 310120000,
    Ataxiar            = 320010000,
    AtaxiarB           = 320020000,
    Bregirun           = 320030000,
    Nidalber           = 320040000,
    ArnaksTemple       = 320050000,
    SpaceOfOblivion    = 320060000,
    SpaceOfDestiny     = 320070000,
    DraupnirCave       = 320080000,
    FireTemple         = 320100000,
    AlquimiaResearchCenter = 320110000,
    ShadowCourtDungeon = 320120000,
    AdmaStronghold     = 320130000,
    IdabProD3          = 320140000,

    // Maps 2.5
    KaisiselAcademy    = 110070000,
    MarcutanPriory     = 120080000,
    Esoterrace         = 300250000,
    EmpyreanCrucible   = 300300000,

    // Map 2.6
    CrucibleChallenge  = 300320000,

    // Maps 2.7
    ArenaOfChaos       = 300350000,
    ArenaOfDiscipline  = 300360000,
    ChaosTrainingGrounds = 300420000,
    DisciplineTrainingGrounds = 300430000,
    PadmarashkaCave    = 320150000,

    // Test Maps
    TestBasic          = 900020000,
    TestServer         = 900030000,
    TestGiantMonster   = 900100000,
    HousingBarrack     = 900110000,
    TestIdArena        = 900120000,
    IdldfFiveReTest    = 900130000,
    TestKgw            = 900140000,
    TestBasicMj        = 900150000,
    TestIntro          = 900170000,
    TestServerArt      = 900180000,
    TestTagMatch       = 900190000,
    TestTimeAttack     = 900200000,
    SystemBasic        = 900220000,

    // Maps 3.0
    Raksang            = 300310000,
    RentusBase         = 300280000,
    AtauramSkyFortress = 300240000,
    SteelRakeCabin     = 300460000,
    TerathDredgion     = 300440000,

    // Map 3.5
    ArenaOfHarmony     = 300450000,
    TiamatStronghold   = 300510000,
    DragonLordsRefuge  = 300520000,
    ArenaOfGlory       = 300550000,
    ShugoImperialTomb  = 300560000,
    HarmonyTrainingGrounds = 300570000,
    UnstableSplinter   = 300600000,
    Hexway             = 300700000,

    // Instances 4.3 NA
    SealedDanuarMysticarium  = 300480000,
    EternalBastion     = 300540000,
    OphidanBridge      = 300590000,
    InfinityShard      = 300800000,
    DanuarReliquary    = 301110000,
    KamarBattlefield   = 301120000,
    SauroSupplyBase    = 301130000,
    SeizedDanuarSanctuary = 301140000,
    NightmareCircus    = 301160000,
    TheNightmareCircus = 301200000,

    // Maps 4.3 NA
    LifePartyConcertHall = 600080000,

    // Maps 4.5 NA + Instances
    EngulfedOphidanBridge = 301210000,
    IronWallWarfront   = 301220000,
    IlluminaqryObelisk = 301230000,
    LegionsKysisBarracks = 301240000,
    LegionsMirenBarracks = 301250000,
    LegionsKrotanBarracks = 301260000,
    LinkgateFoundry    = 301270000,
    KysisBarracks      = 301280000,
    MirenBarracks      = 301290000,
    KrotanBarracks     = 301300000,
    IdgelDome          = 301310000,
    LuckyOphidanBridge = 301320000,
    LuckyDanuarReliquary = 301330000,

    // Maps 4.7 NA + Instances
    QuestLinkgateFoundry    = 301340000,
    InfernalDanuarReliquary = 301360000,
    InfernalIlluminaqryObelisk = 301370000,
    Belus              = 400020000,
    TransidiumAnnex    = 400030000,
    Aspida             = 400040000,
    Atanatos           = 400050000,
    Disillon           = 400060000,
    Kaldor             = 600090000,
    Levinshor          = 600100000,

    // Housing
    Oriel              = 700010000,
    Pernon             = 710010000,

    // Maps 4.7.5.0
    TheShugoEmperorsVault = 301400000,
    WisplightAbbey     = 130090000,
    FateboundAbbey     = 140010000,

    // Maps 4.8
    IdianDepthsDark    = 220100000,
    Cygnea             = 210070000,
    Griffoen           = 210080000,
    IdianDepthsLight   = 210090000,
    Enshar             = 220080000,
    Habrok             = 220090000,

    // Instances 4.8
    RaksangRuins           = 300610000,
    OccupiedRentusBase     = 300620000,
    AnguishedDragonLordsRefuge = 300630000,
    DanuarSanctuary        = 301380000,
    DrakenspireDepths      = 301390000,
    StonespearReach        = 301500000,

    // Housing (personal/legion)
    HousingLcLegion        = 700020000,
    HousingDcLegion        = 710020000,
    HousingIdlfPersonal    = 720010000,
    HousingIddfPersonal    = 730010000,
}

public static class WorldMapTypeExtensions
{
    // Java parity: getId() — the int world map id
    public static int GetId(this WorldMapType t) => (int)t;

    // Java parity: isPersonal()
    public static bool IsPersonal(this WorldMapType t) =>
        t is WorldMapType.HousingLcLegion
         or WorldMapType.HousingDcLegion
         or WorldMapType.HousingIdlfPersonal
         or WorldMapType.HousingIddfPersonal;

    // Java parity: getWorld(int id)
    public static WorldMapType? GetWorld(int id) =>
        Enum.IsDefined(typeof(WorldMapType), id) ? (WorldMapType)id : null;

    // Java parity: of(String worldName)
    public static WorldMapType? Of(string worldName)
    {
        var normalized = worldName.ToLowerInvariant().Replace(" ", "_");
        foreach (WorldMapType t in Enum.GetValues<WorldMapType>())
            if (t.ToString().ToLowerInvariant() == normalized) return t;
        return null;
    }

    // Java parity: getMapId(String worldName)
    public static int GetMapId(string worldName) =>
        Of(worldName) is { } t ? (int)t : 0;

    // Java parity: isPanesterraMap(int id)
    public static bool IsPanesterraMap(int id) =>
        GetWorld(id) is WorldMapType.Belus
                     or WorldMapType.TransidiumAnnex
                     or WorldMapType.Aspida
                     or WorldMapType.Atanatos
                     or WorldMapType.Disillon;
}
