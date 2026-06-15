using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Source
/// </summary>
[AIName("incarnate")]
public class IncarnateAI : SiegeNpcAI
{
    public IncarnateAI(Npc owner) : base(owner)
    {
    }

    // spawn for quest
    protected override void HandleDied()
    {
        base.HandleDied();
        if (GetOwner().GetNpcId() == 259614)
        {
            Spawn(701237, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0);
            DespawnClaw();
        }
    }

    private void DespawnClaw()
    {
        Npc claw = GetPosition().GetWorldMapInstance().GetNpc(701237);
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            claw.GetController().Delete();
            return ValueTask.CompletedTask;
        }, 60000L * 5);
    }
}
