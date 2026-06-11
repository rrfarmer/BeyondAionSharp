using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Items;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>Java parity: model/gameobjects/Homing extends SummonedObject&lt;Creature&gt;.</summary>
public class Homing : SummonedObject<Creature>
{
    private readonly int skillId;
    private readonly ItemAttackType attackType;

    private int attackCount;

    public Homing(Aion.GameServer.Controllers.NpcController controller, Aion.GameServer.Model.Templates.Spawns.SpawnTemplate spawnTemplate, sbyte level, Creature creator, int skillId)
        : base(controller, spawnTemplate, level, creator)
    {
        this.skillId = skillId;
        this.attackType = FindAttackType();
        SetMasterName("");
        SetKnownlist(new Aion.GameServer.World.Knownlist.NpcKnownList(this));
        SetEffectController(new EffectController(this));
    }

    protected override void SetupStatContainers()
    {
        SetGameStats(new HomingGameStats(this));
        SetLifeStats(new NpcLifeStats(this));
    }

    public void SetAttackCount(int attackCount)
    {
        this.attackCount = attackCount;
    }

    public int GetAttackCount()
    {
        return attackCount;
    }

    public override NpcObjectType GetNpcObjectType()
    {
        return NpcObjectType.HOMING;
    }

    public override ItemAttackType GetAttackType()
    {
        return attackType;
    }

    public int GetSkillId()
    {
        return skillId;
    }

    private ItemAttackType FindAttackType()
    {
        if (GetName().Contains("fire"))
            return ItemAttackType.MAGICAL_FIRE;
        else if (GetName().Contains("stone") || GetName().Equals("gryphu"))
            return ItemAttackType.MAGICAL_EARTH;
        else if (GetName().Contains("water"))
            return ItemAttackType.MAGICAL_WATER;
        else if ((GetName().Contains("wind")) || (GetName().Contains("cyclone")) || (GetName().Contains("elemental")))
            return ItemAttackType.MAGICAL_WIND;
        return ItemAttackType.PHYSICAL;
    }
}
