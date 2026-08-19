using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/tiamatStrongHold/SinkingSandAI (@author Cheatkiller).</summary>
[AIName("sinkingsand")]
public class SinkingSandAI : NpcAI
{
    public SinkingSandAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        Useskill();
    }

    /// <summary><c>IDTiamat_Shavorkhan_Sink</c> and its damage twin <c>_SinkDMG</c>.</summary>
    private const int Sink = 283083;
    private const int SinkDamage = 283084;

    /// <summary>Retail's lifetimes: a minute for the sink, six seconds for what it drops.</summary>
    private const int SinkLife = 60;
    private const int SinkDamageLife = 6;

    /// <summary>
    /// The two halves of retail's sink, which this class ran as one.
    /// </summary>
    /// <remarks>
    /// Retail's <c>Sink</c> drops a <c>SinkDMG</c> at its own point on waking and then <b>stands for a
    /// minute</b>, waiting for message 301 to cast and leave. The <c>SinkDMG</c> is the half that casts,
    /// and it lives six seconds.
    /// <para>
    /// <b>Both ids ran the same three-second cast and four-second self-delete here</b>, so the field a
    /// raid is meant to walk around was a flash, and Shabokan spawning the pair meant two casts per
    /// target rather than one. He now spawns only the sink; the sink drops its own damage.
    /// </para>
    /// <para>
    /// <b>Not translated:</b> message 301, which is what ends the sink early in retail. Nothing in this
    /// port sends it, so a sink always stands its full minute.
    /// </para>
    /// </remarks>
    private void Useskill()
    {
        if (GetNpcId() == Sink)
        {
            SpawnFor(SinkDamage, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0,
                SinkDamageLife);
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                AIActions.DeleteOwner(this);
                return System.Threading.Tasks.ValueTask.CompletedTask;
            }, SinkLife * 1000L);
            return;
        }

        // Retail's SinkDMG is two lines -- use_skill on waking, despawn_self -- so it casts at once
        // rather than after three seconds. Scheduled a tick out rather than run inline because a state
        // change made inside BringIntoWorld is overwritten by the rest of the spawn path.
        //
        // Its six-second live_time is a backstop this never reaches: the npc removes itself as soon as
        // it has cast. Same shape recorded for Terath's gravity pair and Ebonsoul's black hole -- the
        // shorter clock always wins, so the lifetime is unobservable and kept only because it is
        // retail's number.
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            AIActions.UseSkill(this, 20723);
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                AIActions.DeleteOwner(this);
                return System.Threading.Tasks.ValueTask.CompletedTask;
            }, 1000L);
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, 500L);
    }
}
