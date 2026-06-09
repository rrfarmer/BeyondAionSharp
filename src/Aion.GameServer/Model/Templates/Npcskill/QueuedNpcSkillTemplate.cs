namespace Aion.GameServer.Model.Templates.Npcskill;

/// <summary>Java parity: model/templates/npcskill/QueuedNpcSkillTemplate (Yeats).</summary>
public class QueuedNpcSkillTemplate : NpcSkillTemplate
{
    public QueuedNpcSkillTemplate(int id, int lv)
        : this(id, lv, -1)
    {
    }

    public QueuedNpcSkillTemplate(int id, int lv, int nextSkillTime)
        : this(id, lv, nextSkillTime, NpcSkillTargetAttribute.MOST_HATED)
    {
    }

    public QueuedNpcSkillTemplate(int id, int lv, int nextSkillTime, NpcSkillTargetAttribute npcSkillTargetAttribute)
    {
        this.id = id;
        this.lv = lv;
        this.prob = 100;
        this.nextSkillTime = nextSkillTime;
        this.target = npcSkillTargetAttribute;
    }
}
