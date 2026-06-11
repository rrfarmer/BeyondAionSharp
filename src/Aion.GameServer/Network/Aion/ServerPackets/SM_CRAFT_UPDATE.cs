using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_CRAFT_UPDATE (Mr. Poke, Yeats). Updates crafting status/progress; per-action system message. skillId 40009 forces delay 1000. ItemTemplate red-tolerated.</summary>
public class SM_CRAFT_UPDATE : AionServerPacket
{
    private int skillId;
    private int itemId;
    private int action;
    private int success;
    private int failure;
    private string itemNameL10n;
    private int executionSpeed;
    private int delay;

    public SM_CRAFT_UPDATE(int skillId, ItemTemplate item, int success, int failure, int action, int executionSpeed, int delay)
    {
        this.action = action;
        this.skillId = skillId;
        this.itemId = item.GetTemplateId();
        this.success = success;
        this.failure = failure;
        this.itemNameL10n = item.GetL10n();
        this.executionSpeed = executionSpeed;
        if (skillId == 40009)
        {
            this.delay = 1000;
        }
        else
        {
            this.delay = delay;
        }
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteH(skillId);
        WriteC(action);
        WriteD(itemId);
        WriteD(success); // max
        WriteD(failure); // max
        WriteD(executionSpeed);
        WriteD(delay); // delay

        switch (action)
        {
            case 0: // init
            case 3: // crit = proc
                WriteD(1330048); // msgId
                WriteS(itemNameL10n); // param
                break;
            case 1: // update (normal)
            case 2: // crit (blue) = +10%
                WriteD(0);
                WriteS(null);
                break;
            case 4: // cancelled
                WriteD(1330051);
                WriteS(null);
                break;
            case 5: // success (end)
                WriteD(1330049);
                WriteS(itemNameL10n); // param
                break;
            case 6: // failed (end)
            case 7: // failure (never used?)
                WriteD(1330050);
                WriteS(itemNameL10n); // param
                break;
        }
    }
}
