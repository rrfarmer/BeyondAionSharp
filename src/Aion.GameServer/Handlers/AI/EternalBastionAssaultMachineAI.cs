using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/eternalBastion/EternalBastionAssaultMachineAI (Cheatkiller, Estrayl).</summary>
[AIName("eternal_bastion_assault_machine")]
public class EternalBastionAssaultMachineAI : EternalBastionConstructAI
{
    public EternalBastionAssaultMachineAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        SpawnSkillArea();
        ThreadPoolManager.GetInstance().Schedule(_ => { SpawnSummons(); return ValueTask.CompletedTask; }, TimeSpan.FromSeconds(3));
    }

    private void SpawnSkillArea()
    {
        int skillAreaNpcId = GetNpcId() switch
        {
            231140 or 231156 or 231157 or 231158 or 231159 or 231160 or 231162 or 231167 => 284686,
            231141 or 231163 or 231164 or 231165 => 284699,
            _ => 0,
        };
        if (skillAreaNpcId != 0)
            Spawn(skillAreaNpcId, GetPosition().GetX(), GetPosition().GetY(), GetPosition().GetZ(), (sbyte)0);
    }

    private void SpawnSummons()
    {
        switch (GetNpcId())
        {
            case 231140:
                SpawnWithWalker(231106, 633.457f, 245.792f, 238.075f, (sbyte)33, "NPCPathIDLDF5b_TD_Mob_Z1_S3_POD01");
                SpawnWithWalker(231108, 635.457f, 247.792f, 238.075f, (sbyte)33, "NPCPathIDLDF5b_TD_Mob_Z1_S3_POD01");
                SpawnWithWalker(231108, 631.457f, 247.792f, 238.075f, (sbyte)33, "NPCPathIDLDF5b_TD_Mob_Z1_S3_POD01");
                break;
            case 231156:
                SpawnWithWalker(231106, 642.871f, 343.420f, 238.075f, (sbyte)20, "NPCPathIDLDF5b_TD_Z1_S5_POD01");
                SpawnWithWalker(231108, 644.871f, 345.420f, 238.075f, (sbyte)20, "NPCPathIDLDF5b_TD_Z1_S5_POD01");
                SpawnWithWalker(231108, 640.871f, 345.420f, 238.075f, (sbyte)20, "NPCPathIDLDF5b_TD_Z1_S5_POD01");
                break;
            case 231157:
                SpawnWithWalker(231106, 776.242f, 326.041f, 253.434f, (sbyte)40, "NPCPathIDLDF5b_TD_Z4_POD02");
                SpawnWithWalker(231108, 778.242f, 328.041f, 253.434f, (sbyte)40, "NPCPathIDLDF5b_TD_Z4_POD02");
                SpawnWithWalker(231108, 774.242f, 328.041f, 253.434f, (sbyte)40, "NPCPathIDLDF5b_TD_Z4_POD02");
                break;
            case 231158:
                SpawnWithWalker(231106, 765.481f, 393.614f, 243.354f, (sbyte)40, "NPCPathIDLDF5b_TD_Mob_Z1_S3_POD3");
                SpawnWithWalker(231108, 767.481f, 395.614f, 243.354f, (sbyte)40, "NPCPathIDLDF5b_TD_Mob_Z1_S3_POD3");
                SpawnWithWalker(231108, 763.481f, 395.614f, 243.354f, (sbyte)40, "NPCPathIDLDF5b_TD_Mob_Z1_S3_POD3");
                break;
            case 231141:
                SpawnWithWalker(231105, 667.631f, 297.565f, 225.700f, (sbyte)20, "NPCPathIDLDF5b_TD_Mob_Z1_S3_POD2");
                SpawnWithWalker(231107, 669.631f, 299.565f, 225.700f, (sbyte)20, "NPCPathIDLDF5b_TD_Mob_Z1_S3_POD2");
                SpawnWithWalker(231107, 665.631f, 299.565f, 225.700f, (sbyte)20, "NPCPathIDLDF5b_TD_Mob_Z1_S3_POD2");
                break;
            case 231163:
                SpawnWithWalker(231105, 731.089f, 365.461f, 230.941f, (sbyte)7, "NPCPathIDLDF5b_TD_Z1_S5_POD02");
                SpawnWithWalker(231107, 731.089f, 365.461f, 230.941f, (sbyte)7, "NPCPathIDLDF5b_TD_Z1_S5_POD02");
                SpawnWithWalker(231107, 731.089f, 365.461f, 230.941f, (sbyte)7, "NPCPathIDLDF5b_TD_Z1_S5_POD02");
                break;
            case 231159:
                SpawnWithWalker(231106, 699.760f, 302.938f, 249.303f, (sbyte)100, "NPCPathIDLDF5b_TD_Z4_POD01");
                SpawnWithWalker(231108, 701.760f, 304.938f, 249.303f, (sbyte)100, "NPCPathIDLDF5b_TD_Z4_POD01");
                SpawnWithWalker(231108, 697.760f, 304.938f, 249.303f, (sbyte)100, "NPCPathIDLDF5b_TD_Z4_POD01");
                break;
            case 231164:
            case 231165:
                SpawnWithWalker(231106, 724.927f, 359.346f, 230.941f, (sbyte)0, "NPCPathIDLDF5b_TD_Z3_POD02");
                SpawnWithWalker(231108, 726.927f, 361.346f, 230.941f, (sbyte)0, "NPCPathIDLDF5b_TD_Z3_POD02");
                SpawnWithWalker(231108, 722.927f, 361.346f, 230.941f, (sbyte)0, "NPCPathIDLDF5b_TD_Z3_POD02");
                break;
            case 231160:
                SpawnWithWalker(230749, 705.203f, 261.673f, 252.434f, (sbyte)40, "NPCPathIDLDF5b_TD_Mob_Z2_POD01");
                SpawnWithWalker(230745, 708.203f, 264.673f, 252.434f, (sbyte)40, "NPCPathIDLDF5b_TD_Mob_Z2_POD01");
                SpawnWithWalker(230744, 702.203f, 264.673f, 252.434f, (sbyte)40, "NPCPathIDLDF5b_TD_Mob_Z2_POD01");
                break;
            case 231162:
                SpawnWithWalker(231106, 747.057f, 296.569f, 233.874f, (sbyte)100, "NPCPathIDLDF5b_TD_Mob_S72");
                SpawnWithWalker(231108, 749.057f, 298.569f, 233.771f, (sbyte)100, "NPCPathIDLDF5b_TD_Mob_S72");
                SpawnWithWalker(231108, 745.057f, 298.569f, 233.889f, (sbyte)100, "NPCPathIDLDF5b_TD_Mob_S72");
                break;
            case 231167:
                SpawnWithWalker(231105, 738.529f, 294.467f, 233.889f, (sbyte)100, "NPCPathIDLDF5b_TD_Mob_S42");
                SpawnWithWalker(231107, 740.529f, 296.467f, 233.889f, (sbyte)100, "NPCPathIDLDF5b_TD_Mob_S42");
                SpawnWithWalker(231107, 736.529f, 296.467f, 233.752f, (sbyte)100, "NPCPathIDLDF5b_TD_Mob_S42");
                break;
            // Siege Towers
            case 231143:
                SpawnWithWalker(231107, 623.235f, 263.392f, 238.484f, (sbyte)3, "NPCPathIDLDF5b_TD_Z1_S4_T1");
                SpawnWithWalker(231105, 625.235f, 265.392f, 238.484f, (sbyte)3, "NPCPathIDLDF5b_TD_Z1_S4_T1");
                SpawnWithWalker(231105, 621.235f, 265.392f, 238.484f, (sbyte)3, "NPCPathIDLDF5b_TD_Z1_S4_T1");
                break;
            case 231152:
                SpawnWithWalker(231107, 621.920f, 298.179f, 238.075f, (sbyte)113, "NPCPathIDLDF5b_TD_Z1_S4_T2");
                SpawnWithWalker(231105, 623.920f, 300.179f, 238.075f, (sbyte)113, "NPCPathIDLDF5b_TD_Z1_S4_T2");
                SpawnWithWalker(231105, 619.920f, 300.179f, 238.075f, (sbyte)113, "NPCPathIDLDF5b_TD_Z1_S4_T2");
                break;
            case 231153:
                SpawnWithWalker(231107, 644.089f, 351.522f, 239.764f, (sbyte)113, "NPCPathIDLDF5b_TD_Z1_S4_T3");
                SpawnWithWalker(231105, 646.089f, 353.522f, 241.151f, (sbyte)113, "NPCPathIDLDF5b_TD_Z1_S4_T3");
                SpawnWithWalker(231105, 642.089f, 353.522f, 239.809f, (sbyte)113, "NPCPathIDLDF5b_TD_Z1_S4_T3");
                break;
            case 231154:
                SpawnWithWalker(231107, 664.091f, 394.303f, 240.223f, (sbyte)83, "NPCPathIDLDF5b_TD_Z1_S4_T4");
                SpawnWithWalker(231105, 666.091f, 396.303f, 240.223f, (sbyte)83, "NPCPathIDLDF5b_TD_Z1_S4_T4");
                SpawnWithWalker(231105, 662.091f, 396.303f, 240.223f, (sbyte)83, "NPCPathIDLDF5b_TD_Z1_S4_T4");
                break;
            case 231155:
                SpawnWithWalker(231107, 692.867f, 396.708f, 241.594f, (sbyte)85, "NPCPathIDLDF5b_TD_Z1_S4_T5");
                SpawnWithWalker(231105, 694.867f, 398.708f, 242.018f, (sbyte)85, "NPCPathIDLDF5b_TD_Z1_S4_T5");
                SpawnWithWalker(231105, 690.867f, 398.708f, 241.594f, (sbyte)85, "NPCPathIDLDF5b_TD_Z1_S4_T5");
                break;
        }
    }

    private void SpawnWithWalker(int npcId, float x, float y, float z, sbyte h, string walker)
    {
        Spawn(npcId, x, y, z, h).GetSpawn().SetWalkerId(walker);
    }
}
