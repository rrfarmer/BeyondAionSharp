using Aion.GameServer.Model.Animations;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_ACTION_ANIMATION (ATracer). Plays an action animation on a target (id + optional level/objId). ActionAnimation red-tolerated.</summary>
public class SM_ACTION_ANIMATION : AionServerPacket
{
    private int targetObjectId;
    private ActionAnimation actionAnimation;
    private int levelOrObjectId;

    public SM_ACTION_ANIMATION(int targetObjectId, ActionAnimation actionAnimation)
        : this(targetObjectId, actionAnimation, 0)
    {
    }

    public SM_ACTION_ANIMATION(int targetObjectId, ActionAnimation actionAnimation, int levelOrObjectId)
    {
        this.targetObjectId = targetObjectId;
        this.actionAnimation = actionAnimation;
        this.levelOrObjectId = levelOrObjectId;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(targetObjectId);
        WriteH(actionAnimation.GetId());
        WriteD(levelOrObjectId);
    }
}
