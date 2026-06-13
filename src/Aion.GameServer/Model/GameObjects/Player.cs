using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Aion.GameServer.Controllers;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>
/// Java parity: model/gameobjects/player/Player extends Creature. The central player object.
/// </summary>
/// <remarks>
/// Ported as faithful 1:1 REPLACING the prior divergent standalone Player.cs (rule 8). Split into faithful
/// <c>partial class</c> files (Player.cs core + Player.*.cs method groups) because the class is 1655L/238 methods.
/// PARTIAL #1: class shell + all ~40 fields + constructor + accessors (Java lines 194-514). Remaining method groups
/// land in subsequent Player.PartN.cs partials. Its ~40 subsystem field types are referenced red until ported.
/// </remarks>
public partial class Player : Creature
{
    // Java parity: property-form bridges over the getX() accessors (reworked call sites use player.Race, player.Dp, ...).
    public Aion.GameServer.Model.Race Race => GetRace();
    public int Level => GetCommonData().GetLevel();
    public int Dp => GetCommonData().GetDp();
    public long Exp => GetCommonData().GetExp();
    public Aion.GameServer.Model.PlayerClass PlayerClass => GetPlayerClass();
    public Aion.GameServer.Model.GameObjects.Players.AbyssRank AbyssRank => GetAbyssRank();
    public Aion.GameServer.Model.GameObjects.Players.Mailbox Mailbox => GetMailbox();
    public Aion.GameServer.Model.Skill.PlayerSkillList Skills => GetSkillList();
    public Aion.GameServer.Model.Stats.Container.PlayerLifeStats LifeStats => GetLifeStats();
    public int TitleId => GetCommonData().GetTitleId();
    public int BonusTitleId => GetCommonData().GetBonusTitleId();
    public bool IsFlyingBeforeDeath { get => GetIsFlyingBeforeDeath(); set => SetIsFlyingBeforeDeath(value); }
    public long ReposeEnergy { get => GetCommonData().GetCurrentReposeEnergy(); set => GetCommonData().SetCurrentReposeEnergy(value); }
    public string Note { get => GetCommonData().GetNote(); set => GetCommonData().SetNote(value); }
    public Aion.GameServer.Model.GameObjects.Players.BindPointPosition BindPoint { get => GetBindPoint(); set => SetBindPoint(value); }
    public int AccountId => playerAccount.GetId();
    public string LegionName => GetLegion() != null ? GetLegion().GetName() : "";
    public Aion.GameServer.Model.GameObjects.Players.PlayerSettings Settings { get => GetPlayerSettings(); set => SetPlayerSettings(value); }
    public Aion.GameServer.Model.GameObjects.Players.Npcfaction.NpcFactions NpcFactions { get => GetNpcFactions(); set => SetNpcFactions(value); }
    public byte AccountMembership => (byte)playerAccount.GetMembership();
    public Aion.GameServer.Model.Templates.Ride.RideInfo RideInfo { get => ride; set => ride = value; }

    public volatile Aion.GameServer.Model.Templates.Ride.RideInfo ride;
    public volatile Aion.GameServer.Model.GameObjects.Players.InRoll inRoll;
    public Aion.GameServer.Model.Ingameshop.InGameShop inGameShop;
    private readonly Aion.GameServer.Model.Account.PlayerAccountData playerAccountData;
    private readonly Aion.GameServer.Model.Account.Account playerAccount;
    private Aion.GameServer.Model.Team.Legion.LegionMember legionMember;

    private Aion.GameServer.Model.GameObjects.Players.Macros macros;
    private Aion.GameServer.Model.Skill.PlayerSkillList skillList;
    private Aion.GameServer.Model.GameObjects.Players.FriendList friendList;
    private Aion.GameServer.Model.GameObjects.Players.BlockList blockList;
    private Aion.GameServer.Model.GameObjects.Players.PetList toyPetList;
    private Aion.GameServer.Model.GameObjects.Players.Mailbox mailbox;
    private Aion.GameServer.Model.GameObjects.Players.PrivateStore store;
    private Aion.GameServer.Model.GameObjects.Players.Title.TitleList titleList;
    private Aion.GameServer.Model.GameObjects.Players.QuestStateList questStateList;
    private Aion.GameServer.Model.GameObjects.Players.RecipeList recipeList;
    private List<Aion.GameServer.Model.House.House> houses;

    private Aion.GameServer.Model.GameObjects.Players.ResponseRequester requester;
    private bool lookingForGroup = false;
    private readonly Aion.GameServer.Model.GameObjects.Players.Equipment equipment;
    private readonly Aion.GameServer.Model.Items.Storage.Storage inventory;
    private readonly Aion.GameServer.Model.Items.Storage.Storage regularWarehouse;
    private readonly Aion.GameServer.Model.Items.Storage.Storage[] petBags = new Aion.GameServer.Model.Items.Storage.Storage[Aion.GameServer.Model.Items.Storage.StorageType.PET_BAG_MAX - Aion.GameServer.Model.Items.Storage.StorageType.PET_BAG_MIN + 1];
    private readonly Aion.GameServer.Model.Items.Storage.Storage[] cabinets = new Aion.GameServer.Model.Items.Storage.Storage[Aion.GameServer.Model.Items.Storage.StorageType.HOUSE_WH_MAX - Aion.GameServer.Model.Items.Storage.StorageType.HOUSE_WH_MIN + 1];
    private Item usingItem;

    private readonly Aion.GameServer.Model.GameObjects.Players.AbsoluteStatOwner absStatsHolder;
    private Aion.GameServer.Model.GameObjects.Players.PlayerSettings playerSettings;

    private Aion.GameServer.Model.Team.Group.PlayerGroup playerGroup;
    private Aion.GameServer.Model.Team.Alliance.PlayerAllianceGroup playerAllianceGroup;

    private Aion.GameServer.Model.GameObjects.Players.AbyssRank abyssRank;
    private Aion.GameServer.Model.GameObjects.Players.Npcfaction.NpcFactions npcFactions;

    private int flyState = 0;
    private Aion.GameServer.Controllers.FlyController flyController;
    private Aion.GameServer.SkillEngine.Task.CraftingTask craftingTask;
    private Aion.GameServer.Model.Templates.Flypath.FlightPath flightPath;
    private Summon summon;
    private Pet pet;
    private Kisk kisk;
    private bool isResByPlayer = false;
    private int resurrectionSkill = 0;
    private bool isFlyingBeforeDeath = false;
    private Npc postman = null;
    private bool isInResurrectPosState = false;
    private float resPosX = 0;
    private float resPosY = 0;
    private float resPosZ = 0;

    private int abyssRankListUpdateMask = 0;

    private Aion.GameServer.Model.GameObjects.Players.BindPointPosition bindPoint;

    private readonly ConcurrentDictionary<int, Aion.GameServer.Model.Items.ItemCooldown> itemCoolDowns = new ConcurrentDictionary<int, Aion.GameServer.Model.Items.ItemCooldown>();
    private readonly Aion.GameServer.Model.GameObjects.Players.PortalCooldownList portalCooldownList;
    private readonly Aion.GameServer.Model.GameObjects.Players.Cooldowns craftCooldowns;
    private readonly Aion.GameServer.Model.GameObjects.Players.Cooldowns houseObjectCooldowns;
    private long nextSkillUse;
    private SkillTemplate lastSkill;
    private long hitTimeBoostExpireTimeMillis;
    private float hitTimeBoostCastSpeed;
    private Aion.GameServer.SkillEngine.Model.ChainSkills chainSkills;
    private readonly Dictionary<AttackStatus, long> lastCounterSkill = new Dictionary<AttackStatus, long>();

    private long prisonEndTimeMillis = 0;
    private long gatherRestrictionMillis;
    private string captchaWord;
    private byte[] captchaImage;

    /// <summary>Connection of this Player.</summary>
    private Aion.GameServer.Network.Aion.AionConnection clientConnection;
    private Aion.GameServer.Model.Templates.Flypath.FlyPathEntry flyLocationId;
    private long flyStartTime;

    private Aion.GameServer.Model.GameObjects.Players.Emotion.EmotionList emotions;
    private Aion.GameServer.Model.GameObjects.Players.Motion.MotionList motions;

    private long flyReuseTime;

    private bool isMentor;

    private long lastMsgTime = 0;
    private int floodMsgCount = 0;

    private int lootingNpcOid;
    private Aion.GameServer.SkillEngine.Effects.RebirthEffect rebirthEffect;

    // Needed to remove supplements queue
    private int subtractedSupplementsCount;
    private int subtractedSupplementId;
    private byte portAnimation;
    private bool isInSprintMode;
    private List<ActionObserver> rideObservers;

    private int battleReturnMap;
    private float[] battleReturnCoords;
    private int robotId;
    private bool isInFfaTeamMode;
    private int customStates;
    private Aion.GameServer.Services.Panesterra.Ahserion.PanesterraFaction panesterraFaction;

    public Player(Aion.GameServer.Model.Account.PlayerAccountData playerAccountData, Aion.GameServer.Model.Account.Account account)
        : base(playerAccountData.GetPlayerCommonData().GetPlayerObjId(), new PlayerController(), null, playerAccountData.GetPlayerCommonData(), null, false)
    {
        this.playerAccountData = playerAccountData;
        this.playerAccount = account;

        this.requester = new Aion.GameServer.Model.GameObjects.Players.ResponseRequester(this);
        this.questStateList = new Aion.GameServer.Model.GameObjects.Players.QuestStateList();
        this.titleList = new Aion.GameServer.Model.GameObjects.Players.Title.TitleList();
        this.equipment = new Aion.GameServer.Model.GameObjects.Players.Equipment(this);
        this.inventory = new Aion.GameServer.Model.Items.Storage.PlayerStorage(this, Aion.GameServer.Model.Items.Storage.StorageType.CUBE);
        this.regularWarehouse = new Aion.GameServer.Model.Items.Storage.PlayerStorage(this, Aion.GameServer.Model.Items.Storage.StorageType.REGULAR_WAREHOUSE);
        for (int i = 0; i < petBags.Length; i++)
            petBags[i] = new Aion.GameServer.Model.Items.Storage.PlayerStorage(this, Aion.GameServer.Model.Items.Storage.StorageType.GetStorageTypeById(Aion.GameServer.Model.Items.Storage.StorageType.PET_BAG_MIN + i));
        for (int i = 0; i < cabinets.Length; i++)
            cabinets[i] = new Aion.GameServer.Model.Items.Storage.PlayerStorage(this, Aion.GameServer.Model.Items.Storage.StorageType.GetStorageTypeById(Aion.GameServer.Model.Items.Storage.StorageType.HOUSE_WH_MIN + i));
        this.portalCooldownList = new Aion.GameServer.Model.GameObjects.Players.PortalCooldownList(this);
        this.craftCooldowns = new Aion.GameServer.Model.GameObjects.Players.Cooldowns();
        this.houseObjectCooldowns = new Aion.GameServer.Model.GameObjects.Players.Cooldowns();
        this.toyPetList = new Aion.GameServer.Model.GameObjects.Players.PetList(this);
        GetController().SetOwner(this);
        moveController = new Aion.GameServer.Controllers.Movement.PlayerMoveController(this);

        SetGameStats(new PlayerGameStats(this));
        SetLifeStats(new Aion.GameServer.Model.Stats.Container.PlayerLifeStats(this));
        inGameShop = new Aion.GameServer.Model.Ingameshop.InGameShop();
        absStatsHolder = new Aion.GameServer.Model.GameObjects.Players.AbsoluteStatOwner(this, 0);
    }

    public bool IsInPlayerMode(Aion.GameServer.Model.Actions.PlayerMode mode)
    {
        return Aion.GameServer.Model.Actions.PlayerActions.IsInPlayerMode(this, mode);
    }

    public void SetPlayerMode(Aion.GameServer.Model.Actions.PlayerMode mode, object obj)
    {
        Aion.GameServer.Model.Actions.PlayerActions.SetPlayerMode(this, mode, obj);
    }

    public void UnsetPlayerMode(Aion.GameServer.Model.Actions.PlayerMode mode)
    {
        Aion.GameServer.Model.Actions.PlayerActions.UnsetPlayerMode(this, mode);
    }

    public override Aion.GameServer.Controllers.Movement.PlayerMoveController GetMoveController()
    {
        return (Aion.GameServer.Controllers.Movement.PlayerMoveController)base.GetMoveController();
    }

    protected sealed override AggroList CreateAggroList()
    {
        return new PlayerAggroList(this);
    }

    public Aion.GameServer.Model.GameObjects.Players.PlayerCommonData GetCommonData()
    {
        return playerAccountData.GetPlayerCommonData();
    }

    public override string Name => GetName(false);

    public string GetName(bool displayCustomTag)
    {
        if (displayCustomTag && Aion.GameServer.Configs.Administration.AdminConfig.NAME_TAGS.Length > 0)
        {
            int index = playerAccount.GetAccessLevel() - 1;
            if (index >= 0 && index < Aion.GameServer.Configs.Administration.AdminConfig.NAME_TAGS.Length)
                return string.Format(Aion.GameServer.Configs.Administration.AdminConfig.NAME_TAGS[index], GetCommonData().GetName());
        }
        return GetCommonData().GetName();
    }

    public Aion.GameServer.Model.GameObjects.Players.PlayerAppearance GetPlayerAppearance()
    {
        return playerAccountData.GetAppearance();
    }

    public void SetPlayerAppearance(Aion.GameServer.Model.GameObjects.Players.PlayerAppearance playerAppearance)
    {
        playerAccountData.SetAppearance(playerAppearance);
    }

    public void SetClientConnection(Aion.GameServer.Network.Aion.AionConnection clientConnection)
    {
        this.clientConnection = clientConnection;
    }

    public Aion.GameServer.Network.Aion.AionConnection GetClientConnection()
    {
        return clientConnection;
    }

    public Aion.GameServer.Model.GameObjects.Players.Macros GetMacros()
    {
        return macros;
    }

    public void SetMacros(Aion.GameServer.Model.GameObjects.Players.Macros macros)
    {
        this.macros = macros;
    }

    public Aion.GameServer.Model.Skill.PlayerSkillList GetSkillList()
    {
        return skillList;
    }

    public void SetSkillList(Aion.GameServer.Model.Skill.PlayerSkillList skillList)
    {
        this.skillList = skillList;
    }

    public Pet GetPet()
    {
        return pet;
    }

    public void SetPet(Pet pet)
    {
        this.pet = pet;
    }

    public Aion.GameServer.Model.GameObjects.Players.FriendList GetFriendList()
    {
        return friendList;
    }

    public bool IsLookingForGroup()
    {
        return lookingForGroup;
    }

    public void SetLookingForGroup(bool lookingForGroup)
    {
        this.lookingForGroup = lookingForGroup;
    }

    public bool IsInAttackMode()
    {
        return IsInState(CreatureState.WEAPON_EQUIPPED);
    }

    public bool IsGatherRestricted()
    {
        return GetGatherRestrictionDurationSeconds() > 0;
    }

    public void SetGatherRestrictionExpirationTime(long millis)
    {
        gatherRestrictionMillis = millis;
    }

    public int GetGatherRestrictionDurationSeconds()
    {
        if (gatherRestrictionMillis == 0)
            return 0;
        int durationSeconds = (int)((gatherRestrictionMillis - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) / 1000);
        if (durationSeconds < 0)
            gatherRestrictionMillis = durationSeconds = 0;
        return durationSeconds;
    }

    public string GetCaptchaWord()
    {
        return captchaWord;
    }

    public void SetCaptchaWord(string captchaWord)
    {
        this.captchaWord = captchaWord;
    }

    public byte[] GetCaptchaImage()
    {
        return captchaImage;
    }

    public void SetCaptchaImage(byte[] captchaImage)
    {
        this.captchaImage = captchaImage;
    }

    public void SetFriendList(Aion.GameServer.Model.GameObjects.Players.FriendList list)
    {
        this.friendList = list;
    }

    public Aion.GameServer.Model.GameObjects.Players.BlockList GetBlockList()
    {
        return blockList;
    }

    public void SetBlockList(Aion.GameServer.Model.GameObjects.Players.BlockList list)
    {
        this.blockList = list;
    }

    public Aion.GameServer.Model.GameObjects.Players.PetList GetPetList()
    {
        return toyPetList;
    }

    // Java covariant-return getLifeStats():PlayerLifeStats -> C# 'new' (method hiding); the body just
    // downcasts the base result, so behaviour is identical and C#'s covariant-return rejection is avoided.
    public new Aion.GameServer.Model.Stats.Container.PlayerLifeStats GetLifeStats()
    {
        return (Aion.GameServer.Model.Stats.Container.PlayerLifeStats)base.GetLifeStats();
    }

    public override PlayerGameStats GetGameStats()
    {
        return (PlayerGameStats)base.GetGameStats();
    }

    public Aion.GameServer.Model.GameObjects.Players.ResponseRequester GetResponseRequester()
    {
        return requester;
    }

    public bool IsOnline()
    {
        return GetClientConnection() != null;
    }

    public int GetQuestExpands()
    {
        return GetCommonData().GetQuestExpands();
    }

    public int GetNpcExpands()
    {
        return GetCommonData().GetNpcExpands();
    }

    public int GetItemExpands()
    {
        return GetCommonData().GetItemExpands();
    }

    public void SetCubeLimit()
    {
        GetInventory().SetLimit(Aion.GameServer.Model.Items.Storage.StorageType.CUBE.GetLimit() + (GetNpcExpands() + GetQuestExpands() + GetItemExpands()) * GetInventory().GetRowLength());
    }

    public PlayerClass GetPlayerClass()
    {
        return GetCommonData().GetPlayerClass();
    }

    public Aion.GameServer.Model.Gender GetGender()
    {
        return GetCommonData().GetGender();
    }

    public override PlayerController GetController()
    {
        return (PlayerController)base.GetController();
    }

    public override sbyte GetLevel()
    {
        return (sbyte)GetCommonData().GetLevel();
    }

    public Aion.GameServer.Model.GameObjects.Players.Equipment GetEquipment()
    {
        return equipment;
    }

    public Item GetUsingItem()
    {
        return usingItem;
    }

    public void SetUsingItem(Item usingItem)
    {
        this.usingItem = usingItem;
    }

    public Aion.GameServer.Model.GameObjects.Players.PrivateStore GetStore()
    {
        return store;
    }

    public void SetStore(Aion.GameServer.Model.GameObjects.Players.PrivateStore store)
    {
        this.store = store;
    }

    public Aion.GameServer.Model.GameObjects.Players.QuestStateList GetQuestStateList()
    {
        return questStateList;
    }

    public void SetQuestStateList(Aion.GameServer.Model.GameObjects.Players.QuestStateList questStateList)
    {
        this.questStateList = questStateList;
    }

    public Aion.GameServer.Model.GameObjects.Players.RecipeList GetRecipeList()
    {
        return recipeList;
    }

    public void SetRecipeList(Aion.GameServer.Model.GameObjects.Players.RecipeList recipeList)
    {
        this.recipeList = recipeList;
    }
}
