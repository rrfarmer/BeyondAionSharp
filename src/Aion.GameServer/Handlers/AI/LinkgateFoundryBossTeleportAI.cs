using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/linkgateFoundry/LinkgateFoundryBossTeleportAI (@author Cheatkiller).</summary>
[AIName("linkgateFoundryBossTeleport")]
public class LinkgateFoundryBossTeleportAI : ActionItemNpcAI
{
    public LinkgateFoundryBossTeleportAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        SelectBoss(player, dialogActionId);
        return true;
    }

    private void SelectBoss(Player player, int dialogActionId)
    {
        int keyId = 185000196;
        int minKeyCount, bossId, msgId;
        bool spawnGuardian;
        switch (dialogActionId)
        {
            case DialogAction.SELECT_BOSS_LEVEL2:
                minKeyCount = 1;
                bossId = 234990;
                msgId = 1402440;
                spawnGuardian = true;
                break;
            case DialogAction.SELECT_BOSS_LEVEL3:
                minKeyCount = 5;
                bossId = 233898;
                msgId = 1402441;
                spawnGuardian = true;
                break;
            case DialogAction.SELECT_BOSS_LEVEL4:
                minKeyCount = 7;
                bossId = 234991;
                msgId = 1402442;
                spawnGuardian = false;
                break;
            default:
                return;
        }
        long keyCount = player.GetInventory().GetItemCountByItemId(keyId);
        if (keyCount < minKeyCount)
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), DialogPage.NO_RIGHT.Id()));
            return;
        }
        player.GetInventory().DecreaseByItemId(keyId, keyCount);
        // replace portal
        AIActions.DeleteOwner(this);
        Spawn(702592, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0, 51);
        // spawn boss and guardian
        PacketSendUtility.BroadcastToMap(GetOwner(), msgId);
        GetOwner().GetPosition().GetWorldMapInstance().GetNpc(233898).GetController().Delete(); // default belsagos spawn (visible from outside)
        Spawn(bossId, 252.2439f, 259.3866f, 312.3536f, (sbyte)41);
        if (spawnGuardian)
            Spawn(player.GetRace() == Race.ELYOS ? 855087 : 855088, 226.76f, 256.7708f, 312.577f, (sbyte)0);
        // teleport player
        TeleportService.TeleportTo(player, GetOwner().GetWorldId(), 211.32f, 260, 314, (byte)0, TeleportAnimation.FADE_OUT_BEAM);
    }
}
