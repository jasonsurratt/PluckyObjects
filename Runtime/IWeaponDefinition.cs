using System.Collections;
using UnityEngine;

namespace Plucky.Objects
{
    public interface IWeaponDefinition
    {
        long recipeId { get; set; }

        /// Returns the mana cost of using this weapon. (Instantaneous? Total over time?)
        float GetManaCost();
    }
}