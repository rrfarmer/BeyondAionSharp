using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Retail;
using Aion.GameServer.Utils.Stats;
using Microsoft.Extensions.Logging;
using Aion.GameServer.Utils;
using System.Runtime.CompilerServices;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>
/// Explicit 4.8 host compatibility mapping. These are current managed stats, not invented
/// native actor images. Player stats are in ordinary units (native normalization ratio1000).
/// Each lookup executes the existing registered item/effect functions on its actual owner.
/// </summary>
internal static class SkyTempleCombatInputs
{
    private static readonly ILogger Log = AionLog.For(nameof(SkyTempleCombatInputs));
    private static readonly ConditionalWeakTable<Npc,object> LegacyNpcPveWarnings = new();
    internal const uint PlayerStatRatio = 1000;
    internal static byte Pose(Creature actor) => (actor.GetState()&15)==5 ? (byte)5 : (byte)0;
    internal static SkillElement Element(string token) => token.ToLowerInvariant() switch
    {
        "physical" => SkillElement.NONE,
        "magical_fire" => SkillElement.FIRE,
        "magical_water" => SkillElement.WATER,
        "magical_air" => SkillElement.WIND,
        "magical_earth" => SkillElement.EARTH,
        "magical_light" => SkillElement.LIGHT,
        "magical_dark" => SkillElement.DARK,
        _ => throw new InvalidDataException("Unsupported retail attack type: " + token)
    };

    internal static RetailNpcNormalScope Scope(int npcId) => RetailNpcNormalScope.Create(
        RetailNpcNormalScope.MainBinarySha256, RetailNpcNormalActorKind.OrdinaryOwnerlessNpc,
        RetailNpcNormalActorKind.Player, npcId, 0);

    internal static uint Unsigned(int value, string name) => value >= 0
        ? (uint)value : throw new InvalidOperationException("Negative current " + name);
    internal static ushort Word(int value, string name) => value is >= 0 and <= ushort.MaxValue
        ? (ushort)value : throw new InvalidOperationException("Current " + name + " is outside native word domain");
    internal static short SignedWord(int value, string name) => value is >= short.MinValue and <= short.MaxValue
        ? (short)value : throw new InvalidOperationException("Current " + name + " is outside native signed-word domain");

    internal static int Moved(Creature actor, StatEnum stat, int value) => checked((int)
        StatFunctions.AdjustStatByMovementModifier(actor, stat, value));

    internal static int ElementalFloor(int level) => level is >= 1 and <= 50 ? -1000
        : level is >= 51 and <= 100 ? -500 - 10 * level
        : throw new ArgumentOutOfRangeException(nameof(level));

    internal static int ElementalDefense(Player target, SkillElement element)
    {
        StatEnum stat = element.GetStatForElement() ?? throw new ArgumentException("Physical attack has no element");
        return Moved(target, stat, target.GetGameStats().GetResistance(stat).GetCurrent());
    }

    internal static int CriticalReduction(Creature target, bool physical) => target.GetGameStats().GetStat(
        physical ? StatEnum.PHYSICAL_CRITICAL_DAMAGE_REDUCE : StatEnum.MAGICAL_CRITICAL_DAMAGE_REDUCE, 0).GetCurrent();

    internal static (int Attack, int Defense) Pve(Creature source, Creature target, bool physical)
    {
        Player? player = target as Player; Npc? selectedNpc = source as Npc;
        bool npcAttack = player is not null && selectedNpc is not null;
        if (!npcAttack) { player = source as Player; selectedNpc = target as Npc; }
        if (player is not null && selectedNpc is not null && RetailNpcStatCatalog.TryGet(selectedNpc, out var profile))
        {
            var inputs = RetailNpcStatCatalog.CombatPve ?? throw new InvalidOperationException("Missing selected statistics combat PvE inputs");
            // Native User getters add the selected WorldID base outside their
            // precomputed bonus-point program. NPC slot190 is a counterparty
            // delta added AFTER that getter, in its original ratio argument.
            var general = npcAttack ? StatEnum.PVE_DEFEND_RATIO : StatEnum.PVE_ATTACK_RATIO;
            var typed = npcAttack ? physical ? StatEnum.PVE_DEFEND_RATIO_PHYSICAL : StatEnum.PVE_DEFEND_RATIO_MAGICAL
                : physical ? StatEnum.PVE_ATTACK_RATIO_PHYSICAL : StatEnum.PVE_ATTACK_RATIO_MAGICAL;
            int current = unchecked(PveStat(player,general) + PveStat(player,typed));
            current = unchecked(current + inputs.PlayerBase(player.GetWorldId(),player.GetRace(),physical,attack:!npcAttack));
            current = unchecked(current + inputs.NpcCounterpartyDelta(profile,player.GetRace(),physical,npcAttack));
            // Native unowned NPC slot190 reads only its source table/template.
            // Legacy NPC PvE stat programs are not extra native operands.
            var stats = selectedNpc.GetGameStats();
            if (new[] { StatEnum.PVE_ATTACK_RATIO,StatEnum.PVE_ATTACK_RATIO_PHYSICAL,StatEnum.PVE_ATTACK_RATIO_MAGICAL,
                StatEnum.PVE_DEFEND_RATIO,StatEnum.PVE_DEFEND_RATIO_PHYSICAL,StatEnum.PVE_DEFEND_RATIO_MAGICAL }
                .Any(s => stats.GetStatsSorted(s).Any()))
                LegacyNpcPveWarnings.GetValue(selectedNpc,n =>
                {
                    Log.LogWarning("RETAIL_STAT_NPC_PVE_PROGRAM_IGNORED npc={Npc}; verified native slot190 uses selected NPC table/template operands",n.GetNpcId());
                    return new object();
                });
            return npcAttack ? (0,current) : (current,0);
        }
        var a = source.GetGameStats(); var d = target.GetGameStats();
        // Retain current4.8 registered gear/effect programs. Selected retail map bases are
        // supplied separately by the catalog readiness contract, not copied from Java maps.
        return (checked(PveStat(source,StatEnum.PVE_ATTACK_RATIO) + a.GetStat(
                physical ? StatEnum.PVE_ATTACK_RATIO_PHYSICAL : StatEnum.PVE_ATTACK_RATIO_MAGICAL, 0).GetCurrent()),
            checked(PveStat(target,StatEnum.PVE_DEFEND_RATIO) + d.GetStat(
                physical ? StatEnum.PVE_DEFEND_RATIO_PHYSICAL : StatEnum.PVE_DEFEND_RATIO_MAGICAL, 0).GetCurrent()));
    }

    internal static int PveStat(Creature actor,StatEnum name)
    {
        if(actor is not Player)return actor.GetGameStats().GetStat(name,0).GetCurrent();
        var stat=new AdditionStat(name,0,actor);
        foreach(var original in actor.GetGameStats().GetStatsSorted(name))
        {
            IStatFunction inner=original;
            while(inner is StatFunctionProxy proxy)inner=proxy.GetProxiedFunction();
            // Exclude ONLY the predefined legacy map program, before any later percent,
            // fixed-rate or integer conversion. Keep the actual proxy for owner conditions.
            if(inner.GetType()==typeof(PvEAttackRatioFunction)||inner.GetType()==typeof(PvEDefendRatioFunction))continue;
            if(original.Validate(stat))original.Apply(stat,new HashSet<CalculationType>());
        }
        StatCapUtil.CalculateBaseValue(stat,actor);
        return stat.GetCurrent();
    }
}
