using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Custom.Instance;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/custom/eternalChallenge/CustomInstanceDominatorAI (@author Jo).
/// </summary>
[AIName("custom_instance_dominator")]
public class CustomInstanceDominatorAI : AggressiveNoLootNpcAI
{
    private int challengerObjId;
    private int challengerRank;
    private ScheduledTask? debuffTask;

    public CustomInstanceDominatorAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();

        WorldMapInstance wmi = GetPosition().GetWorldMapInstance();
        if (!(wmi.GetInstanceHandler() is RoahCustomInstanceHandler))
            return;

        challengerObjId = System.Linq.Enumerable.First(wmi.GetRegisteredObjects());
        challengerRank = CustomInstanceService.GetInstance().LoadOrCreateRank(challengerObjId).GetRank();

        debuffTask = ThreadPoolManager.GetInstance().Schedule(_ => { DebuffTarget(); return ValueTask.CompletedTask; }, 1000L);
        GetOwner().SetSeeState(CreatureSeeState.Search2);
    }

    public override AttackIntention ChooseAttackIntention()
    {
        VisibleObject target = GetTarget();
        if (!IsDead() && target != null)
        {
            if (!GeoService.GetInstance().CanSee(GetOwner(), target))
            {
                World.World.GetInstance().UpdatePosition(GetOwner(), target.GetX(), target.GetY(), target.GetZ(), (byte)30);
                PacketSendUtility.BroadcastPacketAndReceive(GetOwner(), new SM_FORCED_MOVE(GetOwner(), GetOwner()));
            }
        }
        return base.ChooseAttackIntention();
    }

    private void DebuffTarget()
    {
        if (!IsDead())
        {
            if (GetOwner().GetSkillCoolDowns() != null)
                GetOwner().GetSkillCoolDowns().Clear(); // Make CD rank-dependent, not skill-dependent

            if (challengerRank >= CustomInstanceRankEnum.ANCIENT.GetMinRank())
                GetOwner().QueueSkill(3539, 65, 0); // Ignite Aether

            if (challengerRank >= CustomInstanceRankEnum.CERANIUM.GetMinRank())
                GetOwner().QueueSkill(618, 65, 0); // Ankle Snare
            else if (challengerRank >= CustomInstanceRankEnum.GOLD.GetMinRank())
                GetOwner().QueueSkill(1328, 65, 0); // Restraint (^)

            if (challengerRank >= CustomInstanceRankEnum.ANCIENT_PLUS.GetMinRank())
                GetOwner().QueueSkill(3775, 65, 0); // Fear
            else if (challengerRank >= CustomInstanceRankEnum.GOLD.GetMinRank())
                GetOwner().QueueSkill(1417, 65, 0); // Curse Tree (^)

            // SILVER:
            GetOwner().QueueSkill(3581, 65, 0); // Withering Gloom

            if (challengerRank >= CustomInstanceRankEnum.PLATINUM.GetMinRank())
                GetOwner().QueueSkill(3574, 65, 0); // Shackle of Vulnerability
            else // SILVER:
                GetOwner().QueueSkill(3780, 65, 0); // Root of Enervation (^)

            if (challengerRank >= CustomInstanceRankEnum.MITHRIL.GetMinRank())
                if (GetOwner().GetTarget() != null && GetOwner().GetTarget() is Player
                    && ((Player)GetOwner().GetTarget()).GetPlayerClass().IsPhysicalClass())
                    GetOwner().QueueSkill(3571, 65, 0); // Body Root
                else
                    GetOwner().QueueSkill(3572, 65, 0); // Sigil of Silence

            if (challengerRank >= CustomInstanceRankEnum.CERANIUM.GetMinRank())
                if (GetOwner().GetTarget() != null && GetOwner().GetTarget() is Player
                    && ((Player)GetOwner().GetTarget()).GetPlayerClass().IsPhysicalClass())
                    GetOwner().QueueSkill(4135, 65, 0); // Blinding Light
                else
                    GetOwner().QueueSkill(1336, 65, 0); // Curse of Weakness

            if (challengerRank >= CustomInstanceRankEnum.ANCIENT.GetMinRank())
                GetOwner().QueueSkill(4490, 65, 0); // Paralysis Resonation

            debuffTask = ThreadPoolManager.GetInstance().Schedule(_ => { DebuffTarget(); return ValueTask.CompletedTask; }, 30000 - challengerRank * 1000L / 3); // 30s ... 20s
        }
    }

    private void CancelTasks()
    {
        if (debuffTask != null && !debuffTask.IsCancelled)
            debuffTask.Cancel(true);
    }

    protected override void HandleDied()
    {
        CancelTasks();
        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        CancelTasks();
        base.HandleDespawned();
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        if (challengerObjId != 0)
        {
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                Player challenger = GetPosition().GetWorldMapInstance().GetPlayer(challengerObjId);
                if (challenger != null && !GetOwner().GetAggroList().IsHating(challenger) && GetOwner().CanSee(challenger))
                    GetOwner().GetAggroList().AddHate(challenger, 1000);
                return ValueTask.CompletedTask;
            }, 1000L);
        }
    }
}
