using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Ai;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/rakes/BrassEyeGroggetAI (@author xTz).</summary>
[AIName("brasseyegrogget")]
public class BrassEyeGroggetAI : SummonerAI
{
    public BrassEyeGroggetAI(Npc owner)
        : base(owner)
    {
    }

    // todo 4 towers in the room center and fix coordinates of monsters
    // need snif

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
    }

    protected override void HandleIndividualSpawnedSummons(Percentage percent)
    {
        Spawn(percent.GetPercent());
    }

    private void Spawn(int percent)
    {
        int i = 0;
        if (percent < 81 && percent > 60)
        {
            i = 1;
        }
        else if (percent < 61 && percent > 30)
        {
            i = 2;
        }
        else if (percent < 31)
        {
            i = 3;
        }
        int nrSpawn = i;

        // to do move boss to initial position and set pause move and atack
        // after 9 sec first spawn
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            SpawnHelpers1(nrSpawn);
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, 9000L);
    }

    private void SpawnHelpers1(int nrSpawn)
    {
        switch (nrSpawn)
        {
            case 1:
                Spawn(281184, 381.3756f, 495.24835f, 1072.1212f, (sbyte)13);
                Spawn(281181, 379.4199f, 495.36453f, 1072.1212f, (sbyte)13);
                break;
            case 2:
                Spawn(281184, 383.76724f, 527.02856f, 1072.1212f, (sbyte)100);
                Spawn(281181, 381.26767f, 526.40845f, 1072.1212f, (sbyte)100);
                break;
            case 3:
                Spawn(281182, 416.2482f, 500.6516f, 1071.8457f, (sbyte)52);
                Spawn(281182, 415.66647f, 519.5354f, 1071.8457f, (sbyte)52);
                break;
        }

        // next spawn after 35 sec
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            SpawnHelpers2(nrSpawn);
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, 35000L);
    }

    private void SpawnHelpers2(int nrSpawn)
    {
        switch (nrSpawn)
        {
            case 1:
                Spawn(281183, 383.26193f, 528.38403f, 1072.1212f, (sbyte)100);
                Spawn(281184, 383.76724f, 527.02856f, 1072.1212f, (sbyte)100);
                Spawn(281181, 381.26767f, 526.40845f, 1072.1212f, (sbyte)100);
                break;
            case 2:
                Spawn(281182, 429.55338f, 525.7714f, 1075.3801f, (sbyte)62);
                Spawn(281182, 429.52865f, 492.56076f, 1075.3801f, (sbyte)62);
                Spawn(281181, 376.42566f, 502.19736f, 1072.1212f, (sbyte)1);
                break;
            case 3:
                Spawn(281187, 376.42566f, 502.19736f, 1072.1212f, (sbyte)1);
                Spawn(281181, 381.26767f, 526.40845f, 1072.1212f, (sbyte)100);
                break;
        }

        // remove effect after 21 sec
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (GetEffectController().HasAbnormalEffect(18191))
            {
                GetEffectController().RemoveEffect(18191);
            }
            // to do move boss in the room center and remove pause
            // to do some skill boss use
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, 21000L);
    }
}
