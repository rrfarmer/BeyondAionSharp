using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Templates.Walker;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author xTz
/// </summary>
[AIName("imprisoned_reian")]
public class ImprisonedReianAI : GeneralNpcAI
{
    private AtomicBoolean isSaved = new AtomicBoolean(false);
    private AtomicBoolean isAsked = new AtomicBoolean(false);
    private string walkerId;
    private WalkerTemplate template;

    public ImprisonedReianAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        walkerId = GetSpawnTemplate().GetWalkerId();
        GetSpawnTemplate().SetWalkerId(null);
        if (walkerId != null)
        {
            template = DataManager.WALKER_DATA.GetWalkerTemplate(walkerId);
        }
        base.HandleSpawned();
    }

    protected override void HandleMoveArrived()
    {
        RouteStep step = GetOwner().GetMoveController().GetCurrentStep();
        base.HandleMoveArrived();
        if (template.GetRouteSteps().Count - 4 == step.GetStepIndex())
        {
            GetSpawnTemplate().SetWalkerId(null);
            WalkManager.StopWalking(this);
            AIActions.DeleteOwner(this);
        }
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        if (walkerId != null)
        {
            if (creature is Player)
            {
                Player player = (Player)creature;
                if (PositionUtil.GetDistance(GetOwner(), player) <= 21)
                {
                    if (isAsked.CompareAndSet(false, true))
                    {
                        switch (Rnd.Get(1, 10))
                        {
                            case 1:
                                PacketSendUtility.BroadcastMessage(GetOwner(), 390563);
                                break;
                            case 2:
                                PacketSendUtility.BroadcastMessage(GetOwner(), 390567);
                                break;
                        }
                    }
                }
                if (PositionUtil.GetDistance(GetOwner(), player) <= 6)
                {
                    if (isSaved.CompareAndSet(false, true))
                    {
                        GetSpawnTemplate().SetWalkerId(walkerId);
                        WalkManager.StartWalking(this);
                        GetOwner().SetState(CreatureState.ACTIVE, true);
                        PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetObjectId()));
                        switch (Rnd.Get(1, 10))
                        {
                            case 1:
                                PacketSendUtility.BroadcastMessage(GetOwner(), 342410);
                                break;
                            case 2:
                                PacketSendUtility.BroadcastMessage(GetOwner(), 342411);
                                break;
                        }
                    }
                }
            }
        }
    }
}
