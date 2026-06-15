using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/beshmundirTemple/DivineArtifactAI (@author Luzien).</summary>
[AIName("divineartifact")]
public class DivineArtifactAI : AggressiveNpcAI
{
    private bool cooldown = false;

    public DivineArtifactAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (!cooldown)
        {
            AIActions.UseSkill(this, 18915);
            SetCD();
        }
    }

    private void SetCD()
    { // ugly hack to prevent overflow TODO: remove on AI improve
        cooldown = true;

        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            cooldown = false;
            return ValueTask.CompletedTask;
        }, 1000L);
    }
}
