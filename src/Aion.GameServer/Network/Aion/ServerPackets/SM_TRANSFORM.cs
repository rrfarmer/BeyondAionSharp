using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_TRANSFORM (Sweetkr, xTz, kecimis). Sends a creature's transform model + state + restriction flags (skill/move/item/attack/fly bans, panel id), with a custom test ctor and the live transform-model path. TransformType/Creature/TransformModel/AionServerPacket red-tolerated.</summary>
public class SM_TRANSFORM : AionServerPacket
{
    private Creature creature;

    // testing stuff
    private bool custom = false;
    private int modelId;
    private int panelId;
    private TransformType type;
    private int unk1, unk2, unk3, unk4, unk5, unk6, unk7;

    public SM_TRANSFORM(Creature creature)
    {
        this.creature = creature;
    }

    // for testing
    public SM_TRANSFORM(Creature creature, int modelId, int unk7, TransformType type, int unk1, int unk2, int unk3, int unk4, int unk5, int unk6,
        int panelId)
    {
        this.creature = creature;
        this.modelId = modelId;
        this.unk7 = unk7;
        this.type = type;
        this.unk1 = unk1;
        this.unk2 = unk2;
        this.unk3 = unk3;
        this.unk4 = unk4;
        this.unk5 = unk5;
        this.unk6 = unk6;
        this.panelId = panelId;
        this.custom = true;
    }

    /// <summary>
    /// structure SM_TRANSFORM D objectId, D modelId, H state, F 0.25f, F 2.0f, C cannotuseskill, D transformTypeId,
    /// C cannotfly/glide, C cannotuseitem, C attackdisabled, C jumpdisabled, C summondisabled, C movedisabled, D panelId
    /// </summary>
    protected override void WriteImpl(AionConnection con)
    {
        if (custom)
        {
            WriteD(creature.GetObjectId());
            WriteD(modelId);
            WriteH(creature.GetState());
            WriteF(0.25f);
            WriteF(2.0f);
            WriteC(unk7);
            WriteD(type.GetId());
            WriteC(unk1);
            WriteC(unk2);
            WriteC(unk3);
            WriteC(unk4);
            WriteC(unk5);
            WriteC(unk6);
            WriteD(panelId); // display panel
        }
        else
        {
            WriteD(creature.GetObjectId());
            WriteD(creature.GetTransformModel().GetModelId());
            WriteH(creature.GetState());
            WriteF(0.25f);
            WriteF(2.0f);
            WriteC(creature.GetTransformModel().GetBanUseSkills());
            WriteD(creature.GetTransformModel().GetType_().GetId());
            WriteC(creature.GetTransformModel().GetRes6());
            WriteC(creature.GetTransformModel().GetRes5());
            WriteC(creature.GetTransformModel().GetRes3());
            WriteC(creature.GetTransformModel().GetRes2());
            WriteC(creature.GetTransformModel().GetRes1());
            WriteC(creature.GetTransformModel().GetBanMovement());
            WriteD(creature.GetTransformModel().GetPanelId()); // display panel
        }
    }
}
