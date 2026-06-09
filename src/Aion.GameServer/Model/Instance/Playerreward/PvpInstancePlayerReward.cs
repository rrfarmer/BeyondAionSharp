using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Player;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Model.Instance.Playerreward;

/// <summary>Java parity: model/instance/playerreward/PvpInstancePlayerReward.</summary>
public class PvpInstancePlayerReward : InstancePlayerReward
{
    private static readonly ILogger log = NullLogger.Instance;

    private readonly Race race;
    private readonly AtomicInteger capturedZones = new AtomicInteger();
    private int[] reward1;
    private int[] reward2;
    private int[] reward3;
    private int[] reward4;
    private int[] bonusReward;
    private int baseAp;
    private int bonusAp;
    private int baseGp;
    private int bonusGp;

    public PvpInstancePlayerReward(int objectId, Race race)
        : base(objectId)
    {
        this.race = race;
    }

    public int GetReward1ItemId()
    {
        return reward1 == null ? 0 : reward1[0];
    }

    public int GetReward1Count()
    {
        return reward1 == null ? 0 : reward1[1];
    }

    public int GetReward1BonusCount()
    {
        return reward1 == null ? 0 : reward1[2];
    }

    public void SetReward1(int itemId, int count, int bonusCount)
    {
        reward1 = new int[] { itemId, count, bonusCount };
    }

    public int GetReward2ItemId()
    {
        return reward2 == null ? 0 : reward2[0];
    }

    public int GetReward2Count()
    {
        return reward2 == null ? 0 : reward2[1];
    }

    public int GetReward2BonusCount()
    {
        return reward2 == null ? 0 : reward2[2];
    }

    public void SetReward2(int itemId, int count, int bonusCount)
    {
        reward2 = new int[] { itemId, count, bonusCount };
    }

    public int GetReward3ItemId()
    {
        return reward3 == null ? 0 : reward3[0];
    }

    public int GetReward3Count()
    {
        return reward3 == null ? 0 : reward3[1];
    }

    public void SetReward3(int itemId, int count)
    {
        reward3 = new int[] { itemId, count };
    }

    public int GetReward4ItemId()
    {
        return reward4 == null ? 0 : reward4[0];
    }

    public int GetReward4Count()
    {
        return reward4 == null ? 0 : reward4[1];
    }

    public void SetReward4(int itemId, int count)
    {
        reward4 = new int[] { itemId, count };
    }

    public int GetBonusRewardItemId()
    {
        return bonusReward == null ? 0 : bonusReward[0];
    }

    public int GetBonusRewardCount()
    {
        return bonusReward == null ? 0 : bonusReward[1];
    }

    public void SetBonusReward(int itemId, int count)
    {
        bonusReward = new int[] { itemId, count };
    }

    public int GetBaseAp()
    {
        return baseAp;
    }

    public void SetBaseAp(int baseAp)
    {
        this.baseAp = baseAp;
    }

    public int GetBonusAp()
    {
        return bonusAp;
    }

    public void SetBonusAp(int bonusAp)
    {
        this.bonusAp = bonusAp;
    }

    public int GetBaseGp()
    {
        return baseGp;
    }

    public void SetBaseGp(int baseGp)
    {
        this.baseGp = baseGp;
    }

    public int GetBonusGp()
    {
        return bonusGp;
    }

    public void SetBonusGp(int bonusGp)
    {
        this.bonusGp = bonusGp;
    }

    public void IncrementCapturedZones()
    {
        capturedZones.IncrementAndGet();
    }

    public int GetCapturedZones()
    {
        return capturedZones.Get();
    }

    public Race GetRace()
    {
        return race;
    }

    public int GetMythicKunaxEquipment(Player player)
    {
        int[] weapons;
        int[] armor;
        switch (player.GetPlayerClass()) // armor: Chest, Gloves, Shoulders, Pants, Shoes, Helmet
        {
            case PlayerClass.TEMPLAR:
                weapons = new int[] { 100901305, 100001682, 115001702 }; // Greatsword, Sword, Shield
                armor = new int[] { 110601549, 111601512, 112601494, 113601495, 114601502, 125003995 };
                break;
            case PlayerClass.GLADIATOR:
                weapons = new int[] { 100901305, 101301215, 101701319 }; // Greatsword, Polearm, Bow
                armor = new int[] { 110601549, 111601512, 112601494, 113601495, 114601502, 125003995 };
                break;
            case PlayerClass.RANGER:
                weapons = new int[] { 101701319 }; // Bow
                armor = new int[] { 110301751, 111301689, 112301628, 113301720, 114301757, 125003997 };
                break;
            case PlayerClass.ASSASSIN:
                weapons = new int[] { 100001682, 100201455, 101701319 }; // Sword, Dagger, Bow
                armor = new int[] { 110301751, 111301689, 112301628, 113301720, 114301757, 125003997 };
                break;
            case PlayerClass.GUNNER:
                weapons = new int[] { 101801170, 101801170, 101901081 }; // Pistol, Pistol, Cannon
                armor = new int[] { 110301751, 111301689, 112301628, 113301720, 114301757, 125003997 };
                break;
            case PlayerClass.SORCERER:
                weapons = new int[] { 100601378, 100601378, 100501268 }; // Tome, Tome, Orb
                armor = new int[] { 110101754, 111101579, 112101529, 113101591, 114101625, 125003998 };
                break;
            case PlayerClass.SPIRIT_MASTER:
                weapons = new int[] { 100601378, 100501268, 100501268 }; // Tome, Orb, Orb
                armor = new int[] { 110101754, 111101579, 112101529, 113101591, 114101625, 125003998 };
                break;
            case PlayerClass.BARD:
                weapons = new int[] { 102001197 };
                armor = new int[] { 110101754, 111101579, 112101529, 113101591, 114101625, 125003998 };
                break;
            case PlayerClass.CLERIC:
                weapons = new int[] { 101501304, 100101281, 115001702 }; // Staff, Mace, Shield
                armor = new int[] { 110551084, 111501643, 112501582, 113501661, 114501671, 125003996 };
                break;
            case PlayerClass.CHANTER:
                weapons = new int[] { 101501304, 101501304, 100101281, 115001702 }; // Staff, Staff, Mace, Shield
                armor = new int[] { 110551084, 111501643, 112501582, 113501661, 114501671, 125003996 };
                break;
            case PlayerClass.RIDER:
                weapons = new int[] { 102100969 };
                armor = new int[] { 110551084, 111501643, 112501582, 113501661, 114501671, 125003996 };
                break;
            default:
                log.LogWarning("Couldn't get mythic Kunax equipment for " + player + ". Rewards for " + player.GetPlayerClass() + " are not implemented");
                return 0;
        }
        int[] possibleRewards = Rnd.Chance() < 17.5f ? weapons : armor;
        return Rnd.Get(possibleRewards);
    }
}
