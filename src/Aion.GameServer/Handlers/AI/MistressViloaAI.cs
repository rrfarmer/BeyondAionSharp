using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Ai;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/nightmareCircus/MistressViloaAI (@author Farlon).</summary>
[AIName("mistressviloa")]
public class MistressViloaAI : SummonerAI
{
    public MistressViloaAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        PacketSendUtility.BroadcastMessage(GetOwner(), 1501135, 3000);
    }

    protected override void HandleSpawnFinished(SummonGroup summonGroup)
    {
        PacketSendUtility.BroadcastMessage(GetOwner(), 1501134, 1000);
    }
}
