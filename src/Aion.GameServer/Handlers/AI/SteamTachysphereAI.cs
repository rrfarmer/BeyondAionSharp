using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/aturamSkyFortress/SteamTachysphereAI (xTz).</summary>
[AIName("steam_tachysphere")]
public class SteamTachysphereAI : ActionItemNpcAI
{
    public SteamTachysphereAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        QuestState qs = player.GetQuestStateList().GetQuestState(player.GetRace().Equals(Race.ELYOS) ? 18302 : 28302);
        if (qs == null)
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), DialogPage.NO_RIGHT.Id()));
        }
        else if (qs.GetStatus() != QuestStatus.COMPLETE)
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 10));
        }
        else
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
        }
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (dialogActionId == DialogAction.SETPRO1)
        {
            QuestState qs = player.GetQuestStateList().GetQuestState(player.GetRace().Equals(Race.ELYOS) ? 18302 : 28302);
            if (qs != null && qs.GetStatus() == QuestStatus.COMPLETE)
            {
                TeleportService.TeleportTo(player, 300240000, 175.28925f, 625.1088f, 901.009f, (byte)33);
                PacketSendUtility.SendPacket(player, new SM_PLAY_MOVIE(false, 0, 0, 471, false));
                SkillEngine.SkillEngine.GetInstance().GetSkill(player, 19502, 1, player).UseNoAnimationSkill();
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
            }
        }
        return true;
    }
}
