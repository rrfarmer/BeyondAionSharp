using System.Runtime.CompilerServices;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Utils.Stats;
using Aion.GameServer.World;

namespace Aion.GameServer.Services.Instance;

public sealed class InstanceScaler : IStatOwner
{
    private static readonly InstanceScaler Instance = new();
    private static readonly ConditionalWeakTable<WorldMapInstance, Scaling> Scalings = new();

    private InstanceScaler()
    {
    }

    public static void OnEnterInstance(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        if (!InstanceConfig.INSTANCE_SCALING_ENABLE)
            return;

        WorldMapInstance instance = player.GetPosition().GetWorldMapInstance();
        if (!CanScale(instance))
            return;

        Scaling scaling = Scalings.GetValue(instance, _ => new Scaling());
        lock (scaling)
        {
            if (scaling.Update(instance))
                Rescale(instance, scaling);
        }
    }

    public static void OnBeforeSpawn(Npc npc)
    {
        if (!InstanceConfig.INSTANCE_SCALING_ENABLE)
            return;

        WorldMapInstance instance = npc.GetPosition().GetWorldMapInstance();
        if (!CanScale(instance) || !Scalings.TryGetValue(instance, out Scaling? scaling))
            return;

        lock (scaling)
        {
            if (ShouldScale(npc, instance))
                ScaleNpc(npc, scaling);
        }
    }

    private static void Rescale(WorldMapInstance instance, Scaling scaling)
    {
        foreach (Npc npc in instance.GetNpcs())
        {
            if (ShouldScale(npc, instance))
                ScaleNpc(npc, scaling);
        }
    }

    public static bool CanScale(WorldMapInstance instance)
    {
        return InstanceConfig.INSTANCE_SCALING_ENABLE
            && instance.GetMaxPlayers() > 1
            && instance.GetParent().IsInstanceType()
            && !InstanceConfig.INSTANCE_SCALING_EXCLUDED_MAPS.Contains(instance.GetMapId());
    }

    private static bool ShouldScale(Npc npc, WorldMapInstance instance)
    {
        if (npc.IsDead())
            return false;
        Aion.GameServer.Model.GameObjects.Players.Player? player = instance.GetPlayersInside().FirstOrDefault(p => !p.IsStaff());
        return player is not null && npc.IsEnemyFrom(player);
    }

    private static void ScaleNpc(Npc npc, Scaling scaling)
    {
        npc.GetGameStats().EndEffect(Instance);
        if (scaling.StatFunctions.Count > 0)
            npc.GetGameStats().AddEffect(Instance, scaling.StatFunctions);
    }

    public static float CalculateMultiplier(WorldMapInstance instance, float floor, int playerCount) =>
        CalculateMultiplier(instance.GetMaxPlayers(), floor, playerCount);

    internal static float CalculateMultiplier(int maxPlayers, float floor, int playerCount) =>
        Math.Max(floor, (float)Math.Min(playerCount, maxPlayers) / maxPlayers);

    internal sealed class Scaling
    {
        private int playerCount;

        internal IReadOnlyList<InstanceScalerStatFunction> StatFunctions { get; private set; } = Array.Empty<InstanceScalerStatFunction>();

        internal bool Update(WorldMapInstance instance)
        {
            int currentPlayerCount = instance.GetPlayersInside().Count(p => !p.IsStaff());
            return Update(currentPlayerCount, instance.GetMaxPlayers());
        }

        internal bool Update(int currentPlayerCount, int maxPlayers)
        {
            if (playerCount >= currentPlayerCount)
                return false;
            playerCount = currentPlayerCount;
            StatFunctions = CreateStatFunctions(maxPlayers, currentPlayerCount);
            return true;
        }

        internal static IReadOnlyList<InstanceScalerStatFunction> CreateStatFunctions(int maxPlayers, int playerCount)
        {
            var statFunctions = new List<InstanceScalerStatFunction>();
            float hpMultiplier = CalculateMultiplier(maxPlayers, InstanceConfig.INSTANCE_SCALING_HP_FLOOR, playerCount);
            float damageMultiplier = CalculateMultiplier(maxPlayers, InstanceConfig.INSTANCE_SCALING_DMG_FLOOR, playerCount);
            if (hpMultiplier != 1)
                statFunctions.Add(new InstanceScalerStatFunction(StatEnum.MAXHP, hpMultiplier));
            if (damageMultiplier != 1)
            {
                statFunctions.Add(new InstanceScalerStatFunction(StatEnum.PHYSICAL_ATTACK, damageMultiplier));
                statFunctions.Add(new InstanceScalerStatFunction(StatEnum.MAGICAL_ATTACK, damageMultiplier));
                statFunctions.Add(new InstanceScalerStatFunction(StatEnum.BOOST_SPELL_ATTACK, damageMultiplier));
            }
            return statFunctions;
        }
    }

    internal sealed class InstanceScalerStatFunction : StatFunction
    {
        private readonly float rate;

        internal InstanceScalerStatFunction(StatEnum stat, float rate)
        {
            Stat = stat;
            this.rate = rate;
        }

        public override void Apply(Stat2 stat, params CalculationType[] calculationTypes)
        {
            stat.SetBaseRate(stat.GetBaseRate() * rate);
            stat.SetBonusRate(stat.GetBonusRate() * rate);
        }

        public override int GetPriority() => 120;
    }
}
