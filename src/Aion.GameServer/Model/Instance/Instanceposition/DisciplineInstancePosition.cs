using Aion.GameServer.Model.GameObjects.Player;

namespace Aion.GameServer.Model.Instance.Instanceposition;

/// <summary>Java parity: model/instance/instanceposition/DisciplineInstancePosition (xTz). Nested position/zone switch; (byte)h→(sbyte)h.</summary>
public class DisciplineInstancePosition : GeneralInstancePosition
{
    public override void Port(Player player, int zone, int position)
    {
        switch (position)
        {
            case 1:
                switch (zone)
                {
                    case 1:
                        Teleport(player, 1841.294f, 1041.223f, 338.20056f, (sbyte)15);
                        break;
                    case 2:
                        Teleport(player, 278.18478f, 1265.8389f, 263.1712f, (sbyte)73);
                        break;
                    case 3:
                        Teleport(player, 709.78845f, 1766.1855f, 183.43953f, (sbyte)60);
                        break;
                    case 4:
                        Teleport(player, 1817.1067f, 1737.4899f, 311.49692f, (sbyte)1);
                        break;
                }
                break;
            case 2:
                switch (zone)
                {
                    case 1:
                        Teleport(player, 1869.4803f, 1041.8444f, 337.9918f, (sbyte)43);
                        break;
                    case 2:
                        Teleport(player, 251.03516f, 1297.7039f, 248.11426f, (sbyte)105);
                        break;
                    case 3:
                        Teleport(player, 693.93176f, 1761.0234f, 196.12753f, (sbyte)21);
                        break;
                    case 4:
                        Teleport(player, 1851.6932f, 1765.4813f, 305.23187f, (sbyte)90);
                        break;
                }
                break;
            case 3:
                switch (zone)
                {
                    case 1:
                        Teleport(player, 1869.0569f, 1069.1344f, 337.6657f, (sbyte)71);
                        break;
                    case 2:
                        Teleport(player, 315.8269f, 1221.0648f, 263.4517f, (sbyte)51);
                        break;
                    case 3:
                        Teleport(player, 686.09247f, 1756.8987f, 163.4386f, (sbyte)25);
                        break;
                    case 4:
                        Teleport(player, 1851.7856f, 1709.3085f, 305.23566f, (sbyte)31);
                        break;
                }
                break;
            case 4:
                switch (zone)
                {
                    case 1:
                        Teleport(player, 1841.7906f, 1069.6471f, 338.10706f, (sbyte)107);
                        break;
                    case 2:
                        Teleport(player, 346.1267f, 1185.1802f, 244.43742f, (sbyte)44);
                        break;
                    case 3:
                        Teleport(player, 693.11945f, 1771.6886f, 236.5583f, (sbyte)17);
                        break;
                    case 4:
                        Teleport(player, 1887.0206f, 1737.6492f, 311.49692f, (sbyte)62);
                        break;
                }
                break;
        }
    }
}
