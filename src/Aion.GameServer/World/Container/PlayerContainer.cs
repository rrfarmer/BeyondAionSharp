using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.World.Exceptions;

namespace Aion.GameServer.World.Container;

/// <summary>Java parity: world/container/PlayerContainer (-Nemesiss-, Neon). Stores players by objectId and name. ConcurrentHashMap -> ConcurrentDictionary; Iterable -> IEnumerable. DuplicateAionObjectException red-tolerated.</summary>
public class PlayerContainer : IEnumerable<Player>
{
    private readonly ConcurrentDictionary<int, Player> playersById = new ConcurrentDictionary<int, Player>();
    private readonly ConcurrentDictionary<string, Player> playersByName = new ConcurrentDictionary<string, Player>();

    public void Add(Player player)
    {
        playersById.TryGetValue(player.GetObjectId(), out Player prevById);
        playersById[player.GetObjectId()] = player;
        if (prevById != null)
            throw new DuplicateAionObjectException(player, playersById[player.GetObjectId()]);
        playersByName.TryGetValue(player.GetName(), out Player prevByName);
        playersByName[player.GetName()] = player;
        if (prevByName != null)
            throw new DuplicateAionObjectException(player, playersByName[player.GetName()]);
    }

    public void Remove(Player player)
    {
        playersById.TryRemove(player.GetObjectId(), out _);
        playersByName.TryRemove(player.GetName(), out _);
    }

    public Player Get(int objectId)
    {
        return playersById.GetValueOrDefault(objectId);
    }

    public Player Get(string name)
    {
        return playersByName.GetValueOrDefault(name);
    }

    public IEnumerator<Player> GetEnumerator()
    {
        return playersById.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public ICollection<Player> GetAllPlayers()
    {
        return playersById.Values.Where(p => p != null).ToList(); // ensure there are no null values (due to concurrent object removal)
    }

    public void UpdateCachedPlayerName(string oldName, Player player)
    {
        playersByName.TryGetValue(oldName, out Player p);
        playersByName[player.GetName()] = player;
        if (player.Equals(p))
            playersByName.TryRemove(oldName, out _);
    }
}
