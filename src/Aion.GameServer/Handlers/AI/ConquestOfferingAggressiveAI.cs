using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/ConquestOfferingAggressiveAI (Yeats).</summary>
[AIName("conquest_offering_aggressive")]
public class ConquestOfferingAggressiveAI : AggressiveNpcAI
{
    private Npc spawner;

    public ConquestOfferingAggressiveAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        FindAndSetCreator();
    }

    private void FindAndSetCreator()
    {
        if (GetCreatorId() != 0 && GetPosition().GetWorldMapInstance().GetObject(GetCreatorId()) is Npc npc)
            spawner = npc;
    }

    protected override void HandleDied()
    {
        if (spawner != null && !spawner.IsDead())
        {
            spawner.GetAi().OnCustomEvent(1); // notify spawner that npc died
            SpawnRandomNpc();
        }

        base.HandleDied();
    }

    // spawn a shugo or a portal
    private void SpawnRandomNpc()
    {
        int npcId = 0;
        if (Rnd.Chance() < 55)
        {
            if (Rnd.Chance() < 45)
            { // spawn a shugo
                npcId = 856175 + Rnd.Get(0, 3);
            }
            else
            { // spawn a portal
                npcId = GetOwner().GetWorldId() == 210050000 ? 833018 : 833021;
            }
        }

        if (npcId != 0)
            Spawn(npcId, GetOwner().GetX() + 0.3f, GetOwner().GetY() + 0.3f, GetOwner().GetZ() + 0.2f, (sbyte)GetOwner().GetHeading());
    }
}
