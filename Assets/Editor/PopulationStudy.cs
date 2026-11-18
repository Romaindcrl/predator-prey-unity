using System;
using System.Globalization;
using System.IO;
using PredatorPrey;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class PopulationStudy
{
    private static readonly int[] Seeds = { 17, 29, 43, 61, 79 };
    private static readonly int[] RegrowthDelays = { 12, 48 };
    private const int DurationSeconds = 180;
    private const int SampleSeconds = 10;

    [MenuItem("Ecosystem/Run grass regrowth study", true)]
    private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

    [MenuItem("Ecosystem/Run grass regrowth study")]
    private static void FromMenu()
    {
        string path = EditorUtility.SaveFilePanel("Population samples", Path.GetDirectoryName(DefaultPath), "regrowth", "csv");
        if (!string.IsNullOrEmpty(path)) Run(path);
    }

    private static string DefaultPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../Experiments/regrowth.csv"));

    public static void RunBatch()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-studyOutput");
        Run(index >= 0 && index + 1 < args.Length ? args[index + 1] : DefaultPath);
    }

    private static void Run(string path)
    {
        path = Path.GetFullPath(path);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        OrganismView sheep = Prefab("Sheep"), wolf = Prefab("Wolf"), grass = Prefab("Grass");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (StreamWriter csv = new StreamWriter(temp))
            {
                csv.WriteLine("seed,regrowth_s,time_s,sheep,wolves,edible_grass,sheep_births,wolf_births,kills,grazings,regrowths,starvations");
                int run = 0;
                foreach (int seed in Seeds)
                foreach (int delay in RegrowthDelays)
                {
                    if (!Application.isBatchMode)
                        EditorUtility.DisplayProgressBar("Grass regrowth study", $"Seed {seed}, regrowth {delay} s",
                            (float)run / (Seeds.Length * RegrowthDelays.Length));
                    run++;
                    // MeadowScene accepts only its own colliders. This root is removed even if a run fails.
                    GameObject root = new GameObject("Population study");
                    try
                    {
                        MeadowSettings settings = new MeadowSettings { seed = seed, regrowthSeconds = delay };
                        MeadowScene scene = new MeadowScene(settings, root.transform, sheep, wolf, grass, false);
                        WriteSample(csv, seed, delay, scene.Model);
                        for (int i = 1; i <= DurationSeconds * Meadow.StepsPerSecond; i++)
                        {
                            scene.Step();
                            if (i % (SampleSeconds * Meadow.StepsPerSecond) == 0)
                                WriteSample(csv, seed, delay, scene.Model);
                        }
                    }
                    finally { Object.DestroyImmediate(root); }
                }
            }
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
            Debug.Log($"Saved {Seeds.Length * RegrowthDelays.Length} runs to {Path.GetFullPath(path)}");
        }
        finally
        {
            if (!Application.isBatchMode) EditorUtility.ClearProgressBar();
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private static OrganismView Prefab(string name)
    {
        string path = $"Assets/Prefabs/{name}.prefab";
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null || !asset.TryGetComponent(out OrganismView view))
            throw new InvalidOperationException("Missing ecosystem prefab at " + path);
        return view;
    }

    private static void WriteSample(StreamWriter csv, int seed, int delay, Meadow model)
    {
        model.Count(out int sheep, out int wolves, out int grass);
        csv.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11}",
            seed, delay, model.Tick / Meadow.StepsPerSecond, sheep, wolves, grass, model.SheepBirths,
            model.WolfBirths, model.Hunts, model.Grazings, model.Regrowths, model.Starvations));
    }
}
