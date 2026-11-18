using System;
using System.Collections.Generic;
using System.Numerics;

namespace PredatorPrey
{
    public sealed class Meadow
    {
        public const int StepsPerSecond = 20;
        public const float StepSeconds = 1f / StepsPerSecond;
        public MeadowSettings Settings { get; }
        public int Tick { get; private set; }
        public float Time => (float)Tick / StepsPerSecond;

        public IReadOnlyList<Animal> Animals => animals;
        public IReadOnlyList<GrassPatch> Grass => grass;
        public int SheepBirths { get; private set; }
        public int WolfBirths { get; private set; }
        public int Hunts { get; private set; }
        public int Grazings { get; private set; }
        public int Regrowths { get; private set; }
        public int Starvations { get; private set; }

        public event Action<Animal> Born;
        public event Action<Animal> Died;
        private readonly List<Animal> animals = new List<Animal>();
        private readonly List<GrassPatch> grass = new List<GrassPatch>();
        private readonly List<Organism> nearby = new List<Organism>(64);
        private readonly Random random;
        private int nextId;

        public Meadow(MeadowSettings settings)
        {
            settings.Validate();
            Settings = settings;
            random = new Random(settings.seed);
            for (int i = 0; i < settings.grassPatches; i++)
                grass.Add(new GrassPatch(++nextId, RandomPosition()));
            for (int i = 0; i < settings.initialSheep; i++) Spawn(Species.Sheep, RandomPosition(), true);
            for (int i = 0; i < settings.initialWolves; i++) Spawn(Species.Wolf, RandomPosition(), true);
        }

        public void Step(ISpatialQueries space)
        {
            Tick++;
            foreach (GrassPatch patch in grass)
            {
                if (patch.Available) continue;
                patch.Grow(StepSeconds);
                if (patch.Available) Regrowths++;
            }
            foreach (Animal animal in animals)
            {
                animal.Age();
                if (!animal.Available) Starvations++;
            }
            // Everybody chooses using the same pre-movement positions.
            foreach (Animal animal in animals)
                if (animal.Available) animal.Decide(this, space, nearby);
            foreach (Animal animal in animals)
                if (animal.Available) animal.Move(this);

            ResolveMeals(space, AnimalState.Hunt);
            ResolveMeals(space, AnimalState.SeekGrass);
            ResolvePairs(space);
            for (int i = animals.Count - 1; i >= 0; i--)
            {
                Animal animal = animals[i];
                if (animal.Available) continue;
                animals.RemoveAt(i);
                Died?.Invoke(animal);
            }
        }

        private void ResolveMeals(ISpatialQueries space, AnimalState action)
        {
            foreach (Animal eater in animals)
            {
                Organism target = eater.Target;
                if (eater.State != action || target == null || !target.Available || !space.Touching(eater, target))
                    continue;
                if (target is GrassPatch patch)
                {
                    if (!patch.TryGraze(Settings.regrowthSeconds)) continue;
                    Grazings++;
                }
                else
                {
                    ((Animal)target).Die();
                    Hunts++;
                }
                eater.FinishMeal(Time);
            }
        }

        private void ResolvePairs(ISpatialQueries space)
        {
            int parents = animals.Count;
            for (int i = 0; i < parents; i++)
            {
                Animal parent = animals[i];
                if (!(parent.Target is Animal mate) || parent.Id >= mate.Id
                    || !parent.IsMutualMate(mate, Time) || !space.Touching(parent, mate))
                    continue;

                parent.PayBirthCost(Time);
                mate.PayBirthCost(Time);
                Vector2 midpoint = (parent.Position + mate.Position) * 0.5f;
                Spawn(parent.Species, midpoint, false);
                if (parent.Species == Species.Sheep) SheepBirths++;
                else WolfBirths++;
            }
        }

        private void Spawn(Species species, Vector2 position, bool adult)
        {
            Animal child = new Animal(++nextId, species, position, this, adult);
            animals.Add(child);
            Born?.Invoke(child);
        }

        public void Count(out int sheep, out int wolves, out int edibleGrass)
        {
            sheep = 0;
            wolves = 0;
            edibleGrass = 0;
            foreach (Animal animal in animals)
            {
                if (animal.Species == Species.Sheep) sheep++;
                else wolves++;
            }
            foreach (GrassPatch patch in grass)
                if (patch.Available) edibleGrass++;
        }

        internal float Range(float min, float max) => (float)(min + random.NextDouble() * (max - min));

        internal Vector2 RandomPosition()
        {
            float edge = Settings.halfExtent;
            return new Vector2(Range(-edge, edge), Range(-edge, edge));
        }

        internal Vector2 Bound(Vector2 point)
        {
            Vector2 limit = new Vector2(Settings.halfExtent);
            return Vector2.Clamp(point, -limit, limit);
        }

        internal Vector2 EscapePoint(Animal sheep, Animal wolf)
        {
            Vector2 away = sheep.Position - wolf.Position;
            if (away.LengthSquared() < 0.0001f)
                away = new Vector2((float)Math.Cos(sheep.Heading), (float)Math.Sin(sheep.Heading));
            away = Vector2.Normalize(away);

            // A sheep pushed against a border turns along it. At a corner it heads back into the field.
            float edge = Settings.halfExtent - 1;
            Vector2 p = sheep.Position;
            bool blockX = Math.Abs(p.X) >= edge && p.X * away.X > 0;
            bool blockZ = Math.Abs(p.Y) >= edge && p.Y * away.Y > 0;
            if (blockX && blockZ) away = -Vector2.Normalize(p);
            else if (blockX) away = new Vector2(-0.2f * Math.Sign(p.X), Tangent(away.Y, sheep.Id));
            else if (blockZ) away = new Vector2(Tangent(away.X, sheep.Id), -0.2f * Math.Sign(p.Y));
            return Bound(p + away * 5);
        }

        private static float Tangent(float component, int id) =>
            Math.Abs(component) > 0.01f ? Math.Sign(component) : (id % 2 == 0 ? 1 : -1);
    }
}
