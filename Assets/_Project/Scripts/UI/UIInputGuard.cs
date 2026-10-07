using UnityEngine.EventSystems;

/// <summary>
/// True when the mouse is over a UI element that blocks raycasts (buttons,
/// window backgrounds). Used so UI clicks don't orbit the camera or change target.
/// </summary>
public static class UIInputGuard
{
    public static bool IsPointerOverUI() =>
        EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
}
