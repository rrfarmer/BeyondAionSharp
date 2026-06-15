using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/events/CodeRedNurseAI (@author xTz, bobobear).</summary>
[AIName("code_red_nurse")]
public class CodeRedNurseAI : GeneralNpcAI
{
    public CodeRedNurseAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        switch (GetNpcId())
        {
            case 831435: // Jorpine (MON-THU)
            case 831436: // Yennu (MON-THU)
            case 831437: // Dalloren (FRI-SUN)
            case 831518: // Dalliea (FRI-SUN)
            case 831441: // Hylian (MON-THU)
            case 831442: // Rordah (MON-THU)
            case 831443: // Mazka (FRI-SUN)
            case 831524: // Desha (FRI-SUN)
                base.HandleDialogStart(player);
                break;
            default:
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
                break;
        }
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        QuestEnv env = new QuestEnv(GetOwner(), player, questId, dialogActionId);
        env.SetExtendedRewardIndex(extendedRewardIndex);
        if (Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnDialog(env) && dialogActionId != DialogAction.SETPRO1)
        {
            return true;
        }
        if (dialogActionId == DialogAction.SETPRO1)
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(0, 0));
            int skillId = GetNpcId() switch
            {
                831435 or 831441 => 21280, // Jorpine / Hylian (MON-THU)
                831436 or 831442 => 21281, // Yennu / Rordah (MON-THU)
                831437 or 831443 => 21283, // Dalloren / Mazka (FRI-SUN)
                831518 or 831524 => 21309, // Dalliea / Desha (FRI-SUN)
                _ => 0,
            };
            if (skillId != 0)
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), skillId, 1, player).UseWithoutPropSkill();
        }
        else if (dialogActionId == DialogAction.QUEST_SELECT && questId != 0)
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 10, questId));
        }
        return true;
    }
}
