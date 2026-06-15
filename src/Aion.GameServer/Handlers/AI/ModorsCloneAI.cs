using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Ritsu, Estrayl, Yeats
/// </summary>
[AIName("modors_clone")]
public class ModorsCloneAI : AggressiveNoLootNpcAI
{
    private float modifier = 1.35f;

    public ModorsCloneAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        if (GetNpcId() == 284383 || GetNpcId() == 855244)
        {
            ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                PacketSendUtility.BroadcastMessage(GetOwner(), 348682);
                return ValueTask.CompletedTask;
            }, 1000L);
        }
        else
        {
            SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(21181, GetOwner(), GetOwner()); // Malevolence
            ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                PacketSendUtility.BroadcastMessage(GetOwner(), 348683);
                return ValueTask.CompletedTask;
            }, 1000L);
        }
    }

    public override float ModifyOwnerDamage(float damage, Creature effected, Effect effect)
    {
        return damage * modifier;
    }

    public override void OnStartUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        switch (skillTemplate.GetSkillId())
        {
            case 21177:
                if (GetNpcId() == 284383 || GetNpcId() == 855244)
                {
                    PacketSendUtility.BroadcastMessage(GetOwner(), 348677);
                    Spawn(284387, 255.98627f, 259.0136f, 242.73842f, (sbyte)0);
                }
                else
                {
                    PacketSendUtility.BroadcastMessage(GetOwner(), 348680);
                }
                break;
            case 21372:
            case 21174:
                if (GetNpcId() == 284383 || GetNpcId() == 855244)
                {
                    PacketSendUtility.BroadcastMessage(GetOwner(), 348678);
                }
                else
                {
                    PacketSendUtility.BroadcastMessage(GetOwner(), 348679);
                }
                break;
            case 21175:
                break;
        }
        if (GetNpcId() == 284383 || GetNpcId() == 855244)
        {
            GetKnownList().ForEachNpc(npc =>
            {
                if (!npc.IsDead() && (npc.GetNpcId() == 284384 || npc.GetNpcId() == 855245))
                {
                    if (npc.GetTarget() is Creature target && target.IsDead())
                    {
                        npc.SetTarget(GetTarget());
                    }
                    SkillEngine.SkillEngine.GetInstance().GetSkill(npc, skillTemplate.GetSkillId(), skillLevel, npc.GetTarget()).UseWithoutPropSkill();
                }
            });
        }
        modifier = 0.2f;
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        modifier = 1.35f;
    }

    private void CancelVengefulOrb()
    {
        Npc vengefulOrb = GetPosition().GetWorldMapInstance().GetNpc(284387);
        if (vengefulOrb != null)
        {
            vengefulOrb.GetController().CancelCurrentSkill(null);
            vengefulOrb.GetController().Delete();
        }
    }

    public override int ModifyInitialSkillDelay(int delay)
    {
        return 3000;
    }

    protected override void HandleDied()
    {
        CancelVengefulOrb();
        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        CancelVengefulOrb();
        base.HandleDespawned();
    }
}
