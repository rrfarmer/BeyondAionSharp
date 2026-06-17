using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/HaramelInstance (Undertrey) : GeneralInstanceHandler. @InstanceID(300200000); getMostPlayerDamage/getController().delete()/spawn/sendMsg 1:1; PlayerClass switch fall-through preserved.</summary>
[InstanceID(300200000)]
public class HaramelInstance : GeneralInstanceHandler
{
    public HaramelInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnDie(Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 216922:
                Player player = npc.GetAggroList().GetMostPlayerDamage();
                npc.GetController().Delete();
                if (player == null)
                    return;
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDNOVICE_HAMEROON_TREASUREBOX_SPAWN());
                PacketSendUtility.SendPacket(player, new SM_PLAY_MOVIE(false, 0, 0, 457, true));
                switch (player.GetPlayerClass())
                {
                    case PlayerClass.GLADIATOR:
                    case PlayerClass.TEMPLAR:
                        Spawn(700829, 224.137f, 268.608f, 144.898f, (byte)90); // chest warrior
                        break;
                    case PlayerClass.ASSASSIN:
                    case PlayerClass.RANGER:
                    case PlayerClass.GUNNER:
                        Spawn(700830, 224.137f, 268.608f, 144.898f, (byte)90); // chest scout
                        break;
                    case PlayerClass.BARD:
                    case PlayerClass.SORCERER:
                    case PlayerClass.SPIRIT_MASTER:
                        Spawn(700831, 224.137f, 268.608f, 144.898f, (byte)90); // chest mage
                        break;
                    case PlayerClass.CLERIC:
                    case PlayerClass.CHANTER:
                    case PlayerClass.RIDER:
                        Spawn(700832, 224.137f, 268.608f, 144.898f, (byte)90); // chest cleric
                        break;
                }
                Spawn(700852, 224.5984f, 331.1431f, 141.8925f, (byte)90); // spawn opened dimensional gate
                break;
            case 216920: // Brainwashed Dukaki Weakarm
            case 216921: // Brainwashed Dukaki Peon
            case 217067: // Brainwashed MuMu Worker
            case 700950: // Aether Cart
                npc.GetController().Delete();
                break;
        }
    }
}
