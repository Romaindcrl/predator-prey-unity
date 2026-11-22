using UnityEngine;

/// <summary>States of the sheep. The graph is in the README.</summary>
public enum SheepState { Wander, SeekGrass, Eat, Flee, SeekMate }

/// <summary>
/// A sheep explores, eats grass, flees from wolves and mates with other sheep.
/// PickState() holds the transitions in priority order, UpdateStateMachine() performs the current state.
/// </summary>
public class SheepBehaviour : AnimalBehaviour
{
    // Shown right after the energy, under "Live values (Play mode)".
    [SerializeField, Tooltip("Current state (updated in Play mode).")]
    private SheepState state = SheepState.Wander;

    [Header("Fear")]
    [SerializeField, Min(0f), Tooltip("The sheep keeps fleeing this long after the wolf left its sight (s). " +
                                      "Without it, a sheep at the limit of its vision would hesitate between fleeing and coming back.")]
    private float fearDuration = 2f;

    private WolfBehaviour nearestWolf;
    private GrassBehaviour nearestGrass;
    private Vector3 lastWolfPosition;
    private float lastWolfSeenTime = float.NegativeInfinity;

    public SheepState State => state;

    private bool IsScared => Time.time - lastWolfSeenTime < fearDuration;

    protected override void UpdateStateMachine()
    {
        if (IsAlive(nearestWolf))
        {
            lastWolfPosition = nearestWolf.transform.position;
            lastWolfSeenTime = Time.time;
        }

        state = PickState();
        switch (state)
        {
            case SheepState.Flee:
                StopMeal();
                Move(transform.position - lastWolfPosition, runSpeed);
                PlayAnimation(RunAnimation);
                break;
            case SheepState.Eat:
                PlayAnimation(EatAnimation);
                break;
            case SheepState.SeekGrass:
                MoveTo(nearestGrass.transform.position, walkSpeed);
                PlayAnimation(WalkAnimation);
                break;
            case SheepState.SeekMate:
                MoveTo(partner.transform.position, walkSpeed);
                PlayAnimation(WalkAnimation);
                break;
            default:
                Wander();
                PlayAnimation(WalkAnimation);
                break;
        }
    }

    /// <summary>Transitions. The first rule that applies wins: the order of the rules is the priority.</summary>
    private SheepState PickState()
    {
        if (IsScared)
            return SheepState.Flee;                 // 1. Survive: a wolf seen less than fearDuration ago interrupts everything.
        if (IsEating)
            return SheepState.Eat;                  // 2. Finish the meal.
        if (IsHungry && nearestGrass != null && nearestGrass.IsEdible)
            return SheepState.SeekGrass;            // 3. Feed.
        if (CanMate && IsAlive(partner) && partner.CanMate)
            return SheepState.SeekMate;             // 4. Reproduce.
        return SheepState.Wander;                   // 5. Explore.
    }

    protected override void ForgetTargets()
    {
        base.ForgetTargets();
        nearestWolf = null;
        nearestGrass = null;
    }

    protected override void Notice(Collider other, float sqrDistance)
    {
        if (other.TryGetComponent(out WolfBehaviour wolf))
        {
            if (!wolf.IsDead && IsCloser(nearestWolf, sqrDistance))
                nearestWolf = wolf;
        }
        else if (other.TryGetComponent(out GrassBehaviour grass))
        {
            if (grass.IsEdible && IsCloser(nearestGrass, sqrDistance))
                nearestGrass = grass;
        }
        else if (other.TryGetComponent(out SheepBehaviour sheep))
        {
            ConsiderPartner(sheep, sqrDistance);
        }
    }

    /// <summary>Contacts trigger the interactions: SeekGrass → Eat, SeekMate → birth.</summary>
    private void OnTriggerStay(Collider other)
    {
        if (IsDead)
            return;

        if (state == SheepState.SeekGrass && other.TryGetComponent(out GrassBehaviour grass) && grass.TryGraze())
        {
            StartMeal();
            state = SheepState.Eat;
            simulation.OnGrassEaten();
        }
        else if (state == SheepState.SeekMate && other.TryGetComponent(out SheepBehaviour mate)
                 && mate.state == SheepState.SeekMate)
        {
            MateWith(mate);
        }
    }
}
