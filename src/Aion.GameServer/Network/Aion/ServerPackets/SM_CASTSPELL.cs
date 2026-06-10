using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_CASTSPELL (alexa026, rhys2002). Shows a casting-spell animation (effector, spell/level, target by type: object id, ground point, or ground point + 8 unks), cast duration/speed, animation-boost flag. switch-on-targetType (0/3/4, 1, 2). AionServerPacket/Creature/write* red-tolerated.</summary>
public class SM_CASTSPELL : AionServerPacket
{
    private readonly Creature effector;
    private readonly int spellId;
    private readonly int level;
    private readonly int targetType;
    private readonly int targetObjectId;
    private readonly int castDuration;
    private readonly float castSpeed;
    private readonly bool allowAnimationBoostByCastSpeed;

    private float x;
    private float y;
    private float z;

    public SM_CASTSPELL(Creature effector, int spellId, int level, int targetType, int targetObjectId, int castDuration, float castSpeed, bool allowAnimationBoostByCastSpeed)
    {
        this.effector = effector;
        this.spellId = spellId;
        this.level = level;
        this.targetType = targetType;
        this.targetObjectId = targetObjectId;
        this.castDuration = castDuration;
        this.castSpeed = castSpeed;
        this.allowAnimationBoostByCastSpeed = allowAnimationBoostByCastSpeed;
    }

    public SM_CASTSPELL(Creature effector, int spellId, int level, int targetType, float x, float y, float z, int castDuration, float castSpeed, bool allowAnimationBoostByCastSpeed)
        : this(effector, spellId, level, targetType, 0, castDuration, castSpeed, allowAnimationBoostByCastSpeed)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(effector.GetObjectId());
        WriteH(spellId);
        WriteC(level);
        WriteC(targetType);
        switch (targetType)
        {
            case 0:
            case 3:
            case 4:
                WriteD(targetObjectId);
                break;
            case 1:
                WriteF(x);
                WriteF(y);
                WriteF(z);
                break;
            case 2:
                WriteF(x);
                WriteF(y);
                WriteF(z);
                WriteD(0);// unk1
                WriteD(0);// unk2
                WriteD(0);// unk3
                WriteD(0);// unk4
                WriteD(0);// unk5
                WriteD(0);// unk6
                WriteD(0);// unk7
                WriteD(0);// unk8
                break;
        }
        WriteH(castDuration);
        WriteC(0x00);// unk
        WriteF(castSpeed);
        WriteC(allowAnimationBoostByCastSpeed ? 1 : 0); // affects animation time of the next skill based on castSpeed (valid range: 0.5f - 1f)
    }
}
