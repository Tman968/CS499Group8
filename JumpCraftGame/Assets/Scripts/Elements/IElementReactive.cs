using UnityEngine;

/// <summary>
/// A world object that responds to the elements: crates and brambles that burn, a pendulum blade
/// that ice slows and lightning charges, a rune lift that lightning powers.
/// </summary>
public interface IElementReactive
{
    /// <summary>An element touched the object. power is about 1 for one mote or bolt.</summary>
    void OnElement(Element element, float power, Vector2 point, Vector2 direction);

    /// <summary>True if motes stop when they hit it (crates, the lift); false if they pass through.</summary>
    bool BlocksMotes { get; }
}
