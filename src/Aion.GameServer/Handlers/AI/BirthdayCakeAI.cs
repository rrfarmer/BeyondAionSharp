using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.SkillEngine;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/events/BirthdayCakeAI (@author Estrayl).</summary>
[AIName("birthday_cake")]
public class BirthdayCakeAI : GeneralNpcAI
{
    public BirthdayCakeAI(Npc owner)
        : base(owner)
    {
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        switch (dialogActionId)
        {
            case DialogAction.SETPRO1:
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 10821, 1, player).UseWithoutPropSkill();
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 10822, 1, player).UseWithoutPropSkill();
                return true;
        }
        return false;
    }
}
