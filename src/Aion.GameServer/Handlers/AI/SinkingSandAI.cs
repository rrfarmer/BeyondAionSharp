using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/tiamatStrongHold/SinkingSandAI (@author Cheatkiller).</summary>
[AIName("sinkingsand")]
public class SinkingSandAI : NpcAI
{
    public SinkingSandAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        Useskill();
    }

    private void Useskill()
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            AIActions.UseSkill(this, 20723);
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                AIActions.DeleteOwner(this);
                return System.Threading.Tasks.ValueTask.CompletedTask;
            }, 1000L);
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, 3000L);
    }
}
