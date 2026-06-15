using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author xTz
/// </summary>
[AIName("monolithicambusher")]
public class MonolithicAmbusherAI : AggressiveNpcAI
{
    private bool hasHelped;

    public MonolithicAmbusherAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        hasHelped = false;
    }

    protected override void HandleCreatureAggro(Creature creature)
    {
        base.HandleCreatureAggro(creature);
        if (!hasHelped)
        {
            hasHelped = true;
            Help(creature);
        }
    }

    private void Help(Creature creature)
    {
        GetKnownList().ForEachNpc(npc =>
        {
            if (IsInRange(npc, 60))
            {
                if (!npc.IsDead() && npc.GetNpcId() == 216215 && (int)npc.GetSpawn().GetY() == (int)GetSpawnTemplate().GetY())
                {
                    npc.GetAi().OnCreatureEvent(AiEventType.CREATURE_AGGRO, creature);
                }
            }
        });
    }
}
