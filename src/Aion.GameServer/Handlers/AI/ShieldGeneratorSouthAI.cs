using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/illuminaryObelisk/ShieldGeneratorSouthAI (@author Estrayl).</summary>
[AIName("southern_generator")]
public class ShieldGeneratorSouthAI : ShieldGeneratorAI
{
    public ShieldGeneratorSouthAI(Npc owner)
        : base(owner)
    {
    }

    protected override SM_SYSTEM_MESSAGE GetAttackMsg()
    {
        return SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_DEFENCE_03_ATTACKED();
    }

    protected override SM_SYSTEM_MESSAGE GetChargeMsg()
    {
        return SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_OBJ_CHARGE_03();
    }

    protected override SM_SYSTEM_MESSAGE GetDestructionMsg()
    {
        return SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_OBJ_DESTROY_03();
    }

    protected override SM_SYSTEM_MESSAGE GetGateMsg()
    {
        return SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_N_WAVE_03_BEGIN();
    }

    protected override void HandleChargeComplete()
    {
        charges.Add((Npc)Spawn(702224 + chargeCount, 298.304f, 254.48207f, 295.95157f, (sbyte)0));
    }

    protected override void HandleVortexSpawn()
    {
        Spawn(702016, 343.1202f, 254.10585f, 291.62302f, (sbyte)0, 34); // south
        Shout(SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_N_WAVE_01_BEGIN());
    }

    protected override void HandleVortexDespawn()
    {
        foreach (Npc npc in GetPosition().GetWorldMapInstance().GetNpcs(702016))
        {
            npc.GetController().Delete();
        }
    }
}
