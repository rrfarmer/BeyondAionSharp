using System.Collections.Concurrent;
using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/pernon/GaleCycloneAI (xTz).</summary>
[AIName("gale_cyclone")]
public class GaleCycloneAI : NpcAI
{
    private readonly ConcurrentDictionary<int, GaleCycloneObserver> observed = new ConcurrentDictionary<int, GaleCycloneObserver>();

    public GaleCycloneAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleCreatureSee(Creature creature)
    {
        if (creature is Player player)
        {
            observed.GetOrAdd(player.GetObjectId(), _ =>
            {
                GaleCycloneObserver galeCycloneObserver = new GaleCycloneObserver(this, player, GetOwner());
                player.GetObserveController().AddObserver(galeCycloneObserver);
                return galeCycloneObserver;
            });
        }
    }

    protected override void HandleDied()
    {
        Clear();
        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        Clear();
        base.HandleDespawned();
    }

    private void Clear()
    {
        foreach (GaleCycloneObserver o in observed.Values)
            o.Remove();
    }

    private class GaleCycloneObserver : ActionObserver
    {
        private readonly GaleCycloneAI ai;
        private readonly Player player;
        private readonly Creature creature;
        private double oldRange;

        public GaleCycloneObserver(GaleCycloneAI ai, Player player, Creature creature)
            : base(ObserverType.MOVE)
        {
            this.ai = ai;
            this.player = player;
            this.creature = creature;
            oldRange = PositionUtil.GetDistance(player, creature);
        }

        public override void Moved()
        {
            double newRange = PositionUtil.GetDistance(player, creature);
            if (creature.IsDead() || creature.GetLifeStats().IsAboutToDie() || !creature.GetKnownList().Sees(player))
            {
                Remove();
                return;
            }

            if (oldRange > 12 && newRange <= 12)
            {
                SkillEngine.SkillEngine.GetInstance().GetSkill(creature, 20528, 50, player).UseNoAnimationSkill();
            }

            oldRange = newRange;
        }

        public void Remove()
        {
            player.GetObserveController().RemoveObserver(this);
        }

        public override void OnRemoved()
        {
            ai.observed.TryRemove(player.GetObjectId(), out _);
        }
    }
}
