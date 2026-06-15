using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/crucibleChallenge/AdministratorArminosAI (@author xTz).</summary>
[AIName("administratorarminos")]
public class AdministratorArminosAI : NpcAI
{
    public AdministratorArminosAI(Npc owner)
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
        {
            if (player.IsInsideZone(ZoneName.Get("ILLUSION_STADIUM_1_300320000")))
            {
                Spawn(217827, 1250.1598f, 237.97736f, 405.3968f, (sbyte)0);
                Spawn(217828, 1250.1598f, 239.97736f, 405.3968f, (sbyte)0);
                Spawn(217829, 1250.1598f, 235.97736f, 405.3968f, (sbyte)0);
            }
            else if (player.IsInsideZone(ZoneName.Get("ILLUSION_STADIUM_6_300320000")))
            {
                Spawn(217827, 1265.9661f, 793.5348f, 436.64008f, (sbyte)0);
                Spawn(217828, 1265.9661f, 789.5348f, 436.6402f, (sbyte)0);
                Spawn(217829, 1265.9661f, 791.5348f, 436.64014f, (sbyte)0);
            }
            AIActions.DeleteOwner(this);
        }
        return true;
    }
}
