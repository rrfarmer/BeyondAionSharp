using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_EMOTION (SoulKeeper, -Enomine-). Broadcasts a creature emotion/state change (fly/land/sit/loot/ride/resurrect/emote/speed/windstream...) with per-EmotionType payload. Converges PlayerReviveService. switch-arrow groups->stacked case labels; getTypeId()->GetTypeId(); writeF(int hex)->WriteF (int->float). EmotionType/Stat2/AionServerPacket red-tolerated.</summary>
public class SM_EMOTION : AionServerPacket
{
    private int senderObjectId;
    private EmotionType emotionType;
    private int emotion;
    private int targetObjectId;
    private float speed;
    private int state;
    private int baseAttackSpeed;
    private int currentAttackSpeed;
    private float x;
    private float y;
    private float z;
    private byte heading;

    public SM_EMOTION(Creature creature, EmotionType emotionType)
        : this(creature, emotionType, 0, 0)
    {
    }

    public SM_EMOTION(Creature creature, EmotionType emotionType, int emotion, int targetObjectId)
    {
        this.senderObjectId = creature.GetObjectId();
        this.emotionType = emotionType;
        this.emotion = emotion;
        this.targetObjectId = targetObjectId;
        this.state = creature.GetState();
        Stat2 aSpeed = creature.GetGameStats().GetAttackSpeed();
        this.baseAttackSpeed = aSpeed.GetBase();
        this.currentAttackSpeed = aSpeed.GetCurrent();
        this.speed = creature.GetGameStats().GetMovementSpeedFloat();
    }

    public SM_EMOTION(int Objid, EmotionType emotionType, int state)
    {
        this.senderObjectId = Objid;
        this.emotionType = emotionType;
        this.state = state;
    }

    public SM_EMOTION(Player player, EmotionType emotionType, int emotion, float x, float y, float z, byte heading, int targetObjectId)
    {
        this.senderObjectId = player.GetObjectId();
        this.emotionType = emotionType;
        this.emotion = emotion;
        this.x = x;
        this.y = y;
        this.z = z;
        this.heading = heading;
        this.targetObjectId = targetObjectId;

        this.state = player.GetState();
        this.speed = player.GetGameStats().GetMovementSpeedFloat();
        Stat2 aSpeed = player.GetGameStats().GetAttackSpeed();
        this.baseAttackSpeed = aSpeed.GetBase();
        this.currentAttackSpeed = aSpeed.GetCurrent();
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(senderObjectId);
        WriteC(emotionType.GetTypeId());
        WriteH(state);
        WriteF(speed);
        switch (emotionType)
        {
            case EmotionType.LAND_FLYTELEPORT: // fly teleport (land)
            case EmotionType.FLY: // toggle flight mode
            case EmotionType.LAND: // toggle land mode
            case EmotionType.SELECT_TARGET: // select target
            case EmotionType.JUMP:
            case EmotionType.SIT: // sit
            case EmotionType.STAND: // stand
            case EmotionType.ATTACKMODE_IN_MOVE: // toggle attack mode
            case EmotionType.NEUTRALMODE_IN_MOVE: // toggle normal mode
            case EmotionType.WALK: // toggle walk
            case EmotionType.RUN: // toggle run
            case EmotionType.OPEN_PRIVATESHOP: // private shop open
            case EmotionType.CLOSE_PRIVATESHOP: // private shop close
            case EmotionType.POWERSHARD_ON: // powershard on
            case EmotionType.POWERSHARD_OFF: // powershard off
            case EmotionType.ATTACKMODE_IN_STANDING: // toggle attack mode
            case EmotionType.NEUTRALMODE_IN_STANDING: // toggle normal mode
            case EmotionType.START_FEEDING:
            case EmotionType.END_FEEDING:
            case EmotionType.WINDSTREAM_START_BOOST:
            case EmotionType.WINDSTREAM_END_BOOST:
            case EmotionType.WINDSTREAM_END:
            case EmotionType.WINDSTREAM_EXIT:
            case EmotionType.OPEN_DOOR:
            case EmotionType.CLOSE_DOOR:
            case EmotionType.WINDSTREAM_STRAFE:
            case EmotionType.STOP_GLIDE:
            case EmotionType.STOP_FLY:
                break;
            case EmotionType.DIE: // die
            case EmotionType.START_LOOT: // looting start
            case EmotionType.END_LOOT: // looting end
            case EmotionType.START_QUESTLOOT: // looting start (quest)
            case EmotionType.END_QUESTLOOT: // looting end (quest)
                WriteD(targetObjectId);
                break;
            case EmotionType.CHAIR_SIT: // sit (chair)
            case EmotionType.CHAIR_UP: // stand (chair)
                WriteF(x);
                WriteF(y);
                WriteF(z);
                WriteC(heading);
                break;
            case EmotionType.START_FLYTELEPORT:
                // fly teleport (start)
                WriteD(emotion); // teleport Id
                break;
            case EmotionType.WINDSTREAM:
                // entering windstream
                WriteD(emotion); // teleport Id
                WriteD(targetObjectId); // distance
                break;
            case EmotionType.RIDE:
            case EmotionType.RIDE_END:
                if (targetObjectId != 0)
                {
                    WriteD(targetObjectId);// rideId
                }
                WriteF(0x3F);// unk
                WriteF(0x3F);// unk
                WriteF(0x40);// unk
                break;
            case EmotionType.RESURRECT:
                // resurrect
                WriteD(0);
                break;
            case EmotionType.EMOTE:
                // emote
                WriteD(targetObjectId);
                WriteH(emotion);
                WriteC(1);
                break;
            case EmotionType.CHANGE_SPEED:
                // emote startloop
                WriteH(baseAttackSpeed);
                WriteH(currentAttackSpeed);
                WriteC(0);// new 4.0
                break;
            default:
                if (targetObjectId != 0)
                {
                    WriteD(targetObjectId);
                }
                break;
        }
    }
}
