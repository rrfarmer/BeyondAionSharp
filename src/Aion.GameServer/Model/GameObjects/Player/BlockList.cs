using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>
/// Represents a player's list of blocked users. Blocks via a player's CommonData.
/// Java parity: model/gameobjects/player/BlockList implements Iterable&lt;BlockedPlayer&gt;.
/// </summary>
public class BlockList : IEnumerable<BlockedPlayer>
{
    /// <summary>The maximum number of users a block list can contain</summary>
    public const int MAX_BLOCKS = 100;

    // Indexes blocked players by their player ID
    private readonly ConcurrentDictionary<int, BlockedPlayer> blockedList;

    /// <summary>Constructs a new (empty) blocked list</summary>
    public BlockList()
    {
        this.blockedList = new ConcurrentDictionary<int, BlockedPlayer>();
    }

    /// <summary>Constructs a new blocked list with the given initial items</summary>
    /// <param name="initialList">A map of blocked players indexed by their object IDs</param>
    public BlockList(IDictionary<int, BlockedPlayer> initialList)
    {
        this.blockedList = new ConcurrentDictionary<int, BlockedPlayer>(initialList);
    }

    /// <summary>Adds a player to the blocked users list. Does not send packets or update the database.</summary>
    public void Add(BlockedPlayer plr)
    {
        blockedList[plr.GetObjId()] = plr;
    }

    /// <summary>Removes a player from the blocked users list. Does not send packets or update the database.</summary>
    public void Remove(int objIdOfPlayer)
    {
        blockedList.TryRemove(objIdOfPlayer, out _);
    }

    /// <summary>Returns the blocked player with this name if they exist, null if not blocked.</summary>
    public BlockedPlayer GetBlockedPlayer(string name)
    {
        foreach (BlockedPlayer entry in blockedList.Values)
        {
            if (entry.GetName().Equals(name, StringComparison.OrdinalIgnoreCase))
                return entry;
        }
        return null;
    }

    public BlockedPlayer GetBlockedPlayer(int playerObjId)
    {
        return blockedList.TryGetValue(playerObjId, out BlockedPlayer bp) ? bp : null;
    }

    public bool Contains(int playerObjectId)
    {
        return blockedList.ContainsKey(playerObjectId);
    }

    /// <summary>Returns the number of blocked players in this list</summary>
    public int GetSize()
    {
        return blockedList.Count;
    }

    public bool IsFull()
    {
        return GetSize() >= MAX_BLOCKS;
    }

    public IEnumerator<BlockedPlayer> GetEnumerator()
    {
        return blockedList.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
