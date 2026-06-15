using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/dragonLordsRefuge/TiamatDragonAI (@author Estrayl March 10th, 2018).</summary>
[AIName("tiamat_dragon")]
public class TiamatDragonAI : AggressiveNpcAI
{
    public TiamatDragonAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(() => AIActions.UseSkill(this, 20920), 4000L);
        ThreadPoolManager.GetInstance().Schedule(() => GetOwner().QueueSkill(20984, 1), 300000L);
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        switch (skillTemplate.GetSkillId())
        {
            case 20920:
                AIActions.UseSkill(this, 20975); // Fissure Buff
                AIActions.UseSkill(this, 20976); // Wrath Buff
                AIActions.UseSkill(this, 20977); // Gravity Buff
                AIActions.UseSkill(this, 20978); // Petrification Buff
                break;
            case 20984:
                PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_IDTIAMAT_TIAMAT_WARNING_MSG());
                break;
        }
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.REWARD_AP_XP_DP_LOOT => false,
            _ => base.Ask(question),
        };
    }
}
