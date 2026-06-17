using System.Threading;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/PadmarashkasCaveInstance (Ritsu, Luzien) : GeneralInstanceHandler. @InstanceID(320150000); AtomicBoolean/AtomicInteger→int+Interlocked (incrementAndGet→Increment, set(0)→Exchange/Volatile.Write, compareAndSet→CompareExchange); onDie protector/egg logic, onEnterZone movie, onInstanceDestroy reset. 1:1.</summary>
[InstanceID(320150000)]
public class PadmarashkasCaveInstance : GeneralInstanceHandler
{
    private int moviePlayed;
    private int killedPadmarashkaProtector;
    private int killedEggs;

    public PadmarashkasCaveInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnDie(Npc npc)
    {
        int npcId = npc.GetNpcId();
        switch (npcId)
        {
            case 218670:
            case 218671:
            case 218673:
            case 218674:
                if (Interlocked.Increment(ref killedPadmarashkaProtector) == 4)
                {
                    Volatile.Write(ref killedPadmarashkaProtector, 0);
                    Npc padmarashka = GetNpc(218756);
                    if (padmarashka != null && !padmarashka.IsDead())
                    {
                        padmarashka.GetEffectController().UnsetAbnormal(AbnormalState.SLEEP);
                        // padmarashka.getEffectController().broadCastEffects(0);
                        Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(padmarashka, 19187, 55, padmarashka).UseNoAnimationSkill();
                        padmarashka.GetEffectController().RemoveEffect(19186); // skill should handle this TODO: fix
                        ThreadPoolManager.GetInstance()
                            .Schedule(() => padmarashka.GetAi().OnCreatureEvent(AiEventType.CREATURE_AGGRO, instance.GetPlayersInside()[0]), 1000L);
                    }
                }
                break;
            case 282613:
            case 282614:
                if (Interlocked.Increment(ref killedEggs) == 20) // TODO: find value
                {
                    Npc padmarashka = GetNpc(218756);
                    if (padmarashka != null && !padmarashka.IsDead())
                    {
                        Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(20101, padmarashka, padmarashka);
                    }
                }
                break;
        }
    }

    public override void OnEnterZone(Player player, ZoneInstance zone)
    {
        if (zone.GetAreaTemplate().GetZoneName() == ZoneName.Get("PADMARASHKAS_NEST_320150000") && Interlocked.CompareExchange(ref moviePlayed, 1, 0) == 0)
            PacketSendUtility.BroadcastToMap(instance, new SM_PLAY_MOVIE(false, 0, 0, 488, true));
    }

    public override void OnInstanceDestroy()
    {
        Volatile.Write(ref moviePlayed, 0);
        Volatile.Write(ref killedPadmarashkaProtector, 0);
    }
}
