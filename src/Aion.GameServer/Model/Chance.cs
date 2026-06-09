using System.Collections.Generic;
using Aion.GameServer.Commons.Utils;

namespace Aion.GameServer.Model;

/// <summary>
/// Any class implementing this interface will enable calling <see cref="SelectElement{T}(IEnumerable{T})"/> on collections of such elements.
/// Java parity: model/Chance (Neon).
/// </summary>
public interface IChance
{
    float GetChance();

    /// <returns>Random element selected by its chance.</returns>
    static T SelectElement<T>(IEnumerable<T> elements) where T : IChance
    {
        return SelectElement(elements, false);
    }

    /// <returns>Random element selected by its chance. The item will be removed from the input elements if <paramref name="remove"/> is true.</returns>
    static T SelectElement<T>(IEnumerable<T> elements, bool remove) where T : IChance
    {
        float sumOfChances = 0;
        foreach (T element in elements)
            sumOfChances += element.GetChance();

        if (sumOfChances > 0)
        {
            float randomChance = Rnd.NextFloat(sumOfChances);
            float luck = 0;
            foreach (T element in elements)
            {
                luck += element.GetChance();
                if (randomChance <= luck)
                {
                    if (remove)
                        (elements as ICollection<T>)?.Remove(element);
                    return element;
                }
            }
        }
        return default;
    }
}
