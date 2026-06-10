using System.Collections.Concurrent;

namespace Aion.GameServer.Model.GameObjects.Player;

/// <summary>
/// Manages the asking of and responding to SM_QUESTION_WINDOW (Java parity: model/gameobjects/player/ResponseRequester, Ben).
/// Map<Integer, RequestResponseHandler&lt;? extends Creature&gt;>→ConcurrentDictionary&lt;int, RequestResponseHandler&gt; (non-generic
/// base represents the Java wildcard); putIfAbsent==null→TryAdd; remove→TryRemove.
/// </summary>
public class ResponseRequester
{
    private readonly Player player;
    private readonly ConcurrentDictionary<int, RequestResponseHandler> activeRequests = new();

    public ResponseRequester(Player player)
    {
        this.player = player;
    }

    /// <summary>Attempts to register a handler for the given messageId.</summary>
    /// <param name="messageId">id of the request message</param>
    /// <returns>true if the request was added, false otherwise</returns>
    public bool PutRequest(int messageId, RequestResponseHandler handler)
    {
        if (handler == null)
            return false;
        return activeRequests.TryAdd(messageId, handler);
    }

    /// <summary>Responds to the given messageId with the given responseCode.</summary>
    /// <param name="messageId">id of the message to respond to</param>
    /// <returns>true, if there was a request that handled the response, false otherwise</returns>
    public bool Respond(int messageId, int responseCode)
    {
        if (activeRequests.TryRemove(messageId, out RequestResponseHandler handler))
        {
            handler.Handle(player, responseCode);
            return true;
        }
        return false;
    }

    /// <summary>Automatically responds 0 to all requests, passing the given player as the responder.</summary>
    public void DenyAll()
    {
        foreach (RequestResponseHandler handler in activeRequests.Values)
            handler.Handle(player, 0);
        activeRequests.Clear();
    }

    /// <summary>Removes the given response handler, so a response by the player won't work anymore.</summary>
    /// <returns>boolean indicating if a request was removed</returns>
    public bool Remove(int messageId)
    {
        return activeRequests.TryRemove(messageId, out _);
    }
}
