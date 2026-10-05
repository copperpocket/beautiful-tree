using UnityEngine;

/// <summary>
/// Temporary debug overlay. Shows each Animator layer's name, weight and
/// current state in the top-left of the Game view. Attach to the object
/// that has the Animator. Remove or disable when done.
/// </summary>
[RequireComponent(typeof(Animator))]
public class AnimatorLayerDebug : MonoBehaviour
{
    private Animator animator;
    private GUIStyle style;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void OnGUI()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            style.normal.textColor = Color.yellow;
        }

        float y = 10f;
        for (int i = 0; i < animator.layerCount; i++)
        {
            string layerName = animator.GetLayerName(i);
            float weight = i == 0 ? 1f : animator.GetLayerWeight(i);
            var state = animator.GetCurrentAnimatorStateInfo(i);

            GUI.Label(new Rect(10f, y, 700f, 26f),
                $"{i}: {layerName}   weight {weight:F2}   stateHash {state.shortNameHash}",
                style);
            y += 26f;
        }
    }
}
