using UnityEngine;
using UnityEngine.Rendering;

// Applied before the office blackout starts, including when loading an existing saved scene.
public class OfficeAtmosphere : MonoBehaviour
{
    InteriorDarkness darkness;
    ParticleSystem dust;
    Material dustMaterial;
    Material panelMaterial;

    public void Initialize(InteriorDarkness zone)
    {
        darkness = zone;
        zone.fogOfWar = true;
        // No sun and no lamps until the fuses are in: the only light is what comes in through the windows (the
        // "Window" lights stay on through the blackout), the rooms without windows stay dark. No red emergency lamps.
        zone.emergencyLamps = false;
        zone.fogDensity = 0.045f;
        zone.blackFog = new Color(0.025f, 0.012f, 0.012f);
        zone.poweredFog = new Color(0.035f, 0.035f, 0.04f);
        zone.poweredFogScale = 0.7f;
        // dark, not black: enough to make out a room's shape with your eyes used to it
        zone.emergencyAmbient = new Color(0.032f, 0.024f, 0.022f);
        zone.darkAmbient = zone.emergencyAmbient;
        zone.poweredAmbient = new Color(0.08f, 0.08f, 0.085f);

        var panels = GetComponent<PanelLights>();
        if (panels != null && panels.glowing != null)
        {
            var original = panels.glowing;
            panelMaterial = new Material(original) { name = "Office tube panels" };
            if (panelMaterial.HasProperty("_EmissionColor"))
                panelMaterial.SetColor("_EmissionColor", new Color(0.9f, 0.95f, 1f) * 1.1f);
            if (panelMaterial.HasProperty("_BaseColor"))
                panelMaterial.SetColor("_BaseColor", new Color(0.8f, 0.82f, 0.85f));
            foreach (var r in panels.renderers)
            {
                if (r == null) continue;
                var materials = r.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] == original) materials[i] = panelMaterial;
                r.sharedMaterials = materials;
            }
            panels.glowing = panelMaterial;
        }
        CreateDust();
    }

    void CreateDust()
    {
        var shader = Resources.Load<Shader>("OfficeDust");
        if (shader == null) return;
        var go = new GameObject("Office ash");
        go.transform.SetParent(transform, false);
        dust = go.AddComponent<ParticleSystem>();
        dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = dust.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 15f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.04f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.05f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.16f, 0.15f, 0.14f, 0.55f), new Color(0.42f, 0.4f, 0.38f, 0.7f));   // grey ash flakes
        main.gravityModifier = 0.004f;   // (drifting down, slowly)
        main.maxParticles = 900;
        var emission = dust.emission;
        emission.rateOverTime = 75f;
        var shape = dust.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(14f, 3f, 14f);
        var noise = dust.noise;
        noise.enabled = true;
        noise.strength = 0.22f;
        noise.frequency = 0.25f;
        noise.scrollSpeed = 0.08f;
        var fade = dust.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        dustMaterial = new Material(shader);
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = dustMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    void LateUpdate()
    {
        if (dust == null || darkness == null || darkness.player == null) return;
        bool inside = darkness.player.gameObject.activeInHierarchy && darkness.zone.Contains(darkness.player.position);
        if (!inside)
        {
            if (dust.isPlaying) dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return;
        }
        Vector3 size = Vector3.Min(new Vector3(14f, 3f, 14f), darkness.zone.size);
        Vector3 position = darkness.player.position + Vector3.up;
        Vector3 min = darkness.zone.min + size * 0.5f;
        Vector3 max = darkness.zone.max - size * 0.5f;
        position = new Vector3(Mathf.Clamp(position.x, min.x, max.x), Mathf.Clamp(position.y, min.y, max.y), Mathf.Clamp(position.z, min.z, max.z));
        dust.transform.position = position;
        var shape = dust.shape;
        shape.scale = size;
        if (!dust.isPlaying) dust.Play();
    }

    void OnDestroy()
    {
        if (dustMaterial != null) Destroy(dustMaterial);
        if (panelMaterial != null) Destroy(panelMaterial);
    }
}
