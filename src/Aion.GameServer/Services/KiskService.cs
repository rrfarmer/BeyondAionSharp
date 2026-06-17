using System.Collections.Generic;
using System.Collections.Concurrent;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/KiskService (Sarynth, nrg, Sykra).</summary>
public class KiskService
{
    private static readonly KiskService instance = new KiskService();
    private readonly ConcurrentDictionary<int, Kisk> boundButOfflinePlayer = new();
    private readonly ConcurrentDictionary<int, Kisk> ownerPlayer = new();

    /// <summary>Remove kisk references and containers.</summary>
    public void RemoveKisk(Kisk kisk)
    {
        // remove offline binds
        foreach (int memberId in kisk.GetCurrentMemberIds())
        {
            boundButOfflinePlayer.TryRemove(memberId, out _);
        }

        foreach (int obj in new List<int>(ownerPlayer.Keys))
        {
            if (ownerPlayer.TryGetValue(obj, out Kisk owned) && owned.Equals(kisk))
            {
                ownerPlayer.TryRemove(obj, out _);
                break;
            }
        }
        // remove the "2h place cooldown" for the kisk creator
        Aion.GameServer.Model.GameObjects.Players.Player creator = Aion.GameServer.World.World.GetInstance().GetPlayer(kisk.GetCreatorId());
        if (creator != null)
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(creator, new Aion.GameServer.Network.Aion.ServerPackets.SM_KISK_UPDATE(kisk));

        foreach (Aion.GameServer.Model.GameObjects.Players.Player member in kisk.GetCurrentMemberList())
        {
            Aion.GameServer.Services.Teleport.TeleportService.SendKiskBindPoint(member);
            member.SetKisk(null);
            if (member.IsDead()) // player has died and is not revived
                member.GetController().ShowResurrectionOptions();
        }
    }

    public void OnBind(Kisk kisk, Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        if (player.GetKisk() != null)
            player.GetKisk().RemovePlayer(player);

        kisk.AddPlayer(player);
        Aion.GameServer.Services.Teleport.TeleportService.SendKiskBindPoint(player);
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_BINDSTONE_REGISTER());
        // Send Animated Bind Flash
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SM_ACTION_ANIMATION(player.GetObjectId(), ActionAnimation.BindKisk), true);
    }

    public void OnLogin(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        Kisk kisk = this.boundButOfflinePlayer.TryGetValue(player.GetObjectId(), out Kisk k) ? k : null;
        if (kisk != null)
        {
            kisk.AddPlayer(player);
            this.boundButOfflinePlayer.TryRemove(player.GetObjectId(), out _);
        }
    }

    public void OnLogout(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        Kisk kisk = player.GetKisk();
        // store binding if existent
        if (kisk != null)
        {
            this.boundButOfflinePlayer[player.GetObjectId()] = kisk;
        }
    }

    public void RegKisk(Kisk kisk, int objOwnerId)
    {
        ownerPlayer[objOwnerId] = kisk;
    }

    public bool HaveKisk(int objOwnerId)
    {
        return ownerPlayer.ContainsKey(objOwnerId);
    }

    public static KiskService GetInstance()
    {
        return instance;
    }
}
