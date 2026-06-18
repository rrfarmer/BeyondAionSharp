using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.Autogroup;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Instance (ViAl, Estrayl).</summary>
public class Instance : AdminCommand
{
    public Instance()
        : base("instance", "Activates or deactivates registration for pvp instances.")
    {
        SetSyntaxInfo(
            "<open|close> dredgion - Opens/closes the registration for Dredgion (6vs6)",
            "<open|close> id - Opens/closes the registration for Idgel Dome (6vs6)",
            "<open|close> eob - Opens/closes the registration for Engulfed Ophidan Bridge (6vs6)",
            "<open|close> kb - Opens/closes the registration for Kamar Battlefield (12vs12)",
            "<open|close> iww - Opens/closes the registration for Iron Wall Warfront (24vs24)"
        );
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length < 2)
        {
            SendInfo(player);
            return;
        }
        if (paramsArr[0].Equals("open", System.StringComparison.OrdinalIgnoreCase))
            OpenRegistration(player, paramsArr[1]);
        else if (paramsArr[0].Equals("close", System.StringComparison.OrdinalIgnoreCase))
            CloseRegistration(player, paramsArr[1]);
    }

    private void OpenRegistration(Player admin, string instanceName)
    {
        SM_SYSTEM_MESSAGE openingMsg;
        int maskId;
        long registrationPeriod;

        switch (instanceName.ToLower())
        {
            case "dredgion": // Only opening Terath Dredgion
                openingMsg = SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_OPEN_IDDREADGION_03();
                maskId = 3;
                registrationPeriod = AutoGroupConfig.DREDGION_REGISTRATION_PERIOD;
                break;
            case "eob":
                openingMsg = SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_OPEN_IDLDF5_Under_01_War();
                maskId = 108;
                registrationPeriod = AutoGroupConfig.ENGULFED_OPHIDAN_BRIDGE_REGISTRATION_PERIOD;
                break;
            case "id":
                openingMsg = SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_OPEN_IDLDF5_Fortress_Re();
                maskId = 111;
                registrationPeriod = AutoGroupConfig.IDGEL_DOME_REGISTRATION_PERIOD;
                break;
            case "iww":
                openingMsg = SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_OPEN_IDF5_TD_war();
                maskId = 109;
                registrationPeriod = AutoGroupConfig.IRON_WALL_WARFRONT_REGISTRATION_PERIOD;
                break;
            case "kb":
                openingMsg = SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_OPEN_IDKamar();
                maskId = 107;
                registrationPeriod = AutoGroupConfig.KAMAR_BATTLEFIELD_REGISTRATION_PERIOD;
                break;
            default:
                openingMsg = null;
                maskId = 0;
                registrationPeriod = 0;
                break;
        }
        if (maskId != 0)
        {
            if (PeriodicInstanceManager.GetInstance().OpenRegistration(openingMsg, maskId, registrationPeriod))
                SendInfo(admin, "Registration for " + AutoGroupTypeExtensions.GetAGTByMaskId(maskId) + " is now open.");
            else
                SendInfo(admin, "Registration for " + AutoGroupTypeExtensions.GetAGTByMaskId(maskId) + " is already open.");
        }
        else
        {
            SendInfo(admin, "No instance found for " + instanceName);
        }
    }

    private void CloseRegistration(Player admin, string instanceName)
    {
        int maskId;

        switch (instanceName.ToLower())
        {
            case "dredgion": maskId = 3; break;
            case "eob": maskId = 108; break;
            case "id": maskId = 111; break;
            case "iww": maskId = 109; break;
            case "kb": maskId = 107; break;
            default: maskId = 0; break;
        }

        if (maskId != 0)
        {
            if (PeriodicInstanceManager.GetInstance().CloseRegistration(maskId))
                SendInfo(admin, "Registration for " + AutoGroupTypeExtensions.GetAGTByMaskId(maskId) + " is now closed.");
            else
                SendInfo(admin, "Registration for " + AutoGroupTypeExtensions.GetAGTByMaskId(maskId) + " is not open.");
        }
        else
        {
            SendInfo(admin, "No instance found for " + instanceName);
        }
    }
}
