using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/theHexway/CaptainJarkaAI (@author Sykra).</summary>
[AIName("captain_jarka")]
public class CaptainJarkaAI : SummonerAI
{
    public CaptainJarkaAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        // spawn smoke on death
        WorldPosition currentPos = GetPosition();
        Npc smoke = (Npc)Spawn(282465, currentPos.GetX(), currentPos.GetY(), currentPos.GetZ(), (sbyte)0);
        smoke.GetController().Delete();

        // delete all npcs in range
        GetKnownList().ForEachNpc(npc =>
        {
            if (PositionUtil.IsInRange(GetOwner(), npc, 15))
                npc.GetController().Delete();
        });
    }
}
