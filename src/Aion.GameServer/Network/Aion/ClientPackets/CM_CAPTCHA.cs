using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using State = Aion.GameServer.Network.Aion.AionConnection.State;
using LOG = Aion.GameServer.Network.Aion.ServerPackets.SM_ATTACK_STATUS.LOG;
using TYPE = Aion.GameServer.Network.Aion.ServerPackets.SM_ATTACK_STATUS.TYPE;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_CAPTCHA (Cura). Client captcha answer / extract-status query. Client packet idiom: ReadImpl reads, RunImpl handles; readUC/readS->ReadUC/ReadS; equalsIgnoreCase->StringComparison.OrdinalIgnoreCase; SM_ATTACK_STATUS.TYPE/LOG aliased; AionConnection.State aliased; LoggerFactory->NullLogger. AionClientPacket base/SM_SYSTEM_MESSAGE red-tolerated.</summary>
public class CM_CAPTCHA : AionClientPacket
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(CM_CAPTCHA));

    private int type;
    private int count;
    private string word;

    public CM_CAPTCHA(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        type = ReadUC();

        switch (type)
        {
            case 2:
                count = ReadUC();
                word = ReadS();
                break;
            case 4: // /ExtractStatus
                break;
            default:
                log.LogWarning("Unknown CAPTCHA packet type " + type);
                break;
        }
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();

        switch (type)
        {
            case 2:
                if (player.GetCaptchaWord().Equals(word, StringComparison.OrdinalIgnoreCase))
                {
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CAPTCHA_UNRESTRICT());
                    PacketSendUtility.SendPacket(player, new SM_CAPTCHA(true, 0));

                    PunishmentService.SetIsNotGatherable(player, 0, false, 0);

                    // fp bonus (like retail)
                    player.GetLifeStats().IncreaseFp(TYPE.FP, SecurityConfig.CAPTCHA_BONUS_FP_TIME, 0, LOG.REGULAR);
                }
                else
                {
                    int banTime = SecurityConfig.CAPTCHA_EXTRACTION_BAN_TIME + (SecurityConfig.CAPTCHA_EXTRACTION_BAN_ADD_TIME * count);

                    if (count < 3)
                    {
                        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CAPTCHA_UNRESTRICT_FAILED_RETRY(3 - count));
                        PacketSendUtility.SendPacket(player, new SM_CAPTCHA(false, banTime));
                        PunishmentService.SetIsNotGatherable(player, count, true, banTime * 1000L);
                    }
                    else
                    {
                        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CAPTCHA_UNRESTRICT_FAILED());
                        PunishmentService.SetIsNotGatherable(player, count, true, banTime * 1000L);
                    }
                }
                break;
            case 4:
                if (player.IsGatherRestricted())
                    SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_CAPTCHA_RESTRICTED(player.GetGatherRestrictionDurationSeconds()));
                else
                    SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_CAPTCHA_NOT_RESTRICTED());
                break;
        }
    }
}
