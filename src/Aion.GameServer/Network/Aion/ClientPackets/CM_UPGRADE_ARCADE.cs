using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_UPGRADE_ARCADE (ginho1). Upgrade-arcade event actions (start/open/try/reward/resume/reward-list). UpgradeArcadeService red-tolerated.</summary>
public class CM_UPGRADE_ARCADE : AionClientPacket
{
    private byte action;
    private int sessionId;

    public CM_UPGRADE_ARCADE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        action = ReadC();
        sessionId = ReadD();
    }

    protected override void RunImpl()
    {
        if (!EventsConfig.ENABLE_EVENT_ARCADE)
            return;
        Player player = GetConnection().GetActivePlayer();
        switch (action)
        {
            case 0:// get start upgrade arcade info
                UpgradeArcadeService.GetInstance().Start(player, sessionId);
                break;
            case 1:// open upgrade arcade
                UpgradeArcadeService.GetInstance().Open(player);
                break;
            case 2:// try upgrade arcade
                UpgradeArcadeService.GetInstance().StartTry(player);
                break;
            case 3:// get reward
                UpgradeArcadeService.GetInstance().GetReward(player);
                break;
            case 4:// resume upgrade arcade
                UpgradeArcadeService.GetInstance().Resume(player);
                break;
            case 5:// get reward list
                UpgradeArcadeService.GetInstance().ShowRewardList(player);
                break;
            default:
                NullLoggerFactory.Instance.CreateLogger(nameof(CM_UPGRADE_ARCADE)).LogWarning("Unhandled arcade action " + action);
                break;
        }
    }
}
