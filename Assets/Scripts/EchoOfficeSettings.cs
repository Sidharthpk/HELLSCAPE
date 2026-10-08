using UnityEngine;

[CreateAssetMenu(menuName = "HellScape/Echo office materials")]
public class EchoOfficeSettings : ScriptableObject
{
    [System.Serializable]
    public struct MaterialPair { public Material burnt, before; }
    public MaterialPair[] materials;
    [Range(.1f, 3f)] public float transitionSeconds = .85f;
    public float exposure = .85f;
    public Color tint = new Color(.68f, .83f, 1f);
}
