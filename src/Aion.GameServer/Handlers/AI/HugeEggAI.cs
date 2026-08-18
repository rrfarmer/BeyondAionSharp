using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/inggison/HugeEggAI (Cheatkiller).</summary>
[AIName("hugeegg")]
public class HugeEggAI : GeneralNpcAI
{
    public HugeEggAI(Npc owner)
        : base(owner)
    {
    }

    public override bool CanThink()
    {
        return false;
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        return 1;
    }

    /// <summary>Retail <c>LF4_CondorEgg</c> gives the condor three minutes before it leaves.</summary>
    private const int CondorLife = 180;

    protected override void HandleDied()
    {
        base.HandleDied();
        if (Rnd.NextBoolean())
        {
            SpawnFor(217097, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0, CondorLife);
            AIActions.DeleteOwner(this);
        }
    }
}
