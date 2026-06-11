using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Model.Instance.Instanceposition;

/// <summary>Java parity: model/instance/instanceposition/ChaosInstancePosition (xTz). Nested position/zone switch→Teleport coordinate table; (byte)h→(sbyte)h; floats verbatim.</summary>
public class ChaosInstancePosition : GeneralInstancePosition
{
    public override void Port(Player player, int zone, int position)
    {
        switch (position) {
            case 1:
                switch (zone) {
                    case 1:
                        Teleport(player, 1936.75f, 943.5f, 222.40979f, (sbyte)0);
                        break;
                    case 2:
                        Teleport(player, 674.22394f, 1771.7374f, 221.87901f, (sbyte)0);
                        break;
                    case 3:
                        Teleport(player, 1905.0468f, 1257.5624f, 288.64221f, (sbyte)0);
                        break;
                    case 4:
                        Teleport(player, 1378.2219f, 1067.3834f, 340.29468f, (sbyte)0);
                        break;
                    case 5:
                        Teleport(player, 685.27051f, 287.09622f, 514.20245f, (sbyte)0);
                        break;
                    case 6:
                        Teleport(player, 1870.8004f, 1700.0099f, 300.64191f, (sbyte)0);
                        break;
                }
                break;
            case 2:
                switch (zone) {
                    case 1:
                        Teleport(player, 1961.5468f, 930.42542f, 222.22997f, (sbyte)0);
                        break;
                    case 2:
                        Teleport(player, 631.9718f, 1777.4031f, 185.03346f, (sbyte)0);
                        break;
                    case 3:
                        Teleport(player, 1905.8645f, 1235.8971f, 288.47339f, (sbyte)0);
                        break;
                    case 4:
                        Teleport(player, 1375.5717f, 1096.2384f, 340.04309f, (sbyte)0);
                        break;
                    case 5:
                        Teleport(player, 680.8587f, 287.26782f, 512.21454f, (sbyte)0);
                        break;
                    case 6:
                        Teleport(player, 1809.6421f, 1704.156f, 300.66394f, (sbyte)0);
                        break;
                }
                break;
            case 3:
                switch (zone) {
                    case 1:
                        Teleport(player, 1936.75f, 962.65997f, 222.54306f, (sbyte)0);
                        break;
                    case 2:
                        Teleport(player, 676.61804f, 1785.7284f, 222.07115f, (sbyte)0);
                        break;
                    case 3:
                        Teleport(player, 1937.2339f, 1221.2653f, 269.76581f, (sbyte)0);
                        break;
                    case 4:
                        Teleport(player, 1300.9225f, 1058.9489f, 340.4682f, (sbyte)0);
                        break;
                    case 5:
                        Teleport(player, 690.13184f, 244.60457f, 514.25085f, (sbyte)0);
                        break;
                    case 6:
                        Teleport(player, 1885.8636f, 1738.2178f, 300.64774f, (sbyte)0);
                        break;
                }
                break;
            case 4:
                switch (zone) {
                    case 1:
                        Teleport(player, 1961.6898f, 943.5f, 222.65001f, (sbyte)0);
                        break;
                    case 2:
                        Teleport(player, 669.04681f, 1769.1235f, 222.166f, (sbyte)0);
                        break;
                    case 3:
                        Teleport(player, 1929.1152f, 1237.0225f, 270.32001f, (sbyte)0);
                        break;
                    case 4:
                        Teleport(player, 1379.5814f, 1076.59f, 340.73944f, (sbyte)0);
                        break;
                    case 5:
                        Teleport(player, 685.57544f, 240.64261f, 513.90967f, (sbyte)0);
                        break;
                    case 6:
                        Teleport(player, 1814.7386f, 1765.3486f, 300.63184f, (sbyte)0);
                        break;
                }
                break;
            case 5:
                switch (zone) {
                    case 1:
                        Teleport(player, 1890.7455f, 1001.882f, 230.59641f, (sbyte)0);
                        break;
                    case 2:
                        Teleport(player, 665.71942f, 1772.1372f, 223.36128f, (sbyte)0);
                        break;
                    case 3:
                        Teleport(player, 1934.9464f, 1263.8007f, 272.70129f, (sbyte)0);
                        break;
                    case 4:
                        Teleport(player, 1302.1024f, 1037.2123f, 340.72864f, (sbyte)0);
                        break;
                    case 5:
                        Teleport(player, 689.70703f, 248.95837f, 514.29877f, (sbyte)0);
                        break;
                    case 6:
                        Teleport(player, 1826.0723f, 1689.4574f, 311.47198f, (sbyte)0);
                        break;
                }
                break;
            case 6:
                switch (zone) {
                    case 1:
                        Teleport(player, 1983.3049f, 1002.3879f, 228.68951f, (sbyte)0);
                        break;
                    case 2:
                        Teleport(player, 720.59412f, 1777.4561f, 174.83665f, (sbyte)0);
                        break;
                    case 3:
                        Teleport(player, 1877.8018f, 1205.6583f, 269.97739f, (sbyte)0);
                        break;
                    case 4:
                        Teleport(player, 1299.4155f, 1048.0157f, 341.02997f, (sbyte)0);
                        break;
                    case 5:
                        Teleport(player, 689.65814f, 239.0927f, 514.0719f, (sbyte)0);
                        break;
                    case 6:
                        Teleport(player, 1860.2178f, 1776.2961f, 311.39948f, (sbyte)0);
                        break;
                }
                break;
            case 7:
                switch (zone) {
                    case 1:
                        Teleport(player, 1936.75f, 930.6394f, 222.22997f, (sbyte)0);
                        break;
                    case 2:
                        Teleport(player, 671.78864f, 1785.5397f, 223.25864f, (sbyte)0);
                        break;
                    case 3:
                        Teleport(player, 1929.8038f, 1157.0305f, 281.04517f, (sbyte)0);
                        break;
                    case 4:
                        Teleport(player, 1378.1992f, 1052.0417f, 339.97583f, (sbyte)0);
                        break;
                    case 5:
                        Teleport(player, 691.20624f, 279.70129f, 514.32678f, (sbyte)0);
                        break;
                    case 6:
                        Teleport(player, 1837.5425f, 1775.2424f, 300.62946f, (sbyte)0);
                        break;
                }
                break;
            case 8:
                switch (zone) {
                    case 1:
                        Teleport(player, 1943.397f, 885.37384f, 231.46317f, (sbyte)0);
                        break;
                    case 2:
                        Teleport(player, 680.71082f, 1784.8301f, 221.63185f, (sbyte)0);
                        break;
                    case 3:
                        Teleport(player, 1960.0089f, 1259.953f, 288.6601f, (sbyte)0);
                        break;
                    case 4:
                        Teleport(player, 1291.4095f, 1088.9802f, 339.99475f, (sbyte)0);
                        break;
                    case 5:
                        Teleport(player, 686.07214f, 245.58424f, 514.08667f, (sbyte)0);
                        break;
                    case 6:
                        Teleport(player, 1799.6967f, 1749.8506f, 311.4411f, (sbyte)0);
                        break;
                }
                break;
            case 9:
                switch (zone) {
                    case 1:
                        Teleport(player, 1961.47f, 962.65704f, 222.57222f, (sbyte)0);
                        break;
                    case 2:
                        Teleport(player, 679.51208f, 1772.2958f, 221.57156f, (sbyte)0);
                        break;
                    case 3:
                        Teleport(player, 1925.5928f, 1224.0499f, 269.80841f, (sbyte)0);
                        break;
                    case 4:
                        Teleport(player, 1379.8954f, 1086.8831f, 340.6073f, (sbyte)0);
                        break;
                    case 5:
                        Teleport(player, 688.94763f, 287.30219f, 514.50201f, (sbyte)0);
                        break;
                    case 6:
                        Teleport(player, 1886.9553f, 1715.6755f, 311.42749f, (sbyte)0);
                        break;
                }
                break;
            case 10:
                switch (zone) {
                    case 1:
                        Teleport(player, 1936.752f, 949.46191f, 222.65584f, (sbyte)0);
                        break;
                    case 2:
                        Teleport(player, 682.93726f, 1777.0594f, 221.07982f, (sbyte)0);
                        break;
                    case 3:
                        Teleport(player, 1940.4576f, 1232.8687f, 270.27747f, (sbyte)0);
                        break;
                    case 4:
                        Teleport(player, 1290.8484f, 1079.3834f, 340.8075f, (sbyte)0);
                        break;
                    case 5:
                        Teleport(player, 690.72253f, 283.63785f, 514.38959f, (sbyte)0);
                        break;
                    case 6:
                        Teleport(player, 1799.9487f, 1727.0955f, 300.6908f, (sbyte)0);
                        break;
                }
                break;
            case 11:
                switch (zone) {
                    case 1:
                        Teleport(player, 2006.7396f, 891.59174f, 230.5414f, (sbyte)0);
                        break;
                    case 2:
                        Teleport(player, 665.55548f, 1776.9498f, 222.78941f, (sbyte)0);
                        break;
                    case 3:
                        Teleport(player, 1978.6932f, 1282.8806f, 286.25754f, (sbyte)0);
                        break;
                    case 4:
                        Teleport(player, 1295.7224f, 1099.4498f, 340.24512f, (sbyte)0);
                        break;
                    case 5:
                        Teleport(player, 685.85437f, 281.09637f, 514.96991f, (sbyte)0);
                        break;
                    case 6:
                        Teleport(player, 1847.9542f, 1689.8381f, 300.81805f, (sbyte)0);
                        break;
                }
                break;
            case 12:
                switch (zone) {
                    case 1:
                        Teleport(player, 1961.7798f, 949.46002f, 222.70183f, (sbyte)0);
                        break;
                    case 2:
                        Teleport(player, 669.14801f, 1781.4385f, 222.5672f, (sbyte)0);
                        break;
                    case 3:
                        Teleport(player, 1989.6035f, 1192.4663f, 273.16217f, (sbyte)0);
                        break;
                    case 4:
                        Teleport(player, 1381.2518f, 1060.0963f, 340.26367f, (sbyte)0);
                        break;
                    case 5:
                        Teleport(player, 680.53888f, 240.78772f, 511.87012f, (sbyte)0);
                        break;
                    case 6:
                        Teleport(player, 1875.7604f, 1761.2601f, 300.68347f, (sbyte)0);
                        break;
                }
                break;
        }
    }
}
