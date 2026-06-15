using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/darkPoeta/BalaurBarricadeAI (Ritsu, Estrayl).</summary>
[AIName("balaurbarricade")]
public class BalaurBarricadeAI : OneDmgNoActionAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(50, 10);

    public BalaurBarricadeAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        switch (phaseHpPercent)
        {
            case 50:
                SpawnProtectors(true);
                break;
            case 10:
                SpawnProtectors(false);
                break;
        }
    }

    private void SpawnProtectors(bool isFirstSpawn)
    {
        switch (GetNpcId())
        {
            case 700517:
                Spawn(isFirstSpawn ? 215262 : 214883, 282.2922f, 1003.0374f, 113.1999f, (sbyte)25);
                Spawn(isFirstSpawn ? 215262 : 215263, 289.5031f, 1000.1637f, 112.9796f, (sbyte)25);
                break;
            case 700556:
                Spawn(isFirstSpawn ? 215262 : 214883, 315.8379f, 982.8948f, 111.0691f, (sbyte)17);
                Spawn(isFirstSpawn ? 215262 : 215263, 309.0993f, 989.5142f, 112.6760f, (sbyte)17);
                break;
            case 700558:
                Spawn(isFirstSpawn ? 215262 : 214883, 199.7505f, 843.6876f, 100.6562f, (sbyte)59);
                Spawn(isFirstSpawn ? 215262 : 215263, 201.9819f, 853.4918f, 101.0603f, (sbyte)59);
                break;
        }
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        hpPhases.Reset();
    }
}
