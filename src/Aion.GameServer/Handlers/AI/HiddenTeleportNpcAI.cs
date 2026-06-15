using Aion.GameServer.Ai;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Actions;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Templates.Flypath;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/HiddenTeleportNpcAI (@author Estrayl).</summary>
[AIName("hidden_teleporter")]
public class HiddenTeleportNpcAI : NpcAI
{
    public HiddenTeleportNpcAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (dialogActionId == DialogAction.SETPRO1)
            Teleport(player);
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
        return true;
    }

    private void Teleport(Player player)
    {
        int teleId = GetTeleportId();
        if (teleId == 0)
            return;
        FlyPathEntry flypath = DataManager.FLY_PATH.GetPathTemplate(teleId);
        player.SetCurrentFlypath(flypath);
        player.UnsetPlayerMode(PlayerMode.RIDE);
        player.SetState(CreatureState.FLYING);
        player.UnsetState(CreatureState.ACTIVE);
        player.SetFlightTeleportId(teleId * 1000 + 1);
        PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.START_FLYTELEPORT, teleId * 1000 + 1, 0), true);
    }

    private int GetTeleportId()
    {
        switch (GetOwner().GetNpcId())
        {
            case 804811:
                return 279;
            case 804812:
                return 281;
            case 804813:
                return 280;
            case 804814:
                return 282;
            case 804822:
                return 286;
            case 804823:
                return 284;
            case 804824:
                return 283;
            case 804825:
                return 285;
        }

        return 0;
    }
}
