using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/dragonLordsRefuge/EmpyreanLordAI (Bobobear, Estrayl).</summary>
[AIName("empyrean_lord")]
public class EmpyreanLordAI : GeneralNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(50, 15);
    private int tiamatId;

    public EmpyreanLordAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            GetPosition().GetWorldMapInstance().ForEachNpc(npc =>
            {
                if (npc.GetNpcId() >= 219532 && npc.GetNpcId() <= 219539)
                    npc.GetController().Die();
            });
            return ValueTask.CompletedTask;
        }, 9000L);

        tiamatId = GetPosition().GetMapId() == 300520000 ? 219361 : 236276;
        switch (GetNpcId())
        {
            case 219488:
            case 856020:
                ThreadPoolManager.GetInstance().Schedule(ct => { AIActions.UseSkill(this, 20932); return ValueTask.CompletedTask; }, 8500L);
                break;
            case 219491:
            case 856023:
                ThreadPoolManager.GetInstance().Schedule(ct => { AIActions.UseSkill(this, 20936); return ValueTask.CompletedTask; }, 8500L);
                break;
            case 219489:
            case 856021:
                AIActions.TargetCreature(this, GetPosition().GetWorldMapInstance().GetNpc(tiamatId));
                ThreadPoolManager.GetInstance().Schedule(ct => { AIActions.UseSkill(this, 20929); return ValueTask.CompletedTask; }, 8500L);
                break;
            case 219492:
            case 856024:
                AIActions.TargetCreature(this, GetPosition().GetWorldMapInstance().GetNpc(tiamatId));
                ThreadPoolManager.GetInstance().Schedule(ct => { AIActions.UseSkill(this, 20933); return ValueTask.CompletedTask; }, 2500L);
                break;
        }
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        switch (skillTemplate.GetSkillId())
        {
            case 20932:
            case 20936:
                Npc tiamat = GetPosition().GetWorldMapInstance().GetNpc(tiamatId);
                AIActions.TargetCreature(this, tiamat);
                GetAggroList().AddHate(tiamat, int.MaxValue / 4);
                SetStateIfNot(AIState.FIGHT);
                EmoteManager.EmoteStartAttacking(GetOwner(), tiamat);
                break;
        }
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        switch (GetNpcId())
        {
            case 219488:
            case 856020:
            case 219491:
            case 856023:
                switch (phaseHpPercent)
                {
                    case 50:
                        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.IDTIAMAT_TIAMAT_GOD_HP_LOWER_THAN_50p());
                        break;
                    case 15:
                        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.IDTIAMAT_TIAMAT_GOD_HP_LOWER_THAN_15p());
                        break;
                }
                break;
        }
    }
}
