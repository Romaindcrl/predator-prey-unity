using UnityEngine;

/// <summary>Why an animal died. Used for the statistics of the Simulation.</summary>
public enum DeathCause { Starvation, Predation }

/// <summary>
/// Everything sheep and wolves have in common: energy, perception, movement and reproduction.
/// Each species adds its own finite state machine in a subclass (SheepBehaviour, WolfBehaviour).
/// The kinematic Rigidbody is required for Unity to report trigger contacts (OnTriggerStay).
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public abstract class AnimalBehaviour : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f), Tooltip("Speed when wandering or walking to food or a partner (m/s).")]
    protected float walkSpeed = 1.7f;
    [SerializeField, Min(0f), Tooltip("Speed when chasing or fleeing (m/s).")]
    protected float runSpeed = 3.7f;
    [SerializeField, Min(0f), Tooltip("Maximum turning speed (degrees per second).")]
    private float turnSpeed = 280f;
    [SerializeField, Tooltip("When wandering, a new random direction is picked every X to Y seconds.")]
    private Vector2 wanderInterval = new Vector2(5f, 9f);

    [Header("Perception")]
    [SerializeField, Min(0f), Tooltip("Vision radius (m).")]
    private float visionRadius = 8f;
    [SerializeField, Min(0.02f), Tooltip("Time between two scans of the surroundings (s).")]
    private float perceptionInterval = 0.2f;
    [SerializeField, Tooltip("Layers scanned by the vision: animals and food.")]
    private LayerMask perceptionLayers = ~0;

    [Header("Energy")]
    [SerializeField, Min(1f)]
    private float maxEnergy = 100f;
    [SerializeField, Tooltip("Energy of the animals created at the start (random between X and Y).")]
    private Vector2 initialEnergy = new Vector2(55f, 90f);
    [SerializeField, Min(0f), Tooltip("Energy lost every second.")]
    private float energyLossPerSecond = 1f;
    [SerializeField, Min(0f), Tooltip("Energy gained by one meal.")]
    private float energyPerMeal = 30f;
    [SerializeField, Tooltip("The animal looks for food when its energy is at or below this value.")]
    private float hungerThreshold = 65f;
    [SerializeField, Min(0f), Tooltip("Time spent eating one meal, without moving (s).")]
    private float eatingDuration = 1f;

    [Header("Reproduction")]
    [SerializeField, Tooltip("Minimum energy to mate.")]
    private float matingThreshold = 78f;
    [SerializeField, Min(0f), Tooltip("Energy paid by EACH parent for one birth.")]
    private float matingCost = 25f;
    [SerializeField, Min(0f), Tooltip("Minimum time between two births for the same parent (s).")]
    private float matingCooldown = 20f;
    [SerializeField, Min(0f), Tooltip("Age at which a newborn becomes an adult and can mate (s).")]
    private float maturityAge = 16f;
    [SerializeField, Min(1f), Tooltip("Energy of a newborn.")]
    private float newbornEnergy = 40f;
    [SerializeField, Range(0.2f, 1f), Tooltip("Size of a newborn compared to an adult.")]
    private float newbornScale = 0.6f;

    [Header("Display")]
    [SerializeField, Tooltip("Animator of the 3D model (child object). The logic never depends on it.")]
    private Animator animator;

    [Header("Live values (Play mode)")]
    [SerializeField] private float energy;

    protected static readonly int WalkAnimation = Animator.StringToHash("Base Layer.Walk");
    protected static readonly int RunAnimation = Animator.StringToHash("Base Layer.Run");
    protected static readonly int EatAnimation = Animator.StringToHash("Base Layer.Eat");

    // Shared by all animals: Unity scripts run on the main thread, one at a time.
    private static readonly Collider[] Visible = new Collider[256];

    protected Simulation simulation;
    protected AnimalBehaviour partner;   // Nearest visible animal of the same species ready to mate.

    private Collider body;
    private float birthTime;
    private float nextMatingTime;
    private float eatingEndTime;
    private float nextPerceptionTime;
    private float nextWanderTime;
    private Vector3 wanderDirection;
    private int currentAnimation;

    public float Energy => energy;
    public bool IsDead { get; private set; }
    public bool IsAdult => Time.time - birthTime >= maturityAge;
    public bool IsHungry => energy <= hungerThreshold;
    public bool IsEating => Time.time < eatingEndTime;
    public bool CanMate => !IsDead && IsAdult && !IsEating && energy >= matingThreshold && Time.time >= nextMatingTime;

    /// <summary>Called by the Simulation right after Instantiate.</summary>
    public void Init(Simulation owner, bool adult)
    {
        simulation = owner;
        body = GetComponent<Collider>();
        birthTime = adult ? Time.time - maturityAge : Time.time;
        energy = adult ? Random.Range(initialEnergy.x, initialEnergy.y) : newbornEnergy;
        wanderDirection = transform.forward;
        nextWanderTime = Time.time + Random.Range(wanderInterval.x, wanderInterval.y);
        // Spread the scans of all the animals over different frames.
        nextPerceptionTime = Time.time + Random.Range(0f, perceptionInterval);
        UpdateGrowth();
    }

    private void Start()
    {
        // An animal dropped by hand in the scene was not created by the Simulation: it joins it now.
        if (simulation == null)
            FindAnyObjectByType<Simulation>().Adopt(this);
    }

    private void Update()
    {
        if (IsDead)
            return;

        energy -= energyLossPerSecond * Time.deltaTime;
        if (energy <= 0f)
        {
            Die(DeathCause.Starvation);
            return;
        }

        if (Time.time >= nextPerceptionTime)
        {
            Perceive();
            nextPerceptionTime = Time.time + perceptionInterval;
        }

        UpdateStateMachine();
        UpdateGrowth();
    }

    /// <summary>One step of the species' state machine: pick the state, then perform its action.</summary>
    protected abstract void UpdateStateMachine();

    // ---------------------------------------------------------------- Perception

    /// <summary>Looks around (every perceptionInterval) and keeps the nearest relevant targets.</summary>
    private void Perceive()
    {
        ForgetTargets();
        int count = Physics.OverlapSphereNonAlloc(transform.position, visionRadius, Visible,
            perceptionLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider other = Visible[i];
            if (other != body)
                Notice(other, (other.transform.position - transform.position).sqrMagnitude);
        }
    }

    /// <summary>Clears the targets of the previous scan.</summary>
    protected virtual void ForgetTargets()
    {
        partner = null;
    }

    /// <summary>Called for every collider seen during a scan.</summary>
    protected abstract void Notice(Collider other, float sqrDistance);

    /// <summary>True if nothing is kept yet, or if the candidate is closer than what is kept.</summary>
    protected bool IsCloser(Component current, float sqrDistance)
    {
        return current == null || sqrDistance < (current.transform.position - transform.position).sqrMagnitude;
    }

    protected void ConsiderPartner(AnimalBehaviour candidate, float sqrDistance)
    {
        if (CanMate && candidate.CanMate && IsCloser(partner, sqrDistance))
            partner = candidate;
    }

    /// <summary>A target seen during the last scan may have died since (Destroy happens at the end of the frame).</summary>
    protected static bool IsAlive(AnimalBehaviour animal)
    {
        return animal != null && !animal.IsDead;
    }

    // ---------------------------------------------------------------- Movement

    /// <summary>
    /// Turns towards a direction and moves forward. Near an edge, the animal slides along it and is pushed
    /// back inside: a sheep fleeing towards an edge runs along it instead of getting pinned against it.
    /// </summary>
    protected void Move(Vector3 direction, float speed)
    {
        direction.y = 0f;
        Vector3 push = simulation.EdgeRepulsion(transform.position);
        if (Vector3.Dot(direction, push) < 0f)
        {
            // Heading out of the field: keep only the part of the direction that follows the edge.
            direction = Vector3.ProjectOnPlane(direction, push.normalized);
            if (direction.sqrMagnitude < 0.01f)
                direction = Vector3.Cross(Vector3.up, push);   // Straight into the edge: pick a side.
        }

        direction = direction.normalized + push;
        if (direction.sqrMagnitude > 0.0001f)
            TurnTowards(direction);
        transform.position = simulation.ClampToField(transform.position + transform.forward * (speed * Time.deltaTime));
    }

    /// <summary>Walks to a point and stops on it (food or partner).</summary>
    protected void MoveTo(Vector3 destination, float speed)
    {
        Vector3 toDestination = destination - transform.position;
        toDestination.y = 0f;
        float distance = toDestination.magnitude;
        if (distance < 0.05f)
            return;

        TurnTowards(toDestination);
        // Turns on the spot while the target is behind, so the animal never circles around it.
        float alignment = Mathf.Clamp01(Vector3.Dot(transform.forward, toDestination / distance));
        transform.position += transform.forward * Mathf.Min(speed * alignment * Time.deltaTime, distance);
    }

    /// <summary>Default behaviour: walk in a random direction that changes every few seconds.</summary>
    protected void Wander()
    {
        if (Time.time >= nextWanderTime)
        {
            wanderDirection = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            nextWanderTime = Time.time + Random.Range(wanderInterval.x, wanderInterval.y);
        }

        Move(wanderDirection, walkSpeed);
        // Near an edge, keep the corrected heading: the animal turns away instead of following the edge.
        if (simulation.EdgeRepulsion(transform.position) != Vector3.zero)
            wanderDirection = transform.forward;
    }

    private void TurnTowards(Vector3 direction)
    {
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    // ---------------------------------------------------------------- Feeding, reproduction, death

    /// <summary>The energy is gained at once, then the animal stays still during eatingDuration.</summary>
    protected void StartMeal()
    {
        energy = Mathf.Min(maxEnergy, energy + energyPerMeal);
        eatingEndTime = Time.time + eatingDuration;
    }

    protected void StopMeal()
    {
        eatingEndTime = 0f;
    }

    /// <summary>
    /// Both parents pay the cost and start their cooldown, so when the partner's own contact
    /// callback runs, CanMate is already false: one couple gives exactly one newborn.
    /// </summary>
    protected void MateWith(AnimalBehaviour mate)
    {
        if (!CanMate || !mate.CanMate)
            return;

        PayMatingCost();
        mate.PayMatingCost();
        simulation.SpawnOffspring(this, (transform.position + mate.transform.position) / 2f);
    }

    private void PayMatingCost()
    {
        energy -= matingCost;
        nextMatingTime = Time.time + matingCooldown;
        partner = null;
    }

    /// <summary>Removes the animal. The flag and the disabled collider stop any other interaction this frame.</summary>
    public void Die(DeathCause cause)
    {
        if (IsDead)
            return;

        IsDead = true;
        body.enabled = false;
        simulation.OnAnimalDied(this, cause);
        Destroy(gameObject);
    }

    // ---------------------------------------------------------------- Display

    protected void PlayAnimation(int animation)
    {
        if (animator == null || animation == currentAnimation)
            return;

        animator.CrossFadeInFixedTime(animation, 0.15f);
        currentAnimation = animation;
    }

    /// <summary>Newborns are smaller and grow until they become adults.</summary>
    private void UpdateGrowth()
    {
        if (transform.localScale.x >= 1f && IsAdult)
            return;

        float growth = maturityAge > 0f ? Mathf.Clamp01((Time.time - birthTime) / maturityAge) : 1f;
        transform.localScale = Vector3.one * Mathf.Lerp(newbornScale, 1f, growth);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, visionRadius);
    }
}
