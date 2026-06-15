using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/drakenspire/WaveDefenderAI (@author Estrayl).</summary>
[AIName("wave_defender")]
public class WaveDefenderAI : AggressiveNpcAI
{
    private readonly AtomicBoolean isDestinationReached = new AtomicBoolean();

    public WaveDefenderAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleMoveArrived()
    {
        base.HandleMoveArrived();
        if (GetOwner().GetMoveController().IsStop() && isDestinationReached.CompareAndSet(false, true))
            ScheduleLocationUpdate();
    }

    private void ScheduleLocationUpdate()
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            Spawn(GetOwner().GetRace() == Race.ELYOS ? 236248 : 236249, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)GetHeading());
            GetOwner().GetController().Delete();
            return ValueTask.CompletedTask;
        }, 3000L);
    }

    private byte GetHeading()
    {
        return GetOwner().GetSpawn().GetWalkerId() switch
        {
            "301390000_NPCPathFunction_Npc_Path07" => 100,
            "301390000_NPCPathFunction_Npc_Path08" => 110,
            "301390000_NPCPathFunction_Npc_Path09" => 3,
            "301390000_NPCPathFunction_Npc_Path10" => 10,
            "301390000_NPCPathFunction_Npc_Path13" => 77,
            "301390000_NPCPathFunction_Npc_Path14" => 64,
            "301390000_NPCPathFunction_Npc_Path15" => 64,
            "301390000_NPCPathFunction_Npc_Path16" => 55,
            _ => 30,
        };
    }
}
