using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The conquest offering spawners (856150-856173) and the spots they place. Retail patterns
/// <c>LF4_Rotation_*_SpawnNPC_*</c> and <c>DF4_Rotation_*_SpawnNPC_*</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>Retail runs this as two stages and this class ran
/// it as one.</b>
/// <list type="number">
/// <item>a spawner waits <b>eight minutes</b>, then places a <b>solo spot</b> at 51%, a <b>party
/// spot</b> at 22%, or <b>nothing at all</b> at 27%;</item>
/// <item>the spot it places lives <b>ten seconds</b>, and on waking rolls again — 19/19/20/20 for four
/// ordinary monsters and 6/7/7 for three "All" variants, with an eighth taking the remaining two per
/// cent — and puts one at its own point.</item>
/// </list>
/// <para>
/// What stood here rolled <b>once, immediately, on spawning</b>, at 70/30 with a nested 30, and placed
/// the monster directly. No eight-minute cadence, no chance of nothing happening, no spot, and none of
/// retail's odds.
/// </para>
/// <para>
/// <b>Both tables are extracted rather than transcribed.</b> Twenty-four spawners each name their own
/// pair of spots, and forty-eight spots each name their own eight monsters; every one of the forty-eight
/// carries the same weights, which is the kind of regularity worth checking rather than assuming.
/// </para>
/// <para>
/// <b>Not translated.</b> Message <c>13929</c>, which resets a spawner's clock and has no sender in this
/// port, and the flag var each branch sets — retail uses it to stop a spawner re-rolling while its spot
/// is still standing, and the ten-second lifetime does that here.
/// </para>
/// </remarks>
[AIName("conquest_offering_spawner")]
public class ConquestOfferingSpawnerAI : NpcAI, INpcMessageListener
{
    /// <summary>Retail's message from the time-reset npc: start the eight minutes again.</summary>
    public const int TimeReset = 13929;

    /// <summary>Retail's idle timer on every spawner.</summary>
    private const long CycleMillis = 480_000L;

    /// <summary>Retail's <c>test_probability</c> on the two spot branches; the rest is silence.</summary>
    private const int SoloChance = 51;
    private const int PartyChance = 22;

    /// <summary>Retail's <c>live_time</c> on a spot.</summary>
    private const int SpotLife = 10;

    /// <summary>Each spawner's own pair of spots: the solo one, then the party one.</summary>
    private static readonly Dictionary<int, (int Solo, int Party)> Spots =
        new Dictionary<int, (int, int)>
        {
            [856150] = (856314, 856320),
            [856151] = (856315, 856321),
            [856152] = (856316, 856322),
            [856153] = (856317, 856323),
            [856154] = (856318, 856324),
            [856155] = (856319, 856325),
            [856156] = (856354, 856360),
            [856157] = (856355, 856361),
            [856158] = (856356, 856362),
            [856159] = (856357, 856363),
            [856160] = (856358, 856364),
            [856161] = (856359, 856365),
            [856162] = (856326, 856332),
            [856163] = (856327, 856333),
            [856164] = (856328, 856334),
            [856165] = (856329, 856335),
            [856166] = (856330, 856336),
            [856167] = (856331, 856337),
            [856168] = (856366, 856372),
            [856169] = (856367, 856373),
            [856170] = (856368, 856374),
            [856171] = (856369, 856375),
            [856172] = (856370, 856376),
            [856173] = (856371, 856377),
        };
    private ScheduledTask? cycleTask;

    public ConquestOfferingSpawnerAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ArmCycle();
    }

    private void ArmCycle()
    {
        cycleTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            PlaceSpot();
            return ValueTask.CompletedTask;
        }, CycleMillis);
    }

    /// <summary>One turn of the eight-minute clock: a spot, the other spot, or nothing.</summary>
    private void PlaceSpot()
    {
        if (GetOwner().IsSpawned() && Spots.TryGetValue(GetNpcId(), out (int Solo, int Party) pair))
        {
            int roll = Rnd.NextInt(100);
            int spot = roll < SoloChance ? pair.Solo
                : roll < SoloChance + PartyChance ? pair.Party
                : 0;

            if (spot != 0)
                SpawnFor(spot, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(),
                    (sbyte)GetOwner().GetHeading(), SpotLife);
        }

        ArmCycle();
    }

    private void CancelCycle()
    {
        if (cycleTask != null && !cycleTask.IsDone())
            cycleTask.Cancel(true);
        cycleTask = null;
    }

    protected override void HandleDespawned()
    {
        CancelCycle();
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        CancelCycle();
        base.HandleDied();
    }

    /// <summary>
    /// <c>13929</c> — a monster died somewhere near, and its reset npc says so.
    /// </summary>
    /// <remarks>
    /// <b>This is the loop closing.</b> The spawner places a spot, the spot places a monster, the
    /// monster leaves a reset npc where it fell, and the reset npc broadcasts this at fifty metres —
    /// which starts the spawner's eight minutes again rather than letting it run out on its own.
    /// Recorded as having no sender in this port until the monster's death ladder was read.
    /// </remarks>
    public void OnNpcMessage(Npc sender, int messageType, VisibleObject? param)
    {
        if (messageType != TimeReset)
            return;

        CancelCycle();
        ArmCycle();
    }
}

/// <summary>
/// The conquest time-reset npc (856502). Retail pattern <c>F4_Rotation_Fixed_Portal</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Left where a conquest monster falls, it broadcasts
/// <c>13929</c> at fifty metres on waking and every six seconds after — which re-arms the spawner whose
/// spot placed that monster. <b>Nothing in this port sent that message before</b>, so a spawner's clock
/// simply ran on regardless of what the raid did.
/// </remarks>
[AIName("conquest_offering_time_reset")]
public class ConquestOfferingTimeResetAI : NpcAI
{
    /// <summary>Retail's <c>range_as_meter</c> and its idle delay.</summary>
    private const float Earshot = 50f;
    private const long RepeatMillis = 6_000L;

    private ScheduledTask? beatTask;

    public ConquestOfferingTimeResetAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        Announce();
    }

    private void Announce()
    {
        if (!GetOwner().IsSpawned())
            return;

        NpcMessageBus.Broadcast(GetOwner(), ConquestOfferingSpawnerAI.TimeReset, GetOwner(), Earshot, null);
        beatTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            Announce();
            return ValueTask.CompletedTask;
        }, RepeatMillis);
    }

    private void Stop()
    {
        if (beatTask != null && !beatTask.IsDone())
            beatTask.Cancel(true);
        beatTask = null;
    }

    protected override void HandleDespawned()
    {
        Stop();
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        Stop();
        base.HandleDied();
    }
}

/// <summary>
/// A conquest offering spot: it wakes, rolls once for a monster, and is gone in ten seconds.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>The spot is the stage this port did not have</b> —
/// the spawner used to place a monster directly, so the intermediate npc, its ten seconds and its own
/// roll all went missing together.
/// <para>
/// Retail's weights are 19, 19, 20 and 20 for the four ordinary monsters, then 6, 7 and 7 for the three
/// "All" variants — <b>ninety-eight</b> — with an eighth branch carrying no probability at all and
/// taking the remaining two per cent. All forty-eight spots share those weights exactly.
/// </para>
/// </remarks>
[AIName("conquest_offering_spot")]
public class ConquestOfferingSpotAI : NpcAI
{
    /// <summary>Retail's weights, in the order its branches are written. The last is the remainder.</summary>
    /// <summary>What each spot rolls when it wakes, in retail's own order.</summary>
    private static readonly Dictionary<int, int[]> Monsters =
        new Dictionary<int, int[]>
        {
            [856314] = [236307, 236308, 236309, 236310, 236331, 236332, 236333, 236334],
            [856315] = [236311, 236312, 236313, 236314, 236331, 236332, 236333, 236334],
            [856316] = [236315, 236316, 236317, 236318, 236331, 236332, 236333, 236334],
            [856317] = [236319, 236320, 236321, 236322, 236331, 236332, 236333, 236334],
            [856318] = [236323, 236324, 236325, 236326, 236331, 236332, 236333, 236334],
            [856319] = [236327, 236328, 236329, 236330, 236331, 236332, 236333, 236334],
            [856320] = [236335, 236336, 236337, 236338, 236359, 236360, 236361, 236362],
            [856321] = [236339, 236340, 236341, 236342, 236359, 236360, 236361, 236362],
            [856322] = [236343, 236344, 236345, 236346, 236359, 236360, 236361, 236362],
            [856323] = [236347, 236348, 236349, 236350, 236359, 236360, 236361, 236362],
            [856324] = [236351, 236352, 236353, 236354, 236359, 236360, 236361, 236362],
            [856325] = [236355, 236356, 236357, 236358, 236359, 236360, 236361, 236362],
            [856326] = [236363, 236364, 236365, 236366, 236387, 236388, 236389, 236390],
            [856327] = [236367, 236368, 236369, 236370, 236387, 236388, 236389, 236390],
            [856328] = [236371, 236372, 236373, 236374, 236387, 236388, 236389, 236390],
            [856329] = [236375, 236376, 236377, 236378, 236387, 236388, 236389, 236390],
            [856330] = [236379, 236380, 236381, 236382, 236387, 236388, 236389, 236390],
            [856331] = [236383, 236384, 236385, 236386, 236387, 236388, 236389, 236390],
            [856332] = [236391, 236392, 236393, 236394, 236415, 236416, 236417, 236418],
            [856333] = [236395, 236396, 236397, 236398, 236415, 236416, 236417, 236418],
            [856334] = [236399, 236400, 236401, 236402, 236415, 236416, 236417, 236418],
            [856335] = [236403, 236404, 236405, 236406, 236415, 236416, 236417, 236418],
            [856336] = [236407, 236408, 236409, 236410, 236415, 236416, 236417, 236418],
            [856337] = [236411, 236412, 236413, 236414, 236415, 236416, 236417, 236418],
            [856354] = [236530, 236531, 236532, 236533, 236554, 236555, 236556, 236557],
            [856355] = [236534, 236535, 236536, 236537, 236554, 236555, 236556, 236557],
            [856356] = [236538, 236539, 236540, 236541, 236554, 236555, 236556, 236557],
            [856357] = [236542, 236543, 236544, 236545, 236554, 236555, 236556, 236557],
            [856358] = [236546, 236547, 236548, 236549, 236554, 236555, 236556, 236557],
            [856359] = [236550, 236551, 236552, 236553, 236554, 236555, 236556, 236557],
            [856360] = [236558, 236559, 236560, 236561, 236582, 236583, 236584, 236585],
            [856361] = [236562, 236563, 236564, 236565, 236582, 236583, 236584, 236585],
            [856362] = [236566, 236567, 236568, 236569, 236582, 236583, 236584, 236585],
            [856363] = [236570, 236571, 236572, 236573, 236582, 236583, 236584, 236585],
            [856364] = [236574, 236575, 236576, 236577, 236582, 236583, 236584, 236585],
            [856365] = [236578, 236579, 236580, 236581, 236582, 236583, 236584, 236585],
            [856366] = [236586, 236587, 236588, 236589, 236610, 236611, 236612, 236613],
            [856367] = [236590, 236591, 236592, 236593, 236610, 236611, 236612, 236613],
            [856368] = [236594, 236595, 236596, 236597, 236610, 236611, 236612, 236613],
            [856369] = [236598, 236599, 236600, 236601, 236610, 236611, 236612, 236613],
            [856370] = [236602, 236603, 236604, 236605, 236610, 236611, 236612, 236613],
            [856371] = [236606, 236607, 236608, 236609, 236610, 236611, 236612, 236613],
            [856372] = [236614, 236615, 236616, 236617, 236638, 236639, 236640, 236641],
            [856373] = [236618, 236619, 236620, 236621, 236638, 236639, 236640, 236641],
            [856374] = [236622, 236623, 236624, 236625, 236638, 236639, 236640, 236641],
            [856375] = [236626, 236627, 236628, 236629, 236638, 236639, 236640, 236641],
            [856376] = [236630, 236631, 236632, 236633, 236638, 236639, 236640, 236641],
            [856377] = [236634, 236635, 236636, 236637, 236638, 236639, 236640, 236641],
        };

    /// <summary>Retail's weights, in the order its branches are written. The last is the remainder.</summary>
    private static readonly int[] Weights = [19, 19, 20, 20, 6, 7, 7];

    public ConquestOfferingSpotAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        if (!Monsters.TryGetValue(GetNpcId(), out int[]? table))
            return;

        int roll = Rnd.NextInt(100);
        int running = 0;
        for (int i = 0; i < Weights.Length; i++)
        {
            running += Weights[i];
            if (roll < running)
            {
                Place(table[i]);
                return;
            }
        }

        // The eighth branch, which retail writes without a probability.
        Place(table[Weights.Length]);
    }

    // A block body rather than an expression one, so the mutation harness can blank it: it replaces a
    // statement with an empty block, which is not valid after `=>`.
    private void Place(int npcId)
    {
        Spawn(npcId, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(),
            (sbyte)GetOwner().GetHeading());
    }
}
