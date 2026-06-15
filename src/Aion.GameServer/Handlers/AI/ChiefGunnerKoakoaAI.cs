using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Ai;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/rakes/ChiefGunnerKoakoaAI (@author xTz).</summary>
[AIName("gunnerkoakoa")]
public class ChiefGunnerKoakoaAI : SummonerAI
{
    public ChiefGunnerKoakoaAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleIndividualSpawnedSummons(Percentage percent)
    {
        if (GetEffectController().HasAbnormalEffect(18552))
        {
            CheckAbnormalEffect();
        }
        RandomSpawn(Rnd.Get(1, 3));
    }

    private void CheckAbnormalEffect()
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            GetEffectController().RemoveEffect(18552);
            // to do remove pause
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, 21000L);
    }

    private void RandomSpawn(int i)
    {
        // to do pause boss
        Spawn(281212, 757.39746f, 508.70383f, 1012.30084f, (sbyte)0);
        switch (i)
        {
            case 1:
                Spawn(281212, 726.1167f, 503.28836f, 1012.6846f, (sbyte)0);
                Spawn(281212, 736.4446f, 505.3141f, 1012.1576f, (sbyte)0);
                Spawn(281212, 746.9261f, 503.50122f, 1012.68335f, (sbyte)0);
                Spawn(281212, 728.9705f, 492.59402f, 1012.68335f, (sbyte)0);
                Spawn(281212, 739.9526f, 491.54123f, 1011.692f, (sbyte)0);
                Spawn(281212, 749.754f, 491.74677f, 1011.8663f, (sbyte)0);
                Spawn(281212, 756.9996f, 500.01736f, 1011.692f, (sbyte)0);
                Spawn(281213, 736.9722f, 514.6446f, 1011.8599f, (sbyte)0);
                Spawn(281213, 747.5162f, 514.51715f, 1011.692f, (sbyte)0);
                Spawn(281213, 726.8303f, 514.5155f, 1012.6845f, (sbyte)0);
                Spawn(281213, 727.9019f, 524.578f, 1012.68365f, (sbyte)0);
                Spawn(281213, 738.52844f, 525.0482f, 1011.692f, (sbyte)0);
                Spawn(281213, 758.3127f, 520.59143f, 1011.692f, (sbyte)0);
                Spawn(281213, 748.7474f, 525.84f, 1011.859f, (sbyte)0);
                break;
            case 2:
                Spawn(281213, 726.1167f, 503.28836f, 1012.6846f, (sbyte)0);
                Spawn(281213, 736.4446f, 505.3141f, 1012.1576f, (sbyte)0);
                Spawn(281212, 746.9261f, 503.50122f, 1012.68335f, (sbyte)0);
                Spawn(281213, 728.9705f, 492.59402f, 1012.68335f, (sbyte)0);
                Spawn(281213, 739.9526f, 491.54123f, 1011.692f, (sbyte)0);
                Spawn(281212, 749.754f, 491.74677f, 1011.8663f, (sbyte)0);
                Spawn(281212, 756.9996f, 500.01736f, 1011.692f, (sbyte)0);
                Spawn(281212, 736.9722f, 514.6446f, 1011.8599f, (sbyte)0);
                Spawn(281213, 747.5162f, 514.51715f, 1011.692f, (sbyte)0);
                Spawn(281212, 726.8303f, 514.5155f, 1012.6845f, (sbyte)0);
                Spawn(281212, 727.9019f, 524.578f, 1012.68365f, (sbyte)0);
                Spawn(281212, 738.52844f, 525.0482f, 1011.692f, (sbyte)0);
                Spawn(281213, 758.3127f, 520.59143f, 1011.692f, (sbyte)0);
                Spawn(281213, 748.7474f, 525.84f, 1011.859f, (sbyte)0);
                break;
            case 3:
                Spawn(281212, 726.1167f, 503.28836f, 1012.6846f, (sbyte)0);
                Spawn(281212, 736.4446f, 505.3141f, 1012.1576f, (sbyte)0);
                Spawn(281213, 746.9261f, 503.50122f, 1012.68335f, (sbyte)0);
                Spawn(281212, 728.9705f, 492.59402f, 1012.68335f, (sbyte)0);
                Spawn(281212, 739.9526f, 491.54123f, 1011.692f, (sbyte)0);
                Spawn(281213, 749.754f, 491.74677f, 1011.8663f, (sbyte)0);
                Spawn(281213, 756.9996f, 500.01736f, 1011.692f, (sbyte)0);
                Spawn(281213, 736.9722f, 514.6446f, 1011.8599f, (sbyte)0);
                Spawn(281212, 747.5162f, 514.51715f, 1011.692f, (sbyte)0);
                Spawn(281213, 726.8303f, 514.5155f, 1012.6845f, (sbyte)0);
                Spawn(281213, 727.9019f, 524.578f, 1012.68365f, (sbyte)0);
                Spawn(281213, 738.52844f, 525.0482f, 1011.692f, (sbyte)0);
                Spawn(281212, 758.3127f, 520.59143f, 1011.692f, (sbyte)0);
                Spawn(281212, 748.7474f, 525.84f, 1011.859f, (sbyte)0);
                break;
        }
    }
}
