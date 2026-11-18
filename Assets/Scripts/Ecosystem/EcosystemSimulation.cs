using UnityEngine;

namespace PredatorPrey
{
    public sealed class EcosystemSimulation : MonoBehaviour
    {
        [SerializeField] private OrganismView sheepPrefab;
        [SerializeField] private OrganismView wolfPrefab;
        [SerializeField] private OrganismView grassPrefab;
        [SerializeField] private PopulationPlot plot;
        [SerializeField] private Transform ground;
        [SerializeField] private MeadowSettings settings = new MeadowSettings();

        public MeadowScene Habitat { get; private set; }
        private double accumulated;

        private void Start()
        {
            Habitat = new MeadowScene(settings, transform, sheepPrefab, wolfPrefab, grassPrefab, true);
            float fieldWidth = settings.halfExtent * 2 + 4;
            ground.localScale = new Vector3(fieldWidth, ground.localScale.y, fieldWidth);
            Record();
        }

        private void Update()
        {
            if (Habitat == null) return;
            accumulated += Time.deltaTime;
            const double step = 1.0 / Meadow.StepsPerSecond;
            while (accumulated >= step)
            {
                Habitat.Step();
                accumulated -= step;
                if (Habitat.Model.Tick % Meadow.StepsPerSecond == 0) Record();
            }
        }

        private void Record()
        {
            Habitat.Model.Count(out int sheep, out int wolves, out int grass);
            plot.RecordPopulation(sheep, wolves, grass);
        }
    }
}
