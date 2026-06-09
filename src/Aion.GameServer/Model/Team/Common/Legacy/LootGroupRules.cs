using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Model.Actions;
using Aion.GameServer.Model.Drop;
using Aion.GameServer.Model.Templates.Item;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Team.Common.Legacy;

/// <summary>
/// Java parity: model/team/common/legacy/LootGroupRules (ATracer, xTz).
/// Java ConcurrentLinkedDeque → LinkedList (closest Deque with contains/remove; concurrency is a foundational diff).
/// </summary>
public class LootGroupRules
{
    private readonly LootRuleType lootRule;
    private int misc;
    private readonly int commonItemAbove;
    private readonly int superiorItemAbove;
    private readonly int heroicItemAbove;
    private readonly int fabledItemAbove;
    private readonly int eternalItemAbove;
    private readonly int mythicItemAbove;
    private int nrMisc;
    private int nrRoundRobin;
    private readonly LinkedList<DropItem> itemsToBeDistributed = new LinkedList<DropItem>();

    public LootGroupRules()
    {
        lootRule = LootRuleType.ROUNDROBIN;
        commonItemAbove = 0;
        superiorItemAbove = 2;
        heroicItemAbove = 2;
        fabledItemAbove = 2;
        eternalItemAbove = 2;
        mythicItemAbove = 2;
    }

    public LootGroupRules(LootRuleType lootRule, int misc, int commonItemAbove, int superiorItemAbove, int heroicItemAbove, int fabledItemAbove,
        int eternalItemAbove, int mythicItemAbove)
    {
        this.lootRule = lootRule;
        this.misc = misc;
        this.commonItemAbove = commonItemAbove;
        this.superiorItemAbove = superiorItemAbove;
        this.heroicItemAbove = heroicItemAbove;
        this.fabledItemAbove = fabledItemAbove;
        this.eternalItemAbove = eternalItemAbove;
        this.mythicItemAbove = mythicItemAbove;
    }

    public bool GetQualityRule(ItemQuality quality)
    {
        return quality switch
        {
            ItemQuality.COMMON => commonItemAbove != 0, // White
            ItemQuality.RARE => superiorItemAbove != 0, // Green
            ItemQuality.LEGEND => heroicItemAbove != 0, // Blue
            ItemQuality.UNIQUE => fabledItemAbove != 0, // Yellow
            ItemQuality.EPIC => eternalItemAbove != 0, // Orange
            ItemQuality.MYTHIC => mythicItemAbove != 0, // Purple
            _ => false,
        };
    }

    public bool IsMisc(ItemQuality quality)
    {
        return quality.Equals(ItemQuality.JUNK) && misc == 1;
    }

    public LootRuleType GetLootRule()
    {
        return lootRule;
    }

    public int GetAutodistributionId()
    {
        bool isBid = mythicItemAbove == 3;
        bool isRoll = mythicItemAbove == 2;
        return isBid ? 3 : isRoll ? 2 : 0;
    }

    public int GetCommonItemAbove()
    {
        return commonItemAbove;
    }

    public int GetSuperiorItemAbove()
    {
        return superiorItemAbove;
    }

    public int GetHeroicItemAbove()
    {
        return heroicItemAbove;
    }

    public int GetFabledItemAbove()
    {
        return fabledItemAbove;
    }

    public int GetEternalItemAbove()
    {
        return eternalItemAbove;
    }

    public int GetMythicItemAbove()
    {
        return mythicItemAbove;
    }

    public int GetNrMisc()
    {
        return nrMisc;
    }

    public void SetNrMisc(int nrMisc)
    {
        this.nrMisc = nrMisc;
    }

    public void SetPlayersInRoll(ICollection<Aion.GameServer.Model.GameObjects.Player.Player> players, int time, int index, int npcId)
    {
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            foreach (Aion.GameServer.Model.GameObjects.Player.Player player in players)
            {
                if (player.IsInPlayerMode(PlayerMode.IN_ROLL))
                {
                    Aion.GameServer.Model.GameObjects.Player.InRoll inRoll = player.inRoll;
                    if (inRoll.GetIndex() == index && inRoll.GetNpcId() == npcId)
                        Aion.GameServer.Services.Drop.DropDistributionService.GetInstance().HandleRollOrBid(player, inRoll.GetRollType(), 0, 0, inRoll.GetItemId(), inRoll.GetNpcId(),
                            inRoll.GetIndex());
                }
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(time));
    }

    public int GetNrRoundRobin()
    {
        return nrRoundRobin;
    }

    public void SetNrRoundRobin(int nrRoundRobin)
    {
        this.nrRoundRobin = nrRoundRobin;
    }

    public int GetMisc()
    {
        return misc;
    }

    public void AddItemToBeDistributed(DropItem dropItem)
    {
        itemsToBeDistributed.AddLast(dropItem);
    }

    public bool ContainDropItem(DropItem dropItem)
    {
        return itemsToBeDistributed.Contains(dropItem);
    }

    public void RemoveItemToBeDistributed(DropItem dropItem)
    {
        itemsToBeDistributed.Remove(dropItem);
    }

    public LinkedList<DropItem> GetItemsToBeDistributed()
    {
        return itemsToBeDistributed;
    }
}
