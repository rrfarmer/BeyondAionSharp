using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_ATTACK_STATUS (alexa026, ATracer, kecimis). HP/MP/FP change status packet. CRITICAL: nested TYPE enum carries DUPLICATE int values (HP=7=DAMAGE, ABSORBED_MP=20=DAMAGE_MP, FP=26=FP_DAMAGE, DROWNING=12=TYPE12) and the switch distinguishes constants by identity not value — so TYPE/LOG ported as sealed value-classes (reference identity preserved) and the switch as reference-equality chains, like RiftInformer. getValue()->GetValue(); Creature red-tolerated.</summary>
public class SM_ATTACK_STATUS : AionServerPacket
{
    private Creature creature;
    private TYPE type;
    private int skillId;
    private int value;
    private int logId;

    public sealed class TYPE
    {
        // missing
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

        private int value;

        private TYPE(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return this.value;
        }
    }

    public sealed class LOG
    {
        public static readonly LOG SPELLATK = new LOG(1);
        public static readonly LOG HEAL = new LOG(3);
        public static readonly LOG MPHEAL = new LOG(4);
        public static readonly LOG CASEHEAL = new LOG(21);
        public static readonly LOG SKILLLATKDRAININSTANT = new LOG(23);
        public static readonly LOG SPELLATKDRAININSTANT = new LOG(24);
        public static readonly LOG POISON = new LOG(25);
        public static readonly LOG BLEED = new LOG(26);
        public static readonly LOG PROCATKINSTANT = new LOG(93); // changed in 4.5
        public static readonly LOG DELAYEDSPELLATKINSTANT = new LOG(97); // changed in 4.5
        public static readonly LOG MAGICCOUNTERATK = new LOG(112);
        // 119 unk
        // 131 unk
        public static readonly LOG SPELLATKDRAIN = new LOG(132); // changed in 4.5
        public static readonly LOG FPHEAL = new LOG(134); // changed in 4.5
        public static readonly LOG FPATTACK = new LOG(137);
        public static readonly LOG MPATTACK = new LOG(141);
        public static readonly LOG REGULAR = new LOG(191); // 4.8

        private int value;

        private LOG(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return this.value;
        }
    }

    public SM_ATTACK_STATUS(Creature creature, TYPE type, int skillId, int value, LOG log)
    {
        this.creature = creature;
        this.type = type;
        this.skillId = skillId;
        this.value = value;
        this.logId = log.GetValue();
    }

    public SM_ATTACK_STATUS(Creature creature, TYPE type, int skillId, int value)
        : this(creature, type, skillId, value, LOG.REGULAR)
    {
    }

    public SM_ATTACK_STATUS(Creature creature, int value)
        : this(creature, TYPE.REGULAR, 0, value, LOG.REGULAR)
    {
    }

    protected override void WriteImpl(AionConnection con)
    {
        int hpOrMp;
        WriteD(creature.GetObjectId());
        if (type == TYPE.DAMAGE || type == TYPE.DELAYDAMAGE || type == TYPE.FALL_DAMAGE || type == TYPE.FP_DAMAGE
            || type == TYPE.MAGICCOUNTERATK || type == TYPE.DISPELBUFFCOUNTERATK || type == TYPE.USED_HP || type == TYPE.DROWNING)
        {
            WriteD(-value);
            hpOrMp = creature.GetLifeStats().GetHpPercentage();
        }
        else if (type == TYPE.USED_MP || type == TYPE.DAMAGE_MP)
        {
            WriteD(-value);
            hpOrMp = creature.GetLifeStats().GetMpPercentage();
        }
        else if (type == TYPE.MP || type == TYPE.NATURAL_MP || type == TYPE.HEAL_MP || type == TYPE.ABSORBED_MP)
        {
            WriteD(value);
            hpOrMp = creature.GetLifeStats().GetMpPercentage();
        }
        else
        {
            WriteD(value);
            hpOrMp = creature.GetLifeStats().GetHpPercentage();
        }
        WriteC(type.GetValue());
        WriteC(hpOrMp);
        WriteH(skillId);
        WriteH(logId);
    }

    /*
     * logId depends on effecttemplate
     * effecttemplate (TYPE) LOG.getValue()
     *
     * spellattack (7) 1 (as negative value)//checked 4.5
     * heal(7) 3 //checked 4.5
     * mpheal (21) 4 //checked 4.5
     * SpellAtkDrainInstantEffect(20) 24 (refactoring shard, soul absorption) //checked 4.5
     * poison(hp) 25
     * bleed(hp) 26
     * procatkinstant - (7) 93 // checked in 4.5
     * delaydamage(10) 97 (lava tsunami) // checked in 4.5
     * falldmg (17) 170 hp as cost
     * parameter(4) 187 // checked in 4.5
     * mp regen(natural_mp) 187 //187 in 4.5
     * hp regen(natural_hp) 187 //187 in 4.5
     * fp regen(natural_fp) 187 //187 in 4.5
     * fp pot(fp) 171
     * prochp(7) 187 //checked in 4.5
     * procmp(21) 187 //checked in 4.5
     * heal_instant (regular) 171 protecteffect on protector - (8) 171 4.5
     * type="MP(21)" skillId="17722" logId="UNKNOWN(141) - mpattack
     * type="UNKNOWN(15)" skillId="2196" logId="UNKNOWN(112) - magiccounteratk
     * type="DAMAGE_HEAL_HP(7)" skillId="2858" logId="UNKNOWN(3073)" - spellatk(Flame Cage only)???
     * type="DAMAGE_HEAL_HP(7)"  skillId="8759" logId="UNKNOWN(132)" - spellatkdrain
     * type="DAMAGE_HEAL_HP(7)"  skillId="2391" logId="UNKNOWN(21)" - caseheal(hp)
     * type="DAMAGE_HEAL_FP(26)" skillId="8772" logId="UNKNOWN(134) - fpheal
     * type="UNKNOWN(16)" skillId="0" logId="REGULAR(187)" - dispelbuffcounteratk(2404)
     * type="UNKNOWN(13)" skillId="0" logId="REGULAR(187)" - setting hp after resurrect
     * TODO find rest of logIds
     */
}
