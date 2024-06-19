using Plucky.Objects;
using System;
using System.Collections.Generic;

namespace knockback
{
    /// DO NOT ADD OR DELETE IN THE MIDDLE
    /// 
    /// Add new values before EnumLength or enums in Unity Editor will get mucked up.
    public enum EntityStatType
    {
        HealthMax = 0,
        HealthRegen = 1,
        ManaMax = 2,
        ManaRegen = 3,
        ThrowingSpeed = 4,
        /// The puppet master pin weight
        Sturdiness = 5,
        /// The minimum sturdiness where the entity can barely walk. Remove me?
        SturdinessMin = 6,
        /// The pin weight when swiping
        SturdinessSwiping = 7,
        /// The scale of the character, 1 is normal size.
        Scale = 8,
        /// The maximum cost of a weapon for this character.
        WeaponCostMax = 9,
        Strength = 10,
        ThrowingRange = 11,
        Damage = 12,
        EnumLength
    }

    public interface IEntityStats
    {
        /// <summary>
        /// arrowDrawTime is the time it takes to draw an arrow in seconds.
        /// </summary>
        float arrowDrawTime { get; }

        /// <summary>
        /// validWeaponParts are the parts that are explicitly valid. Foundations must be in this
        /// list to be used as the first foundation.
        /// </summary>
        HashSet<Type> validWeaponParts { get; }

        bool CanUseWeapon(IWeaponDefinition def);

        float GetFloat(EntityStatType statType);

        /// The enemy will start running at this distance.
        float GetChargeDistance();

        /// Returns the movement speed of the character in m/s
        float GetMovementSpeedWalking();

        /// Returns the movement speed of the character in m/s
        float GetMovementSpeedRunning();

        /// A list of pre-canned & pre-defined recipes
        List<IWeaponRecipe> GetPredefinedRecipes();

        /// Delay after the animation starts till the object should be launched (better in
        /// animation events?)
        float GetThrowingAnimationDelay();

        /// The target should be within this many degrees of forward before throwing.
        float GetThrowingAngle();

        /// Return the throwing delay between throwing projectiles seconds.
        float GetThrowingDelay();

        /// The entity's turning speed in deg/s
        float GetTurningSpeed();
    }
}
