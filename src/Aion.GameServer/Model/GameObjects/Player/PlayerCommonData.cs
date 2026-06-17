using System;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>
/// This class is holding base information about player, that may be used even when player itself is not online.
/// Java parity: model/gameobjects/player/PlayerCommonData extends CreatureTemplate.
/// </summary>
public class PlayerCommonData : Aion.GameServer.Model.GameObjects.CreatureTemplate
{
    private readonly int playerObjId;
    private Aion.GameServer.Model.Race race;
    private string name;
    private Aion.GameServer.Model.PlayerClass playerClass;
    /// <summary>Should be changed right after character creation</summary>
    private int level = 0;
    private long exp = 0;
    private long expRecoverable = 0;
    private Aion.GameServer.Model.Gender gender;
    private DateTime? lastOnline;
    private bool online;
    private string note;
    private int mapId;
    private float x, y, z;
    private byte heading;
    private int questExpands = 0;
    private int npcExpands = 0;
    private int itemExpands = 0;
    private int warehouseNpcExpands = 0;
    private int warehouseBonusExpands = 0;
    private int titleId = -1;
    private int bonusTitleId = -1;
    private int dp = 0;
    private int mailboxLetters;
    private int soulSickness = 0;
    private bool noExp = false;
    private long reposeCurrent;
    private long reposeMax;
    private long salvationPoint;
    private int mentorFlagTime;
    private int worldOwnerId;
    private bool isDaeva;
    private bool isInEditMode;

    private BoundRadius boundRadius;

    private long lastTransferTime;

    // TODO: Move all function to playerService or Player class.
    public PlayerCommonData(int objId)
    {
        this.playerObjId = objId;
    }

    public int GetPlayerObjId()
    {
        return playerObjId;
    }

    public long GetExp()
    {
        return this.exp;
    }

    public int GetQuestExpands()
    {
        return this.questExpands;
    }

    public void SetQuestExpands(int questExpands)
    {
        this.questExpands = questExpands;
    }

    public void SetNpcExpands(int npcExpands)
    {
        this.npcExpands = npcExpands;
    }

    public int GetNpcExpands()
    {
        return npcExpands;
    }

    public int GetItemExpands()
    {
        return this.itemExpands;
    }

    public void SetItemExpands(int itemExpands)
    {
        this.itemExpands = itemExpands;
    }

    public long GetExpShown()
    {
        return this.exp - DataManager.PLAYER_EXPERIENCE_TABLE.GetStartExpForLevel(GetLevel());
    }

    public long GetExpNeed()
    {
        if (GetLevel() == DataManager.PLAYER_EXPERIENCE_TABLE.MaxLevel)
        {
            return 0;
        }
        return DataManager.PLAYER_EXPERIENCE_TABLE.GetStartExpForLevel(GetLevel() + 1)
            - DataManager.PLAYER_EXPERIENCE_TABLE.GetStartExpForLevel(GetLevel());
    }

    /// <summary>calculate the lost experience must be called before setexp</summary>
    public void CalculateExpLoss()
    {
        long expLost = Aion.GameServer.Utils.Stats.XPLossEnumExtensions.GetExpLoss(GetLevel(), this.GetExpNeed());

        int unrecoverable = (int)(expLost * 0.33333333);
        int recoverable = (int)expLost - unrecoverable;
        long allExpLost = recoverable + this.expRecoverable;

        if (this.GetExpShown() > unrecoverable)
        {
            this.exp = this.exp - unrecoverable;
        }
        else
        {
            this.exp = this.exp - this.GetExpShown();
        }
        if (this.GetExpShown() > recoverable)
        {
            this.expRecoverable = allExpLost;
            this.exp = this.exp - recoverable;
        }
        else
        {
            this.expRecoverable = this.expRecoverable + this.GetExpShown();
            this.exp = this.exp - this.GetExpShown();
        }
        if (this.expRecoverable > GetExpNeed() * 0.25)
        {
            this.expRecoverable = (long)Math.Floor(GetExpNeed() * 0.25 + 0.5);
        }
        if (this.GetPlayer() != null)
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(GetPlayer(),
                new Aion.GameServer.Network.Aion.ServerPackets.SM_STATUPDATE_EXP(GetExpShown(), GetExpRecoverable(), GetExpNeed(), this.GetCurrentReposeEnergy(), this.GetMaxReposeEnergy()));
    }

    public void SetRecoverableExp(long expRecoverable)
    {
        this.expRecoverable = expRecoverable;
    }

    public void ResetRecoverableExp()
    {
        long el = this.expRecoverable;
        this.expRecoverable = 0;
        this.SetExp(this.exp + el);
    }

    public long GetExpRecoverable()
    {
        return this.expRecoverable;
    }

    public void AddExp(long value, Rates rates)
    {
        AddExp(value, rates, null);
    }

    public void AddExp(long value, Rates rates, string name)
    {
        if (noExp)
            return;

        long reward = value;
        long repose = 0;
        long salvation = 0;
        Player player = GetPlayer();
        if (player != null && player.GetWorldId() == 301200000) // nightmare circus
            return;

        if (player != null)
            reward = rates.CalcResult(player, value);

        if (reward > 0)
        {
            if (GetCurrentReposeEnergy() > 0)
            {
                long allowedExp = Math.Min(GetCurrentReposeEnergy(), reward);
                AddReposeEnergy(-allowedExp);
                repose = (long)((allowedExp / 100f) * 40); // 40% bonus for the amount of used repose energy
            }

            if (IsReadyForSalvationPoints() && GetCurrentSalvationPercent() > 0)
            {
                salvation = (long)((reward / 100f) * GetCurrentSalvationPercent());
                // TODO! remove salvation points?
            }

            reward += repose + salvation;
        }

        SetExp(exp + reward);
        if (player != null)
        {
            if (repose > 0 && salvation > 0)
            {
                if (name != null) // You have gained %num1 XP from %0 (Energy of Repose %num2, Energy of Salvation %num3).
                    Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_GET_EXP_VITAL_MAKEUP_BONUS(name, reward, repose, salvation));
                else // You have gained %num1 XP(Energy of Repose %num2, Energy of Salvation %num3).
                    Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_GET_EXP2_VITAL_MAKEUP_BONUS(reward, repose, salvation));
            }
            else if (repose > 0 && salvation == 0)
            {
                if (name != null) // You have gained %num1 XP from %0 (Energy of Repose %num2).
                    Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_GET_EXP_VITAL_BONUS(name, reward, repose));
                else // You have gained %num1 XP(Energy of Repose %num2).
                    Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_GET_EXP2_VITAL_BONUS(reward, repose));
            }
            else if (repose == 0 && salvation > 0)
            {
                if (name != null) // You have gained %num1 XP from %0 (Energy of Salvation %num2).
                    Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_GET_EXP_MAKEUP_BONUS(name, reward, salvation));
                else // You have gained %num1 XP (Energy of Salvation %num2).
                    Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_GET_EXP2_MAKEUP_BONUS(reward, salvation));
            }
            else
            {
                if (name != null) // You have gained %num1 XP from %0.
                    Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_GET_EXP(name, reward));
                else // You have gained %num1 XP.
                    Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_GET_EXP2(reward));
            }
            if (GetLevel() == 9 && exp >= DataManager.PLAYER_EXPERIENCE_TABLE.GetStartExpForLevel(10))
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_LEVEL_LIMIT_QUEST_NOT_FINISHED1());
        }
    }

    public bool IsInEditMode()
    {
        return isInEditMode;
    }

    public void SetInEditMode(bool isInEditMode)
    {
        this.isInEditMode = isInEditMode;
    }

    public bool IsReadyForSalvationPoints()
    {
        return GetLevel() >= 15;
    }

    public bool IsReadyForReposeEnergy()
    {
        return GetLevel() >= 10;
    }

    public void AddReposeEnergy(long add)
    {
        reposeCurrent += add;
        if (reposeCurrent < 0)
            reposeCurrent = 0;
        else if (reposeCurrent > GetMaxReposeEnergy())
            reposeCurrent = GetMaxReposeEnergy();
    }

    public void UpdateMaxRepose()
    {
        if (!IsReadyForReposeEnergy())
        {
            reposeCurrent = 0;
            reposeMax = 0;
        }
        else
        {
            reposeMax = (long)(GetExpNeed() * 0.25f); // Retail 99%
            reposeCurrent = Math.Min(reposeMax, reposeCurrent);
        }
    }

    public void SetCurrentReposeEnergy(long value)
    {
        reposeCurrent = value;
    }

    public long GetCurrentReposeEnergy()
    {
        return reposeCurrent;
    }

    public long GetMaxReposeEnergy()
    {
        return reposeMax;
    }

    /// <summary>sets the exp and level value</summary>
    public void SetExp(long exp)
    {
        if (exp != this.exp || level == 0 && exp == 0)
        {
            PlayerExperienceTable pxt = DataManager.PLAYER_EXPERIENCE_TABLE;
            int maxLevel = isDaeva || !online && (UpdateDaeva() || exp > pxt.GetStartExpForLevel(10)) ? pxt.MaxLevel : 10;
            int oldLevel = level;

            this.exp = Math.Min(exp, pxt.GetStartExpForLevel(maxLevel));
            // maxLevel is 66 (10 for non daeva) but 65 (9 for non daeva) should be shown with full XP bar
            level = Math.Min(pxt.GetLevelForExp(this.exp), maxLevel - 1);

            Player player = GetPlayer();
            if (player != null)
            {
                player.GetController().OnLevelChange(oldLevel, level);
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player,
                    new Aion.GameServer.Network.Aion.ServerPackets.SM_STATUPDATE_EXP(GetExpShown(), GetExpRecoverable(), GetExpNeed(), GetCurrentReposeEnergy(), GetMaxReposeEnergy()));
            }
        }
    }

    public void SetNoExp(bool value)
    {
        this.noExp = value;
    }

    public bool GetNoExp()
    {
        return noExp;
    }

    public Aion.GameServer.Model.Race GetRace()
    {
        return race;
    }

    public int GetMentorFlagTime()
    {
        return mentorFlagTime;
    }

    public bool IsHaveMentorFlag()
    {
        return mentorFlagTime > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000;
    }

    public void SetMentorFlagTime(int mentorFlagTime)
    {
        this.mentorFlagTime = mentorFlagTime;
    }

    public void SetRace(Aion.GameServer.Model.Race race)
    {
        this.race = race;
    }

    public override string GetName()
    {
        return name;
    }

    public void SetName(string name)
    {
        this.name = name;
    }

    public Aion.GameServer.Model.PlayerClass GetPlayerClass()
    {
        return playerClass;
    }

    public void SetPlayerClass(Aion.GameServer.Model.PlayerClass playerClass)
    {
        this.playerClass = playerClass;
    }

    public bool IsOnline()
    {
        return online;
    }

    public void SetOnline(bool online)
    {
        this.online = online;
    }

    public Aion.GameServer.Model.Gender GetGender()
    {
        return gender;
    }

    public void SetGender(Aion.GameServer.Model.Gender gender)
    {
        this.gender = gender;
    }

    public int GetMapId()
    {
        return mapId;
    }

    public void SetMapId(int mapId)
    {
        this.mapId = mapId;
    }

    public float GetX()
    {
        return x;
    }

    public void SetX(float x)
    {
        this.x = x;
    }

    public float GetY()
    {
        return y;
    }

    public void SetY(float y)
    {
        this.y = y;
    }

    public float GetZ()
    {
        return z;
    }

    public void SetZ(float z)
    {
        this.z = z;
    }

    public byte GetHeading()
    {
        return heading;
    }

    public void SetHeading(byte heading)
    {
        this.heading = heading;
    }

    /// <summary>Timestamp the player was last online. May be null</summary>
    public DateTime? GetLastOnline()
    {
        return lastOnline;
    }

    /// <summary>
    /// Unix timestamp the player was last online (measured in seconds since 1970-01-01T00:00:00Z). 0 if he was never online before.
    /// </summary>
    public int GetLastOnlineEpochSeconds()
    {
        return lastOnline == null ? 0 : (int)(new DateTimeOffset(DateTime.SpecifyKind(lastOnline.Value, DateTimeKind.Utc)).ToUnixTimeSeconds());
    }

    public void SetLastOnline(DateTime? timestamp)
    {
        lastOnline = timestamp;
    }

    public int GetLevel()
    {
        return level;
    }

    /// <summary>This will only set the specified level &gt;= 10 if the player is a daeva.</summary>
    public void SetLevel(int level)
    {
        SetExp(DataManager.PLAYER_EXPERIENCE_TABLE.GetStartExpForLevel(level));
    }

    public string GetNote()
    {
        return note;
    }

    public void SetNote(string note)
    {
        this.note = note;
    }

    public int GetTitleId()
    {
        return titleId;
    }

    public void SetTitleId(int titleId)
    {
        this.titleId = titleId;
    }

    public int GetBonusTitleId()
    {
        return bonusTitleId;
    }

    public void SetBonusTitleId(int bonusTitleId)
    {
        this.bonusTitleId = bonusTitleId;
    }

    /// <summary>
    /// Gets the corresponding Player for this common data. Returns null if the player is not online.
    /// </summary>
    public Player GetPlayer()
    {
        return online ? Aion.GameServer.World.World.GetInstance().GetPlayer(playerObjId) : null;
    }

    public void AddDp(int dp)
    {
        SetDp(this.dp + dp);
    }

    /// <summary>//TODO move to lifestats -&gt; db save? =&gt; PlayerGameStats#onStatsChange()</summary>
    public void SetDp(int dp)
    {
        if (playerClass.IsStartingClass())
            return;

        int maxDp = (GetPlayer() == null) ? -1 : GetPlayer().GetGameStats().GetMaxDp().GetCurrent();
        this.dp = (maxDp >= 0 && dp > maxDp) ? maxDp : dp;

        if (GetPlayer() != null)
        {
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(GetPlayer(), new Aion.GameServer.Network.Aion.ServerPackets.SM_DP_INFO(playerObjId, this.dp), true);
            GetPlayer().GetGameStats().UpdateStatsAndSpeedVisually();
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(GetPlayer(), new Aion.GameServer.Network.Aion.ServerPackets.SM_STATUPDATE_DP(this.dp));
        }
    }

    public int GetDp()
    {
        return this.dp;
    }

    public override int GetTemplateId()
    {
        return 100000 + race.GetRaceId() * 2 + gender.GetGenderId();
    }

    public override int GetL10nId()
    {
        return 0;
    }

    public void SetWhNpcExpands(int value)
    {
        this.warehouseNpcExpands = value;
    }

    public int GetWhNpcExpands()
    {
        return warehouseNpcExpands;
    }

    public int GetWhBonusExpands()
    {
        return warehouseBonusExpands;
    }

    public void SetWhBonusExpands(int value)
    {
        this.warehouseBonusExpands = value;
    }

    public void SetMailboxLetters(int count)
    {
        this.mailboxLetters = count;
    }

    public int GetMailboxLetters()
    {
        return mailboxLetters;
    }

    public void SetBoundingRadius(BoundRadius boundRadius)
    {
        this.boundRadius = boundRadius;
    }

    public override BoundRadius GetBoundRadius()
    {
        return boundRadius;
    }

    public void SetDeathCount(int count)
    {
        this.soulSickness = count;
    }

    public int GetDeathCount()
    {
        return this.soulSickness;
    }

    /// <summary>Value returned here means % of exp bonus.</summary>
    public byte GetCurrentSalvationPercent()
    {
        if (salvationPoint <= 0)
            return 0;

        long per = salvationPoint / 1000;
        if (per > 30)
            return 30;

        return (byte)per;
    }

    public void AddSalvationPoints(long points)
    {
        salvationPoint += points;
    }

    public void SetCurrentSalvationPoints(long points)
    {
        salvationPoint = points;
    }

    public void ResetSalvationPoints()
    {
        salvationPoint = 0;
    }

    public void SetLastTransferTime(long value)
    {
        this.lastTransferTime = value;
    }

    public long GetLastTransferTime()
    {
        return this.lastTransferTime;
    }

    public int GetWorldOwnerId()
    {
        return worldOwnerId;
    }

    public void SetWorldOwnerId(int worldOwnerId)
    {
        this.worldOwnerId = worldOwnerId;
    }

    /// <summary>
    /// True, if the player has a main class and completed the ascension quest (gets updated on login and quest completion).
    /// </summary>
    public bool IsDaeva()
    {
        return isDaeva;
    }

    public void SetDaeva(bool isDaeva)
    {
        this.isDaeva = isDaeva;
    }

    /// <summary>True, if player was promoted to daeva. False if he already has daeva status or wasn't promoted.</summary>
    public bool UpdateDaeva()
    {
        if (isDaeva)
            return false;

        if (playerClass.IsStartingClass())
            return false;

        Aion.GameServer.Model.GameObjects.Players.QuestStateList qsl;
        Player player = GetPlayer();
        if (player != null)
            qsl = player.GetQuestStateList();
        else
            qsl = Aion.GameServer.Dao.PlayerQuestListDAO.Load(playerObjId);

        // check both quest states in case a player changed race
        Aion.GameServer.QuestEngine.Model.QuestStatus? elyAscentQuestStatus = qsl.GetQuestState(1006) != null ? qsl.GetQuestState(1006).GetStatus() : (Aion.GameServer.QuestEngine.Model.QuestStatus?)null;
        Aion.GameServer.QuestEngine.Model.QuestStatus? asmoAscentQuestStatus = qsl.GetQuestState(2008) != null ? qsl.GetQuestState(2008).GetStatus() : (Aion.GameServer.QuestEngine.Model.QuestStatus?)null;
        if (elyAscentQuestStatus != Aion.GameServer.QuestEngine.Model.QuestStatus.COMPLETE && asmoAscentQuestStatus != Aion.GameServer.QuestEngine.Model.QuestStatus.COMPLETE)
            return false;

        SetDaeva(true);
        return true;
    }
}
