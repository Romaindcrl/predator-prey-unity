using System.Collections.Generic;
using System.Numerics;

namespace PredatorPrey
{
    public enum Species { Sheep, Wolf, Grass }
    public enum AnimalState { Wander, Flee, SeekGrass, Hunt, Eat, SeekMate, Dead }

    public abstract class Organism
    {
        public int Id { get; }
        public Species Species { get; }
        public Vector2 Position { get; internal set; }
        public abstract bool Available { get; }

        protected Organism(int id, Species species, Vector2 position)
        {
            Id = id;
            Species = species;
            Position = position;
        }
    }

    public sealed class GrassPatch : Organism
    {
        public override bool Available => remaining <= 0;
        public float Growth => Available ? 1 : 1 - remaining / regrowthDelay;
        private float remaining, regrowthDelay;

        internal GrassPatch(int id, Vector2 position) : base(id, Species.Grass, position) { }
        internal void Grow(float seconds) => remaining -= seconds;

        internal bool TryGraze(float regrowthSeconds)
        {
            if (!Available) return false;
            regrowthDelay = regrowthSeconds;
            remaining = regrowthSeconds;
            return true;
        }
    }

    // Perception and physical contact are separate inputs to the decision rules.
    public interface ISpatialQueries
    {
        void FindNearby(Animal observer, float radius, List<Organism> results);
        bool Touching(Organism first, Organism second);
    }
}
