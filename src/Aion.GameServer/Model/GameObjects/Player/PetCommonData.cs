using System;
using System.Threading.Tasks;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>Java parity: model/gameobjects/player/PetCommonData implements Expirable.</summary>
public class PetCommonData : IExpirable
{
    private readonly int objectId;
    private readonly int templateId;
    private readonly int masterObjectId;
    private int decoration;
    private string name;
    private DateTime? birthday;
    internal Aion.GameServer.Services.ToyPet.PetFeedProgress feedProgress = null;
    internal Aion.GameServer.Model.Templates.Pet.PetDopingBag dopingBag = null;
    private volatile bool cancelFeed = false;
    private long refeedTime;
    private long startMoodTime;
    private int shuggleCounter;
    private int lastSentPoints;
    private long moodCdStarted;
    private long giftCdStarted;
    private int expireTime;
    private DateTime? despawnTime;
    private bool isLooting = false;
    private bool isSelling = false;
    private volatile Aion.GameServer.Utils.ScheduledTask refeedTask;

    public PetCommonData(int objectId, int templateId, int masterObjectId, int expireTime)
    {
        this.objectId = objectId;
        this.templateId = templateId;
        this.masterObjectId = masterObjectId;
        this.expireTime = expireTime;
        Aion.GameServer.Model.Templates.Pet.PetTemplate template = Aion.GameServer.Dataholders.DataManager.PET_DATA.GetPetTemplate(templateId);
        if (template.ContainsFunction(Aion.GameServer.Model.Templates.Pet.PetFunctionType.FOOD))
        {
            int flavourId = template.GetPetFunction(Aion.GameServer.Model.Templates.Pet.PetFunctionType.FOOD).GetId();
            int lovedLimit = Aion.GameServer.Dataholders.DataManager.PET_FEED_DATA.GetFlavourById(flavourId).GetLovedFoodLimit();
            feedProgress = new Aion.GameServer.Services.ToyPet.PetFeedProgress((byte)(lovedLimit & 0xFF));
        }
        if (template.ContainsFunction(Aion.GameServer.Model.Templates.Pet.PetFunctionType.DOPING))
        {
            dopingBag = new Aion.GameServer.Model.Templates.Pet.PetDopingBag();
        }
    }

    public int GetObjectId()
    {
        return objectId;
    }

    public int GetMasterObjectId()
    {
        return masterObjectId;
    }

    public int GetDecoration()
    {
        return decoration;
    }

    public void SetDecoration(int decoration)
    {
        this.decoration = decoration;
    }

    public string GetName()
    {
        return name;
    }

    public void SetName(string name)
    {
        this.name = name;
    }

    public int GetTemplateId()
    {
        return templateId;
    }

    public int GetBirthday()
    {
        if (birthday == null)
            return 0;

        return (int)(ToMillis(birthday.Value) / 1000);
    }

    public DateTime? GetBirthdayTimestamp()
    {
        return birthday;
    }

    public void SetBirthday(DateTime? birthday)
    {
        this.birthday = birthday;
    }

    public long GetRefeedTime()
    {
        return refeedTime;
    }

    public void SetRefeedTime(long curentTime)
    {
        this.refeedTime = curentTime;
    }

    public bool GetCancelFeed()
    {
        return cancelFeed;
    }

    public void SetCancelFeed(bool cancelFeed)
    {
        this.cancelFeed = cancelFeed;
    }

    public void ScheduleRefeed(long reFoodTime)
    {
        CancelRefeedTask();
        refeedTask = Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            refeedTime = 0;
            feedProgress.SetHungryLevel(Aion.GameServer.Services.ToyPet.PetHungryLevel.HUNGRY);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(reFoodTime));
    }

    public void CancelRefeedTask()
    {
        if (refeedTask != null)
            refeedTask.Cancel();
    }

    public long GetRefeedDelay()
    {
        long time = refeedTime - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (time < 0)
        {
            refeedTime = 0;
            time = 0;
        }

        return time;
    }

    public long GetMoodStartTime()
    {
        return startMoodTime;
    }

    public int GetShuggleCounter()
    {
        return shuggleCounter;
    }

    public void SetShuggleCounter(int shuggleCounter)
    {
        this.shuggleCounter = shuggleCounter;
    }

    public int GetMoodPoints(bool forPacket)
    {
        if (startMoodTime == 0)
            startMoodTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        int points = (int)Math.Floor((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startMoodTime) / 1000f + 0.5f) + shuggleCounter * 1000;
        if (forPacket && points > 9000)
            return 9000;
        return points;
    }

    public int GetLastSentPoints()
    {
        return lastSentPoints;
    }

    public void SetLastSentPoints(int points)
    {
        lastSentPoints = points;
    }

    public bool IncreaseShuggleCounter()
    {
        if (GetMoodRemainingTime() > 0)
            return false;
        this.moodCdStarted = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        this.shuggleCounter++;
        return true;
    }

    public void ClearMoodStatistics()
    {
        this.startMoodTime = 0;
        this.shuggleCounter = 0;
    }

    public void SetStartMoodTime(long startMoodTime)
    {
        this.startMoodTime = startMoodTime;
    }

    /// <summary>moodCdStarted</summary>
    public long GetMoodCdStarted()
    {
        return moodCdStarted;
    }

    /// <param name="moodCdStarted">the moodCdStarted to set</param>
    public void SetMoodCdStarted(long moodCdStarted)
    {
        this.moodCdStarted = moodCdStarted;
    }

    public int GetMoodRemainingTime()
    {
        long stop = moodCdStarted + 600000;
        long remains = stop - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (remains <= 0)
        {
            SetMoodCdStarted(0);
            return 0;
        }
        return (int)(remains / 1000);
    }

    /// <summary>the giftCdStarted</summary>
    public long GetGiftCdStarted()
    {
        return giftCdStarted;
    }

    /// <param name="giftCdStarted">the giftCdStarted to set</param>
    public void SetGiftCdStarted(long giftCdStarted)
    {
        this.giftCdStarted = giftCdStarted;
    }

    public int GetGiftRemainingTime()
    {
        long stop = giftCdStarted + 3600 * 1000;
        long remains = stop - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (remains <= 0)
        {
            SetGiftCdStarted(0);
            return 0;
        }
        return (int)(remains / 1000);
    }

    /// <summary>the despawnTime</summary>
    public DateTime? GetDespawnTime()
    {
        return despawnTime;
    }

    /// <param name="despawnTime">the despawnTime to set</param>
    public void SetDespawnTime(DateTime? despawnTime)
    {
        this.despawnTime = despawnTime;
    }

    /// <summary>feedProgress, null if pet has no feed function</summary>
    public Aion.GameServer.Services.ToyPet.PetFeedProgress GetFeedProgress()
    {
        return feedProgress;
    }

    public void SetIsLooting(bool isLooting)
    {
        this.isLooting = isLooting;
    }

    public bool IsLooting()
    {
        return this.isLooting;
    }

    public bool IsSelling()
    {
        return isSelling;
    }

    public void SetIsSelling(bool selling)
    {
        isSelling = selling;
    }

    public Aion.GameServer.Model.Templates.Pet.PetDopingBag GetDopingBag()
    {
        return dopingBag;
    }

    public int GetExpireTime()
    {
        return expireTime;
    }

    public void OnExpire(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_PET_ABANDON_EXPIRE_TIME_COMPLETE(name));
        Aion.GameServer.Services.ToyPet.PetAdoptionService.SurrenderPet(player, templateId);
    }

    // Java parity: java.sql.Timestamp.getTime() returns epoch millis.
    private static long ToMillis(DateTime dt)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
    }
}
