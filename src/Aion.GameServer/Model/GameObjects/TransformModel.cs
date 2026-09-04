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
    // Java parity: TribeClass transformTribe — nullable (Java enum reference set to null by PolymorphEffect.endEffect).
    private TribeClass? _transformTribe;

    // restrictions
    protected bool _cantUseSkills;
    protected bool _cantMove;
    protected bool _cantRecall;
    protected bool _cantJump;
    protected bool _cantAttack;
    protected bool _cantUseItems;
    protected bool _cantFly;

    public TransformModel(Creature creature)
    {
        _originalType = creature is Player ? TransformType.PC : TransformType.NONE;
        _transformType = TransformType.NONE;
        _owner = creature;
    }

    // Java parity: apply(int)
    public void Apply(int modelId) => Apply(modelId, _originalType, 0, false, false, false, false, false, false, false);

    // Java parity: apply(int, TransformType, int, bool, bool, bool, bool, bool, bool, bool)
    public void Apply(int modelId, TransformType type, int panelId, bool cantUseSkills, bool cantMove, bool cantRecall, bool cantJump, bool cantAttack, bool cantUseItems, bool cantFly)
    {
        int originalModelId = _owner.GetObjectTemplate().GetTemplateId();
        if (modelId == 0 || modelId == originalModelId) // reset
        {
            _modelId = originalModelId;
            _transformType = _originalType;
            _panelId = 0;
            _cantUseSkills = false;
            _cantMove = false;
            _cantRecall = false;
            _cantJump = false;
            _cantAttack = false;
            _cantUseItems = false;
            _cantFly = false;
        }
        else // set new
        {
            _modelId = modelId;
            _transformType = type;
            _panelId = panelId;
            _cantUseSkills = cantUseSkills;
            _cantMove = cantMove;
            _cantRecall = cantRecall;
            _cantJump = cantJump;
            _cantAttack = cantAttack;
            _cantUseItems = cantUseItems;
            _cantFly = cantFly;
        }

        UpdateVisually();
    }

    // Java parity: updateVisually()
    public void UpdateVisually() => PacketSendUtility.BroadcastPacketAndReceive(_owner, new Aion.GameServer.Network.Aion.ServerPackets.SM_TRANSFORM(_owner));

    // Java parity: private updateTribeVisually()
    private void UpdateTribeVisually()
    {
        if (_owner is Npc npc)
        {
            npc.GetKnownList().ForEachPlayer(player =>
                PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SM_CUSTOM_SETTINGS(npc.ObjectId, 0, npc.GetTypeValue(player).GetId(), 0)));
        }
        else if (_owner is Player player)
        {
            player.GetKnownList().ForEachNpc(npc =>
                PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SM_CUSTOM_SETTINGS(npc.ObjectId, 0, npc.GetTypeValue(player).GetId(), 0)));
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
        !_cantUseSkills && !_cantMove && !_cantRecall && !_cantJump && !_cantAttack && !_cantUseItems && !_cantFly;

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
    public TribeClass? GetTribe() => _transformTribe;

    // Java parity: setTribe(TribeClass)
    public void SetTribe(TribeClass? transformTribe)
    {
        _transformTribe = transformTribe;
        UpdateTribeVisually();
    }

    public bool CantUseSkills() => _cantUseSkills;
    public bool CantMove() => _cantMove;
    public bool CantRecall() => _cantRecall;
    public bool CantJump() => _cantJump;
    public bool CantAttack() => _cantAttack;
    public bool CantUseItems() => _cantUseItems;
    public bool CantFly() => _cantFly;
}
