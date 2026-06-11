using Aion.GameServer.Controllers;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Knownlist;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>Java parity: model/gameobjects/HouseObject (Rolandas).</summary>
public abstract class HouseObject<T> : VisibleObject, IExpirable, IPersistable where T : PlaceableHouseObject
{
    private int expireEnd;
    private float x;
    private float y;
    private float z;
    private sbyte heading;
    private int ownerUsedCount = 0;
    private int visitorUsedCount = 0;
    private int? color = null;
    private int colorExpireEnd;

    private readonly HouseRegistry registry;
    // don't set it directly, ever!!! Use setPersistentState() method instead
    private IPersistable.PersistentState persistentState = IPersistable.PersistentState.NEW;

    public HouseObject(HouseRegistry registry, int objId, int templateId)
        : this(registry, objId, templateId, false)
    {
    }

    public HouseObject(HouseRegistry registry, int objId, int templateId, bool autoReleaseObjectId)
        : base(objId, new PlaceableObjectController<T>(), null, DataManager.HOUSING_OBJECT_DATA.GetTemplateById(templateId), null, autoReleaseObjectId)
    {
        this.registry = registry;
        GetController().SetOwner(this);
        SetKnownlist(new PlayerAwareKnownList(this));
    }

    public IPersistable.PersistentState GetPersistentState()
    {
        return persistentState;
    }

    public void SetPersistentState(IPersistable.PersistentState persistentState)
    {
        switch (persistentState)
        {
            case IPersistable.PersistentState.DELETED:
                if (this.persistentState == IPersistable.PersistentState.NEW)
                    this.persistentState = IPersistable.PersistentState.NOACTION;
                else if (this.persistentState != IPersistable.PersistentState.DELETED)
                {
                    this.persistentState = IPersistable.PersistentState.DELETED;
                    registry.SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
                }
                break;
            case IPersistable.PersistentState.UPDATE_REQUIRED:
                if (this.persistentState == IPersistable.PersistentState.NEW)
                {
                    registry.SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
                    break;
                }
                goto default;
            default:
                if (this.persistentState != persistentState)
                {
                    this.persistentState = persistentState;
                    registry.SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
                }
                break;
        }
    }

    public int GetExpireTime()
    {
        return expireEnd;
    }

    public void SetExpireTime(int time)
    {
        expireEnd = time;
    }

    public void OnExpire(Player player)
    {
        DespawnAndRemoveHouseObject(player, true);
    }

    protected void DespawnAndRemoveHouseObject(Player player, bool isExpired)
    {
        if (IsSpawnedByPlayer())
        {
            PacketSendUtility.SendPacket(player, new SM_HOUSE_EDIT(7, 0, GetObjectId()));
            GetController().Delete();
        }
        PacketSendUtility.SendPacket(player, new SM_HOUSE_EDIT(4, 1, GetObjectId()));
        if (isExpired)
            PacketSendUtility.SendPacket(player, SmSystemMessage.STR_MSG_HOUSING_OBJECT_DELETE_EXPIRE_TIME(GetObjectTemplate().GetL10n()));
        else
            PacketSendUtility.SendPacket(player, SmSystemMessage.STR_MSG_HOUSING_OBJECT_DELETE_USE_COUNT_FINAL(GetObjectTemplate().GetL10n()));
        registry.DiscardObject(this, false);

        // if owner is not online, we should save his items
        Player owner = Aion.GameServer.World.World.GetInstance().GetPlayer(registry.GetOwner().GetOwnerId());
        if (owner == null || !owner.IsOnline())
            registry.Save();
    }

    public new T GetObjectTemplate()
    {
        return (T) base.GetObjectTemplate();
    }

    public new float GetX()
    {
        return x;
    }

    public void SetX(float x)
    {
        if (this.x != x)
        {
            this.x = x;
            SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
            if (Position != null)
                Position.SetXYZH(x, null, null, null);
        }
    }

    public new float GetY()
    {
        return y;
    }

    public void SetY(float y)
    {
        if (this.y != y)
        {
            this.y = y;
            SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
            if (Position != null)
                Position.SetXYZH(null, y, null, null);
        }
    }

    public new float GetZ()
    {
        return z;
    }

    public void SetZ(float z)
    {
        if (this.z != z)
        {
            this.z = z;
            SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
            if (Position != null)
                Position.SetXYZH(null, null, z, null);
        }
    }

    public new sbyte GetHeading()
    {
        return heading;
    }

    public void SetHeading(sbyte heading)
    {
        if (this.heading != heading)
        {
            this.heading = heading;
            SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
            if (Position != null)
                Position.SetXYZH(null, null, null, heading);
        }
    }

    public int GetRotation()
    {
        int rotation = this.heading & 0xFF;
        return rotation * 3;
    }

    public void SetRotation(int rotation)
    {
        SetHeading(PositionUtil.ConvertAngleToHeading(rotation));
    }

    public PlaceLocation GetPlaceLocation()
    {
        return GetObjectTemplate().GetLocation();
    }

    public PlaceArea GetPlaceArea()
    {
        return GetObjectTemplate().GetArea();
    }

    public int GetPlacementLimit(bool trial)
    {
        LimitType limitType = GetObjectTemplate().GetPlacementLimit();
        if (trial)
            return limitType.GetTrialObjectPlaceLimit(registry.GetOwner().GetBuilding().GetSize());
        return limitType.GetObjectPlaceLimit(registry.GetOwner().GetBuilding().GetSize());
    }

    public ItemQuality GetQuality()
    {
        return GetObjectTemplate().GetQuality();
    }

    public float GetTalkingDistance()
    {
        return GetObjectTemplate().GetTalkingDistance();
    }

    public HousingCategory GetCategory()
    {
        return GetObjectTemplate().GetCategory();
    }

    public HouseRegistry GetRegistry()
    {
        return registry;
    }

    public Aion.GameServer.Model.House.House GetOwnerHouse()
    {
        return registry.GetOwner();
    }

    public int GetPlayerId()
    {
        return registry.GetOwner().GetOwnerId();
    }

    public int GetOwnerUsedCount()
    {
        return ownerUsedCount;
    }

    public void IncrementOwnerUsedCount()
    {
        this.ownerUsedCount++;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    public void IncrementVisitorUsedCount()
    {
        this.visitorUsedCount++;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
        Player owner = Aion.GameServer.World.World.GetInstance().GetPlayer(registry.GetOwner().GetOwnerId());
        // if owner is not online, we should save his items
        if (owner == null || !owner.IsOnline())
            registry.Save();
    }

    public void SetOwnerUsedCount(int ownerUsedCount)
    {
        if (this.ownerUsedCount != ownerUsedCount)
        {
            this.ownerUsedCount = ownerUsedCount;
            SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
        }
    }

    public int GetVisitorUsedCount()
    {
        return visitorUsedCount;
    }

    public void SetVisitorUsedCount(int visitorUsedCount)
    {
        if (this.visitorUsedCount != visitorUsedCount)
        {
            this.visitorUsedCount = visitorUsedCount;
            SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
        }
    }

    /// <summary>Means the player has it spawned, not the game server.</summary>
    public bool IsSpawnedByPlayer()
    {
        return x != 0 || y != 0 || z != 0;
    }

    public new PlaceableObjectController<T> GetController()
    {
        return (PlaceableObjectController<T>) base.GetController();
    }

    public virtual void Spawn()
    {
        if (!IsSpawnedByPlayer())
            return;
        if (Position == null || !IsSpawned())
        {
            Position = Aion.GameServer.World.World.GetInstance().CreatePosition(registry.GetOwner().GetWorldId(), x, y, z, heading, registry.GetOwner().GetInstanceId());
            Aion.GameServer.SpawnEngine.SpawnEngine.BringIntoWorld(this);
        }
        else
        {
            UpdateKnownlist();
        }
    }

    /// <summary>Removes house from spawn but it remains in registry.</summary>
    public void RemoveFromHouse()
    {
        GetController().Delete();
        x = y = z = heading = 0;
        Position = null;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    // Java parity: Expirable.canExpireNow() is a default method; C# default-interface methods aren't class-overridable,
    // so HouseObject declares it virtual (returns the interface default true) to let subclasses override (rule 8 diff).
    public virtual bool CanExpireNow()
    {
        return true;
    }

    public virtual void OnUse(Player player)
    {
    }

    public void OnDialogRequest(Player player)
    {
        OnUse(player);
    }

    public virtual void OnDespawn()
    {
    }

    public int? GetColor()
    {
        return color;
    }

    public void SetColor(int? color)
    {
        this.color = color;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    public int GetColorExpireEnd()
    {
        return colorExpireEnd;
    }

    public void SetColorExpireEnd(int colorExpireEnd)
    {
        this.colorExpireEnd = colorExpireEnd;
    }
}
