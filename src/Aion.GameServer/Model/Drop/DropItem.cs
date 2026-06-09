using System.Collections.Generic;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Model.Drop;

/// <summary>Java parity: model/drop/DropItem (ATracer).</summary>
public class DropItem
{
    private int index = 0;
    private long count = 0;
    private readonly Drop dropTemplate;
    private List<int> playerObjIds;
    private bool isFreeForAll = false;
    private long highestValue = 0;
    private Aion.GameServer.Model.GameObjects.Player.Player winningPlayer = null;
    private bool isItemWonNotCollected = false;
    private bool isDistributeItem = false;
    private int npcObj;
    private int optionalSocket = 0;

    public DropItem(Drop dropTemplate)
    {
        this.dropTemplate = dropTemplate;
        this.playerObjIds = new List<int>();
        if (DataManager.ITEM_DATA.GetItemTemplate(dropTemplate.GetItemId()).GetOptionSlotBonus() != 0)
            optionalSocket = -1;
    }

    /// <summary>Regenerates item count upon each call.</summary>
    public void CalculateCount()
    {
        count = Aion.Commons.Utils.Rnd.Get(dropTemplate.GetMinAmount(), dropTemplate.GetMaxAmount());
    }

    public int GetIndex()
    {
        return index;
    }

    public void SetIndex(int index)
    {
        this.index = index;
    }

    public long GetCount()
    {
        return count;
    }

    public void SetCount(long count)
    {
        this.count = count;
    }

    public Drop GetDropTemplate()
    {
        return dropTemplate;
    }

    public List<int> GetPlayerObjIds()
    {
        return playerObjIds;
    }

    public bool CanViewDropItem(int objId)
    {
        return playerObjIds.Count == 0 || playerObjIds.Contains(objId);
    }

    public void SetPlayerObjId(int playerObjId)
    {
        if (playerObjId > 0 && !playerObjIds.Contains(playerObjId))
            this.playerObjIds.Add(playerObjId);
    }

    public void IsFreeForAll(bool isFreeForAll)
    {
        this.isFreeForAll = isFreeForAll;
    }

    public bool IsFreeForAll()
    {
        return isFreeForAll;
    }

    public long GetHighestValue()
    {
        return highestValue;
    }

    public void SetHighestValue(long highestValue)
    {
        this.highestValue = highestValue;
    }

    public void SetWinningPlayer(Aion.GameServer.Model.GameObjects.Player.Player winningPlayer)
    {
        this.winningPlayer = winningPlayer;
    }

    public Aion.GameServer.Model.GameObjects.Player.Player GetWinningPlayer()
    {
        if (winningPlayer != null)
        {
            if (winningPlayer.IsOnline())
            {
                return winningPlayer;
            }
            else
            {
                Aion.GameServer.Model.GameObjects.Player.Player player = Aion.GameServer.World.World.GetInstance().GetPlayer(winningPlayer.GetObjectId());
                if (player != null)
                {
                    return player;
                }
                else
                {
                    return winningPlayer;
                }
            }
        }
        return winningPlayer;
    }

    public void IsItemWonNotCollected(bool isItemWonNotCollected)
    {
        this.isItemWonNotCollected = isItemWonNotCollected;
    }

    public bool IsItemWonNotCollected()
    {
        return isItemWonNotCollected;
    }

    public void IsDistributeItem(bool isDistributeItem)
    {
        this.isDistributeItem = isDistributeItem;
    }

    public bool IsDistributeItem()
    {
        return isDistributeItem;
    }

    public int GetNpcObj()
    {
        return npcObj;
    }

    public void SetNpcObj(int npcObj)
    {
        this.npcObj = npcObj;
    }

    public int GetOptionalSocket()
    {
        return optionalSocket;
    }

    public bool IsOnlyPossibleLooter(Aion.GameServer.Model.GameObjects.Player.Player player)
    {
        if (playerObjIds.Count != 1)
            return false;
        return playerObjIds.Contains(player.GetObjectId());
    }

    public int GetLootEffectId()
    {
        return dropTemplate.GetItemId() switch
        {
            166020000 or 166020001 or 166020002 or 166020003 => 1003, // Omega Enchantment Stone
            168000034 or 168000035 or 168000073 or 168000074 or 168000117 or 168000118 or 168000120 or 168000121 or 168000161 or 168000162 or 168000164 or 168000165
                or 168000213 or 168000216 or 168000223 or 168000228 or 168000230 or 168000233 or 168000240 or 168000245 => 1003, // Godstones
            188053083 => 1003, // Tempering Solution Chest
            188053547 or 188053548 or 188053646 or 188053647 => 1002, // Nether Dragon King weapon boxes
            190100004 or 190100052 => 1003, // Mounts
            _ => 0,
        };
    }
}
