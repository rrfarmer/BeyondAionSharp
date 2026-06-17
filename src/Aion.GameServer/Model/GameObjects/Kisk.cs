using System.Collections.Generic;
using System.Collections.Concurrent;
using Aion.GameServer.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>Java parity: model/gameobjects/Kisk (Sarynth, nrg). extends SummonedObject&lt;Player&gt;.</summary>
public class Kisk : SummonedObject<Aion.GameServer.Model.GameObjects.Players.Player>
{
    private static readonly ILogger log = NullLogger.Instance;

    private const long KISK_LIFETIME_IN_SEC = 2L * 60 * 60; // TimeUnit.HOURS.toSeconds(2)
    private readonly int legionId;
    private readonly Race ownerRace;

    private readonly Aion.GameServer.Model.Templates.Stats.KiskStatsTemplate kiskStatsTemplate;

    private int remainingResurrections;

    private readonly ConcurrentDictionary<int, byte> kiskMemberIds;

    public Kisk(Aion.GameServer.Controllers.NpcController controller, Aion.GameServer.Model.Templates.Spawns.SpawnTemplate spawnTemplate, Aion.GameServer.Model.GameObjects.Players.Player owner)
        : base(controller, spawnTemplate, (sbyte)Aion.GameServer.Dataholders.DataManager.NPC_DATA.GetNpcTemplate(spawnTemplate.GetNpcId()).GetLevel(), null)
    {
        this.kiskStatsTemplate = GetObjectTemplate().GetKiskStatsTemplate() == null ? new Aion.GameServer.Model.Templates.Stats.KiskStatsTemplate()
            : GetObjectTemplate().GetKiskStatsTemplate();
        this.kiskMemberIds = new ConcurrentDictionary<int, byte>();
        this.remainingResurrections = this.kiskStatsTemplate.GetMaxResurrects();
        this.legionId = owner.GetLegion() == null ? 0 : owner.GetLegion().GetLegionId();
        this.ownerRace = owner.GetRace();
        SetCreatorId(owner.GetObjectId());
        SetMasterName(owner.GetName());
        SetKnownlist(new Aion.GameServer.World.Knownlist.PlayerAwareKnownList(this));
        SetEffectController(new Aion.GameServer.Controllers.Effects.EffectController(this));
    }

    public override bool IsEnemy(Creature creature)
    {
        return creature.IsEnemyFrom(this);
    }

    /// <summary>Required so that the enemy race can attack the Kisk!</summary>
    public override bool IsEnemyFrom(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        return !player.GetRace().Equals(ownerRace) && IsInsidePvPZone() && player.IsInsidePvPZone();
    }

    public override CreatureType GetTypeValue(Creature creature)
    {
        if (creature is Aion.GameServer.Model.GameObjects.Players.Player player)
            return IsEnemyFrom(player) ? CreatureType.ATTACKABLE : CreatureType.SUPPORT;
        return base.GetTypeValue(creature);
    }

    /// <returns>NpcObjectType.NORMAL</returns>
    public override NpcObjectType GetNpcObjectType()
    {
        return NpcObjectType.NORMAL;
    }

    /// <summary>1 ~ race 2 ~ legion 3 ~ solo 4 ~ group 5 ~ alliance.</summary>
    public int GetUseMask()
    {
        return kiskStatsTemplate.GetUseMask();
    }

    public List<Aion.GameServer.Model.GameObjects.Players.Player> GetCurrentMemberList()
    {
        List<Aion.GameServer.Model.GameObjects.Players.Player> currentMemberList = new List<Aion.GameServer.Model.GameObjects.Players.Player>();

        foreach (int memberId in kiskMemberIds.Keys)
        {
            Aion.GameServer.Model.GameObjects.Players.Player member = Aion.GameServer.World.World.GetInstance().GetPlayer(memberId);
            if (member != null)
                currentMemberList.Add(member);
        }

        return currentMemberList;
    }

    public int GetCurrentMemberCount()
    {
        return kiskMemberIds.Count;
    }

    public ICollection<int> GetCurrentMemberIds()
    {
        return kiskMemberIds.Keys;
    }

    public int GetMaxMembers()
    {
        return kiskStatsTemplate.GetMaxMembers();
    }

    public int GetRemainingResurrects()
    {
        return remainingResurrections;
    }

    public int GetMaxRessurects()
    {
        return kiskStatsTemplate.GetMaxResurrects();
    }

    public int GetRemainingLifetime()
    {
        if (IsDead())
            return 0;
        long timeElapsed = GetMillisSinceSpawn() / 1000;
        int timeRemaining = (int)(KISK_LIFETIME_IN_SEC - timeElapsed);
        return System.Math.Max(timeRemaining, 0);
    }

    /// <returns>True if the player may bind to this kisk.</returns>
    public bool CanBind(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        return GetCurrentMemberCount() < GetMaxMembers() && IsUseAllowed(player);
    }

    private bool IsUseAllowed(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        switch (GetUseMask())
        {
            case 0: // Test item (no restrictions)
                return true;
            case 1: // Race
                if (ownerRace == player.GetRace())
                    return true;
                break;
            case 2: // Legion
                if (player.GetObjectId() == GetCreatorId() || legionId != 0 && Aion.GameServer.Services.LegionService.GetInstance().GetLegion(legionId).IsMember(player.GetObjectId()))
                    return true;
                break;
            case 3: // Solo
                return player.GetObjectId() == GetCreatorId();
            case 4: // Group (PlayerGroup or PlayerAllianceGroup)
                if (player.GetObjectId() == GetCreatorId() || player.IsInTeam() && player.GetCurrentGroup().HasMember(GetCreatorId()))
                    return true;
                break;
            case 5: // Alliance (PlayerGroup or PlayerAlliance)
                if (player.GetObjectId() == GetCreatorId() || player.IsInTeam() && player.GetCurrentTeam().HasMember(GetCreatorId()))
                    return true;
                break;
            default:
                log.LogWarning("Unhandled UseMask " + GetUseMask() + " for Kisk " + GetNpcId());
                break;
        }

        return false;
    }

    public void AddPlayer(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        if (kiskMemberIds.TryAdd(player.GetObjectId(), 0))
        {
            BroadcastKiskUpdate();
        }
        else
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SM_KISK_UPDATE(this));
        }
        player.SetKisk(this);
    }

    public void RemovePlayer(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        player.SetKisk(null);
        if (kiskMemberIds.TryRemove(player.GetObjectId(), out _))
            BroadcastKiskUpdate();
    }

    private void BroadcastKiskUpdate()
    {
        // on all members, but not the ones in knownlist, they will receive the update in the next step
        foreach (Aion.GameServer.Model.GameObjects.Players.Player member in GetCurrentMemberList())
        {
            if (!GetKnownList().Knows(member))
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(member, new Aion.GameServer.Network.Aion.ServerPackets.SM_KISK_UPDATE(this));
        }

        // all players having the same race in knownlist
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(this, new Aion.GameServer.Network.Aion.ServerPackets.SM_KISK_UPDATE(this), player => player.GetRace() == ownerRace);
    }

    // Java parity: Kisk.broadcastPacket(SM_SYSTEM_MESSAGE message) — uses the faithful SM_SYSTEM_MESSAGE
    // (the STR_BINDSTONE_* factories return SM_SYSTEM_MESSAGE), not the reworked SM_SYSTEM_MESSAGE.
    public void BroadcastPacket(Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE message)
    {
        foreach (Aion.GameServer.Model.GameObjects.Players.Player member in GetCurrentMemberList())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(member, message);
        }
    }

    public void ResurrectionUsed()
    {
        remainingResurrections--;
        BroadcastKiskUpdate();
        if (remainingResurrections <= 0)
            GetController().Delete();
    }

    public Race GetOwnerRace()
    {
        return ownerRace;
    }

    public bool IsActive()
    {
        return !IsDead() && GetRemainingResurrects() > 0;
    }
}
