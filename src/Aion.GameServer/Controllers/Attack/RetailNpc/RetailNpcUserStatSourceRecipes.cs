using System.Collections.Immutable;
using System.Globalization;
using System.Xml.Linq;
using GI = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserGearStatLifeInputs;
using GG = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserOwnedStatGraph;
using C = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserStatSourceCatalog;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Named Java-owned/current-source constructor recipes. No registration, learning,
/// reroll or startEffect is executed. A source child and an actual registration owner are distinct.</summary>
internal static class RetailNpcUserStatSourceRecipes
{
    internal const string Policy = "NpcNativeGearRefreshWithJava48OwnedLifeV1/GeneralSelectedSourceProjectionV1";
    internal enum Kind { Item, Fusion, Random, SetBonus, Enchant, Tempering, Buf, Mastery, Absolute, Title, Cp, Instance, Predefined, CurrentConstructor }
    internal enum Program { Basic, MaxHp, MaxMp, DuplicateDelay, WeaponMastery, ArmorMastery, ShieldMastery, RetainedPredefined }
    internal enum Predicate { Charge, OnFly, Weapon }
    internal enum OwnerType { Unknown, Item, Stone, RandomBonus, ItemSet, Enchant, Tempering, Effect, Title, Instance, Cp, Predefined, ReviewedConstructor }
    internal sealed record Condition(Predicate Kind, int Value, ImmutableArray<string> Weapons);
    internal sealed record OriginalOperand(int ChildOrdinal, int Value, GI.Binding Current, string Field);
    internal sealed record Selection(Kind Kind, int Child = 0, int Variant = 0, int Level = 0,
        int EquippedParts = 0, long EquipmentMask = 0, string? ExplicitTableName = null,
        int PlumeRandom = 0, int? ArmorFixedBonus = null, int? ArmorFactor = null,
        ImmutableArray<OriginalOperand> OriginalOperands = default);
    internal sealed record Registration(string Occurrence, long Sequence, GI.Binding Owner, OwnerType OwnerType,
        C.Catalog.Record? Source, Selection Selection, bool? SourceRecipeUnchanged,
        ImmutableArray<CurrentFunction> ActualFunctions, string FactoryJavaSha256, string FactoryCSharpSha256,
        C.Catalog.Record? ItemSource = null, GI.Binding? ItemOwner = null, C.Catalog.Record? ActionSource = null,
        bool FusionRandom = false);
    internal sealed record CurrentFunction(string Entry, string Stat, string Class, int Value, bool Bonus,
        GI.Binding Owner, bool? CompleteOriginalFields, GI.AbsField? Abs = null,
        string? OriginalObjectIdentity = null, int? ArmorFixedBonus = null,
        int? ArmorFactor = null, string? MasteryGroup = null);
    internal sealed record Function(string Entry, string Stat, string Class, int Value, bool Bonus,
        Program Program, GI.FunctionKind Kind, ImmutableArray<Condition> Conditions, GI.Binding Owner,
        OwnerType OwnerType, string RawIdentity, string SourceIdentity, string Registration,
        int SourceOrdinal, int Priority, GI.AbsField? Abs = null,
        string? Group = null, int ArmorFixedBonus = 0, int ArmorFactor = 0,
        string? OriginalObjectIdentity = null);
    internal sealed record FactoryPair(string Java, string CSharp);
    // Filled from exact paired source identities by the external source-pin generator.
    private static readonly ImmutableDictionary<string, FactoryPair> Factories = CreateFactories();
    private static ImmutableDictionary<string, FactoryPair> CreateFactories()
    {
        var rows = ImmutableDictionary.CreateBuilder<string, FactoryPair>(StringComparer.Ordinal);
        rows.Add("ItemEquipmentListener", new("930c2783516f5f63e8c1bcb123b03ddd6ceb4e07ec5a4ca1ae67486c6c95bfd4", "1692740dcbf8f0f9c2cc5a000dc9c8251eeaa240c03287f1b5b12cca5d1bf5cb"));
        rows.Add("RandomBonusEffect", new("cb4a555167691afadc7dd33a8d5ba3d8899758c19c318cc3deb5102b6fa8ebe6", "0bd1a5904b3102fbc37b5ef95e3e14dbf9bf17bfa8376738281e474482a83998"));
        rows.Add("EnchantEffect", new("e2289a4fcb5ffaf32e8dea3ce4f5a9083ab1786fa7f43d8f6fc474bb15860eaa", "0a3396c285dc4212c35b457115190f082003d00c2e606f839ca9d0d5830854de"));
        rows.Add("TemperingEffect", new("f214bdf5ebceb3bb2158c7f16fb2d5d49c6598b74530e198b4ac6f3b25132bd9", "8c63f2a7d30ecb8909c1aa8173f755c084c689aa6894536d45e8bf485035280f"));
        rows.Add("BufEffect", new("2a8f0edc37cdaec151a8648c008fdbe7b03ae89000f8fb15e9d2c8a9316c158b", "75f097394488c3e53205517858f20bd4a77aa03bd42bea5abfb5da7ab14cd48d"));
        rows.Add("AbstractAbsoluteStatEffect", new("0fbe9c005352d5df46b6200557099675d1c21abc427abec2297a6a9699b901e0", "864a1671c57ef7c9825bdf3a308d70c8a8bee654dd7686485b2c3926e64fae43"));
        rows.Add("TitleChangeListener", new("7ed33f1711627a3f5ff1b4fab9c28b35c78bb4759320d0f038ca68cadf962082", "7fb49645c9032177792d01cb84be78362f414b50582d976bc22583e4f7f3db90"));
        rows.Add("CPBuff", new("55e9adca815ad8ff3640e7f36415284705b8673240c804982d61902072388844", "bd62bd8a9894045f1d886c21be88ec23ffa5438b2b4dd4c7efc8342485995cc2"));
        rows.Add("InstanceBuff", new("4a9cbc389328a049a5d7c84fe0f7b75674c0caebd80ef11dcbc709e211cb08ac", "f8dc3bfd98bdd597a1da21ad5d68f76d30f135c561d1c8eed8ee6c1caad9fb92"));
        rows.Add("PlayerStatFunctions", new("900d889928c5c8410a4de6fbe6cd39e121e0f283ac45a1f81711f00082fc2a76", "fe54ab19402c9a36ee1fa770c32aa4b285b1398378d2b2d17a870e62527cc6fe"));
        rows.Add("StatFunction", new("c4877137e83a9eff8ff12318a12cf25ad6f4548d2af373add68651663b6456b7", "e0cfe3c1f75b1dfa67b7fe2999834fa309a5898a2bd125fe2312206874011eec"));
        rows.Add("ItemTemplate", new("0176383e1196bd178af551309b054b478a8a1a44205459a122de036e7687f459", "b0266e9b2fbbe605b4067e8f6a3bebd6a2cd41e9648ae9b4a69459743d8def45"));
        rows.Add("WeaponStats", new("907683cb7bd52fbfd00c361145a00aa3a176aa11ae79b2e6345028ee01f714ab", "552f5b247525bf47e9b83b663d3de66a0011cae37c00865fc9e2f37e9a714106"));
        rows.Add("ItemGroup", new("b6ff7668768735115fea881a584177de5d410939ba6fe2c555f5a6222de788cf", "411ce1e8ecf058357a54d8c9ea423af4d1a32e1f508790560e880f86c69c3820"));
        rows.Add("ItemAttackType", new("b53416f057a9885adeda1a2854ec658fca9a54a7509c89032874e4de53b86a71", "136767e4608e2786a668500054bd1bba0331d9c1672244dd760eb5667beeac3c"));
        rows.Add("PlayerGameStats", new("dd54b3375dbf09306a3aa41d0e2070f7790adcc95607df14f1cb8db388beabe1", "627febcbb81f074ab1cfb592f37dff5af4d0d6179420fc75a9441aa90d70f069"));
        rows.Add("ItemSlot", new("e3ec916aebce131757a07a3b8b5a392c03b9d3e2b41c51e0f6d5d138fcf0780e", "83f315e0869071afdbd4c92a99b810d915a57d972c90700d775e9c9342ea4f9e"));
        rows.Add("StatAbsFunction", new("6b1ccdcfd4180135721275351c9eea844b2fca70c0049ff23b365275bef67cba", "fab4699b12332b07bbc6a2cac1401b05306665d8c14c9af445f84c527712dcf4"));
        rows.Add("StatWeaponMasteryFunction", new("7bf5406fe462eea8ede03b776da9f563bc45d5406753c979a5fc1af1920cc755", "f5f86967a10354645013618103943add16851e546615f546d20f9cb48d748066"));
        rows.Add("StatArmorMasteryFunction", new("9bca48a3359b949cf11773c44eb5676f0570e9fa4be94030ac9dc65e0dcf313e", "ef731f90feaa1c1c93077dfaba1a75807dd8161b03a3584d0c5efdc79cbf83f8"));
        rows.Add("StatShieldMasteryFunction", new("4210fcb043b3de2fae089a8fa6d8bd328db4e635142a5e25e83c2d1686f097ff", "4c8dedcd326e4898bae18ef22fd8c8fe6c78ac2aee99fb3a82f68eada7e32f95"));
        rows.Add("Conditions", new("dc82169376d709e64911b6d3384bcee2c53cf0315696196d0ba03ffb6b726960", "40e6fb0ad6495d1e85a1c43ebb44202d77b87b45b203cad09fc20f57edd5d73f"));
        rows.Add("ItemChargeCondition", new("5b010c707f880addb820ad7e90fc71b477264de54c95b7a4b17745407842f620", "977b41bff249df4f43e3d039bb587b9ff87daa65b5f8c4b19ffd52a27f93f8a8"));
        rows.Add("OnFlyCondition", new("1d04b6af733cdde14b78ee4dc783c89362d3e36811341f7b8c5fd558e5eeadff", "ca00fc81ccc1a8bcb04082abbd30160a9eaed3da20bc6c3a2352ff390692477d"));
        rows.Add("WeaponCondition", new("5b3c78050a0a565566771fa5ff692401083dabb06643cb408ceca3a40acf438e", "47d0c9ab0111dfa37552b3610c76d751c5ae7cbcf945c3d89f534c0b987b9a3a"));
        rows.Add("APBoostEffect", new("afbb02ef451919a32b9db3328275a73eea6115079f5c6cf54b74d3beca4adf90", "d9ed5df0922a7a8268fc1af6d23fbd16472af6a12589b941984096c8f0e472b0"));
        rows.Add("AbsoluteSlowEffect", new("3f55c84ecd5c90c70aa65a2c4b5b1f8717dfa26bb53206f72fd95a0a97ccdd28", "f22da5e47e04cb75975146e425368d5c33de107641cab56e3febe75bf8cb2e20"));
        rows.Add("AbsoluteSnareEffect", new("69d8dbb6c1d6680f83f3860988529a4e2fbe7b38f81714dc4ec7522310b64eee", "1113795385b9425dcf07ed9749da44782c0e70a98788746255e58e9119c7dcc6"));
        rows.Add("ArmorMasteryEffect", new("ec599f60416849f9031950160d5096496f834ed9188d78f7bf4a6e75a10173ba", "0219421cc04c02aa9a89a1a3f636bcf2b63006530a5dcdb3223fa496c89ba0b9"));
        rows.Add("BoostDropRateEffect", new("ed6de7b5f92966dbc714a9c0d875009d961b74481d8b135f918a0afe59da27ea", "377eda3fb8d407e5d32df2189300aa8ca49368c740ebc30471c36265028a5a2d"));
        rows.Add("BoostHateEffect", new("03204cb725f6324fc33f49d5901f4c4da41288bc9ecbb7a94eec9deabc6e3439", "36b8177715f0f3f9885cada6dd06cc96f643203ba94d714a5beae57b715c8309"));
        rows.Add("BoostHealEffect", new("11a934d454fa9fe3e7d3a26d5ab699d70e4d3c56bff6ab061d4d8702745f299a", "0f4a294558f98f5b7fb5c2d8758c3dc4276f0482c0a581034b0d3132c089cbc7"));
        rows.Add("BoostSkillCastingTimeEffect", new("9c609a82b65e7cfda921946e3d06427a12983b1ca36fc374bd14903f1c1c65eb", "166210db7c198bef5b628602dc8e5ce5760c3ee770d872a3ce8f882dfebee02a"));
        rows.Add("BoostSpellAttackEffect", new("b591e6b26eedf9c5783e950ef6d32f51255d1cd2da989e10edfcdcf2e283d2fd", "a0da9517f5d2795f4e98d050e9b2aacdd77b2385920ec1e2f9236148d725b0cb"));
        rows.Add("CurseEffect", new("9b2d2587670620360f42dcea8731422898c93f478523074e804e85d899ee6b76", "acc7c332ad33c39c927a4344e36c892a615ad0a4b9670c7cf976a8b5478b8372"));
        rows.Add("DRBoostEffect", new("5f858b81d5f84f583527868f0975e3c3a35e21a5ad7d82434fa352b419494178", "6bbc8d6e1929571423842fbe127cc6b532f91dbd4d7d641e3abc99d9d0f7514b"));
        rows.Add("DeboostHealEffect", new("33ee48d6923962b36849f5b5b8503dbce079a5ee30420da0a46389e7bc01321d", "239c9554a8d1303ae1ea44d9d9999094ad683326ffbf7f7851b7aa31f322eab0"));
        rows.Add("ExtendAuraRangeEffect", new("84d7377d867016a671395e5779ed2b27cf628c3a7a57b2e89a5c2a738a9415ec", "bbe888bb712e97a598d5553339c813d72ed189a593b71ed10ae1105f90903d3c"));
        rows.Add("HideEffect", new("6205ef0fc3727ac1519907129ba501de7b3520c554cd18389767cf95d44304c0", "962e19fedff06d6ebbd4d84ed8a40e9dfafe1d1bae4682d8bec857a56e79e9ae"));
        rows.Add("OneTimeBoostHealEffect", new("0256ef45d0f49fd60e143ba4e7d42a0ae3c14c09235b490d36c0aadfabcd9249", "68923e67df8651a9fa4e9f71c09293f96477e8ef7f9861514f87eafb8a78e3e6"));
        rows.Add("ShieldMasteryEffect", new("95dd1373bcadddc93d885030b2a2324d2c0e815bc008f837f25f5afec2eef7c0", "a058e178a99803bb9a8a26c039cb0677e0a4a3d5464686b6076a6647df5f78d9"));
        rows.Add("SkillXPBoostEffect", new("0fffc5242bc1f7e056e10e2ae7d7a078724f8bcb6de414e701d91e4c11d7ab03", "1fa03f1015b1c377fdb67c62c8c15211c4ea7c0e84f665097b66f3f08b730934"));
        rows.Add("SlowEffect", new("65b997b49ce0488e2dd6b1fe1fadcaf97cd31d4e05ab573e6c443e7826f60052", "d6ffdb908341e3c3b86b090cfae7e0f581d9a2fc03e8db58f19963214f90ba67"));
        rows.Add("SnareEffect", new("93559d2b7c3127a9b5bc58908bba27e2ee5a64d239512d263a07e63197924e8a", "c9432af5f8229663bedbc627ececd7ecb38b34ffb644a4b31976b0e827f6818e"));
        rows.Add("StatboostEffect", new("aa7daa41066e4f65d79d59ef181bfda11aba05b7bb910b85cce8739243157571", "bfc90987b3f8bc6371eb76be4907885ac5c4c4af48b2f1f05c8c45c7285a23e2"));
        rows.Add("StatdownEffect", new("a2c6431d3bfbca774b5e1fee049281fde6a92864f2236caf15694e9c094fafc6", "856f0669333baee1e8dd98621b4113a5046511a38c269119c750fe9f2e6a9515"));
        rows.Add("StatupEffect", new("df2c190eb5a5ce10a9724e54c788ef9442374d64419c311e04e9ba30c2807632", "d3861b238b829909350ee70c82ada5b452ef8f6bc1a62f7208c6671fafd12512"));
        rows.Add("SubTypeBoostResistEffect", new("126c9a9797be0a020fe6c5c3fb6afbbb6764934ce0f5aa464501ed5eeffd9b86", "7645cd8eeda074adcba77d32186193f2f0ae85f23843f69f1b409335eff0473d"));
        rows.Add("WeaponMasteryEffect", new("e9e3046bcbbfb89fd41fb24ccebf0770ede9c0f9a0c375a34071aa92f797fc62", "3d35c86f3001628508a938774513d0e8a25784021965249ba5b0597f54137297"));
        rows.Add("WeaponStatboostEffect", new("cf6e8716e08d9366f8f330a42f50ca9e45fff3193f3c7d6466e1bce68d22f881", "8913da7d1ad2a53daf068b8ab5e53a7d6130b7960828c6e78d6d91874d2c81f0"));
        rows.Add("WeaponStatupEffect", new("4bd70c14a076d42a47764034a682fbf5bcc14c5a4eab050c8fcf1054f8ed4e97", "2f13b090294bec22bfc698080c133208be6e08cf8aae846e6bf6d1ae8de208c9"));
        rows.Add("XPBoostEffect", new("14cf355d3166a5813a7a4860bb5a0192abfca427efcaa4fd818f50e31c8755d8", "95161c44db20382c78439f13078092a14d037179e83e8a0bb932f60cea86f6da"));
        return rows.ToImmutable();
    }
    internal static FactoryPair Factory(string name)
    { GI.Require(Factories.TryGetValue(name, out var value), "named factory source proof"); return value; }
    internal static readonly ImmutableHashSet<string> ReadStats = ImmutableHashSet.Create(StringComparer.Ordinal,
        "HEALTH", "WILL", "MAXHP", "MAXMP", "SPEED", "FLY_SPEED", "ATTACK_SPEED");
    private static readonly ImmutableHashSet<string> StatNames = Enum.GetNames<Aion.GameServer.Model.Stats.Container.StatEnum>().ToImmutableHashSet(StringComparer.Ordinal);
    private static readonly ImmutableHashSet<string> TwoHand = ImmutableHashSet.Create(StringComparer.Ordinal,
        "NOWEAPON", "GREATSWORD", "ORB", "SPELLBOOK", "POLEARM", "STAFF", "BOW", "HARP", "CANNON", "KEYBLADE", "TOOLRODS", "TOOLPICKS");
    private static readonly ImmutableDictionary<string, string> BufBindings = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["statup"]="StatupEffect", ["statdown"]="StatdownEffect", ["statboost"]="StatboostEffect",
        ["weaponstatboost"]="WeaponStatboostEffect", ["weaponstatup"]="WeaponStatupEffect",
        ["wpnmastery"]="WeaponMasteryEffect", ["armormastery"]="ArmorMasteryEffect", ["shieldmastery"]="ShieldMasteryEffect",
        ["subtypeboostresist"]="SubTypeBoostResistEffect", ["boostskillcastingtime"]="BoostSkillCastingTimeEffect",
        ["snare"]="SnareEffect", ["drboost"]="DRBoostEffect", ["boosthate"]="BoostHateEffect", ["apboost"]="APBoostEffect",
        ["extendedaurarange"]="ExtendAuraRangeEffect", ["boostspellattack"]="BoostSpellAttackEffect", ["boostheal"]="BoostHealEffect",
        ["xpboost"]="XPBoostEffect", ["slow"]="SlowEffect", ["deboostheal"]="DeboostHealEffect", ["hide"]="HideEffect",
        ["curse"]="CurseEffect", ["onetimeboostheal"]="OneTimeBoostHealEffect", ["boostdroprate"]="BoostDropRateEffect",
        ["absolutesnare"]="AbsoluteSnareEffect", ["skillxpboost"]="SkillXPBoostEffect", ["absoluteslow"]="AbsoluteSlowEffect"
    }.ToImmutableDictionary(StringComparer.Ordinal);
    internal static ImmutableArray<Function> Construct(C.Catalog catalog, Registration r, string operation, string epoch)
    {
        GI.Require(catalog is not null && r is not null && r.Owner is not null && r.Selection is not null,"named constructor input");
        r.Owner.Validate(operation, epoch);
        GI.Require(Enum.IsDefined(r.Selection.Kind) && Enum.IsDefined(r.OwnerType) && r.OwnerType != OwnerType.Unknown
            && r.SourceRecipeUnchanged == true && !string.IsNullOrWhiteSpace(r.Occurrence) && r.Sequence >= 0
            && !r.ActualFunctions.IsDefault, "current registration/constructor fields");
        string factoryName = r.Selection.Kind switch
        {
            Kind.Item or Kind.Fusion or Kind.SetBonus => "ItemEquipmentListener",
            Kind.Random => "RandomBonusEffect", Kind.Enchant => "EnchantEffect", Kind.Tempering => "TemperingEffect",
            Kind.Buf => "BufEffect", Kind.Mastery => "BufEffect", Kind.Absolute => "AbstractAbsoluteStatEffect",
            Kind.Title => "TitleChangeListener", Kind.Cp => "CPBuff", Kind.Instance => "InstanceBuff",
            Kind.Predefined => "PlayerStatFunctions", Kind.CurrentConstructor => "StatFunction", _ => ""
        };
        FactoryPair pair = Factory(factoryName);
        GI.Require(pair.Java == r.FactoryJavaSha256 && pair.CSharp == r.FactoryCSharpSha256, "paired named source constructor");
        var result = ImmutableArray.CreateBuilder<Function>();
        void Add(string stat, GI.FunctionKind kind, int value, bool bonus, ImmutableArray<Condition> conditions,
            string raw, Program program = Program.Basic, string? @class = null, string? group = null,
            int fixedBonus = 0, int factor = 0, int? exactPriority = null)
        {
            GI.Require(StatNames.Contains(stat), "symbolic source stat");
            int index = result.Count; string entry = r.Occurrence + "/" + index.ToString(CultureInfo.InvariantCulture);
            GI.AbsField? abs = index < r.ActualFunctions.Length ? r.ActualFunctions[index].Abs : null;
            int priority = kind switch { GI.FunctionKind.Add or GI.FunctionKind.Sub => bonus ? 60 : 30,
                GI.FunctionKind.Rate => bonus ? 50 : 20, GI.FunctionKind.Set => bonus ? 70 : 40,
                GI.FunctionKind.Intrinsic => program is Program.MaxHp or Program.MaxMp ? 30 : 16,
                GI.FunctionKind.Abs => AbsPriority(abs, stat, value, bonus, r.Owner, operation, epoch),
                _ => throw new GI.Domain("function subclass") };
            if (program == Program.DuplicateDelay) priority = 60;
            if (exactPriority.HasValue) priority=exactPriority.Value;
            result.Add(new(entry, stat, @class ?? Class(kind), value, bonus, program, kind, conditions,
                r.Owner, r.OwnerType, raw, r.Source?.Identity ?? "source-predefined", r.Occurrence, index,
                priority, abs, group, fixedBonus, factor));
        }
        if (r.Selection.Kind == Kind.Predefined)
        {
            GI.Require(r.Source is null && r.OwnerType == OwnerType.Predefined && r.Owner.Owner == "predefined", "actual predefined null-owner binding");
            (string Class, string Stat, Program Program, int Priority)[] names = [
                ("PhysicalAttackFunction","PHYSICAL_ATTACK",Program.RetainedPredefined,30), ("MagicalAttackFunction","MAGICAL_ATTACK",Program.RetainedPredefined,30),
                ("AttackSpeedFunction","ATTACK_SPEED",Program.DuplicateDelay,60), ("BoostCastingTimeFunction","BOOST_CASTING_TIME",Program.RetainedPredefined,60),
                ("PvPAttackRatioFunction","PVP_ATTACK_RATIO",Program.RetainedPredefined,60), ("PDefFunction","PHYSICAL_DEFENSE",Program.RetainedPredefined,16),
                ("MaxHpFunction","MAXHP",Program.MaxHp,30), ("MaxMpFunction","MAXMP",Program.MaxMp,30),
                ("BlockFunction","BLOCK",Program.RetainedPredefined,16), ("ParryFunction","PARRY",Program.RetainedPredefined,16),
                ("EvasionFunction","EVASION",Program.RetainedPredefined,16), ("PhysicalCriticalFunction","PHYSICAL_CRITICAL",Program.RetainedPredefined,16),
                ("PhysicalAccuracyFunction","PHYSICAL_ACCURACY",Program.RetainedPredefined,16), ("PvEAttackRatioFunction","PVE_ATTACK_RATIO",Program.RetainedPredefined,16),
                ("PvEDefendRatioFunction","PVE_DEFEND_RATIO",Program.RetainedPredefined,16)];
            foreach (var name in names) Add(name.Stat, GI.FunctionKind.Intrinsic, 0, false, [], "predefined/"+name.Class, name.Program, name.Class, exactPriority:name.Priority);
        }
        else if (r.Selection.Kind == Kind.CurrentConstructor)
        {
            GI.Require(catalog.SourceOrigin==C.Origin.SyntheticFixture,"conditional basic-operator fixtures cannot admit a real selected-source code producer");
            GI.Require(r.Source is null && r.OwnerType == OwnerType.ReviewedConstructor, "constructor owner/source recipe");
            foreach (CurrentFunction current in r.ActualFunctions)
            {
                GI.FunctionKind kind = ParseKind(current.Class);
                Add(current.Stat, kind, current.Value, current.Bonus, [], "current-constructor/" + current.Entry);
            }
        }
        else
        {
            GI.Require(r.Source is not null && ReferenceEquals(r.Source.Owner, catalog)
                && r.Source.Metadata.Key.Authority == C.Authority.CSharp, "current selected catalog source");
            catalog.ValidateRecord(r.Source);
            // Matching baseline identity remains a separate raw source, not a Java file rollback.
            var key = r.Source.Metadata.Key;
            _ = catalog.Selected(key with { Authority = C.Authority.Java });
            XElement root = r.Source.Read(); string prefix = r.Source.Identity;
            if(r.Selection.Kind is Kind.Fusion or Kind.Random or Kind.Enchant or Kind.Tempering)
            {
                GI.Require(r.ItemSource is not null && ReferenceEquals(r.ItemSource.Owner,catalog)
                    && r.ItemSource.Metadata.Key.Authority==C.Authority.CSharp && r.ItemSource.Metadata.Key.Family==C.Family.Item
                    && r.ItemOwner is not null,"actual constructor item source/owner");
                r.ItemOwner.Validate(operation,epoch); catalog.ValidateRecord(r.ItemSource);
            }
            void Nodes(IEnumerable<XElement> nodes, bool skill = false, Program program = Program.Basic, string? group = null)
            {
                int sourceOrdinal = 0;
                foreach (XElement node in nodes)
                {
                    GI.Require(node.Elements().All(x => x.Name == "conditions") && node.Elements("conditions").Count() <= 1, "exact function child shape");
                    GI.FunctionKind kind = skill ? Attr(node, "func") switch
                    { "ADD" => GI.FunctionKind.Add, "PERCENT" => GI.FunctionKind.Rate, "REPLACE" => GI.FunctionKind.Set,
                        _ => throw new GI.Domain("Buf change function") } : ParseKind(node.Name.LocalName);
                    string stat = Attr(node, skill ? "stat" : "name"); int value = GI.Integer(node, "value", 0);
                    if (skill) value = unchecked(value + unchecked(GI.Integer(node, "delta", 0) * r.Selection.Level));
                    bool bonus = skill ? kind != GI.FunctionKind.Set : GI.Boolean(node, "bonus");
                    // Mastery startEffect constructs a new wrapper and does not copy Buf conditions.
                    var conditions = program==Program.Basic ? Conditions(node) : ImmutableArray<Condition>.Empty;
                    string raw = prefix + "/" + r.Selection.Kind + "/" + r.Selection.Child + "/" + sourceOrdinal++;
                    if (program == Program.WeaponMastery)
                    {
                        if (TwoHand.Contains(group!)) Add(stat, GI.FunctionKind.Rate, value, bonus, conditions, raw, program, "StatWeaponMasteryFunction", group);
                        else if (stat is "PHYSICAL_ATTACK" or "MAGICAL_ATTACK")
                        {
                            Add("MAIN_HAND_POWER", GI.FunctionKind.Rate, value, bonus, conditions, raw+"/main", program, "StatWeaponMasteryFunction", group);
                            Add("OFF_HAND_POWER", GI.FunctionKind.Rate, value, bonus, conditions, raw+"/off", program, "StatWeaponMasteryFunction", group);
                        }
                    }
                    else Add(stat, program is Program.ArmorMastery or Program.ShieldMastery ? GI.FunctionKind.Rate : kind,
                        value, bonus, conditions, raw, program, program == Program.ArmorMastery ? "StatArmorMasteryFunction" : program == Program.ShieldMastery ? "StatShieldMasteryFunction" : null,
                        group, r.Selection.ArmorFixedBonus ?? 0, r.Selection.ArmorFactor ?? 0);
                }
            }
            switch (r.Selection.Kind)
            {
                case Kind.Item:
                    RequireFamily(r, C.Family.Item); GI.Require(r.OwnerType is OwnerType.Item or OwnerType.Stone, "item/stone proxy owner");
                    IEnumerable<XElement> direct = Single(root, "modifiers", false)?.Elements() ?? [];
                    if ((r.Selection.EquipmentMask & 3) != 0)
                        direct = direct.Where(x => x.Attribute("name")?.Value is not ("ATTACK_SPEED" or "PVP_ATTACK_RATIO" or "BOOST_CASTING_TIME"));
                    Nodes(direct); break;
                case Kind.Fusion:
                    RequireFamily(r, C.Family.Item); GI.Require(r.OwnerType == OwnerType.Item && (r.Selection.EquipmentMask & 3) != 0, "fusion actual item owner/slot");
                    Nodes((Single(root,"modifiers",false)?.Elements() ?? []).Where(x => x.Attribute("name")?.Value is not ("ATTACK_SPEED" or "PVP_ATTACK_RATIO" or "BOOST_CASTING_TIME")));
                    XElement? stats = Single(root, "weapon_stats", false);
                    if (stats is not null)
                    {
                        int boost = GG.JavaInt(GG.Mul(.1f, GI.Integer(stats,"boost_magical_skill",0)));
                        float mean = GG.Div(unchecked(GI.Integer(stats,"min_damage",0)+GI.Integer(stats,"max_damage",0)),2f);
                        int attack = GG.JavaInt(GG.Mul(.1f,mean)); string fusionGroup = ItemGroup(root);
                        if (fusionGroup is "ORB" or "STAFF" or "SPELLBOOK" or "GUN" or "CANNON" or "HARP" or "KEYBLADE")
                            Add("BOOST_MAGICAL_SKILL", GI.FunctionKind.Add, boost, false, [], prefix+"/fusion-boost");
                        string attackType=Attr(r.ItemSource!.Read(),"attack_type");
                        GI.Require(attackType is "PHYSICAL" or "MAGICAL_EARTH" or "MAGICAL_WATER" or "MAGICAL_WIND" or "MAGICAL_FIRE","base item attack type binding");
                        Add(attackType=="PHYSICAL"?"PHYSICAL_ATTACK":"MAGICAL_ATTACK",GI.FunctionKind.Add,attack,false,[],prefix+"/fusion-attack");
                    }
                    break;
                case Kind.Random:
                    RequireFamily(r,C.Family.Random); GI.Require(r.OwnerType==OwnerType.RandomBonus && r.Selection.Variant>0, "current persisted random owner/index");
                    if(key.Secondary=="INVENTORY")
                        GI.Require(GI.Integer(r.ItemSource!.Read(),"rnd_bonus",0)==GI.Integer(root,"id"),"inventory/fusion original set source");
                    else
                    {
                        GI.Require(key.Secondary=="POLISH" && r.ActionSource is not null && ReferenceEquals(r.ActionSource.Owner,catalog)
                            && r.ActionSource.Metadata.Key.Authority==C.Authority.CSharp && r.ActionSource.Metadata.Key.Family==C.Family.Item,"polish action source identity");
                        XElement actions=Single(r.ActionSource.Read(),"actions",true)!;
                        GI.Require(GI.Integer(Single(actions,"polish",true)!,"set_id")==GI.Integer(root,"id"),"polish composite set source");
                    }
                    XElement[] variants=root.Elements("modifiers").ToArray(); GI.Require(r.Selection.Variant<=variants.Length,"one-based random index");
                    Nodes(variants[r.Selection.Variant-1].Elements()); break;
                case Kind.SetBonus:
                    RequireFamily(r,C.Family.Set); GI.Require(r.OwnerType==OwnerType.ItemSet && r.Selection.EquippedParts>=0,"actual set owner/count");
                    GI.Require(root.Elements("fullbonus").Count()<=1,"single full set bonus");
                    XElement[] bonuses=root.Elements("partbonus").Concat(root.Elements("fullbonus")).ToArray();
                    GI.Require(r.Selection.Child>=0 && r.Selection.Child<bonuses.Length,"ordered set bonus occurrence");
                    XElement bonus=bonuses[r.Selection.Child];
                    int required=bonus.Name=="partbonus" ? GI.Integer(bonus,"count") : root.Elements("itempart").Count();
                    GI.Require(bonus.Name=="partbonus" ? required<=r.Selection.EquippedParts : required==r.Selection.EquippedParts,"current registered set qualification");
                    Nodes(Single(bonus,"modifiers",true)!.Elements()); break;
                case Kind.Enchant:
                    RequireFamily(r,C.Family.Enchant); GI.Require(r.OwnerType==OwnerType.Enchant,"enchant actual owner");
                    GI.Require((r.ItemSource!.Read().Attribute("enchant_name")?.Value ?? ItemGroup(r.ItemSource.Read()))==key.Primary,"enchant explicit-name/group selection");
                    foreach(XElement node in Level(root,"enchant_data",r.Selection.Level).Elements("enchant_stat"))
                    {
                        string stat=Attr(node,"stat"); bool main=r.Selection.EquipmentMask is 1 or 3;
                        if(stat=="BOOST_MAGICAL_SKILL" && !main)continue;
                        Add(stat,GI.FunctionKind.Add,GI.Integer(node,"value"),false,[],prefix+"/enchant/"+result.Count);
                    }
                    break;
                case Kind.Tempering:
                    GI.Require(r.OwnerType==OwnerType.Tempering && r.Selection.Level>=0,"tempering actual owner/level");
                    if(key.Family==C.Family.Item)
                    {
                        GI.Require(root.Attribute("item_group")?.Value=="PLUME","PLUME code constructor");
                        GI.Require(r.ItemSource!.Identity==r.Source.Identity,"PLUME source/current item");
                        bool physical=Attr(root,"tempering_name")=="TSHIRT_PHYSICAL";
                        Add(physical?"PHYSICAL_ATTACK":"BOOST_MAGICAL_SKILL",GI.FunctionKind.Add,
                            unchecked(r.Selection.PlumeRandom+unchecked((physical?4:20)*r.Selection.Level)),true,[],prefix+"/plume-stat");
                        Add("MAXHP",GI.FunctionKind.Add,unchecked(150*r.Selection.Level),true,[],prefix+"/plume-hp");
                    }
                    else
                    {
                        RequireFamily(r,C.Family.Tempering);
                        GI.Require((r.ItemSource!.Read().Attribute("tempering_name")?.Value ?? ItemGroup(r.ItemSource.Read()))==key.Primary,"tempering explicit-name/group selection");
                        foreach(XElement node in Level(root,"tempering_data",r.Selection.Level).Elements("tempering_stat"))
                            Add(Attr(node,"stat"),GI.FunctionKind.Add,GI.Integer(node,"value"),false,[],prefix+"/temper/"+result.Count);
                    }
                    break;
                case Kind.Buf: case Kind.Mastery:
                    RequireFamily(r,C.Family.Skill); GI.Require(r.OwnerType==OwnerType.Effect && r.Selection.Level>=0,"actual effect owner/materialized level");
                    XElement[] effects=Single(root,"effects",true)!.Elements().ToArray();
                    GI.Require(r.Selection.Child>=0 && r.Selection.Child<effects.Length,"exact effect child ordinal");
                    XElement effect=effects[r.Selection.Child]; string tag=effect.Name.LocalName;
                    GI.Require(BufBindings.ContainsKey(tag),"named change-producing effect class");
                    // EffectTemplate conditions and other raw fields govern activation. The
                    // current registration is already known; getModifiers reads only change.
                    Program program=tag switch { "wpnmastery"=>Program.WeaponMastery,"armormastery"=>Program.ArmorMastery,"shieldmastery"=>Program.ShieldMastery,_=>Program.Basic };
                    GI.Require(r.Selection.Kind==Kind.Mastery ? program!=Program.Basic : program==Program.Basic,"actual factory wrapper");
                    string? group=program==Program.WeaponMastery ? GroupToken(Attr(effect,"weapon")) : program==Program.ArmorMastery ? ArmorToken(Attr(effect,"armor")) : null;
                    // These are current constructor fields, not freshly calculated base/equipment
                    // results. Presence is independent of CompleteOriginalFields and literal zero.
                    GI.Require(program!=Program.ArmorMastery || r.Selection.ArmorFixedBonus.HasValue
                        && r.Selection.ArmorFactor is >=0,"known current armor constructor fields");
                    Nodes(effect.Elements("change"),true,program,group); break;
                case Kind.Absolute:
                    RequireFamily(r,C.Family.Absolute); GI.Require(r.OwnerType==OwnerType.Effect,"absolute set actual effect owner");
                    Nodes(Single(root,"modifiers",true)!.Elements()); break;
                case Kind.Title: case Kind.Cp:
                    RequireFamily(r,r.Selection.Kind==Kind.Title?C.Family.Title:C.Family.Cp);
                    GI.Require(r.OwnerType==(r.Selection.Kind==Kind.Title?OwnerType.Title:OwnerType.Cp),"actual title/CP owner");
                    Nodes(Single(root,"modifiers",true)!.Elements()); break;
                case Kind.Instance:
                    RequireFamily(r,C.Family.Instance); GI.Require(r.OwnerType==OwnerType.Instance && !r.Selection.OriginalOperands.IsDefault,"instance registration-time operands");
                    XElement[] penalties=root.Elements("penalty_attr").ToArray();
                    GI.Require(r.Selection.OriginalOperands.Length==penalties.Length,"complete original instance base inputs");
                    for(int i=0;i<penalties.Length;i++)
                    {
                        var original=r.Selection.OriginalOperands[i]; original.Current.Validate(operation,epoch);
                        GI.Require(original.ChildOrdinal==i && original.Current.Owner==r.Owner.Owner && original.Current.Generation==r.Owner.Generation
                            && original.Field=="InstanceBuff.applyEffect.getStat(stat,0).getBase","original instance constructor field binding");
                        XElement node=penalties[i]; int value=GI.Integer(node,"value");
                        string func=Attr(node,"func");GI.Require(func is "ADD" or "PERCENT" or "REPLACE","instance Func binding");
                        value=func=="PERCENT" ? JavaDivide(unchecked(original.Value*value),100) : value;
                        Add(Attr(node,"stat"),GI.FunctionKind.Add,value,true,[],prefix+"/instance/"+i);
                    }
                    break;
                default: throw new GI.Domain("unreviewed source factory");
            }
        }
        GI.Require(result.Count==r.ActualFunctions.Length,"complete actually registered constructor functions/siblings");
        for(int i=0;i<result.Count;i++)
        {
            Function expected=result[i]; CurrentFunction actual=r.ActualFunctions[i]; actual.Owner.Validate(operation,epoch);
            GI.Require(actual.CompleteOriginalFields==true && actual.Entry==expected.Entry && actual.Stat==expected.Stat
                && actual.Class==expected.Class && actual.Value==expected.Value && actual.Bonus==expected.Bonus
                && actual.Owner.Owner==r.Owner.Owner && actual.Owner.Generation==r.Owner.Generation
                && !string.IsNullOrWhiteSpace(actual.OriginalObjectIdentity),"source/current original function identity/fields");
            GI.Require(expected.Program is not (Program.WeaponMastery or Program.ArmorMastery)
                || actual.MasteryGroup==expected.Group,"current mastery subtype/group field");
            GI.Require(expected.Program!=Program.ArmorMastery || actual.ArmorFixedBonus.HasValue
                && actual.ArmorFactor.HasValue && actual.ArmorFixedBonus==expected.ArmorFixedBonus
                && actual.ArmorFactor==expected.ArmorFactor,"current armor subtype constructor fields");
            result[i]=expected with { OriginalObjectIdentity=actual.OriginalObjectIdentity };
        }
        return result.ToImmutable();
    }
    // Exact Java-bound spellings, not Enum.TryParse numeric/flags acceptance. Raw XML
    // absence is retained; only ItemTemplate.itemGroup has the proved NONE initializer.
    private static readonly ImmutableHashSet<string> Groups=ImmutableHashSet.Create(StringComparer.Ordinal,
        "NONE","NOWEAPON","SWORD","GREATSWORD","EXTRACT_SWORD","DAGGER","MACE","ORB","SPELLBOOK","POLEARM","STAFF","BOW","HARP","GUN","CANNON","KEYBLADE","SHIELD","TORSO","GLOVE","SHOULDER","PANTS","SHOES","RB_TORSO","RB_GLOVE","RB_SHOULDER","RB_PANTS","RB_SHOES","CL_TORSO","CL_GLOVE","CL_SHOULDER","CL_PANTS","CL_SHOES","LT_TORSO","LT_GLOVE","LT_SHOULDER","LT_PANTS","LT_SHOES","CH_TORSO","CH_GLOVE","CH_SHOULDER","CH_PANTS","CH_SHOES","PL_TORSO","PL_GLOVE","PL_SHOULDER","PL_PANTS","PL_SHOES","EARRING","RING","NECKLACE","BELT","WING","PLUME","HEAD","LT_HEADS","CL_HEADS","CL_MULTISLOT","CL_SHIELD","POWER_SHARDS","STIGMA","ARROW","NPC_MACE","TOOLRODS","TOOLHOES","TOOLPICKS","MANASTONE","SPECIAL_MANASTONE","RECIPE","ENCHANTMENT","PACK_SCROLL","FLUX","BALIC_EMOTION","BALIC_MATERIAL","RAWHIDE","SOULSTONE","GATHERABLE","GATHERABLE_BONUS","DROP_MATERIAL","COINS","MEDALS","QUEST","KEY","CRAFT_BOOST","TAMPERING","COMBINATION","SKILLBOOK","GODSTONE","STIGMA_SHARD");
    private static readonly ImmutableHashSet<string> ArmorTypes=ImmutableHashSet.Create(StringComparer.Ordinal,
        "ALL_ARMOR","NONE","CHAIN","CLOTHES","LEATHER","PLATE","ROBE","SHIELD","ARROW","WING","ONE_HAND","TWO_HAND","STIGMA","PLUME");
    internal static string GroupToken(string token)
    {GI.Require(Groups.Contains(token),"unknown/case-changed ItemGroup token");return token;}
    internal static string ArmorToken(string token)
    {GI.Require(ArmorTypes.Contains(token),"unknown/case-changed ItemSubType token");return token;}
    internal static string ItemGroup(XElement item)
    {string? raw=item.Attribute("item_group")?.Value;return raw is null?"NONE":GroupToken(raw);}
    private static ImmutableArray<string> WeaponTokens(XElement node)
    {
        XAttribute? raw=node.Attribute("weapon");GI.Require(raw is not null,"missing weapon list; no NONE default");
        return raw.Value.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Select(GroupToken).ToImmutableArray();
    }
    internal static ImmutableArray<Condition> Conditions(XElement node)
    {
        GI.Require(node.Elements("conditions").Count()<=1,"single ordered condition list");
        var rows=ImmutableArray.CreateBuilder<Condition>();
        foreach(XElement condition in node.Elements("conditions").SelectMany(x=>x.Elements()))
        {
            GI.Require(!condition.HasElements,"condition shape");
            rows.Add(condition.Name.LocalName switch
            {
                "charge"=>new(Predicate.Charge,GI.Integer(condition,"value"),[]),
                "onfly"=>new(Predicate.OnFly,0,[]),
                "weapon"=>new(Predicate.Weapon,0,WeaponTokens(condition)),
                _=>throw new GI.Domain("unreviewed reached function predicate")
            });
        }
        return rows.ToImmutable();
    }
    internal static GI.FunctionKind ParseKind(string kind)=>kind switch
    {
        "add" or "StatAddFunction"=>GI.FunctionKind.Add,"sub" or "StatSubFunction"=>GI.FunctionKind.Sub,
        "rate" or "StatRateFunction"=>GI.FunctionKind.Rate,"set" or "StatSetFunction"=>GI.FunctionKind.Set,
        "abs" or "StatAbsFunction"=>GI.FunctionKind.Abs,_=>throw new GI.Domain("source function class binding")
    };
    private static string Class(GI.FunctionKind kind)=>kind switch
    { GI.FunctionKind.Add=>"StatAddFunction",GI.FunctionKind.Sub=>"StatSubFunction",GI.FunctionKind.Rate=>"StatRateFunction",
        GI.FunctionKind.Set=>"StatSetFunction",GI.FunctionKind.Abs=>"StatAbsFunction",_=>throw new GI.Domain("function class") };
    private static int AbsPriority(GI.AbsField? abs,string stat,int value,bool bonus,GI.Binding owner,string operation,string epoch)
    {
        var function=new GI.Function(GI.FunctionKind.Abs,stat,value,bonus,[],owner.Owner,0,owner,abs,GI.AbsSource);
        return function.Priority(operation,epoch);
    }
    internal static string Attr(XElement node,string name)
    { string? value=node.Attribute(name)?.Value; GI.Require(!string.IsNullOrWhiteSpace(value),"required source attribute "+name); return value; }
    internal static XElement? Single(XElement node,string name,bool required)
    { XElement[] rows=node.Elements(name).ToArray(); GI.Require(rows.Length<=1 && (!required || rows.Length==1),"source child "+name); return rows.FirstOrDefault(); }
    private static XElement Level(XElement root,string child,int level)
    { XElement[] rows=root.Elements(child).Where(x=>GI.Integer(x,"level")==level).ToArray(); GI.Require(rows.Length==1,"selected table level");return rows[0]; }
    private static void RequireFamily(Registration r,C.Family family)=>GI.Require(r.Source?.Metadata.Key.Family==family,"source family selector");
    internal static int JavaDivide(int a,int b)=>a==int.MinValue && b==-1 ? int.MinValue : a/b;
}
