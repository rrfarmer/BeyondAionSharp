using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The four Empyrean Lord avatars of the Dragon Lord's Refuge, normal and hard. Java parity:
/// <c>ai/instance/dragonLordsRefuge/EmpyreanLordAI</c> (Bobobear, Estrayl), with the two NPCs each
/// avatar places taken from retail patterns <c>Kaisinel_Avatar1</c>/<c>2</c>,
/// <c>Markutan_Avatar1</c>/<c>2</c> and their <c>IDTiamat_Hard_God*</c> twins.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Eight npc ids share this class across two
/// difficulties, and the eight retail patterns behind them agree pair for pair — so what an avatar
/// places is decided by <em>which of the four roles</em> it is, exactly as the casts already are.
/// <para>
/// <b>Neither of the two was ever placed.</b> The first avatar of each god calls up a visible
/// <b>kaisinel</b> (283159) or <b>marchutan</b> (283160) seven seconds after arriving — a named NPC on
/// tribe <c>IDTIAMAT_SPAWNHEAL</c>, which is the god appearing to mend the raid — and it stands for
/// twenty seconds. The second avatar of each arrives inside its own teleport effect (283175 / 283176),
/// which lasts six.
/// </para>
/// <para>
/// The seven seconds are retail's <c>set_idle_timer</c> on <c>on_wake_up</c>, and are unrelated to the
/// 8500 and 2500 millisecond casts below, which are aionemu's own and are left alone.
/// </para>
/// <para>
/// <b>Not translated: the four corner broadcasters.</b> Each first avatar's <c>on_die</c> puts an
/// <c>IDTiamat_Tiamat_Broadcast_God_OnDie</c> (283181) on each corner of the arena — (215, 188),
/// (791, 195), (216, 834), (777, 839) — for ten seconds, and each relays <c>broadcast_message 71</c>
/// fifty metres. It is how a hundred-metre broadcast covers a room far bigger than that, the same
/// trick Lower Udas Temple uses. But all eight listeners for 71 are the Tiamat keys, and every one of
/// them answers with a bare <c>SKILLI_INDEX</c> cast, so relaying it here would place four NPCs whose
/// only purpose is a message nothing on our side can act on.
/// </para>
/// <para>
/// <b>Also not translated:</b> the avatars' whole message web — 20, 23, 32, 37, 38, 39, 200, 201, 202
/// — and the <c>set_condition_spawn_variable</c> calls around it. Message 32 is the one that matters:
/// Tiamat sends it, and a first avatar answers by casting, arming a five-hundred-millisecond timer,
/// then leaving inside its own teleport effect. That is the god withdrawing, and it needs both the
/// message and the index.
/// </para>
/// </remarks>
[AIName("empyrean_lord")]
public class EmpyreanLordAI : GeneralNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(50, 15);
    private int tiamatId;

    /// <summary>What the first avatar of each god calls up, and how long it stays.</summary>
    private const int KaisinelSpawnHeal = 283159;
    private const int MarchutanSpawnHeal = 283160;
    private const int SpawnHealSeconds = 20;

    /// <summary>Retail's <c>set_idle_timer</c> on waking, which is what the spawn hangs off.</summary>
    private const int SpawnHealDelayMillis = 7000;

    /// <summary>The effect the second avatar of each god arrives inside.</summary>
    private const int KaisinelTeleport = 283175;
    private const int MarchutanTeleport = 283176;
    private const int TeleportSeconds = 6;

    /// <summary>What this avatar places when it wakes, or 0 for one not in the table.</summary>
    /// <param name="delayMillis">Retail's own delay: immediate for a teleport, seven seconds otherwise.</param>
    internal static int ArrivalSpawnFor(int npcId, out int liveSeconds, out int delayMillis)
    {
        (int npc, int life, int delay) = npcId switch
        {
            219488 or 856020 => (KaisinelSpawnHeal, SpawnHealSeconds, SpawnHealDelayMillis),
            219491 or 856023 => (MarchutanSpawnHeal, SpawnHealSeconds, SpawnHealDelayMillis),
            219489 or 856021 => (KaisinelTeleport, TeleportSeconds, 0),
            219492 or 856024 => (MarchutanTeleport, TeleportSeconds, 0),
            _ => (0, 0, 0),
        };

        liveSeconds = life;
        delayMillis = delay;
        return npc;
    }

    public EmpyreanLordAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            GetPosition().GetWorldMapInstance().ForEachNpc(npc =>
            {
                if (npc.GetNpcId() >= 219532 && npc.GetNpcId() <= 219539)
                    npc.GetController().Die();
            });
            return ValueTask.CompletedTask;
        }, 9000L);

        PlaceArrival();

        tiamatId = GetPosition().GetMapId() == 300520000 ? 219361 : 236276;
        switch (GetNpcId())
        {
            case 219488:
            case 856020:
                ThreadPoolManager.GetInstance().Schedule(ct => { AIActions.UseSkill(this, 20932); return ValueTask.CompletedTask; }, 8500L);
                break;
            case 219491:
            case 856023:
                ThreadPoolManager.GetInstance().Schedule(ct => { AIActions.UseSkill(this, 20936); return ValueTask.CompletedTask; }, 8500L);
                break;
            case 219489:
            case 856021:
                AIActions.TargetCreature(this, GetPosition().GetWorldMapInstance().GetNpc(tiamatId));
                ThreadPoolManager.GetInstance().Schedule(ct => { AIActions.UseSkill(this, 20929); return ValueTask.CompletedTask; }, 8500L);
                break;
            case 219492:
            case 856024:
                AIActions.TargetCreature(this, GetPosition().GetWorldMapInstance().GetNpc(tiamatId));
                ThreadPoolManager.GetInstance().Schedule(ct => { AIActions.UseSkill(this, 20933); return ValueTask.CompletedTask; }, 2500L);
                break;
        }
    }

    /// <summary>Places whatever this avatar arrives with, on retail's own delay, for retail's own time.</summary>
    private void PlaceArrival()
    {
        int npcId = ArrivalSpawnFor(GetNpcId(), out int liveSeconds, out int delayMillis);
        if (npcId == 0)
            return;

        // Scheduled even at zero delay: this runs from inside the owner's own BringIntoWorld, and a
        // spawn made there races the rest of that path. Same reason AttackAfterSpawn defers.
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!GetOwner().IsSpawned() || IsDead())
                return ValueTask.CompletedTask;

            WorldPosition here = GetPosition();
            if (Spawn(npcId, here.GetX(), here.GetY(), here.GetZ(), (sbyte)here.GetHeading()) is Npc placed)
                ThreadPoolManager.GetInstance().Schedule(_ =>
                {
                    if (placed.IsSpawned())
                        placed.GetController().DeleteIfAliveOrCancelRespawn();
                    return ValueTask.CompletedTask;
                }, liveSeconds * 1000L);

            return ValueTask.CompletedTask;
        }, delayMillis);
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        switch (skillTemplate.GetSkillId())
        {
            case 20932:
            case 20936:
                Npc tiamat = GetPosition().GetWorldMapInstance().GetNpc(tiamatId);
                AIActions.TargetCreature(this, tiamat);
                GetAggroList().AddHate(tiamat, int.MaxValue / 4);
                SetStateIfNot(AIState.FIGHT);
                EmoteManager.EmoteStartAttacking(GetOwner(), tiamat);
                break;
        }
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        switch (GetNpcId())
        {
            case 219488:
            case 856020:
            case 219491:
            case 856023:
                switch (phaseHpPercent)
                {
                    case 50:
                        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.IDTIAMAT_TIAMAT_GOD_HP_LOWER_THAN_50p());
                        break;
                    case 15:
                        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.IDTIAMAT_TIAMAT_GOD_HP_LOWER_THAN_15p());
                        break;
                }
                break;
        }
    }
}
