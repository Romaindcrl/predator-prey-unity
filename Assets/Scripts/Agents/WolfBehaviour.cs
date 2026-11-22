using UnityEngine;

/// <summary>States of the wolf. The graph is in the README.</summary>
public enum WolfState { Wander, Chase, Eat, SeekMate }

/// <summary>
/// A wolf explores, hunts the nearest sheep when it is hungry and mates with other wolves.
/// PickState() holds the transitions in priority order, UpdateStateMachine() performs the current state.
/// </summary>
public class WolfBehaviour : AnimalBehaviour
{
    [SerializeField, Tooltip("Current state (updated in Play mode).")]
    private WolfState state = WolfState.Wander;

    private SheepBehaviour nearestSheep;

    public WolfState State => state;

    protected override void UpdateStateMachine()
    {
        state = PickState();
        switch (state)
        {
            case WolfState.Eat:
                PlayAnimation(EatAnimation);
                break;
            case WolfState.Chase:
                MoveTo(nearestSheep.transform.position, runSpeed);
                PlayAnimation(RunAnimation);
                break;
            case WolfState.SeekMate:
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
    private WolfState PickState()
    {
        if (IsEating)
            return WolfState.Eat;                   // 1. Finish the meal.
        if (IsHungry && IsAlive(nearestSheep))
            return WolfState.Chase;                 // 2. Hunt.
        if (CanMate && IsAlive(partner) && partner.CanMate)
            return WolfState.SeekMate;              // 3. Reproduce.
        return WolfState.Wander;                    // 4. Explore.
    }

    protected override void ForgetTargets()
    {
        base.ForgetTargets();
        nearestSheep = null;
    }

    protected override void Notice(Collider other, float sqrDistance)
    {
        if (other.TryGetComponent(out SheepBehaviour sheep))
        {
            if (!sheep.IsDead && IsCloser(nearestSheep, sqrDistance))
                nearestSheep = sheep;
        }
        else if (other.TryGetComponent(out WolfBehaviour wolf))
        {
            ConsiderPartner(wolf, sqrDistance);
        }
    }

    /// <summary>Contacts trigger the interactions: Chase → Eat (the sheep dies), SeekMate → birth.</summary>
    private void OnTriggerStay(Collider other)
    {
        if (IsDead)
            return;

        if (state == WolfState.Chase && other.TryGetComponent(out SheepBehaviour sheep) && !sheep.IsDead)
        {
            sheep.Die(DeathCause.Predation);
            StartMeal();
            state = WolfState.Eat;
        }
        else if (state == WolfState.SeekMate && other.TryGetComponent(out WolfBehaviour mate)
                 && mate.state == WolfState.SeekMate)
        {
            MateWith(mate);
        }
    }
}
