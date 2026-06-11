using System.Collections.Generic;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Event;
using Aion.GameServer.Model.Templates.Event.Upgradearcade;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_UPGRADE_ARCADE (ginho1, Neon, Estrayl). Upgrade-arcade event packet (actions 0-10: icon/start/open/upgrade/success/fail/reward/frenzy/disable/reward-list). 10 ctors. ArcadeProgress/ArcadeRewards/DataManager/EventsConfig red-tolerated.</summary>
public class SM_UPGRADE_ARCADE : AionServerPacket
{
    private readonly int action;
    private ArcadeProgress progress;
    private int sessionId;
    private bool showIcon;
    private bool success;
    private int frenzyDurationSeconds;
    private bool disableWindow;
    private int rewardItemId;
    private long rewardItemCount;
    private List<ArcadeRewards> arcadeRewards;

    public SM_UPGRADE_ARCADE()
    {
        this.action = 2;
    }

    public SM_UPGRADE_ARCADE(bool showIcon)
    {
        this.action = 0;
        this.showIcon = showIcon;
    }

    public SM_UPGRADE_ARCADE(ArcadeProgress progress, int sessionId)
    {
        this.action = 1;
        this.progress = progress;
        this.sessionId = sessionId;
    }

    public SM_UPGRADE_ARCADE(bool success, ArcadeProgress progress)
    {
        this.action = 3;
        this.success = success;
        this.progress = progress;
    }

    public SM_UPGRADE_ARCADE(ArcadeProgress progress)
    {
        this.action = 4;
        this.progress = progress;
    }

    public SM_UPGRADE_ARCADE(ArcadeProgress progress, bool resumeAllowed)
    {
        this.action = 5;
        this.progress = progress;
    }

    public SM_UPGRADE_ARCADE(int itemId, long count)
    {
        this.action = 6;
        this.rewardItemId = itemId;
        this.rewardItemCount = count;
    }

    public SM_UPGRADE_ARCADE(int frenzyDurationSeconds)
    {
        this.action = 7;
        this.frenzyDurationSeconds = frenzyDurationSeconds;
    }

    public SM_UPGRADE_ARCADE(int action, bool disableWindow)
    {
        this.action = action;
        this.disableWindow = disableWindow;
    }

    public SM_UPGRADE_ARCADE(List<ArcadeRewards> rewards)
    {
        this.action = 10;
        this.arcadeRewards = rewards;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(action);

        switch (action)
        {
            case 0:// show icon
                WriteD(showIcon ? 1 : 0);
                break;
            case 1: // show start upgrade arcade info
                WriteD(sessionId);// SessionId
                WriteD(progress.GetFrenzyPoints());// frenzy meter
                foreach (ArcadeRewards arcadeReward in DataManager.UPGRADE_ARCADE_DATA.GetRewards())
                    WriteD(arcadeReward.GetMinLevel());
                WriteD(DataManager.UPGRADE_ARCADE_DATA.GetMaxUpgradeLevel().GetLevel());
                WriteC(1);
                WriteC(DataManager.UPGRADE_ARCADE_DATA.GetUpgradeLevels().Count * 2);
                foreach (ArcadeLevel arcadeLevel in DataManager.UPGRADE_ARCADE_DATA.GetUpgradeLevels())
                    WriteS(arcadeLevel.GetIcon());
                break;
            case 2: // open upgrade arcade
                WriteC(1);// unk
                break;
            case 3: // upgrade start
                WriteC(success ? 1 : 0);// 1 success - 0 fail
                WriteD(progress.GetFrenzyPoints());
                break;
            case 4: // update success
                WriteD(progress.GetCurrentLevel());// upgradeLevel
                break;
            case 5: // upgrade fail
                WriteD(progress.GetCurrentLevel());// upgradeLevel
                WriteC(progress.GetResumeLevel() > 0 ? 1 : 0);// canResume? 1 yes - 0 no
                WriteQ(EventsConfig.ARCADE_RESUME_TOKEN);// needed Arcade Token
                break;
            case 6: // show reward item
                WriteD(rewardItemId);
                WriteQ(rewardItemCount);
                break;
            case 7: // frenzy time
                WriteD(frenzyDurationSeconds);
                break;
            case 8: // disable window
                WriteC(disableWindow ? 1 : 0); // msg when true: you don't have enough tokens
                break;
            case 10: // show reward list
                foreach (ArcadeRewards arcadetab in arcadeRewards)
                    WriteC(arcadetab.GetArcadeRewardItems().Count);

                foreach (ArcadeRewards arcadetab in arcadeRewards)
                {
                    foreach (ArcadeRewardItem arcadetabitem in arcadetab.GetArcadeRewardItems())
                    {
                        WriteD(arcadetabitem.GetItemId());
                        WriteQ(arcadetabitem.GetNormalCount());
                        WriteQ(arcadetabitem.GetFrenzyCount());
                    }
                }
                break;
        }
    }
}
