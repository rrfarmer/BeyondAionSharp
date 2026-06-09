using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;

namespace Aion.GameServer.Ai;

/// <summary>Java parity: ai/AIRequest.</summary>
public abstract class AIRequest
{
    public abstract void AcceptRequest(Creature requester, Player responder, int requestId);

    public virtual void DenyRequest(Creature requester, Player responder)
    {
    }
}
