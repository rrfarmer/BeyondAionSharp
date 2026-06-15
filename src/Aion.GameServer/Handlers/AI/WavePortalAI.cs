using System.Collections.Generic;

using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/drakenspire/WavePortalAI (@author Estrayl).</summary>
[AIName("wave_portal")]
public class WavePortalAI : NoActionAI
{
    private readonly List<ScheduledTask> tasks = new List<ScheduledTask>();

    public WavePortalAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        int staticId = GetOwner().GetSpawn().GetStaticId();
        tasks.Add(ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct =>
        {
            switch (staticId)
            {
                case 84: // Top Left
                    Spawn(236204, 581.92f, 823.65f, 1609.64f, (sbyte)15, "301390000_Wave_Top_Left_01");
                    Spawn(236204, 581.92f, 823.65f, 1609.64f, (sbyte)15, "301390000_Wave_Top_Left_02");
                    break;
                case 405: // Central
                    Spawn(Rnd.Get(236216, 236220), 635.17f, 811.90f, 1598.50f, (sbyte)30, "301390000_Wave_Commander_Left", 1000);
                    Spawn(Rnd.Get(236216, 236220), 635.17f, 811.90f, 1598.50f, (sbyte)30, "301390000_Wave_Commander_Middle", 0);
                    Spawn(Rnd.Get(236216, 236220), 635.17f, 811.90f, 1598.50f, (sbyte)30, "301390000_Wave_Commander_Right", 1000);
                    break;
                case 548: // Bottom Left
                    Spawn(236204, 687.29f, 825.78f, 1609.66f, (sbyte)45, "301390000_Wave_Bottom_Left_01");
                    Spawn(236204, 687.29f, 825.78f, 1609.66f, (sbyte)45, "301390000_Wave_Bottom_Left_02");
                    break;
                case 398: // Bottom Central
                    Spawn(236205, 704.21f, 877.60f, 1604.55f, (sbyte)60, "301390000_Wave_Bottom_Central_01");
                    Spawn(236205, 704.21f, 877.60f, 1604.55f, (sbyte)60, "301390000_Wave_Bottom_Central_02");
                    break;
                case 401: // Top Central
                    Spawn(236205, 575.15f, 877.52f, 1600.89f, (sbyte)0, "301390000_Wave_Top_Central_01");
                    Spawn(236205, 575.15f, 877.52f, 1600.89f, (sbyte)0, "301390000_Wave_Top_Central_02");
                    break;
                case 399: // Bottom Right
                    Spawn(236206, 690.59f, 932.85f, 1618.27f, (sbyte)75, "301390000_Wave_Bottom_Right_01");
                    Spawn(236206, 690.59f, 932.85f, 1618.27f, (sbyte)75, "301390000_Wave_Bottom_Right_02");
                    break;
                case 407: // Top Right
                    Spawn(236206, 576.92f, 936.11f, 1620.33f, (sbyte)104, "301390000_Wave_Top_Right_01");
                    Spawn(236206, 576.92f, 936.11f, 1620.33f, (sbyte)104, "301390000_Wave_Top_Right_02");
                    break;
            }
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(8000), System.TimeSpan.FromMilliseconds(staticId == 405 ? 40000 : 22000)));
    }

    private void Spawn(int npcId, float x, float y, float z, sbyte heading, string walkerId)
    {
        Spawn(npcId, x, y, z, heading, walkerId, 0);
    }

    private void Spawn(int npcId, float x, float y, float z, sbyte heading, string walkerId, int delay)
    {
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            Npc npc = (Npc)Spawn(npcId, x, y, z, heading);
            npc.GetSpawn().SetWalkerId(walkerId);
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, (long)delay));
    }

    protected override void HandleDespawned()
    {
        foreach (ScheduledTask task in tasks)
            if (task != null && !task.IsCancelled)
                task.Cancel(true);
        base.HandleDespawned();
    }
}
