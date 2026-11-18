using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PredatorPrey
{
    public sealed class MeadowScene : ISpatialQueries
    {
        public Meadow Model { get; }
        private readonly OrganismView[] prefabs;
        private readonly Transform[] groups;
        private readonly Dictionary<Organism, OrganismView> views = new Dictionary<Organism, OrganismView>();
        private readonly int detectionLayers = LayerMask.GetMask("Animals", "Food");
        private readonly bool visible;
        private Collider[] hits = new Collider[100];

        public MeadowScene(MeadowSettings settings, Transform root, OrganismView sheep, OrganismView wolf,
            OrganismView grass, bool showGraphics)
        {
            Model = new Meadow(settings);
            visible = showGraphics;
            prefabs = new[] { sheep, wolf, grass };
            groups = new[] { Group("Sheep", root), Group("Wolves", root), Group("Grass", root) };
            foreach (GrassPatch patch in Model.Grass) Add(patch);
            foreach (Animal animal in Model.Animals) Add(animal);
            Model.Born += Add;
            Model.Died += Remove;
            Physics.SyncTransforms();
        }

        private static Transform Group(string name, Transform root)
        {
            Transform group = new GameObject(name).transform;
            group.SetParent(root, false);
            return group;
        }

        private void Add(Organism body)
        {
            int index = (int)body.Species;
            OrganismView view = Object.Instantiate(prefabs[index], groups[index]);
            view.Bind(body, this, visible);
            views.Add(body, view);
        }

        private void Remove(Animal animal)
        {
            GameObject instance = views[animal].gameObject;
            views.Remove(animal);
            instance.SetActive(false);
            if (Application.isPlaying) Object.Destroy(instance);
            else Object.DestroyImmediate(instance);
        }

        public void Step()
        {
            Model.Step(this);
            foreach (Animal animal in Model.Animals) views[animal].Refresh();
            foreach (GrassPatch patch in Model.Grass) views[patch].Refresh();
            Physics.SyncTransforms();
        }

        public void FindNearby(Animal observer, float radius, List<Organism> results)
        {
            Vector3 center = OrganismView.GroundPosition(observer) + views[observer].Shape.center;
            int count;
            while (true)
            {
                count = Physics.OverlapSphereNonAlloc(center, radius, hits, detectionLayers, QueryTriggerInteraction.Collide);
                if (count < hits.Length) break;
                Array.Resize(ref hits, hits.Length * 2);
            }

            for (int i = 0; i < count; i++)
                if (hits[i].TryGetComponent(out OrganismView view) && view.Owner == this && view.Body != observer)
                    results.Add(view.Body);
        }

        public bool Touching(Organism first, Organism second)
        {
            // Resolve bites and births at the new positions, even before the next physics sync.
            return Physics.ComputePenetration(
                views[first].Shape, OrganismView.GroundPosition(first), Quaternion.identity,
                views[second].Shape, OrganismView.GroundPosition(second), Quaternion.identity,
                out _, out _);
        }
    }
}
