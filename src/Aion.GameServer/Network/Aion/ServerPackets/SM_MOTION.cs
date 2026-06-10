using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Player.Motion;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_MOTION (MrPoke). Motion list (action 1), add (2), set (5), remove (6), and a player's 5 active motion slots (7). Converges PlayerEnterWorldService. Collection->ICollection; Map->Dictionary; map.get->GetValueOrDefault; switch-on-action. Motion/AionServerPacket red-tolerated.</summary>
public class SM_MOTION : AionServerPacket
{
    byte action;
    short motionId;
    int remainingTime;

    int playerId;
    Dictionary<int, Motion> activeMotions;

    ICollection<Motion> motions;

    byte type;

    public SM_MOTION(ICollection<Motion> motions)
    {
        this.action = 1;
        this.motions = motions;
    }

    public SM_MOTION(short motionId, int remainingTime)
    {
        this.action = 2;
        this.motionId = motionId;
        this.remainingTime = remainingTime;
    }

    public SM_MOTION(short motionId, byte type)
    {
        this.action = 5;
        this.motionId = motionId;
        this.type = type;
    }

    public SM_MOTION(short motionId)
    {
        this.action = 6;
        this.motionId = motionId;
    }

    public SM_MOTION(int playerId, Dictionary<int, Motion> activeMotions)
    {
        this.action = 7;
        this.playerId = playerId;
        this.activeMotions = activeMotions;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(action);
        switch (action)
        {
            case 1:
                WriteH(motions.Count);
                foreach (Motion motion in motions)
                {
                    WriteH(motion.GetId());
                    WriteD(motion.SecondsUntilExpiration());
                    WriteC(motion.IsActive() ? 1 : 0);
                }
                break;
            case 2: // Add motion
                WriteH(motionId);
                WriteD(remainingTime);
                break;
            case 5: // Set motion
                WriteH(motionId);
                WriteC(type);
                break;
            case 6: // remove
                WriteH(motionId);
                break;
            case 7: // Player motions
                WriteD(playerId);
                for (int i = 1; i < 6; i++)
                {
                    Motion motion = activeMotions.GetValueOrDefault(i);
                    if (motion == null)
                        WriteH(0);
                    else
                        WriteH(motion.GetId());
                }
                break;
        }
    }
}
