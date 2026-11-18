using System;

namespace PredatorPrey
{
    [Serializable]
    public sealed class SpeciesSettings
    {
        public float walkSpeed, runSpeed, turnDegreesPerSecond, sightRadius;
        public float energyPerSecond, mealEnergy, hungryAt, eatingSeconds;
        public float breedAt, parentCost, maturitySeconds, cooldownSeconds;

        public static SpeciesSettings Sheep() => new SpeciesSettings
        {
            walkSpeed = 1.7f, runSpeed = 3.7f, turnDegreesPerSecond = 280, sightRadius = 8,
            energyPerSecond = 1.05f, mealEnergy = 27, hungryAt = 68, eatingSeconds = 1,
            breedAt = 78, parentCost = 23, maturitySeconds = 16, cooldownSeconds = 22
        };

        public static SpeciesSettings Wolf() => new SpeciesSettings
        {
            walkSpeed = 2, runSpeed = 4.9f, turnDegreesPerSecond = 300, sightRadius = 12,
            energyPerSecond = 1, mealEnergy = 46, hungryAt = 62, eatingSeconds = 2.4f,
            breedAt = 81, parentCost = 28, maturitySeconds = 24, cooldownSeconds = 32
        };

        public bool IsValid => walkSpeed > 0 && runSpeed > 0 && turnDegreesPerSecond > 0
            && sightRadius > 0 && energyPerSecond > 0 && mealEnergy > 0 && eatingSeconds >= 0
            && hungryAt > 0 && hungryAt < breedAt && breedAt <= 100
            && parentCost > 0 && parentCost < breedAt && maturitySeconds >= 0 && cooldownSeconds > 0;
    }

    [Serializable]
    public sealed class MeadowSettings
    {
        public int seed = 17, initialSheep = 32, initialWolves = 5, grassPatches = 84;
        public float halfExtent = 30, regrowthSeconds = 12;
        public SpeciesSettings sheep = SpeciesSettings.Sheep(), wolves = SpeciesSettings.Wolf();

        public void Validate()
        {
            if (initialSheep < 0 || initialWolves < 0 || grassPatches < 0 || halfExtent < 2
                || regrowthSeconds <= 0 || sheep == null || wolves == null || !sheep.IsValid || !wolves.IsValid)
                throw new ArgumentException("Check the meadow settings: counts, field size, timers, speeds and energy thresholds.");
        }
    }
}
