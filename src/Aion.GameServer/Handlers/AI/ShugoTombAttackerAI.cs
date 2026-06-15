using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Ritsu, Estrayl
/// </summary>
[AIName("shugo_tomb_attacker")]
public class ShugoTombAttackerAI : GeneralNpcAI
{
    private static readonly List<int> npc_ids = new List<int> { 831251, 831250, 831304, 831305, 831130 };
    private readonly AtomicBoolean isWalkerComplete = new AtomicBoolean(false);
    private bool canThink = true;

    public ShugoTombAttackerAI(Npc owner) : base(owner)
    {
    }

    public override bool CanThink()
    {
        return canThink;
    }

    protected override void HandleSpawned()
    {
        canThink = false;
        base.HandleSpawned();
    }

    protected override void HandleMoveArrived()
    {
        base.HandleMoveArrived();
        if (GetOwner().GetMoveController().GetCurrentStep().IsLastStep() && IsDestinationReached() && isWalkerComplete.CompareAndSet(false, true))
        {
            GetSpawnTemplate().SetWalkerId(null);
            WalkManager.StopWalking(this);
            canThink = true;
            HandleHate();
        }
    }

    protected void HandleHate()
    {
        EmoteManager.EmoteStopAttacking(GetOwner());
        List<Npc> towers = new List<Npc>(
            npc_ids.SelectMany(id => GetOwner().GetPosition().GetWorldMapInstance().GetNpcs(id))
                   .Where(npc => npc != null && !npc.IsDead() && PositionUtil.IsInRange(GetOwner(), npc, 30)).ToList());

        if (towers.Count == 0)
            AIActions.DeleteOwner(this);

        towers.Sort((a, b) => PositionUtil.GetDistance(GetOwner(), a).CompareTo(PositionUtil.GetDistance(GetOwner(), b)));

        int hateToAdd = 1000000;
        foreach (Npc tower in towers)
        {
            GetOwner().GetAggroList().AddHate(tower, hateToAdd);
            hateToAdd -= 50000;
        }
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.IS_IMMUNE_TO_ABNORMAL_STATES or AIQuestion.ALLOW_DECAY or AIQuestion.REWARD_LOOT => false,
            _ => base.Ask(question),
        };
    }
}
