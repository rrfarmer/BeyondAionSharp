using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Summons;

namespace Aion.GameServer.Controllers;

/// <summary>Java parity: controllers/SiegeWeaponController (xTz).</summary>
public class SiegeWeaponController : SummonController
{
    private Aion.GameServer.Model.Templates.Npcskill.NpcSkillTemplates skills;

    public SiegeWeaponController(int npcId)
    {
        skills = DataManager.NPC_SKILL_DATA.GetNpcSkillList(npcId);
    }

    public override void Release(UnsummonType unsummonType)
    {
        GetMaster().GetController().CancelTask(TaskId.SUMMON_FOLLOW);
        GetOwner().GetMoveController().AbortMove();
        base.Release(unsummonType);
    }

    public override void RestMode()
    {
        GetMaster().GetController().CancelTask(TaskId.SUMMON_FOLLOW);
        base.RestMode();
        GetOwner().GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.StopFollowMe, GetMaster());
    }

    public override void SetUnkMode()
    {
        base.SetUnkMode();
        GetMaster().GetController().CancelTask(TaskId.SUMMON_FOLLOW);
    }

    public sealed override void GuardMode()
    {
        base.GuardMode();
        GetMaster().GetController().CancelTask(TaskId.SUMMON_FOLLOW);
        GetOwner().SetTarget(GetMaster());
        GetOwner().GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.FollowMe, GetMaster());
        GetOwner().GetMoveController().MoveToTargetObject();
        GetMaster().GetController().AddTask(TaskId.SUMMON_FOLLOW, Aion.GameServer.Ai.Follow.FollowStartService.NewFollowingToTargetCheckTask(GetOwner(), GetMaster()));
    }

    public override void AttackMode(int targetObjId)
    {
        Creature target = (Creature)GetOwner().GetKnownList().GetObject(targetObjId);
        if (target == null || !Aion.GameServer.World.Geo.GeoService.GetInstance().CanSee(GetOwner(), target))
        {
            return;
        }
        if (IsValidTarget(target))
        {
            base.AttackMode(targetObjId);
            GetOwner().SetTarget(target);
            GetOwner().GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.FollowMe, target);
            GetOwner().GetMoveController().MoveToTargetObject();
            GetMaster().GetController().AddTask(TaskId.SUMMON_FOLLOW, Aion.GameServer.Ai.Follow.FollowStartService.NewFollowingToTargetCheckTask(GetOwner(), target));
        }
    }

    public bool IsValidTarget(Creature target)
    {
        Aion.GameServer.Model.GameObjects.Players.Player master = GetOwner().GetMaster();
        if (master == null)
        {
            return false;
        }
        Race masterRace = master.GetRace();
        if (!IsBalaurBoss(target))
        {
            if (masterRace == Race.ASMODIANS && target.GetRace() != Race.PC_LIGHT_CASTLE_DOOR && target.GetRace() != Race.DRAGON_CASTLE_DOOR
                    && target.GetRace() != Race.GCHIEF_LIGHT && target.GetRace() != Race.GCHIEF_DRAGON)
            {
                return false;
            }
            else if (masterRace == Race.ELYOS && target.GetRace() != Race.PC_DARK_CASTLE_DOOR && target.GetRace() != Race.DRAGON_CASTLE_DOOR
                    && target.GetRace() != Race.GCHIEF_DARK && target.GetRace() != Race.GCHIEF_DRAGON)
            {
                return false;
            }
        }
        return true;
    }

    private bool IsBalaurBoss(Creature creature)
    {
        return creature.GetRace() == Race.DRAKAN && creature is Aion.GameServer.Model.GameObjects.Siege.SiegeNpc
            && ((Aion.GameServer.Model.GameObjects.Siege.SiegeNpc)creature).GetObjectTemplate().GetRating() == Aion.GameServer.Model.Templates.Npc.NpcRating.Legendary;
    }

    public override void OnDie(Creature lastAttacker)
    {
        GetMaster().GetController().CancelTask(TaskId.SUMMON_FOLLOW);
        base.OnDie(lastAttacker);
    }

    public Aion.GameServer.Model.Templates.Npcskill.NpcSkillTemplates GetNpcSkillTemplates()
    {
        return skills;
    }
}
