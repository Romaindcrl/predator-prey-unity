using PredatorPrey;
using UnityEditor;

[CustomEditor(typeof(OrganismView))]
public sealed class OrganismViewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        Organism body = ((OrganismView)target).Body;
        if (body == null) return;
        EditorGUILayout.Space();
        if (body is Animal animal)
        {
            EditorGUILayout.LabelField("State", animal.State.ToString());
            EditorGUILayout.LabelField("Energy", animal.Energy.ToString("0.0") + " / 100");
            EditorGUILayout.LabelField("Target", animal.Target == null ? "—" : animal.Target.Species + " " + animal.Target.Id);
        }
        else EditorGUILayout.LabelField("Grass", body.Available ? "Edible" : "Regrowing");
    }

    public override bool RequiresConstantRepaint() => ((OrganismView)target).Body != null;
}
