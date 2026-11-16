using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Gives every button in every scene a click and a hover sound, without wiring each one by hand.
/// Buttons are found when they appear (it looks again every second, for menus built at runtime).
/// </summary>
public class UiSounds : MonoBehaviour
{
    readonly HashSet<Button> hooked = new();
    float scan;

    void Update()
    {
        scan -= Time.unscaledDeltaTime;
        if (scan > 0f) return;
        scan = 1f;
        hooked.RemoveWhere(b => b == null);   // forget buttons destroyed with their scene
        foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include))
        {
            if (!hooked.Add(button)) continue;
            button.onClick.AddListener(() => Sfx.PlayFlat("ui_click"));
            if (button.GetComponent<UiHoverSound>() == null) button.gameObject.AddComponent<UiHoverSound>();
        }
    }
}

/// <summary>A soft tick when the pointer moves onto a button.</summary>
public class UiHoverSound : MonoBehaviour, IPointerEnterHandler
{
    Selectable selectable;

    void Awake() => selectable = GetComponent<Selectable>();

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (selectable != null && !selectable.IsInteractable()) return;
        Sfx.PlayFlat("ui_hover");
    }
}
