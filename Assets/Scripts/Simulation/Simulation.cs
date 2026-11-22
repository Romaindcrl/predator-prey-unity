using System.Collections.Generic;
using UnityEngine;

/// <summary>Counters shown in the Inspector in Play mode and saved by PopulationExperiment.</summary>
[System.Serializable]
public class EcosystemStatistics
{
    public int sheep;
    public int wolves;
    public int sheepBirths;
    public int wolfBirths;
    public int kills;
    public int sheepStarved;
    public int wolvesStarved;
    public int grazings;
}

/// <summary>
/// Creates the ecosystem (grass, sheep, wolves), knows the size of the field and counts the populations.
/// It never decides anything for the animals: each agent runs its own state machine.
/// </summary>
public class Simulation : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private SheepBehaviour sheepPrefab;
    [SerializeField] private WolfBehaviour wolfPrefab;
    [SerializeField] private GrassBehaviour grassPrefab;

    [Header("Start")]
    [SerializeField, Min(0)] private int initialSheep = 32;
    [SerializeField, Min(0)] private int initialWolves = 5;
    [SerializeField, Min(0)] private int grassCount = 84;
    [SerializeField, Tooltip("Same seed = same starting positions. 0 = different at every run.")]
    private int seed = 17;

    [Header("Field")]
    [SerializeField, Min(2f), Tooltip("The square field goes from -halfSize to +halfSize around this object (m).")]
    private float halfSize = 30f;
    [SerializeField, Min(0.1f), Tooltip("Distance from an edge at which the animals start turning back (m).")]
    private float edgeMargin = 4f;
    [SerializeField, Tooltip("Ground, resized to the field at the start.")]
    private Transform ground;

    [Header("Display")]
    [SerializeField] private PopulationPlot plot;
    [SerializeField, Min(0.1f), Tooltip("Time between two points of the population plot (s).")]
    private float plotInterval = 1f;

    [Header("Live statistics (Play mode)")]
    [SerializeField] private EcosystemStatistics statistics = new EcosystemStatistics();

    private readonly List<GrassBehaviour> grass = new List<GrassBehaviour>();
    private Transform grassGroup;
    private Transform sheepGroup;
    private Transform wolfGroup;

    public EcosystemStatistics Statistics => statistics;

    private void Start()
    {
        if (ground != null)
        {
            float width = 2f * halfSize + 4f;
            ground.localScale = new Vector3(width, ground.localScale.y, width);
        }

        grassGroup = CreateGroup("Grass");
        sheepGroup = CreateGroup("Sheep");
        wolfGroup = CreateGroup("Wolves");
        Populate();
        InvokeRepeating(nameof(UpdatePlot), 0f, plotInterval);
    }

    private Transform CreateGroup(string groupName)
    {
        Transform group = new GameObject(groupName).transform;
        group.SetParent(transform, false);
        return group;
    }

    private void Populate()
    {
        if (seed != 0)
            Random.InitState(seed);

        for (int i = 0; i < grassCount; i++)
            grass.Add(Instantiate(grassPrefab, RandomPosition(), RandomRotation(), grassGroup));
        for (int i = 0; i < initialSheep; i++)
            Spawn(sheepPrefab, RandomPosition(), adult: true);
        for (int i = 0; i < initialWolves; i++)
            Spawn(wolfPrefab, RandomPosition(), adult: true);
    }

    private void Spawn(AnimalBehaviour prefab, Vector3 position, bool adult)
    {
        Transform group = prefab is SheepBehaviour ? sheepGroup : wolfGroup;
        AnimalBehaviour animal = Instantiate(prefab, position, RandomRotation(), group);
        animal.Init(this, adult);
        CountAnimal(animal, +1);
    }

    private void CountAnimal(AnimalBehaviour animal, int change)
    {
        if (animal is SheepBehaviour)
            statistics.sheep += change;
        else
            statistics.wolves += change;
    }

    // ---------------------------------------------------------------- Called by the agents

    /// <summary>An animal dropped by hand in the scene (in Play mode) joins the simulation as an adult.</summary>
    public void Adopt(AnimalBehaviour animal)
    {
        animal.Init(this, adult: true);
        CountAnimal(animal, +1);
    }

    /// <summary>One newborn between the two parents (see AnimalBehaviour.MateWith).</summary>
    public void SpawnOffspring(AnimalBehaviour parent, Vector3 position)
    {
        if (parent is SheepBehaviour)
        {
            Spawn(sheepPrefab, position, adult: false);
            statistics.sheepBirths++;
        }
        else
        {
            Spawn(wolfPrefab, position, adult: false);
            statistics.wolfBirths++;
        }
    }

    public void OnAnimalDied(AnimalBehaviour animal, DeathCause cause)
    {
        CountAnimal(animal, -1);
        if (cause == DeathCause.Predation)
            statistics.kills++;
        else if (animal is SheepBehaviour)
            statistics.sheepStarved++;
        else
            statistics.wolvesStarved++;
    }

    public void OnGrassEaten()
    {
        statistics.grazings++;
    }

    // ---------------------------------------------------------------- Field

    public Vector3 RandomPosition()
    {
        return transform.position + new Vector3(Random.Range(-halfSize, halfSize), 0f, Random.Range(-halfSize, halfSize));
    }

    private static Quaternion RandomRotation()
    {
        return Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
    }

    public Vector3 ClampToField(Vector3 position)
    {
        Vector3 local = position - transform.position;
        local.x = Mathf.Clamp(local.x, -halfSize, halfSize);
        local.z = Mathf.Clamp(local.z, -halfSize, halfSize);
        return transform.position + local;
    }

    /// <summary>
    /// Zero in the middle of the field. Within edgeMargin of an edge, points back inside and grows up
    /// to 2 at the edge, so it outweighs any desired direction (a vector of length 1).
    /// </summary>
    public Vector3 EdgeRepulsion(Vector3 position)
    {
        Vector3 local = position - transform.position;
        float inner = halfSize - edgeMargin;
        Vector3 push = Vector3.zero;
        if (Mathf.Abs(local.x) > inner)
            push.x = -Mathf.Sign(local.x) * (Mathf.Abs(local.x) - inner) / edgeMargin;
        if (Mathf.Abs(local.z) > inner)
            push.z = -Mathf.Sign(local.z) * (Mathf.Abs(local.z) - inner) / edgeMargin;
        return 2f * push;
    }

    // ---------------------------------------------------------------- Populations

    public int CountEdibleGrass()
    {
        int count = 0;
        foreach (GrassBehaviour tuft in grass)
        {
            if (tuft.IsEdible)
                count++;
        }
        return count;
    }

    private void UpdatePlot()
    {
        if (plot != null)
            plot.RecordPopulation(statistics.sheep, statistics.wolves, CountEdibleGrass());
    }

    /// <summary>Removes every organism and starts again with another seed (used by PopulationExperiment).</summary>
    public void Restart(int newSeed, float grassRegrowthTime)
    {
        foreach (Transform group in new[] { grassGroup, sheepGroup, wolfGroup })
        {
            foreach (Transform child in group)
            {
                child.gameObject.SetActive(false);   // Leaves the physics scene immediately...
                Destroy(child.gameObject);           // ...and is destroyed at the end of the frame.
            }
        }

        grass.Clear();
        statistics = new EcosystemStatistics();
        seed = newSeed;
        Populate();
        foreach (GrassBehaviour tuft in grass)
            tuft.RegrowthTime = grassRegrowthTime;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(2f * halfSize, 0.1f, 2f * halfSize));
    }
}
