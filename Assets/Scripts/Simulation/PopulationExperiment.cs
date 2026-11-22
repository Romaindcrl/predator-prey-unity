using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Extension: compares several grass regrowth times. Every (seed, regrowth time) pair is simulated for
/// durationSeconds, as fast as the computer allows, and the populations are saved to a CSV file.
/// Enable this component on the Simulation object, then press Play. Play mode stops by itself at the end.
/// </summary>
[RequireComponent(typeof(Simulation))]
public class PopulationExperiment : MonoBehaviour
{
    [SerializeField] private int[] seeds = { 17, 29, 43, 61, 79, 97, 113, 131, 151, 173 };
    [SerializeField] private float[] regrowthTimes = { 12f, 24f, 48f };
    [SerializeField, Min(1f), Tooltip("Simulated duration of each run (s).")]
    private float durationSeconds = 600f;
    [SerializeField, Min(0.1f), Tooltip("Time between two rows of the CSV (s).")]
    private float sampleInterval = 10f;
    [SerializeField, Range(0.01f, 0.1f), Tooltip("Simulated time per frame (s). Fixed so that the results do not depend on the frame rate.")]
    private float timeStep = 0.02f;
    [SerializeField, Tooltip("CSV file, relative to the project folder.")]
    private string outputFile = "Experiments/regrowth.csv";
    [SerializeField, Tooltip("Turns the camera off during the experiment: without rendering, it runs much faster.")]
    private bool hideDisplay = true;

    private IEnumerator Start()
    {
        Simulation simulation = GetComponent<Simulation>();
        yield return null;   // Let Simulation.Start create its groups first.

        // Every frame now advances exactly timeStep seconds of simulated time, whatever the real frame rate.
        Time.captureDeltaTime = timeStep;
        Camera display = Camera.main;
        if (hideDisplay && display != null)
            display.enabled = false;
        StringBuilder csv = new StringBuilder(
            "seed,regrowth_s,time_s,sheep,wolves,edible_grass,sheep_births,wolf_births,kills,grazings,sheep_starved,wolves_starved\n");

        int run = 0;
        foreach (int seed in seeds)
        {
            foreach (float regrowth in regrowthTimes)
            {
                run++;
                Debug.Log($"Experiment run {run}/{seeds.Length * regrowthTimes.Length}: seed {seed}, regrowth {regrowth} s");
                simulation.Restart(seed, regrowth);
                float start = Time.time;
                for (float t = 0f; t <= durationSeconds + 0.001f; t += sampleInterval)
                {
                    while (Time.time - start < t - 0.001f)
                        yield return null;
                    AppendRow(csv, seed, regrowth, t, simulation);
                }
            }
        }

        Time.captureDeltaTime = 0f;
        if (display != null)
            display.enabled = true;
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputFile));
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, csv.ToString());
        Debug.Log($"Experiment saved to {path}");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void OnDisable()
    {
        // If Play mode is stopped in the middle of the experiment, give the normal time back.
        Time.captureDeltaTime = 0f;
    }

    private static void AppendRow(StringBuilder csv, int seed, float regrowth, float time, Simulation simulation)
    {
        EcosystemStatistics s = simulation.Statistics;
        csv.AppendLine(string.Join(",",
            seed.ToString(CultureInfo.InvariantCulture), regrowth.ToString(CultureInfo.InvariantCulture),
            Mathf.RoundToInt(time).ToString(CultureInfo.InvariantCulture),
            s.sheep, s.wolves, simulation.CountEdibleGrass(), s.sheepBirths, s.wolfBirths, s.kills, s.grazings,
            s.sheepStarved, s.wolvesStarved));
    }
}
