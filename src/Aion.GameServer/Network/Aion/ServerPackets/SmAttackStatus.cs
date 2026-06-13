using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>
/// Java parity: network/aion/serverpackets/SM_ATTACK_STATUS. Faithful 1:1 rewrite replacing the divergent
/// SmAttackStatusType/SmAttackStatusLog port (rule 8). Class name kept PascalCase per the C# packet convention.
/// </summary>
/// <remarks>
/// <see cref="TYPE"/> is modeled as a Java-style class-enum (static instances) because Java's TYPE has multiple
/// constants sharing the same id in DIFFERENT write branches (e.g. DAMAGE_MP and ABSORBED_MP are both 20 but one
/// writes -value, the other +value). A plain C# enum would collapse these aliases and cannot distinguish them in a
/// switch. <see cref="LOG"/> has no duplicate ids, so it stays a plain enum.
/// </remarks>
public sealed class SmAttackStatus : GameServerPacket
{
    public const int PacketOpCode = 5;

    private readonly Creature creature;
    private readonly TYPE type;
    private readonly int skillId;
    private readonly int value;
    private readonly int logId;

    public SmAttackStatus(Creature creature, TYPE type, int skillId, int value, LOG log)
        : base(PacketOpCode)
    {
        this.creature = creature;
        this.type = type;
        this.skillId = skillId;
        this.value = value;
        this.logId = (int)log;
    }

    public SmAttackStatus(Creature creature, TYPE type, int skillId, int value)
        : this(creature, type, skillId, value, LOG.REGULAR)
    {
    }

    public SmAttackStatus(Creature creature, int value)
        : this(creature, TYPE.REGULAR, 0, value, LOG.REGULAR)
    {
    }

    public int SkillId => skillId;

    protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
    {
        int hpOrMp;
        buffer.WriteD(creature.GetObjectId());
        if (type == TYPE.DAMAGE || type == TYPE.DELAYDAMAGE || type == TYPE.FALL_DAMAGE || type == TYPE.FP_DAMAGE
            || type == TYPE.MAGICCOUNTERATK || type == TYPE.DISPELBUFFCOUNTERATK || type == TYPE.USED_HP || type == TYPE.DROWNING)
        {
            buffer.WriteD(-value);
            hpOrMp = creature.GetLifeStats().GetHpPercentage();
        }
        else if (type == TYPE.USED_MP || type == TYPE.DAMAGE_MP)
        {
            buffer.WriteD(-value);
            hpOrMp = creature.GetLifeStats().GetMpPercentage();
        }
        else if (type == TYPE.MP || type == TYPE.NATURAL_MP || type == TYPE.HEAL_MP || type == TYPE.ABSORBED_MP)
        {
            buffer.WriteD(value);
            hpOrMp = creature.GetLifeStats().GetMpPercentage();
        }
        else
        {
            buffer.WriteD(value);
            hpOrMp = creature.GetLifeStats().GetHpPercentage();
        }
        buffer.WriteC(type.GetValue());
        buffer.WriteC(hpOrMp);
        buffer.WriteH(skillId);
        buffer.WriteH(logId);
    }

    /// <summary>Java parity: SmAttackStatus.TYPE (class-enum; constants may share ids).</summary>
    public sealed class TYPE
    {
        public static readonly TYPE TYPE1 = new TYPE(1);
        public static readonly TYPE TYPE2 = new TYPE(2);
        public static readonly TYPE TYPE9 = new TYPE(9);
        public static readonly TYPE TYPE11 = new TYPE(11);
        public static readonly TYPE TYPE12 = new TYPE(12);
        public static readonly TYPE TYPE14 = new TYPE(14);
        public static readonly TYPE TYPE25 = new TYPE(25);
        public static readonly TYPE NATURAL_HP = new TYPE(3);
        public static readonly TYPE USED_HP = new TYPE(4); // when skill uses hp as cost parameter
        public static readonly TYPE REGULAR = new TYPE(5);
        public static readonly TYPE ABSORBED_HP = new TYPE(6);
        public static readonly TYPE DAMAGE = new TYPE(7);
        public static readonly TYPE HP = new TYPE(7);
        public static readonly TYPE PROTECTDMG = new TYPE(8);
        public static readonly TYPE DELAYDAMAGE = new TYPE(10);
        public static readonly TYPE DROWNING = new TYPE(12);
        public static readonly TYPE HPAFTERRES = new TYPE(13); // when setting hp after resurrection, TODO implement
        public static readonly TYPE MAGICCOUNTERATK = new TYPE(15);
        public static readonly TYPE DISPELBUFFCOUNTERATK = new TYPE(16); // TODO implement
        public static readonly TYPE FALL_DAMAGE = new TYPE(17);
        public static readonly TYPE DOOR_REPAIR = new TYPE(18);
        public static readonly TYPE HEAL_MP = new TYPE(19);
        public static readonly TYPE DAMAGE_MP = new TYPE(20);
        public static readonly TYPE ABSORBED_MP = new TYPE(20);
        public static readonly TYPE MP = new TYPE(21);
        public static readonly TYPE NATURAL_MP = new TYPE(22);
        public static readonly TYPE USED_MP = new TYPE(23); // when skill uses mp as cost parameter
        public static readonly TYPE FP_RINGS = new TYPE(24);
        public static readonly TYPE FP = new TYPE(26);
        public static readonly TYPE FP_DAMAGE = new TYPE(26);
        public static readonly TYPE NATURAL_FP = new TYPE(27);

        private readonly int value;

        private TYPE(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return this.value;
        }
    }

    /// <summary>Java parity: SmAttackStatus.LOG.</summary>
    public enum LOG
    {
        SPELLATK = 1,
        HEAL = 3,
        MPHEAL = 4,
        CASEHEAL = 21,
        SKILLLATKDRAININSTANT = 23,
        SPELLATKDRAININSTANT = 24,
        POISON = 25,
        BLEED = 26,
        PROCATKINSTANT = 93, // changed in 4.5
        DELAYEDSPELLATKINSTANT = 97, // changed in 4.5
        MAGICCOUNTERATK = 112,
        SPELLATKDRAIN = 132, // changed in 4.5
        FPHEAL = 134, // changed in 4.5
        FPATTACK = 137,
        MPATTACK = 141,
        REGULAR = 191 // 4.8
    }
}
