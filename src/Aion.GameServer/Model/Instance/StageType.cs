using System.Collections.Generic;

namespace Aion.GameServer.Model.Instance;

/// <summary>Java parity: model/instance/StageType (xTz). Enum w/(id,type) per member→enum + StageTypeExtensions (static data dict; ids repeat across members, fine for dict). getType→GetType_() (avoids Object.GetType clash).</summary>
public enum StageType
{
    DEFAULT,
    START_STAGE_1_ELEVATOR, START_STAGE_1_ROUND_1, START_STAGE_1_ROUND_2, START_STAGE_1_ROUND_3, START_STAGE_1_ROUND_4, START_STAGE_1_ROUND_5,
    START_STAGE_2_ELEVATOR, START_STAGE_2_ROUND_1, START_STAGE_2_ROUND_2, START_STAGE_2_ROUND_3, START_STAGE_2_ROUND_4, START_STAGE_2_ROUND_5,
    START_STAGE_3_ELEVATOR, START_STAGE_3_ROUND_1, START_STAGE_3_ROUND_2, START_STAGE_3_ROUND_3, START_STAGE_3_ROUND_4, START_STAGE_3_ROUND_5,
    START_STAGE_4_ELEVATOR, START_STAGE_4_ROUND_1, START_ALTERNATIVE_STAGE_4_ROUND_1, START_STAGE_4_ROUND_2, START_STAGE_4_ROUND_3, START_STAGE_4_ROUND_4, START_STAGE_4_ROUND_5,
    START_STAGE_5, START_STAGE_5_ROUND_1, START_STAGE_5_ROUND_2, START_STAGE_5_ROUND_3, START_STAGE_5_ROUND_4, START_STAGE_5_ROUND_5,
    START_STAGE_6, START_STAGE_6_ROUND_1, START_STAGE_6_ROUND_2, START_STAGE_6_ROUND_3, START_STAGE_6_ROUND_4, START_STAGE_6_ROUND_5,
    START_STAGE_7, START_STAGE_7_ROUND_1, START_STAGE_7_ROUND_2, START_STAGE_7_ROUND_3, START_STAGE_7_ROUND_4, START_STAGE_7_ROUND_5,
    START_STAGE_8, START_STAGE_8_ROUND_1, START_STAGE_8_ROUND_2, START_STAGE_8_ROUND_3, START_STAGE_8_ROUND_4, START_STAGE_8_ROUND_5,
    START_STAGE_9, START_STAGE_9_ROUND_1, START_STAGE_9_ROUND_2, START_STAGE_9_ROUND_3, START_STAGE_9_ROUND_4, START_STAGE_9_ROUND_5,
    START_STAGE_10, START_STAGE_10_ROUND_1, START_STAGE_10_ROUND_2, START_STAGE_10_ROUND_3, START_STAGE_10_ROUND_4, START_STAGE_10_ROUND_5,
    PASS_STAGE_1, PASS_STAGE_2, PASS_STAGE_4, PASS_STAGE_5, PASS_STAGE_6,
    PASS_GROUP_STAGE_1, PASS_GROUP_STAGE_2, PASS_GROUP_STAGE_3, PASS_GROUP_STAGE_4, PASS_GROUP_STAGE_5, PASS_GROUP_STAGE_6, PASS_GROUP_STAGE_7, PASS_GROUP_STAGE_8, PASS_GROUP_STAGE_9, PASS_GROUP_STAGE_10,
    START_BONUS_STAGE_2, START_BONUS_STAGE_3, START_BONUS_STAGE_4, START_BONUS_STAGE_6,
    PVP_STAGE_1, PVP_STAGE_2, PVP_STAGE_3, PVP_STAGE_4, PVP_STAGE_5, PVP_STAGE_6, PVP_STAGE_OVER
}

public static class StageTypeExtensions
{
    private readonly struct StageData
    {
        public readonly int Id;
        public readonly int Type;

        public StageData(int id, int type)
        {
            Id = id;
            Type = type;
        }
    }

    private static readonly Dictionary<StageType, StageData> data = new()
    {
        [StageType.DEFAULT] = new StageData(0, 0), // 34464
        [StageType.START_STAGE_1_ELEVATOR] = new StageData(35464, 1),
        [StageType.START_STAGE_1_ROUND_1] = new StageData(35465, 1),
        [StageType.START_STAGE_1_ROUND_2] = new StageData(35466, 1),
        [StageType.START_STAGE_1_ROUND_3] = new StageData(35467, 1),
        [StageType.START_STAGE_1_ROUND_4] = new StageData(35468, 1),
        [StageType.START_STAGE_1_ROUND_5] = new StageData(35469, 1),
        [StageType.START_STAGE_2_ELEVATOR] = new StageData(36464, 1),
        [StageType.START_STAGE_2_ROUND_1] = new StageData(36465, 1),
        [StageType.START_STAGE_2_ROUND_2] = new StageData(36466, 1),
        [StageType.START_STAGE_2_ROUND_3] = new StageData(36467, 1),
        [StageType.START_STAGE_2_ROUND_4] = new StageData(36468, 1),
        [StageType.START_STAGE_2_ROUND_5] = new StageData(36469, 1),
        [StageType.START_STAGE_3_ELEVATOR] = new StageData(37464, 1),
        [StageType.START_STAGE_3_ROUND_1] = new StageData(37465, 1),
        [StageType.START_STAGE_3_ROUND_2] = new StageData(37466, 1),
        [StageType.START_STAGE_3_ROUND_3] = new StageData(37467, 1),
        [StageType.START_STAGE_3_ROUND_4] = new StageData(37468, 1),
        [StageType.START_STAGE_3_ROUND_5] = new StageData(37469, 1),
        [StageType.START_STAGE_4_ELEVATOR] = new StageData(38464, 1),
        [StageType.START_STAGE_4_ROUND_1] = new StageData(38465, 1),
        [StageType.START_ALTERNATIVE_STAGE_4_ROUND_1] = new StageData(38465, 1),
        [StageType.START_STAGE_4_ROUND_2] = new StageData(38466, 1),
        [StageType.START_STAGE_4_ROUND_3] = new StageData(38467, 1),
        [StageType.START_STAGE_4_ROUND_4] = new StageData(38468, 1),
        [StageType.START_STAGE_4_ROUND_5] = new StageData(38469, 1),
        [StageType.START_STAGE_5] = new StageData(8392, 3),
        [StageType.START_STAGE_5_ROUND_1] = new StageData(8393, 3),
        [StageType.START_STAGE_5_ROUND_2] = new StageData(8394, 3),
        [StageType.START_STAGE_5_ROUND_3] = new StageData(8395, 3),
        [StageType.START_STAGE_5_ROUND_4] = new StageData(8396, 3),
        [StageType.START_STAGE_5_ROUND_5] = new StageData(8397, 3),
        [StageType.START_STAGE_6] = new StageData(43856, 4),
        [StageType.START_STAGE_6_ROUND_1] = new StageData(43857, 4),
        [StageType.START_STAGE_6_ROUND_2] = new StageData(43858, 4),
        [StageType.START_STAGE_6_ROUND_3] = new StageData(43859, 4),
        [StageType.START_STAGE_6_ROUND_4] = new StageData(43860, 4),
        [StageType.START_STAGE_6_ROUND_5] = new StageData(43861, 4),
        [StageType.START_STAGE_7] = new StageData(13784, 6),
        [StageType.START_STAGE_7_ROUND_1] = new StageData(13785, 6),
        [StageType.START_STAGE_7_ROUND_2] = new StageData(13786, 6),
        [StageType.START_STAGE_7_ROUND_3] = new StageData(13787, 6),
        [StageType.START_STAGE_7_ROUND_4] = new StageData(13788, 6),
        [StageType.START_STAGE_7_ROUND_5] = new StageData(13789, 6),
        [StageType.START_STAGE_8] = new StageData(49248, 7),
        [StageType.START_STAGE_8_ROUND_1] = new StageData(49249, 7),
        [StageType.START_STAGE_8_ROUND_2] = new StageData(49250, 7),
        [StageType.START_STAGE_8_ROUND_3] = new StageData(49251, 7),
        [StageType.START_STAGE_8_ROUND_4] = new StageData(49252, 7),
        [StageType.START_STAGE_8_ROUND_5] = new StageData(49253, 7),
        [StageType.START_STAGE_9] = new StageData(19176, 9),
        [StageType.START_STAGE_9_ROUND_1] = new StageData(19177, 9),
        [StageType.START_STAGE_9_ROUND_2] = new StageData(19178, 9),
        [StageType.START_STAGE_9_ROUND_3] = new StageData(19179, 9),
        [StageType.START_STAGE_9_ROUND_4] = new StageData(19180, 9),
        [StageType.START_STAGE_9_ROUND_5] = new StageData(19181, 9),
        [StageType.START_STAGE_10] = new StageData(54640, 10),
        [StageType.START_STAGE_10_ROUND_1] = new StageData(54641, 10),
        [StageType.START_STAGE_10_ROUND_2] = new StageData(54642, 10),
        [StageType.START_STAGE_10_ROUND_3] = new StageData(54643, 10),
        [StageType.START_STAGE_10_ROUND_4] = new StageData(54644, 10),
        [StageType.START_STAGE_10_ROUND_5] = new StageData(54645, 10),
        [StageType.PASS_STAGE_1] = new StageData(35566, 1),
        [StageType.PASS_STAGE_2] = new StageData(36565, 1),
        [StageType.PASS_STAGE_4] = new StageData(38566, 1),
        [StageType.PASS_STAGE_5] = new StageData(39566, 1),
        [StageType.PASS_STAGE_6] = new StageData(40565, 1),
        [StageType.PASS_GROUP_STAGE_1] = new StageData(35569, 1),
        [StageType.PASS_GROUP_STAGE_2] = new StageData(36569, 1),
        [StageType.PASS_GROUP_STAGE_3] = new StageData(37569, 1),
        [StageType.PASS_GROUP_STAGE_4] = new StageData(38569, 1),
        [StageType.PASS_GROUP_STAGE_5] = new StageData(8497, 3),
        [StageType.PASS_GROUP_STAGE_6] = new StageData(43961, 4),
        [StageType.PASS_GROUP_STAGE_7] = new StageData(13789, 6),
        [StageType.PASS_GROUP_STAGE_8] = new StageData(49253, 7),
        [StageType.PASS_GROUP_STAGE_9] = new StageData(19181, 9),
        [StageType.PASS_GROUP_STAGE_10] = new StageData(54645, 10),
        [StageType.START_BONUS_STAGE_2] = new StageData(36470, 1),
        [StageType.START_BONUS_STAGE_3] = new StageData(37470, 1),
        [StageType.START_BONUS_STAGE_4] = new StageData(38470, 1),
        [StageType.START_BONUS_STAGE_6] = new StageData(43862, 4),
        [StageType.PVP_STAGE_1] = new StageData(1, 0),
        [StageType.PVP_STAGE_2] = new StageData(2, 0),
        [StageType.PVP_STAGE_3] = new StageData(3, 0),
        [StageType.PVP_STAGE_4] = new StageData(4, 0),
        [StageType.PVP_STAGE_5] = new StageData(5, 0),
        [StageType.PVP_STAGE_6] = new StageData(6, 0),
        [StageType.PVP_STAGE_OVER] = new StageData(0, 0),
    };

    public static int GetId(this StageType self)
    {
        return data[self].Id;
    }

    public static int GetType_(this StageType self)
    {
        return data[self].Type;
    }
}
