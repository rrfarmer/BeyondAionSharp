using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.GameObjects.Player;

/// <summary>
/// Implemented by handlers of CM_QUESTION_RESPONSE responses.
/// Java parity: model/gameobjects/player/RequestResponseHandler&lt;T extends Creature&gt;.
/// </summary>
public abstract class RequestResponseHandler<T> where T : Creature
{
    private readonly T requester;

    public RequestResponseHandler(T requester)
    {
        this.requester = requester;
    }

    /// <summary>Called when a response is received (0 = no, 1 = yes).</summary>
    public void Handle(Player responder, int response)
    {
        if (response == 0)
            DenyRequest(requester, responder);
        else
            AcceptRequest(requester, responder);
    }

    /// <summary>Called when the player accepts a request.</summary>
    public abstract void AcceptRequest(T requester, Player responder);

    /// <summary>Called when the player denies a request.</summary>
    public virtual void DenyRequest(T requester, Player responder)
    {
    }
}
