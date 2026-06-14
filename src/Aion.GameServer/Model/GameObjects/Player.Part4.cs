using System.Collections.Generic;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.World;

namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>
/// Java parity: model/gameobjects/player/Player — partial #4 (Java lines ~1210-1655): resurrection positional
/// state, siege-world, permission, emotions/motions/bind-point, attack-type, fly-path, abyss-rank-update mask,
/// salvation, pvp-target, npc-factions, self-rez stones, looting, mentor, race, skill-cooldown/disabled,
/// flood, rebirth, supplements, port-animation, houses, battle-return, sprint, ride-observers, abs-stats,
/// position, robot, can-perform-move, toString, custom-states, panesterra.
/// </summary>
public partial class Player
{
    /// <summary>the Resurrection Positional State</summary>
    public bool IsInResPostState()
    {
        return isInResurrectPosState;
    }

    /// <param name="value">Resurrection Positional State to set</param>
    public void SetResPosState(bool value)
    {
        this.isInResurrectPosState = value;
    }

    /// <param name="value">Resurrection Positional X value to set</param>
    public void SetResPosX(float value)
    {
        this.resPosX = value;
    }

    /// <summary>the Resurrection Positional X value</summary>
    public float GetResPosX()
    {
        return resPosX;
    }

    /// <param name="value">Resurrection Positional Y value to set</param>
    public void SetResPosY(float value)
    {
        this.resPosY = value;
    }

    /// <summary>the Resurrection Positional Y value</summary>
    public float GetResPosY()
    {
        return resPosY;
    }

    /// <param name="value">Resurrection Positional Z value to set</param>
    public void SetResPosZ(float value)
    {
        this.resPosZ = value;
    }

    /// <summary>the Resurrection Positional Z value</summary>
    public float GetResPosZ()
    {
        return resPosZ;
    }

    public bool IsInSiegeWorld()
    {
        switch (GetWorldId())
        {
            case 210050000:
            case 220070000:
            case 400010000:
                return true;
            default:
                return false;
        }
    }

    public bool HasPermission(byte perm)
    {
        return playerAccount.GetMembership() >= perm;
    }

    /// <summary>Returns the emotions.</summary>
    public Aion.GameServer.Model.GameObjects.Players.Emotion.EmotionList GetEmotions()
    {
        return emotions;
    }

    /// <param name="emotions">The emotions to set.</param>
    public void SetEmotions(Aion.GameServer.Model.GameObjects.Players.Emotion.EmotionList emotions)
    {
        this.emotions = emotions;
    }

    public Aion.GameServer.Model.GameObjects.Players.BindPointPosition GetBindPoint()
    {
        return bindPoint;
    }

    public void SetBindPoint(Aion.GameServer.Model.GameObjects.Players.BindPointPosition bindPoint)
    {
        this.bindPoint = bindPoint;
    }

    public int speedHackCounter;
    public int abnormalHackCounter;

    public override Aion.GameServer.Model.Templates.Items.ItemAttackType GetAttackType()
    {
        Item weapon = GetEquipment().GetMainHandWeapon();
        if (weapon != null)
            return weapon.GetItemTemplate().GetAttackType();
        return Aion.GameServer.Model.Templates.Items.ItemAttackType.PHYSICAL;
    }

    public long GetFlyStartTime()
    {
        return flyStartTime;
    }

    public Aion.GameServer.Model.Templates.Flypath.FlyPathEntry GetCurrentFlyPath()
    {
        return flyLocationId;
    }

    public void ResetAbyssRankListUpdated()
    {
        this.abyssRankListUpdateMask = 0;
    }

    public void SetAbyssRankListUpdated(Aion.GameServer.Model.GameObjects.Players.AbyssRank.AbyssRankUpdateType type)
    {
        this.abyssRankListUpdateMask |= type.Value();
    }

    public bool IsAbyssRankListUpdated(Aion.GameServer.Model.GameObjects.Players.AbyssRank.AbyssRankUpdateType type)
    {
        return (abyssRankListUpdateMask & type.Value()) == type.Value();
    }

    public void AddSalvationPoints(long points)
    {
        GetCommonData().AddSalvationPoints(points);
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(this, new Aion.GameServer.Network.Aion.ServerPackets.SM_STATS_INFO(this));
    }

    public override bool IsPvpTarget(Creature creature)
    {
        return creature.GetActingCreature() is Player;
    }

    public bool IsTargetingNpcWithFunction(int objectId, int dialogActionId)
    {
        VisibleObject target = GetTarget();
        return target is Npc npc && target.GetObjectId() == objectId && npc.GetObjectTemplate().SupportsAction(dialogActionId);
    }

    /// <summary>the motions</summary>
    public Aion.GameServer.Model.GameObjects.Players.Motion.MotionList GetMotions()
    {
        return motions;
    }

    /// <param name="motions">the motions to set</param>
    public void SetMotions(Aion.GameServer.Model.GameObjects.Players.Motion.MotionList motions)
    {
        this.motions = motions;
    }

    /// <summary>the npcFactions</summary>
    public Aion.GameServer.Model.GameObjects.Players.Npcfaction.NpcFactions GetNpcFactions()
    {
        return npcFactions;
    }

    /// <param name="npcFactions">the npcFactions to set</param>
    public void SetNpcFactions(Aion.GameServer.Model.GameObjects.Players.Npcfaction.NpcFactions npcFactions)
    {
        this.npcFactions = npcFactions;
    }

    /// <summary>the flyReuseTime</summary>
    public long GetFlyReuseTime()
    {
        return flyReuseTime;
    }

    /// <param name="flyReuseTime">the flyReuseTime to set</param>
    public void SetFlyReuseTime(long flyReuseTime)
    {
        this.flyReuseTime = flyReuseTime;
    }

    /// <summary>
    /// Stone Use Order determined by highest inventory slot. :( If player has two types, wrong one might be used.
    /// </summary>
    public Item GetSelfRezStone()
    {
        Item item;
        item = GetReviveStone(161001001);
        if (item == null)
            item = GetReviveStone(161000003);
        if (item == null)
            item = GetReviveStone(161000004);
        if (item == null)
            item = GetReviveStone(161000001);
        return item;
    }

    /// <summary>stoneItem or null</summary>
    private Item GetReviveStone(int stoneId)
    {
        Item item = GetInventory().GetFirstItemByItemId(stoneId);
        if (item != null && HasCooldown(item))
            item = null;
        return item;
    }

    /// <summary>Need to find how an item is determined as able to self-rez.</summary>
    public bool HaveSelfRezItem()
    {
        return GetSelfRezStone() != null;
    }

    public void UnsetResPosState()
    {
        if (IsInResPostState())
        {
            SetResPosState(false);
            SetResPosX(0);
            SetResPosY(0);
            SetResPosZ(0);
        }
    }

    public bool IsLooting()
    {
        return lootingNpcOid != 0;
    }

    public void SetLootingNpcOid(int lootingNpcOid)
    {
        this.lootingNpcOid = lootingNpcOid;
    }

    public int GetLootingNpcOid()
    {
        return lootingNpcOid;
    }

    public bool IsMentor()
    {
        return isMentor;
    }

    public void SetMentor(bool isMentor)
    {
        this.isMentor = isMentor;
    }

    public override Aion.GameServer.Model.Race GetRace()
    {
        return GetCommonData().GetRace();
    }

    public Aion.GameServer.Model.Race GetOppositeRace()
    {
        return GetRace() == Aion.GameServer.Model.Race.ELYOS ? Aion.GameServer.Model.Race.ASMODIANS : Aion.GameServer.Model.Race.ELYOS;
    }

    public override int GetSkillCooldown(SkillTemplate template)
    {
        return IsInCustomState(Aion.GameServer.Model.GameObjects.Players.CustomPlayerState.NO_SKILL_COOLDOWN_MODE) ? 0 : template.GetCooldown();
    }

    public void SetLastMessageTime()
    {
        if ((System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastMsgTime) / 1000 < Aion.GameServer.Configs.Main.SecurityConfig.FLOOD_DELAY)
            floodMsgCount++;
        else
            floodMsgCount = 0;
        lastMsgTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public int FloodMsgCount()
    {
        return floodMsgCount;
    }

    public void SetRebirthEffect(Aion.GameServer.SkillEngine.Effects.RebirthEffect rebirthEffect)
    {
        this.rebirthEffect = rebirthEffect;
    }

    public Aion.GameServer.SkillEngine.Effects.RebirthEffect GetRebirthEffect()
    {
        return rebirthEffect;
    }

    public bool CanUseRebirthRevive()
    {
        return rebirthEffect != null || HasAccess(Aion.GameServer.Configs.Administration.AdminConfig.AUTO_RES);
    }

    /// <summary>
    /// Put up supplements to subtraction queue, so that when moving they would not decrease, need update as confirmation.
    /// To update use updateSupplements()
    /// </summary>
    public void SubtractSupplements(int count, int supplementId)
    {
        subtractedSupplementsCount = count;
        subtractedSupplementId = supplementId;
    }

    /// <summary>Update supplements in queue and clear the queue</summary>
    public void UpdateSupplements()
    {
        if (subtractedSupplementId == 0 || subtractedSupplementsCount == 0)
            return;
        GetInventory().DecreaseByItemId(subtractedSupplementId, subtractedSupplementsCount);
        subtractedSupplementsCount = 0;
        subtractedSupplementId = 0;
    }

    public byte GetPortAnimationId()
    {
        return portAnimation;
    }

    public void SetPortAnimation(Aion.GameServer.Model.Animations.ArrivalAnimation portAnimation)
    {
        this.portAnimation = (byte)portAnimation;
    }

    public override bool IsSkillDisabled(SkillTemplate template)
    {
        Aion.GameServer.SkillEngine.Condition.ChainCondition cond = template.GetChainCondition();
        if (cond != null && cond.GetAllowedActivations() > 1) // exception for multicast
        {
            int chainCount = GetChainSkills().GetCurrentChainCount(cond.GetCategory());
            if (chainCount > 0 && chainCount < cond.GetAllowedActivations() && !GetChainSkills().IsChainExpired())
                return false;
        }
        if (base.IsSkillDisabled(template))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(this, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_SKILL_NOT_READY());
            return true;
        }
        return false;
    }

    public List<Aion.GameServer.Model.House.House> GetHouses()
    {
        if (houses == null)
            ResetHouses();
        return houses;
    }

    public void ResetHouses()
    {
        houses = Aion.GameServer.Services.HousingService.GetInstance().FindPlayerHouses(GetObjectId());
    }

    public Aion.GameServer.Model.House.House GetActiveHouse()
    {
        foreach (Aion.GameServer.Model.House.House house in GetHouses())
            if (!house.IsInactive())
                return house;

        return null;
    }

    public float[] GetBattleReturnCoords()
    {
        return battleReturnCoords;
    }

    public void SetBattleReturnCoords(int mapId, float[] coords)
    {
        this.battleReturnMap = mapId;
        this.battleReturnCoords = coords;
    }

    public int GetBattleReturnMap()
    {
        return battleReturnMap;
    }

    public bool IsInSprintMode()
    {
        return isInSprintMode;
    }

    public void SetSprintMode(bool isInSprintMode)
    {
        this.isInSprintMode = isInSprintMode;
    }

    public void SetRideObservers(ActionObserver observer)
    {
        if (rideObservers == null)
            rideObservers = new List<ActionObserver>();

        lock (rideObservers)
        {
            rideObservers.Add(observer);
        }
    }

    public List<ActionObserver> GetRideObservers()
    {
        return rideObservers;
    }

    public Aion.GameServer.Model.GameObjects.Players.AbsoluteStatOwner GetAbsoluteStats()
    {
        return absStatsHolder;
    }

    public override void SetPosition(WorldPosition position)
    {
        base.SetPosition(position);
        GetMoveController().ResetLastPositionFromClient(); // if we don't reset it, material collision handlers (such as shields) affect you on teleport
        GetCommonData().SetMapId(position.GetMapId());
        GetCommonData().SetX(position.GetX());
        GetCommonData().SetY(position.GetY());
        GetCommonData().SetZ(position.GetZ());
        GetCommonData().SetHeading(position.GetHeading());
        GetCommonData().SetWorldOwnerId(position.GetMapRegion() == null ? 0 : position.GetWorldMapInstance().GetOwnerId());
    }

    public int GetRobotId()
    {
        return robotId;
    }

    public void SetRobotId(int robotId)
    {
        this.robotId = robotId;
    }

    public bool IsInRobotMode()
    {
        return robotId != 0;
    }

    public override bool CanPerformMove()
    {
        // player cannot move is transformed
        if (GetTransformModel().GetBanMovement() == 1)
            return false;

        return base.CanPerformMove();
    }

    public override string ToString()
    {
        return "Player [id=" + GetObjectId() + ", name=" + GetName() + "]";
    }

    public void SetCustomState(Aion.GameServer.Model.GameObjects.Players.CustomPlayerState state)
    {
        customStates |= state.GetMask();
    }

    public void UnsetCustomState(Aion.GameServer.Model.GameObjects.Players.CustomPlayerState state)
    {
        customStates &= ~state.GetMask();
    }

    public bool IsInCustomState(Aion.GameServer.Model.GameObjects.Players.CustomPlayerState state)
    {
        return (customStates & state.GetMask()) == state.GetMask();
    }

    public Aion.GameServer.Services.Panesterra.Ahserion.PanesterraFaction? GetPanesterraFaction()
    {
        return panesterraFaction;
    }

    public void SetPanesterraFaction(Aion.GameServer.Services.Panesterra.Ahserion.PanesterraFaction? panesterraFaction)
    {
        this.panesterraFaction = panesterraFaction;
    }
}
