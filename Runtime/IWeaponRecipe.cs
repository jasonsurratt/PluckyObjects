using System.Collections;
using UnityEngine;

namespace Plucky.Objects
{
    public interface IWeaponRecipe
    {
        IWeaponDefinition recipeMin { get; set; }
        IWeaponDefinition recipeMax { get; set; }
    }
}