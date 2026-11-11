using UnityEngine;

// Stand-in sound effects built from noise, so the act works before real clips are dropped in.
// Every script that uses these has an AudioClip field; assign a real clip there to replace them.
public static class ProceduralAudio
{
    const int Rate = 44100;

    static AudioClip Make(string name, float length, System.Func<float, float, float> sample)
    {
        int n = (int)(length * Rate);
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(sample(i / (float)Rate, Random.value * 2f - 1f), -1f, 1f);
        var clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    public static AudioClip Gunshot() => Make("Gunshot", 0.4f, (t, n) =>
        n * Mathf.Exp(-t * 22f) * 0.9f + Mathf.Sin(2f * Mathf.PI * 65f * t) * Mathf.Exp(-t * 10f) * 0.7f);

    public static AudioClip Thud() => Make("Thud", 0.7f, (t, n) =>
        (Mathf.Sin(2f * Mathf.PI * 48f * t) + n * 0.35f) * Mathf.Exp(-t * 6f));

    // a huge footfall: a sub-bass drop that sweeps down and rings out, under a short dull crack
    public static AudioClip Stomp() => Make("Stomp", 1.2f, (t, n) =>
    {
        float phase = 2f * Mathf.PI * (28f * t + 40f * (1f - Mathf.Exp(-t * 7f)) / 7f);
        float body = Mathf.Sin(phase) + 0.5f * Mathf.Sin(phase * 0.5f);
        float env = Mathf.Min(1f, t * 250f) * Mathf.Exp(-t * 3.2f);
        return body * env * 0.8f + n * Mathf.Exp(-t * 45f) * 0.3f;
    });

    public static AudioClip Click() => Make("Click", 0.06f, (t, n) => n * Mathf.Exp(-t * 90f) * 0.6f);

    // a scooter's reedy two-note horn: beep-beeeeep
    public static AudioClip ScooterHorn() => Make("ScooterHorn", 0.9f, (t, n) =>
    {
        if (!(t < 0.18f || (t > 0.26f && t < 0.85f))) return 0f;
        return (Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 415f * t)) * 0.5f + Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 520f * t)) * 0.4f) * 0.55f;
    });

    // a two-stroke flat out: a rasping buzz with its firing pulse (whole cycles in a second, so it loops)
    public static AudioClip ScooterEngine() => Make("ScooterEngine", 1f, (t, n) =>
    {
        float saw = 2f * (t * 118f % 1f) - 1f;
        float pop = Mathf.Abs(Mathf.Sin(Mathf.PI * 59f * t));
        return (saw * 0.5f + n * 0.25f) * (0.35f + 0.65f * pop) * 0.6f;
    });

    // low breathy noise with a slow syllable-like wobble
    public static AudioClip Whisper(float length = 3f)
    {
        float low = 0f;
        return Make("Whisper", length, (t, n) =>
        {
            low = Mathf.Lerp(low, n, 0.18f);
            float syllables = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 3.3f * t) * Mathf.Sin(2f * Mathf.PI * 0.6f * t);
            float edge = Mathf.Min(t / 0.3f, (length - t) / 0.5f, 1f);
            return low * 0.9f * syllables * edge;
        });
    }

    // old hinge: stick-slip pulses ringing a wandering squeal
    public static AudioClip Creak(float length = 2.4f)
    {
        float phase = 0f, freqPhase = 0f;
        return Make("Creak", length, (t, n) =>
        {
            float rate = 18f + 40f * Mathf.PerlinNoise(t * 1.3f, 0.5f);          // grip-slip bursts per second
            phase += rate / Rate;
            float burst = Mathf.Exp(-(phase - Mathf.Floor(phase)) * 7f);
            freqPhase += (420f + 520f * Mathf.PerlinNoise(t * 0.8f, 3.1f)) / Rate;
            float tone = Mathf.Sin(2f * Mathf.PI * freqPhase) + 0.35f * Mathf.Sin(4f * Mathf.PI * freqPhase);
            float edge = Mathf.Min(t / 0.15f, (length - t) / 0.4f, 1f);
            return (tone * burst * 0.55f + n * burst * 0.08f) * edge;
        });
    }

    // rolling rumble for the flood and the wave (loops cleanly)
    public static AudioClip Rumble(float length = 4f)
    {
        float low = 0f;
        return Make("Rumble", length, (t, n) =>
        {
            low = Mathf.Lerp(low, n, 0.02f);
            return low * 4f * (0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 0.5f * t));
        });
    }

    // a TV left on in another room: band-passed hiss with a murmur of speech in it (loops)
    public static AudioClip TvMurmur(float length = 6f)
    {
        float low = 0f, band = 0f;
        return Make("TvMurmur", length, (t, n) =>
        {
            low = Mathf.Lerp(low, n, 0.35f);
            band = Mathf.Lerp(band, low, 0.5f);
            float talk = 0.35f + 0.65f * Mathf.Clamp01(Mathf.Sin(2f * Mathf.PI * 4.5f * t) * Mathf.Sin(2f * Mathf.PI * 0.5f * t) * 2f);
            return (low - band) * 1.6f * talk + n * 0.02f;
        });
    }

    // rooftop wind / falling air (loops)
    public static AudioClip Wind(float length = 5f)
    {
        float low = 0f;
        return Make("Wind", length, (t, n) =>
        {
            low = Mathf.Lerp(low, n, 0.04f + 0.03f * Mathf.Sin(2f * Mathf.PI * t / length * 2f));
            return low * 5f * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * t / length));
        });
    }

    // title-screen drone: detuned low tones slowly beating against each other over a wind hiss (loops cleanly:
    // every frequency fits a whole number of cycles in the clip)
    public static AudioClip Drone(float length = 16f)
    {
        float low = 0f;
        return Make("Drone", length, (t, n) =>
        {
            low = Mathf.Lerp(low, n, 0.01f);
            float swell = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * t / length * 2f);
            float tone = Mathf.Sin(2f * Mathf.PI * 41.25f * t) * 0.35f
                       + Mathf.Sin(2f * Mathf.PI * 41.5f * t) * 0.3f
                       + Mathf.Sin(2f * Mathf.PI * 61.875f * t) * 0.18f * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / length * 3f))
                       + Mathf.Sin(2f * Mathf.PI * 123.75f * t) * 0.05f;
            return (tone * swell + low * 3f) * 0.6f;
        });
    }
}

// Code-built blood spray (moved here from Zombie so the killer bleeds the same way).
public static class BloodFx
{
    static Material fallbackMat;
    static Material poolMat;

    public static void Spray(Vector3 point, Vector3 direction, int count, float speed, Material mat = null, float size = 1f)
    {
        Quaternion rot = direction.sqrMagnitude > 0.001f ? Quaternion.LookRotation(direction) : Quaternion.identity;

        var go = new GameObject("BloodSpray");
        go.transform.SetPositionAndRotation(point, rot);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // must be stopped before changing duration

        var main = ps.main;
        main.duration = 0.3f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f * size, 0.14f * size);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.35f, 0f, 0f), new Color(0.6f, 0.02f, 0.02f));
        main.gravityModifier = 1.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.05f;

        if (mat == null && fallbackMat == null)
            fallbackMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = Drop() };   // (round: with no texture every drop was a square)
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.material = mat != null ? mat : fallbackMat;
        rend.renderMode = ParticleSystemRenderMode.Stretch;   // drops pulled out along their flight
        rend.velocityScale = 0.05f;
        rend.lengthScale = 1.2f;

        ps.Play();
        Object.Destroy(go, 2f);
    }

    static Texture2D Drop()
    {
        var t = new Texture2D(32, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float r = Mathf.Sqrt((x - 15.5f) * (x - 15.5f) + (y - 15.5f) * (y - 15.5f)) / 16f;
                t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - r) * 3f)));
            }
        t.Apply();
        return t;
    }

    // a wet pool lying on the ground, with a ragged edge: scale it up to spread it
    public static GameObject Pool(Vector3 at, Transform parent)
    {
        if (poolMat == null)
        {
            poolMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            poolMat.SetTexture("_BaseMap", PoolTexture());
            poolMat.SetColor("_BaseColor", new Color(0.3f, 0.012f, 0.012f, 1f));
            poolMat.SetFloat("_Smoothness", 0.85f);
            poolMat.SetFloat("_Surface", 1f);
            poolMat.SetFloat("_Blend", 0f);
            poolMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            poolMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            poolMat.SetFloat("_ZWrite", 0f);
            poolMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            poolMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(q.GetComponent<Collider>());
        q.name = "Blood pool";
        q.transform.SetParent(parent, true);
        q.transform.SetPositionAndRotation(at + Vector3.up * 0.04f, Quaternion.Euler(90f, Random.value * 360f, 0f));
        q.transform.localScale = Vector3.one * 0.01f;
        var r = q.GetComponent<Renderer>();
        r.sharedMaterial = poolMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return q;
    }

    static Texture2D PoolTexture()
    {
        const int N = 256;
        var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x - N * 0.5f) / (N * 0.5f), dy = (y - N * 0.5f) / (N * 0.5f);
                float a = Mathf.Atan2(dy, dx), r = Mathf.Sqrt(dx * dx + dy * dy);
                // the edge wanders by angle (sines of it: no seam at +-pi), with fine noise for the tongues
                float edge = 0.62f + 0.12f * Mathf.Sin(a * 3f + 1f) + 0.08f * Mathf.Sin(a * 7f + 2f) + 0.05f * Mathf.Sin(a * 13f);
                edge += 0.16f * (Mathf.PerlinNoise(dx * 4f + 9f, dy * 4f + 3f) - 0.5f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((edge - r) * 22f)));
            }
        t.Apply();
        return t;
    }
}
