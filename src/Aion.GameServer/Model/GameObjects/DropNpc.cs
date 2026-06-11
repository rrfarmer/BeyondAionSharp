using System.Collections.Generic;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>Java parity: model/gameobjects/DropNpc (Simple).</summary>
public class DropNpc
{
    private readonly int objectIdId;
    private ISet<int> allowedLooters = new HashSet<int>();
    private ICollection<Aion.GameServer.Model.GameObjects.Players.Player> inRangePlayers = new List<Aion.GameServer.Model.GameObjects.Players.Player>();
    private ICollection<Aion.GameServer.Model.GameObjects.Players.Player> playerStatus = new List<Aion.GameServer.Model.GameObjects.Players.Player>();
    private Aion.GameServer.Model.GameObjects.Players.Player lootingPlayer = null;
    private int distributionId = 0;
    private bool distributionType;
    private int currentIndex = 0;
    private System.WeakReference<Aion.GameServer.Model.Team.TemporaryPlayerTeam<Aion.GameServer.Model.Team.ITeamMember<Aion.GameServer.Model.GameObjects.Players.Player>>> lootingTeam;
    private int lootingTeamId;
    private int maxRoll;
    private Aion.GameServer.Model.Team.Common.Legacy.LootGroupRules lastLootGroupRules;
    private bool isFreeForAll = false;
    private long remaingDecayTime;

    public DropNpc(int objectIdId)
    {
        this.objectIdId = objectIdId;
    }

    public void SetAllowedLooters(ISet<int> allowedLooters)
    {
        this.allowedLooters = allowedLooters;
    }

    public void SetAllowedLooter(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        allowedLooters.Add(player.GetObjectId());
    }

    public ISet<int> GetAllowedLooters()
    {
        return allowedLooters;
    }

    public bool IsAllowedToLoot(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        return isFreeForAll || allowedLooters.Contains(player.GetObjectId());
    }

    public void SetLootingPlayer(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        this.lootingPlayer = player;
    }

    public Aion.GameServer.Model.GameObjects.Players.Player GetLootingPlayer()
    {
        return lootingPlayer;
    }

    public bool IsBeingLooted()
    {
        return lootingPlayer != null;
    }

    public void SetDistributionId(int distributionId)
    {
        this.distributionId = distributionId;
    }

    public int GetDistributionId()
    {
        return distributionId;
    }

    public void SetDistributionType(bool distributionType)
    {
        this.distributionType = distributionType;
    }

    public bool GetDistributionType()
    {
        return distributionType;
    }

    public void SetCurrentIndex(int currentIndex)
    {
        this.currentIndex = currentIndex;
    }

    public int GetCurrentIndex()
    {
        return currentIndex;
    }

    public int GetLootingTeamId()
    {
        return lootingTeamId;
    }

    public int GetMaxRoll()
    {
        return maxRoll;
    }

    public Aion.GameServer.Model.Team.Common.Legacy.LootGroupRules GetLootGroupRules()
    {
        Aion.GameServer.Model.Team.TemporaryPlayerTeam<Aion.GameServer.Model.Team.ITeamMember<Aion.GameServer.Model.GameObjects.Players.Player>> team =
            lootingTeam == null ? null : (lootingTeam.TryGetTarget(out var t) ? t : null);
        if (team != null)
            lastLootGroupRules = team.GetLootGroupRules();
        return lastLootGroupRules;
    }

    public void SetLootingTeam(Aion.GameServer.Model.Team.TemporaryPlayerTeam<Aion.GameServer.Model.Team.ITeamMember<Aion.GameServer.Model.GameObjects.Players.Player>> team)
    {
        lootingTeam = new System.WeakReference<Aion.GameServer.Model.Team.TemporaryPlayerTeam<Aion.GameServer.Model.Team.ITeamMember<Aion.GameServer.Model.GameObjects.Players.Player>>>(team);
        lootingTeamId = team.GetTeamId();
        maxRoll = team is Aion.GameServer.Model.Team.Alliance.PlayerAlliance alli ? (alli.IsInLeague() ? 10000 : 1000) : 100;
        lastLootGroupRules = team.GetLootGroupRules();
    }

    public void SetInRangePlayers(ICollection<Aion.GameServer.Model.GameObjects.Players.Player> inRangePlayers)
    {
        this.inRangePlayers = inRangePlayers;
    }

    public ICollection<Aion.GameServer.Model.GameObjects.Players.Player> GetInRangePlayers()
    {
        return inRangePlayers;
    }

    public void AddPlayerStatus(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        playerStatus.Add(player);
    }

    public void DelPlayerStatus(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        playerStatus.Remove(player);
    }

    public ICollection<Aion.GameServer.Model.GameObjects.Players.Player> GetPlayerStatus()
    {
        return playerStatus;
    }

    public bool ContainsPlayerStatus(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        return playerStatus.Contains(player);
    }

    public bool IsFreeForAll()
    {
        return isFreeForAll;
    }

    public void StartFreeForAll()
    {
        isFreeForAll = true;
        distributionId = 0;
        allowedLooters.Clear();
    }

    public int GetObjectId()
    {
        return objectIdId;
    }

    public long GetRemaingDecayTime()
    {
        return remaingDecayTime;
    }

    public void SetRemaingDecayTime(long remaingDecayTime)
    {
        this.remaingDecayTime = remaingDecayTime;
    }
}
