using System;
using System.Collections.Generic;
using System.Numerics;

namespace PredatorPrey
{
    public sealed class Animal : Organism
    {
        public override bool Available => State != AnimalState.Dead;
        public AnimalState State { get; internal set; } = AnimalState.Wander;
        public float Energy { get; internal set; }
        public float Heading { get; private set; } // Radians from +X toward +Z.
        public Organism Target { get; private set; }
        public SpeciesSettings Rules { get; }

        private readonly float bornAt;
        private float nextMateAt, eatingUntil, nextScan, nextTurn;
        private Vector2 waypoint, destination;
        private Animal predator, mate;
        private Organism food;

        internal Animal(int id, Species species, Vector2 position, Meadow field, bool adult)
            : base(id, species, position)
        {
            Rules = species == Species.Sheep ? field.Settings.sheep : field.Settings.wolves;
            bornAt = adult ? field.Time - Rules.maturitySeconds : field.Time;
            Energy = adult ? field.Range(54, 90) : field.Range(31, 41);
            nextMateAt = field.Time + (adult ? field.Range(0, 6) : Rules.maturitySeconds);
            Heading = field.Range(0, 2 * (float)Math.PI);
            waypoint = position;
        }

        private bool ReadyToMate(float now) => Available && now >= nextMateAt && now >= eatingUntil
            && now - bornAt >= Rules.maturitySeconds && Energy >= Rules.breedAt;

        internal void Age()
        {
            Energy = Math.Max(0, Energy - Rules.energyPerSecond * Meadow.StepSeconds);
            if (Energy <= 0) Die();
        }

        private bool InSight(Organism other) => other != null && other.Available
            && Vector2.DistanceSquared(Position, other.Position) <= Rules.sightRadius * Rules.sightRadius;

        internal void Decide(Meadow field, ISpatialQueries space, List<Organism> nearby)
        {
            if (field.Time >= nextScan)
            {
                Scan(field.Time, space, nearby);
                nextScan = field.Time + field.Range(0.4f, 0.6f);
            }

            Target = null;
            if (InSight(predator))
            {
                State = AnimalState.Flee;
                Target = predator;
                eatingUntil = 0;
                destination = field.EscapePoint(this, predator);
            }
            else if (field.Time < eatingUntil)
                State = AnimalState.Eat;
            else if (Energy <= Rules.hungryAt && InSight(food))
                Approach(Species == Species.Sheep ? AnimalState.SeekGrass : AnimalState.Hunt, food);
            else if (ReadyToMate(field.Time) && InSight(mate) && mate.ReadyToMate(field.Time))
                Approach(AnimalState.SeekMate, mate);
            else
            {
                State = AnimalState.Wander;
                if (field.Time >= nextTurn || Vector2.DistanceSquared(Position, waypoint) < 0.5f)
                {
                    waypoint = field.RandomPosition();
                    nextTurn = field.Time + field.Range(3, 7);
                }
                destination = waypoint;
            }
        }

        private void Scan(float now, ISpatialQueries space, List<Organism> nearby)
        {
            nearby.Clear();
            space.FindNearby(this, Rules.sightRadius, nearby);
            predator = null;
            mate = null;
            food = null;
            foreach (Organism other in nearby)
            {
                if (other == this || !InSight(other)) continue;
                if (Species == Species.Sheep && other.Species == Species.Wolf)
                    predator = (Animal)Nearer(predator, other);
                else if ((Species == Species.Sheep && other.Species == Species.Grass)
                    || (Species == Species.Wolf && other.Species == Species.Sheep))
                    food = Nearer(food, other);
                else if (other is Animal partner && partner.Species == Species && partner.ReadyToMate(now))
                    mate = (Animal)Nearer(mate, partner);
            }
        }

        private Organism Nearer(Organism current, Organism candidate)
        {
            if (current == null) return candidate;
            float a = Vector2.DistanceSquared(Position, current.Position);
            float b = Vector2.DistanceSquared(Position, candidate.Position);
            // PhysX does not guarantee hit order. Stable ties keep the experiment repeatable.
            return b < a || (b == a && candidate.Id < current.Id) ? candidate : current;
        }

        private void Approach(AnimalState state, Organism target)
        {
            State = state;
            Target = target;
            destination = target.Position;
        }

        internal void Move(Meadow field)
        {
            if (State == AnimalState.Eat) return;
            Vector2 delta = destination - Position;
            float distance = delta.Length();
            if (distance < 0.001f) return;

            float targetHeading = (float)Math.Atan2(delta.Y, delta.X);
            float turn = targetHeading - Heading;
            turn = (float)Math.Atan2(Math.Sin(turn), Math.Cos(turn));
            float maxTurn = Rules.turnDegreesPerSecond * ((float)Math.PI / 180) * Meadow.StepSeconds;
            Heading += Math.Max(-maxTurn, Math.Min(maxTurn, turn));
            Vector2 forward = new Vector2((float)Math.Cos(Heading), (float)Math.Sin(Heading));
            float speed = State == AnimalState.Flee || State == AnimalState.Hunt ? Rules.runSpeed : Rules.walkSpeed;
            Position = field.Bound(Position + forward * Math.Min(distance, speed * Meadow.StepSeconds));
        }

        internal void FinishMeal(float now)
        {
            Energy = Math.Min(100, Energy + Rules.mealEnergy);
            eatingUntil = now + Rules.eatingSeconds;
            State = AnimalState.Eat;
            Target = null;
        }

        internal bool IsMutualMate(Animal other, float now) => State == AnimalState.SeekMate
            && other.State == AnimalState.SeekMate && Target == other && other.Target == this
            && ReadyToMate(now) && other.ReadyToMate(now);

        internal void PayBirthCost(float now)
        {
            Energy -= Rules.parentCost;
            nextMateAt = now + Rules.cooldownSeconds;
            Target = null;
            State = AnimalState.Wander;
        }

        internal void Die()
        {
            State = AnimalState.Dead;
            Target = null;
        }
    }
}
