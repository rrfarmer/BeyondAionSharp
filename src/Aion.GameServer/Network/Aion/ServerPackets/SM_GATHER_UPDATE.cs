using Aion.GameServer.Model.Templates.Gather;
using Aion.GameServer.Network.Aion;
using static Aion.GameServer.Network.Aion.Serverpackets.SM_SYSTEM_MESSAGE;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_GATHER_UPDATE (ATracer, orz, Yeats, Neon). Updates current gathering status/progress; per-action system message. switch-arrow -> switch statement; static-import STR_* from SM_SYSTEM_MESSAGE. GatherableTemplate/Material/SM_SYSTEM_MESSAGE red-tolerated.</summary>
public class SM_GATHER_UPDATE : AionServerPacket
{
    private readonly int skillId;
    private readonly int action;
    private readonly int itemId;
    private readonly int success;
    private readonly int failure;
    private readonly string l10n;
    private readonly int executionSpeed;
    private readonly int delay;

    public SM_GATHER_UPDATE(GatherableTemplate template, Material material, int success, int failure, int action, int executionSpeed, int delay)
    {
        this.skillId = template.GetHarvestSkill();
        this.action = action;
        this.itemId = material.GetItemId();
        this.success = success;
        this.failure = failure;
        this.executionSpeed = executionSpeed;
        this.delay = delay;
        this.l10n = material.GetL10n();
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteH(skillId);
        WriteC(action);
        WriteD(itemId);
        WriteD(success);
        WriteD(failure);
        WriteD(executionSpeed);
        WriteD(delay);
        switch (action)
        {
            case 0:
                WriteSystemMsgInfo(STR_EXTRACT_GATHER_START_1_BASIC(null).GetId());
                break; // init
            case 1:
                WriteSystemMsgInfo(0);
                break; // For updates both for ground and aerial
            case 2:
                WriteSystemMsgInfo(0);
                break; // Light blue bar = +10%
            case 3:
                WriteSystemMsgInfo(0);
                break; // Purple bar = 100%
            case 5:
                WriteSystemMsgInfo(STR_EXTRACT_GATHER_CANCEL_1_BASIC().GetId());
                break; // canceled
            case 6:
                WriteSystemMsgInfo(STR_EXTRACT_GATHER_SUCCESS_1_BASIC(null).GetId());
                break; // success
            case 7:
                WriteSystemMsgInfo(STR_EXTRACT_GATHER_FAIL_1_BASIC(null).GetId());
                break; // failure
            case 8:
                WriteSystemMsgInfo(STR_EXTRACT_GATHER_OCCUPIED_BY_OTHER().GetId());
                break; // deselects target
        }
    }

    private void WriteSystemMsgInfo(int msgId)
    {
        WriteD(msgId); // msgId
        WriteS(msgId == 0 ? null : l10n); // parameter
    }
}
