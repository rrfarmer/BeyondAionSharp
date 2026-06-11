using System.Collections.Generic;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Drop;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Ai;

/// <summary>
/// Java parity: ai/AIActions (ATracer). Common AI actions with access to the AI owner. Java wildcard AbstractAI&lt;? extends Creature&gt;
/// -> C# generic methods &lt;T&gt;(AbstractAI&lt;T&gt;) where T : Creature (NpcAI=AbstractAI&lt;Npc&gt; callers). Anonymous
/// RequestResponseHandler&lt;Creature&gt; / DialogObserver subclasses -> nested classes capturing request/requestId via ctor.
/// AIRequest/DropRegistrationService/RespawnService red-tolerated where unported.
/// </summary>
public class AIActions
{
    /// <summary>Despawn and delete owner</summary>
    public static void DeleteOwner<T>(AbstractAI<T> ai) where T : Creature
    {
        ai.GetOwner().GetController().Delete();
    }

    /// <summary>AI's owner will die</summary>
    public static void Die<T>(AbstractAI<T> ai) where T : Creature
    {
        ai.GetOwner().GetController().Die(ai.GetOwner());
    }

    /// <summary>AI's owner will die from specified attacker</summary>
    public static void Die<T>(AbstractAI<T> ai, Creature attacker) where T : Creature
    {
        ai.GetOwner().GetController().Die(attacker);
    }

    /// <summary>Use skill or add intention to use (will be implemented later)</summary>
    public static void UseSkill<T>(AbstractAI<T> ai, int skillId, int level) where T : Creature
    {
        ai.GetOwner().GetController().UseSkill(skillId, level);
    }

    public static void UseSkill<T>(AbstractAI<T> ai, int skillId) where T : Creature
    {
        ai.GetOwner().GetController().UseSkill(skillId);
    }

    public static void TargetSelf<T>(AbstractAI<T> ai) where T : Creature
    {
        ai.GetOwner().SetTarget(ai.GetOwner());
    }

    public static void TargetCreature<T>(AbstractAI<T> ai, Creature target) where T : Creature
    {
        ai.GetOwner().SetTarget(target);
    }

    public static void HandleUseItemFinish<T>(AbstractAI<T> ai, Player player) where T : Creature
    {
        ai.GetPosition().GetWorldMapInstance().GetInstanceHandler().HandleUseItemFinish(player, (Npc)ai.GetOwner());
    }

    public static void RegisterDrop<T>(AbstractAI<T> ai, Player player, ICollection<Player> registeredPlayers) where T : Creature
    {
        DropRegistrationService.GetInstance().RegisterDrop((Npc)ai.GetOwner(), player, registeredPlayers);
    }

    public static void ScheduleRespawn<T>(AbstractAI<T> ai) where T : Creature
    {
        RespawnService.ScheduleRespawn(ai.GetOwner());
    }

    /// <summary>
    /// Add RequestResponseHandler to player, valid within 5 meters around the AI owner (client auto-declines when walking out of range).
    /// </summary>
    public static void AddRequest<T>(AbstractAI<T> ai, Player player, int requestId, AIRequest request, params object[] requestParams) where T : Creature
    {
        AddRequest(ai, player, requestId, 5, request, requestParams);
    }

    /// <summary>
    /// Add RequestResponseHandler to player, valid in the given range around the AI owner. For special windows like artifact
    /// activation, this parameter instead controls the reuse cooldown.
    /// </summary>
    public static void AddRequest<T>(AbstractAI<T> ai, Player player, int requestId, int rangeOrCooldownSeconds, AIRequest request,
        params object[] requestParams) where T : Creature
    {
        bool requested = player.GetResponseRequester().PutRequest(requestId, new AIRequestResponseHandler(ai.GetOwner(), request, requestId));

        if (requested)
        {
            if (rangeOrCooldownSeconds > 0)
            {
                player.GetObserveController().AddObserver(new AIDialogObserver(ai.GetOwner(), player, rangeOrCooldownSeconds, request));
            }
            PacketSendUtility.SendPacket(player, new SM_QUESTION_WINDOW(requestId, ai.GetObjectId(), rangeOrCooldownSeconds, requestParams));
        }
    }

    /// <summary>Java anonymous RequestResponseHandler&lt;Creature&gt; -> nested class delegating to the AIRequest.</summary>
    private sealed class AIRequestResponseHandler : RequestResponseHandler<Creature>
    {
        private readonly AIRequest request;
        private readonly int requestId;

        public AIRequestResponseHandler(Creature owner, AIRequest request, int requestId) : base(owner)
        {
            this.request = request;
            this.requestId = requestId;
        }

        public override void DenyRequest(Creature requester, Player responder)
        {
            request.DenyRequest(requester, responder);
        }

        public override void AcceptRequest(Creature requester, Player responder)
        {
            request.AcceptRequest(requester, responder, requestId);
        }
    }

    /// <summary>Java anonymous DialogObserver -> nested class; tooFar() denies via the protected Requester/Responder fields.</summary>
    private sealed class AIDialogObserver : DialogObserver
    {
        private readonly AIRequest request;

        public AIDialogObserver(Creature owner, Player player, int range, AIRequest request) : base(owner, player, range)
        {
            this.request = request;
        }

        public override void TooFar()
        {
            request.DenyRequest(Requester, Responder);
        }
    }
}
