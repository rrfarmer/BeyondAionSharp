using Aion.GameServer.Model.Templates.Item.Enums;

namespace Aion.GameServer.SkillEngine.Model;

/// <summary>
/// Main/off-hand weapon-group pair used for skill weapon checks.
/// Java parity: skillengine/model/WeaponTypeWrapper.
/// </summary>
public class WeaponTypeWrapper : IComparable<WeaponTypeWrapper>
{
    private readonly ItemGroup? _mainHand;
    private readonly ItemGroup? _offHand;

    public WeaponTypeWrapper(ItemGroup? mainHand, ItemGroup? offHand)
    {
        if (mainHand != null && offHand != null)
        {
            switch (mainHand)
            {
                case ItemGroup.DAGGER:
                    _mainHand = ItemGroup.DAGGER;
                    _offHand = ItemGroup.DAGGER;
                    break;
                case ItemGroup.SWORD:
                    _mainHand = ItemGroup.SWORD;
                    _offHand = ItemGroup.SWORD;
                    break;
                case ItemGroup.MACE:
                    _mainHand = ItemGroup.MACE;
                    _offHand = ItemGroup.MACE;
                    break;
                case ItemGroup.TOOLHOES:
                    _mainHand = ItemGroup.TOOLHOES;
                    _offHand = ItemGroup.TOOLHOES;
                    break;
                case ItemGroup.GUN:
                    _mainHand = ItemGroup.GUN;
                    _offHand = ItemGroup.GUN;
                    break;
                default:
                    _mainHand = mainHand;
                    _offHand = null;
                    break;
            }
        }
        else
        {
            _mainHand = mainHand;
            _offHand = offHand;
        }
    }

    // Java parity: equals(Object)
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
            return true;
        if (obj == null || GetType() != obj.GetType())
            return false;
        var other = (WeaponTypeWrapper)obj;
        return _mainHand == other._mainHand && _offHand == other._offHand;
    }

    // Java parity: toString()
    public override string ToString() => "mainHand=\"" + _mainHand + "\" offHand=\"" + _offHand + "\"";

    // Java parity: hashCode()
    public override int GetHashCode()
    {
        const int prime = 31;
        int result = 1;
        result = prime * result + (_mainHand == null ? 0 : _mainHand.GetHashCode());
        result = prime * result + (_offHand == null ? 0 : _offHand.GetHashCode());
        return result;
    }

    // Java parity: compareTo(WeaponTypeWrapper)
    public int CompareTo(WeaponTypeWrapper? o)
    {
        if (_mainHand == null || o!.GetMainHand() == null)
            return 0;
        if (_offHand != null && o.GetOffHand() != null)
            return 0;
        if (_offHand != null && o.GetOffHand() == null)
            return 1;
        if (_offHand == null && o.GetOffHand() != null)
            return -1;
        return string.CompareOrdinal(_mainHand.ToString(), o.GetMainHand().ToString());
    }

    // Java parity: getMainHand()
    public ItemGroup? GetMainHand() => _mainHand;

    // Java parity: getOffHand()
    public ItemGroup? GetOffHand() => _offHand;
}
