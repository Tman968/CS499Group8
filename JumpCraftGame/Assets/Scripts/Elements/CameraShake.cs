using UnityEngine;

/// <summary>
/// Screen shake as "trauma" that decays: shakes stack up to a limit, and the camera offset grows
/// with trauma squared, so small knocks barely register and big blasts really rattle. CameraFollow
/// adds Offset to its position every frame.
/// </summary>
public static class CameraShake
{
    static float trauma;
    static float lastTime = -1f;
    static Vector3 offset;

    const float MaxOffset = 0.35f;
    const float DecayPerSecond = 1.6f;

    /// <summary>Adds trauma: 0.1 is a nudge, 0.5 a blast, 1 the most there is.</summary>
    public static void Add(float amount) => trauma = Mathf.Clamp01(trauma + amount);

    /// <summary>The camera offset for this frame.</summary>
    public static Vector3 Offset
    {
        get
        {
            if (Time.time != lastTime)
            {
                lastTime = Time.time;
                trauma = Mathf.Max(0f, trauma - DecayPerSecond * Time.deltaTime);
                var shake = trauma * trauma * MaxOffset;
                var t = Time.time * 32f;
                offset = new Vector3((Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f * shake,
                                     (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * shake, 0f);
            }
            return offset;
        }
    }
}
