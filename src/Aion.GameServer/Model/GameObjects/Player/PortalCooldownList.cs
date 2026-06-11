using System;
using System.Collections.Generic;

namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>Java parity: model/gameobjects/player/PortalCooldownList.</summary>
public class PortalCooldownList
{
    private readonly Player owner;
    private Dictionary<int, PortalCooldown> portalCooldowns;

    internal PortalCooldownList(Player owner)
    {
        this.owner = owner;
    }

    public bool IsPortalUseDisabled(int worldId)
    {
        if (portalCooldowns == null || !portalCooldowns.ContainsKey(worldId))
            return false;

        PortalCooldown coolDown = portalCooldowns[worldId];
        if (coolDown == null)
            return false;

        if (coolDown.GetReuseTime() < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
            portalCooldowns.Remove(worldId);
            return false;
        }

        return coolDown.GetEnterCount() >= Aion.GameServer.Dataholders.DataManager.INSTANCE_COOLTIME_DATA.GetInstanceMaxCountByWorldId(worldId);
    }

    public long GetPortalCooldownTime(int worldId)
    {
        if (portalCooldowns == null || !portalCooldowns.ContainsKey(worldId))
            return 0;
        long coolDown = portalCooldowns[worldId].GetReuseTime();

        if (coolDown < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
            portalCooldowns.Remove(worldId);
            return 0;
        }

        return coolDown;
    }

    public PortalCooldown GetPortalCooldown(int worldId)
    {
        return portalCooldowns == null ? null : (portalCooldowns.TryGetValue(worldId, out PortalCooldown cd) ? cd : null);
    }

    public Dictionary<int, PortalCooldown> GetPortalCoolDowns()
    {
        return portalCooldowns;
    }

    public void SetPortalCoolDowns(Dictionary<int, PortalCooldown> portalCoolDowns)
    {
        this.portalCooldowns = portalCoolDowns;
    }

    public void AddPortalCooldown(int worldId, long useDelay)
    {
        if (portalCooldowns == null)
            portalCooldowns = new Dictionary<int, PortalCooldown>();

        if (!portalCooldowns.TryGetValue(worldId, out PortalCooldown portalCooldown) || portalCooldown == null)
            portalCooldown = new PortalCooldown(worldId, useDelay, 0);

        portalCooldown.IncreaseEnterCount();
        portalCooldowns[worldId] = portalCooldown;

        Aion.GameServer.Dao.PortalCooldownsDAO.StorePortalCooldowns(owner);

        SendEntryInfo(worldId);
    }

    public void SendEntryInfo(int worldId)
    {
        if (owner.IsInTeam())
            owner.GetCurrentTeam().SendPackets(new Aion.GameServer.Network.Aion.ServerPackets.SmInstanceInfo((byte)2, owner, worldId));
        else
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, new Aion.GameServer.Network.Aion.ServerPackets.SmInstanceInfo((byte)2, owner, worldId));
    }

    public void RemovePortalCooldown(int worldId)
    {
        if (portalCooldowns != null)
            portalCooldowns.Remove(worldId);
    }

    public bool HasCooldowns()
    {
        return portalCooldowns != null && portalCooldowns.Count > 0;
    }

    public int Size()
    {
        return portalCooldowns != null ? portalCooldowns.Count : 0;
    }
}
