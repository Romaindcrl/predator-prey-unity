using UnityEngine;

/// <summary>
/// A grass tuft, food for the sheep. Once grazed, its collider is disabled (sheep can no longer see
/// or touch it) and it grows back. It becomes edible again after regrowthTime.
/// </summary>
[RequireComponent(typeof(Collider))]
public class GrassBehaviour : MonoBehaviour
{
    [SerializeField, Min(0.1f), Tooltip("Time for a grazed tuft to become edible again (s).")]
    private float regrowthTime = 12f;
    [SerializeField, Range(0.05f, 1f), Tooltip("Size of a freshly grazed tuft compared to a fully grown one.")]
    private float grazedScale = 0.25f;
    [SerializeField, Tooltip("3D model, scaled while the tuft grows back.")]
    private Transform graphics;
    [SerializeField, Tooltip("Plays the wind animation of the 3D model.")]
    private Animator animator;

    private static readonly int SwayAnimation = Animator.StringToHash("Base Layer.Sway");

    private Collider trigger;
    private Vector3 fullScale;
    private float grazedTime;

    public bool IsEdible { get; private set; } = true;

    public float RegrowthTime
    {
        get => regrowthTime;
        set => regrowthTime = Mathf.Max(0.1f, value);
    }

    private void Awake()
    {
        trigger = GetComponent<Collider>();
        fullScale = graphics.localScale;
    }

    private void Start()
    {
        // Each tuft starts the wind animation at a different moment.
        if (animator != null)
            animator.Play(SwayAnimation, 0, Random.value);
    }

    /// <summary>Returns false if the tuft was already eaten (two sheep in contact at the same time).</summary>
    public bool TryGraze()
    {
        if (!IsEdible)
            return false;

        IsEdible = false;
        trigger.enabled = false;
        grazedTime = Time.time;
        graphics.localScale = fullScale * grazedScale;
        return true;
    }

    private void Update()
    {
        if (IsEdible)
            return;

        float growth = Mathf.Clamp01((Time.time - grazedTime) / regrowthTime);
        graphics.localScale = fullScale * Mathf.Lerp(grazedScale, 1f, growth);
        if (growth >= 1f)
        {
            IsEdible = true;
            trigger.enabled = true;
        }
    }
}
