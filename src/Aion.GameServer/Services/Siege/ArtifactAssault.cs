using System;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services.Siege;

/// <summary>Java parity: services/siege/ArtifactAssault (Luzien, Whoop) extends Assault&lt;ArtifactSiege&gt;. Balaur assault on an artifact: handleAssault spawns one assaulter near the boss (level-based npc id), onAssaultFinish announces kill if captured. Math.toRadians->x*PI/180; Rnd.nextFloat->NextFloat; forEachPlayer lambda; switch on boss level. Assault&lt;ArtifactSiege&gt; constraint red-tolerated (invariance-bound erasure).</summary>
public class ArtifactAssault : Assault<ArtifactSiege>
{
    public ArtifactAssault(ArtifactSiege siege)
        : base(siege)
    {
    }

    public override void HandleAssault()
    {
        SpawnAssaulter();
    }

    public override void OnAssaultFinish(bool captured)
    {
        if (captured)
            siegeLocation.ForEachPlayer(p => PacketSendUtility.SendPacket(p, SM_SYSTEM_MESSAGE.STR_ABYSS_DRAGON_BOSS_KILLED(GetBossNpcL10n())));
    }

    private void SpawnAssaulter()
    {
        double angleRadians = Rnd.NextFloat(180f) * Math.PI / 180;
        float x1 = (float)(boss.GetX() + Math.Cos(angleRadians));
        float y1 = (float)(boss.GetY() + Math.Sin(angleRadians));

        SpawnTemplate spawnTemplate = SpawnEngine.NewSiegeSpawn(worldId, GetAssaulterIdByBossLvl(), locationId, SiegeRace.BALAUR, SiegeModType.ASSAULT,
            x1, y1, boss.GetZ(), (byte)0);
        Npc assaulter = (Npc)SpawnEngine.SpawnObject(spawnTemplate, 1);
        assaulter.GetAggroList().AddHate(boss, 1000);
    }

    private int GetAssaulterIdByBossLvl()
    {
        switch (boss.GetLevel())
        {
            case 40:
                return 276719;
            case 50:
                return 277016;
            default:
                return 251463;
        }
    }
}
