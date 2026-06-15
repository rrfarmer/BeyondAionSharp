using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/DeliveryManAI (-Nemesiss-, Neon).</summary>
[AIName("deliveryman")]
public class DeliveryManAI : FollowingNpcAI
{
    private const int SERVICE_TIME = 5 * 60 * 1000;

    public DeliveryManAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        Player owner = GetPlayer();
        GetOwner().GetController().AddTask(TaskId.DESPAWN,
            ThreadPoolManager.GetInstance().Schedule(ct => { AIActions.DeleteOwner(this); return ValueTask.CompletedTask; }, SERVICE_TIME));
        HandleFollowMe(owner);
        HandleCreatureMoved(owner);
        PacketSendUtility.BroadcastMessage(GetOwner(), 390266, 1500); // Here is your mail, akakak!
        PacketSendUtility.BroadcastMessage(GetOwner(), 390268, 30000); // Time is silver my friend, akakak!
    }

    protected override void HandleDespawned()
    {
        PacketSendUtility.BroadcastMessage(GetOwner(), 390267); // Whiririkk, let's go!
        Player player = World.World.GetInstance().GetPlayer(GetOwner().GetCreatorId());
        if (player != null && GetOwner().Equals(player.GetPostman()))
            player.SetPostman(null);
        base.HandleDespawned();
    }

    protected override void HandleDialogStart(Player player)
    {
        if (player.Equals(GetPlayer()))
        {
            player.GetMailbox().mailBoxState = PlayerMailboxState.EXPRESS;
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), DialogPage.MAIL.Id()));
        }
        else
        {
            PacketSendUtility.BroadcastMessage(GetOwner(), 390269); // There is no mail for you, nyerk.
        }
    }

    private Player GetPlayer()
    {
        return GetOwner().GetPosition().GetWorldMapInstance().GetPlayer(GetOwner().GetCreatorId());
    }
}
