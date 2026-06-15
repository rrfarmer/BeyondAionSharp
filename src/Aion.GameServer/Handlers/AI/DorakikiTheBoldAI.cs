using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/beshmundirTemple/DorakikiTheBoldAI (@author Cheatkiller).</summary>
[AIName("dorakiki_the_bold")]
public class DorakikiTheBoldAI : AggressiveNpcAI
{
    public DorakikiTheBoldAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttackComplete()
    {
        base.HandleAttackComplete();
        if (GetEffectController().HasAbnormalEffect(18901))
        {
            GetEffectController().RemoveEffect(18901);
        }
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        if (GetEffectController().HasAbnormalEffect(18901))
        {
            GetEffectController().RemoveEffect(18901);
        }
    }
}
