using System;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/theShugoEmperorsVault/IDSweep_Shugos (Yeats).</summary>
[AIName("IDSweep_shugos")]
public class IDSweep_Shugos : AggressiveNoLootNpcAI
{
    private int baseDamage;

    public IDSweep_Shugos(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        IInstanceHandler handler = GetOwner().GetPosition().GetWorldMapInstance().GetInstanceHandler();
        InstanceScore reward;
        if (handler != null)
        {
            reward = handler.GetInstanceScore();
            if (reward != null)
            {
                if (reward.GetInstanceProgressionType() == InstanceProgressionType.END_PROGRESS)
                    GetOwner().GetController().Delete();
            }
        }
        baseDamage = GetOwner().GetGameStats().GetStatsTemplate().GetAttack();
    }

    public override float ModifyOwnerDamage(float damage, Creature effected, Effect effect)
    {
        if (effect == null)
        {
            int rndDamage = Rnd.Get(-(int)Math.Floor(baseDamage * 0.2f + 0.5f), (int)Math.Floor(baseDamage * 0.25f + 0.5f));
            damage = baseDamage + rndDamage;
        }
        return damage;
    }
}
