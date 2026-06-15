using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/quests/InfiltratorsAI (Cheatkiller).</summary>
[AIName("infiltrator")]
public class InfiltratorsAI : AggressiveNpcAI
{
    public InfiltratorsAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        int owner = GetOwner().GetNpcId();
        int spawnNpc = 0;
        switch (owner)
        {
            case 282913:
                spawnNpc = 282914;
                break;
            case 282918:
                spawnNpc = 282920;
                break;
            case 282920:
                spawnNpc = 282922;
                break;
            case 282917:
                spawnNpc = 282915;
                break;
            case 282915:
                spawnNpc = 282916;
                break;
            case 282919:
                spawnNpc = 282921;
                break;
            case 282921:
                spawnNpc = 282923;
                break;
        }
        Spawn(spawnNpc, GetOwner().GetSpawn().GetX(), GetOwner().GetSpawn().GetY(), GetOwner().GetSpawn().GetZ(), (sbyte)GetOwner().GetSpawn().GetHeading());
    }
}
