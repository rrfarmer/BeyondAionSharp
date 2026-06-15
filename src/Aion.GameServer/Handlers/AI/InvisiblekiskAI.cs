using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/InvisiblekiskAI (Cheatkiller, Bobobear).</summary>
[AIName("invisible_kisk")]
public class InvisiblekiskAI : KiskAI
{
    public InvisiblekiskAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        AIActions.UseSkill(this, GetHideSkillId());
    }

    private int GetHideSkillId()
    {
        switch (GetNpcId())
        {
            case 701768:
            case 701770:
                return 21261;
            case 701769:
            case 701771:
                return 21262;
        }
        return 0;
    }
}
