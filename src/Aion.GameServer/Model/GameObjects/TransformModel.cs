using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>
/// Transformation (polymorph/skin + restrictions) state of a creature.
/// Java parity: model/gameobjects/TransformModel.
/// </summary>
public class TransformModel
{
    private readonly Creature _owner;

    private int _modelId;
    private int _eventModelId;
    private readonly TransformType _originalType;
    private TransformType _transformType;
    private int _panelId;
    private TribeClass _transformTribe;

    // restrictions
    protected int BanUseSkills;
    protected int BanMovement;
    protected int Res1;
    protected int Res2;
    protected int Res3;
    protected int Res5;
    protected int Res6;

    public TransformModel(Creature creature)
    {
        _originalType = creature is Player ? TransformType.PC : TransformType.NONE;
        _transformType = TransformType.NONE;
        _owner = creature;
    }

    // Java parity: apply(int)
    public void Apply(int modelId) => Apply(modelId, _originalType, 0, 0, 0, 0, 0, 0, 0, 0);

    // Java parity: apply(int, TransformType, int, int, int, int, int, int, int, int)
    public void Apply(int modelId, TransformType type, int panelId, int banUseSkills, int banMovement, int res1, int res2, int res3, int res5, int res6)
    {
        int originalModelId = _owner.GetObjectTemplate().GetTemplateId();
        if (modelId == 0 || modelId == originalModelId) // reset
        {
            _modelId = originalModelId;
            _transformType = _originalType;
            _panelId = 0;
            BanUseSkills = 0;
            BanMovement = 0;
            Res1 = 0;
            Res2 = 0;
            Res3 = 0;
            Res5 = 0;
            Res6 = 0;
        }
        else // set new
        {
            _modelId = modelId;
            _transformType = type;
            _panelId = panelId;
            BanUseSkills = banUseSkills;
            BanMovement = banMovement;
            Res1 = res1;
            Res2 = res2;
            Res3 = res3;
            Res5 = res5;
            Res6 = res6;
        }

        UpdateVisually();
    }

    // Java parity: updateVisually()
    public void UpdateVisually() => PacketSendUtility.BroadcastPacketAndReceive(_owner, new SmTransform(_owner));

    // Java parity: private updateTribeVisually()
    private void UpdateTribeVisually()
    {
        if (_owner is Npc npc)
        {
            npc.GetKnownList().ForEachPlayer(player =>
                PacketSendUtility.SendPacket(player, new SmCustomSettings(npc.ObjectId, 0, npc.GetTypeValue(player).GetId(), 0)));
        }
        else if (_owner is Player player)
        {
            player.GetKnownList().ForEachNpc(npc =>
                PacketSendUtility.SendPacket(player, new SmCustomSettings(npc.ObjectId, 0, npc.GetTypeValue(player).GetId(), 0)));
        }
    }

    // Java parity: getModelId()
    public int GetModelId()
    {
        if (_eventModelId == _owner.GetObjectTemplate().GetTemplateId() && _transformType == TransformType.PC && IsUnrestricted())
            return _eventModelId; // Player removed visual appearance via Nomorph command
        if (IsActive())
            return _modelId;
        if (_eventModelId > 0)
            return _eventModelId;
        return _owner.GetObjectTemplate().GetTemplateId();
    }

    // Java parity: isUnrestricted()
    public bool IsUnrestricted() =>
        BanUseSkills == 0 && BanMovement == 0 && Res1 == 0 && Res2 == 0 && Res3 == 0 && Res5 == 0 && Res6 == 0;

    // Java parity: setEventModelId(int)
    public void SetEventModelId(int eventModelId) => _eventModelId = eventModelId;

    // Java parity: getEventModelId()
    public int GetEventModelId() => _eventModelId;

    // Java parity: getType() — exposed as a property (a method GetType() would clash with Object.GetType()).
    public TransformType Type => _transformType;

    // Java parity: getType() - GetType_ is the project-wide getType() convention name.
    public TransformType GetType_() => _transformType;

    // Java parity: getPanelId()
    public int GetPanelId() => _panelId;

    // Java parity: isActive()
    public bool IsActive() => _modelId > 0 && _modelId != _owner.GetObjectTemplate().GetTemplateId();

    // Java parity: getTribe()
    public TribeClass GetTribe() => _transformTribe;

    // Java parity: setTribe(TribeClass)
    public void SetTribe(TribeClass transformTribe)
    {
        _transformTribe = transformTribe;
        UpdateTribeVisually();
    }

    public int GetBanUseSkills() => BanUseSkills;
    public int GetBanMovement() => BanMovement;
    public int GetRes1() => Res1;
    public int GetRes2() => Res2;
    public int GetRes3() => Res3;
    public int GetRes5() => Res5;
    public int GetRes6() => Res6;
}
