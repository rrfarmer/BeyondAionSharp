using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/dragonLordsRefuge/HM_CalindiFlamelordAI (Cheatkiller, Yeats, Estrayl).</summary>
[AIName("IDTiamat_HM_calindi_flamelord")]
public class HM_CalindiFlamelordAI : CalindiFlamelordAI
{
    public HM_CalindiFlamelordAI(Npc owner)
        : base(owner)
    {
    }

    protected override void StartHallucinatoryVictoryEvent()
    {
        if (GetPosition().GetWorldMapInstance().GetNpc(731629) == null && GetPosition().GetWorldMapInstance().GetNpc(731630) == null)
            GetOwner().QueueSkill(21887, 1);
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        switch (skillTemplate.GetSkillId())
        {
            case 21887:
                Spawn(731629, 482.21f, 458.06f, 427.42f, (sbyte)98);
                Spawn(731630, 482.21f, 571.16f, 427.42f, (sbyte)22);
                RndSpawn(856298);
                break;
            case 21888:
                Creature target = GetRandomTarget();
                if (target != null)
                    Spawn(856299, target.GetX(), target.GetY(), target.GetZ(), (sbyte)0);
                break;
        }
    }

    protected override void BlazeEngraving()
    {
        if (Rnd.Chance() < 3 && GetPosition().GetWorldMapInstance().GetNpc(856299) == null)
            GetOwner().QueueSkill(21888, 60);
    }
}
