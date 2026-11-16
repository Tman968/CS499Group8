using UnityEngine;

/// <summary>Passes the swinging blade's hits up to its PendulumHazard.</summary>
public class PendulumBlade : MonoBehaviour, IElementReactive
{
    public PendulumHazard hazard;

    public bool BlocksMotes => true;

    public void OnElement(Element element, float power, Vector2 point, Vector2 direction) =>
        hazard.OnElement(element, power, point, direction);

    void OnCollisionEnter2D(Collision2D collision) => hazard.Struck(collision.collider);
}
