using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/drakenspire/BeritraPortalAI (@author Estrayl).</summary>
[AIName("beritra_portal")]
public class BeritraPortalAI : ActionItemNpcAI
{
    public BeritraPortalAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        switch (Rnd.Get(1, 3))
        {
            case 1:
                TeleportService.TeleportTo(player, 301390000, 174.7f, 518.2f, 1749.6f, (byte)59, TeleportAnimation.FADE_OUT);
                break;
            case 2:
                TeleportService.TeleportTo(player, 301390000, 173.4f, 517.9f, 1749.6f, (byte)59, TeleportAnimation.FADE_OUT);
                break;
            case 3:
                TeleportService.TeleportTo(player, 301390000, 173.4f, 514.6f, 1749.6f, (byte)59, TeleportAnimation.FADE_OUT);
                break;
        }
        player.GetController().StartProtectionActiveTask();
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            PacketSendUtility.SendPacket(player, new SM_PLAY_MOVIE(false, 0, 0, 915, true));
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, 1000L);
    }
}
