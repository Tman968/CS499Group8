using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Pooled 2D point lights for brief flashes: lightning strikes, bursts, shattering ice. Each flash
/// fades out over its duration and its light goes back to the pool.
/// </summary>
public class ElementLights : MonoBehaviour
{
    const int PoolSize = 24;

    class Flash
    {
        public Light2D light;
        public float age, duration, intensity;
        public bool flicker;
    }

    static ElementLights instance;
    readonly List<Flash> active = new();
    readonly Stack<Light2D> pool = new();

    static ElementLights Instance
    {
        get
        {
            if (instance == null && Application.isPlaying) new GameObject("ElementLights").AddComponent<ElementLights>();
            return instance;
        }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        for (var i = 0; i < PoolSize; i++) pool.Push(MakeLight(transform));
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>Makes a point light, off, under parent. Also used by streams for their steady glow.</summary>
    public static Light2D MakeLight(Transform parent)
    {
        var go = new GameObject("ElementLight");
        go.transform.SetParent(parent, false);
        var light = go.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.pointLightInnerRadius = 0.1f;
        light.pointLightOuterRadius = 2f;
        light.intensity = 0f;
        light.falloffIntensity = 0.6f;
        light.enabled = false;
        return light;
    }

    /// <summary>Lights up radius around pos, fading out over duration seconds.</summary>
    public static void Burst(Vector2 pos, Color color, float radius, float intensity, float duration, bool flicker = false)
    {
        var self = Instance;
        if (self == null) return;
        Light2D light;
        if (self.pool.Count > 0) light = self.pool.Pop();
        else if (self.active.Count > 0)
        {
            // Out of lights: steal the one closest to finishing.
            var oldest = self.active[0];
            self.active.RemoveAt(0);
            light = oldest.light;
        }
        else return;

        light.transform.position = new Vector3(pos.x, pos.y, 0f);
        light.color = color;
        light.pointLightOuterRadius = radius;
        light.pointLightInnerRadius = radius * 0.08f;
        light.intensity = intensity;
        light.enabled = true;
        self.active.Add(new Flash { light = light, duration = duration, intensity = intensity, flicker = flicker });
    }

    void Update()
    {
        var dt = Time.deltaTime;
        for (var i = active.Count - 1; i >= 0; i--)
        {
            var f = active[i];
            f.age += dt;
            if (f.age >= f.duration)
            {
                f.light.enabled = false;
                pool.Push(f.light);
                active.RemoveAt(i);
                continue;
            }
            var k = 1f - f.age / f.duration;
            k *= k;
            if (f.flicker) k *= 0.6f + 0.4f * Mathf.PerlinNoise(f.age * 40f, i);
            f.light.intensity = f.intensity * k;
        }
    }
}
