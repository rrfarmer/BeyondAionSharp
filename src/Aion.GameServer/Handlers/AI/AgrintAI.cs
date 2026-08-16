using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The eight seasonal agrints of the housing fields. Java parity: ai/worlds/AgrintAI (xTz, Neon),
/// with the underlings' cadence taken from retail patterns <c>HLFP_Agrint*</c> and <c>HDFP_Agrint*</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. All eight patterns agree, so this is one mechanic
/// eight times over rather than a per-season judgement.
/// <para>
/// <b>The underlings were on the wrong trigger.</b> This class called five of them once, when the
/// agrint fell past half health, and they stayed for the rest of the fight. Retail calls five
/// <b>thirty seconds into the fight and every two hundred seconds after that</b>, five metres out,
/// and each lives <b>twenty seconds</b>. So they are a recurring squall rather than a single
/// permanent wave, and an agrint killed quickly never sees the second one.
/// </para>
/// <para>
/// <b>Recorded, deliberately not changed: the death drop.</b> Every pattern spawns <b>48</b> chests
/// at 24 metres for ten minutes; this class spawns <b>6</b> at one to six metres with no lifetime.
/// That is an eightfold difference in what an agrint pays out, and it is reward economy rather than
/// AI behaviour — the same call recorded for the Conquest rotation's shugo odds. The numbers are here
/// so the decision is a one-line change whenever somebody wants to make it. (The Asmodian winter
/// pattern uses 23 metres where the other seven use 24, which is the kind of detail that says these
/// really are eight hand-written patterns.)
/// </para>
/// </remarks>
[AIName("agrint")]
public class AgrintAI : OneDmgAI
{
    /// <summary>Retail's battle timer 2: thirty seconds in, then every two hundred.</summary>
    private static readonly TimeSpan FirstUnderlings = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan UnderlingInterval = TimeSpan.FromSeconds(200);

    private const int UnderlingsPerWave = 5;
    private const float UnderlingRange = 5f;
    private const long UnderlingLifeMillis = 20000L;

    /// <summary>
    /// The two id offsets that reach a season's underling: the Elyos agrints run 218850..218853 and
    /// the Asmodian 218862..218865, and both sides share underlings 219170..219173.
    /// </summary>
    private const int ElyosUnderlingOffset = 320;
    private const int AsmodianUnderlingOffset = 308;

    private ScheduledTask? underlingTask;
    private readonly AtomicBoolean started = new AtomicBoolean();

    public AgrintAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        switch (GetNpcId())
        {
            case 218862:
            case 218850:
                PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_HF_SpringAgrintAppear());
                break;
            case 218863:
            case 218851:
                PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_HF_SummerAgrintAppear());
                break;
            case 218864:
            case 218852:
                PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_HF_FallAgrintAppear());
                break;
            case 218865:
            case 218853:
                PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_HF_WinterAgrintAppear());
                break;
        }
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (started.CompareAndSet(false, true))
            underlingTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(
                _ => { CallUnderlings(); return ValueTask.CompletedTask; },
                FirstUnderlings, UnderlingInterval);
    }

    /// <summary>Which underling this agrint calls, or 0 for an npc id that is not one of the eight.</summary>
    internal static int UnderlingFor(int agrintId) => agrintId switch
    {
        >= 218850 and <= 218853 => agrintId + ElyosUnderlingOffset,
        >= 218862 and <= 218865 => agrintId + AsmodianUnderlingOffset,
        _ => 0,
    };

    private void CallUnderlings()
    {
        int npcId = UnderlingFor(GetNpcId());
        if (npcId == 0)
            return;

        for (int i = 0; i < UnderlingsPerWave; i++)
        {
            if (RndSpawnInRange(npcId, UnderlingRange) is not Npc underling)
                continue;

            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                underling.GetController().DeleteIfAliveOrCancelRespawn();
                return ValueTask.CompletedTask;
            }, UnderlingLifeMillis);
        }
    }

    private void CancelUnderlings()
    {
        if (underlingTask != null && !underlingTask.IsDone())
            underlingTask.Cancel(true);
        underlingTask = null;
        started.Set(false);
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        CancelUnderlings();
    }

    protected override void HandleDespawned()
    {
        CancelUnderlings();
        base.HandleDespawned();
    }

    private void SpawnChests(int npcId)
    {
        RndSpawnInRange(npcId, 1, 6);
        RndSpawnInRange(npcId, 1, 6);
        RndSpawnInRange(npcId, 1, 6);
        RndSpawnInRange(npcId, 1, 6);
        RndSpawnInRange(npcId, 1, 6);
        RndSpawnInRange(npcId, 1, 6);
    }

    protected override void HandleDied()
    {
        CancelUnderlings();
        switch (GetNpcId())
        {
            case 218850:
                SpawnChests(218874);
                break;
            case 218851:
                SpawnChests(218876);
                break;
            case 218852:
                SpawnChests(218878);
                break;
            case 218853:
                SpawnChests(218880);
                break;
            case 218862:
                SpawnChests(218882);
                break;
            case 218863:
                SpawnChests(218884);
                break;
            case 218864:
                SpawnChests(218886);
                break;
            case 218865:
                SpawnChests(218888);
                break;
        }
        base.HandleDied();
    }
}
