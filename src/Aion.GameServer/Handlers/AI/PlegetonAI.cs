using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Tiger0319, xTz, Gigi
/// </summary>
[AIName("plegeton")]
public class PlegetonAI : NpcAI
{
    private bool isStartTimer = false;

    public PlegetonAI(Npc owner) : base(owner)
    {
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (dialogActionId == DialogAction.SETPRO1)
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
            switch (GetNpcId())
            {
                case 799517:
                    PacketSendUtility.SendPacket(player, new SM_PLAY_MOVIE(false, 0, 0, 448, false));
                    if (!isStartTimer)
                    {
                        isStartTimer = true;
                        SendTimer();
                        ThreadPoolManager.GetInstance().Schedule(_ =>
                        {
                            Npc npc = GetPosition().GetWorldMapInstance().GetNpc(216586);
                            if (npc != null && !npc.IsDead())
                            {
                                npc.GetController().Delete();
                                PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(0, 0));
                                GetPosition().GetWorldMapInstance().SetDoorState(467, true);
                            }
                            return ValueTask.CompletedTask;
                        }, 420000L);
                    }
                    TeleportService.TeleportTo(player, 300170000, 958.45233f, 430.4892f, 219.80301f);
                    break;
                case 799518:
                    PacketSendUtility.SendPacket(player, new SM_PLAY_MOVIE(false, 0, 0, 449, false));
                    TeleportService.TeleportTo(player, 300170000, 822.0199f, 465.1819f, 220.29918f);
                    break;
                case 799519:
                    PacketSendUtility.SendPacket(player, new SM_PLAY_MOVIE(false, 0, 0, 450, false));
                    TeleportService.TeleportTo(player, 300170000, 777.1054f, 300.39005f, 219.89926f);
                    break;
                case 799520:
                    PacketSendUtility.SendPacket(player, new SM_PLAY_MOVIE(false, 0, 0, 451, false));
                    TeleportService.TeleportTo(player, 300170000, 942.3092f, 270.91855f, 219.86185f);
                    break;
            }
        }
        return true;
    }

    private void SendTimer()
    {
        PacketSendUtility.BroadcastToMap(GetOwner(), new SM_QUEST_ACTION(0, 420));
    }

    protected override void HandleDialogStart(Player player)
    {
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
    }
}
