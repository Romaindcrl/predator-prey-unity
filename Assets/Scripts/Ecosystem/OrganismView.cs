using UnityEngine;

namespace PredatorPrey
{
    [RequireComponent(typeof(SphereCollider))]
    public sealed class OrganismView : MonoBehaviour
    {
        [SerializeField] private Transform graphics;
        [SerializeField] private Animator animator;
        public Organism Body { get; private set; }
        public SphereCollider Shape { get; private set; }
        public MeadowScene Owner { get; private set; }

        private bool showGraphics;
        private int previousMotion;
        private static readonly int Walk = Animator.StringToHash("Base Layer.Walk");
        private static readonly int Run = Animator.StringToHash("Base Layer.Run");
        private static readonly int Eat = Animator.StringToHash("Base Layer.Eat");

        internal void Bind(Organism body, MeadowScene owner, bool visible)
        {
            Body = body;
            Owner = owner;
            Shape = GetComponent<SphereCollider>();
            showGraphics = visible;
            graphics.gameObject.SetActive(visible);
            gameObject.name = body.Species + " " + body.Id;
            transform.position = GroundPosition(body);
            Refresh();
        }

        public static Vector3 GroundPosition(Organism body) => new Vector3(body.Position.X, 0, body.Position.Y);

        internal void Refresh()
        {
            if (Body is GrassPatch patch)
            {
                if (Shape.enabled != patch.Available) Shape.enabled = patch.Available;
                bool display = showGraphics && patch.Available;
                if (graphics.gameObject.activeSelf != display) graphics.gameObject.SetActive(display);
                return;
            }

            Animal animal = (Animal)Body;
            Quaternion heading = Quaternion.Euler(0, 90 - animal.Heading * Mathf.Rad2Deg, 0);
            transform.SetPositionAndRotation(GroundPosition(animal), heading);
            if (!showGraphics || animator == null) return;

            int motion = animal.State == AnimalState.Eat ? Eat
                : animal.State == AnimalState.Flee || animal.State == AnimalState.Hunt ? Run : Walk;
            if (motion != previousMotion)
            {
                animator.CrossFadeInFixedTime(motion, 0.12f);
                previousMotion = motion;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!(Body is Animal animal)) return;
            Gizmos.color = new Color(0.97f, 0.8f, 0.18f, 0.65f);
            Gizmos.DrawWireSphere(transform.position, animal.Rules.sightRadius);
            if (animal.Target == null) return;
            Gizmos.DrawLine(transform.position, GroundPosition(animal.Target));
        }
    }
}
