using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/nightmareCircus/NightmareHarlequinAI (@author Ritsu).</summary>
[AIName("nightmareharlequin")]
public class NightmareHarlequinAI : GeneralNpcAI
{
    public NightmareHarlequinAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        // without an deny dialog we have to refrain from sending dialogs if the player is not allowed to talk to the npc. maybe it's invisible on retail?
        if (DialogService.IsInteractionAllowed(player, GetOwner()))
        {
            base.HandleDialogStart(player);
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
            Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), player.GetRace() == Race.ELYOS ? 21470 : 21472, 1, player).UseWithoutPropSkill();
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
        }
        else if (dialogActionId == DialogAction.QUEST_SELECT && questId != 0)
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 10, questId));
        }
        return true;
    }
}
