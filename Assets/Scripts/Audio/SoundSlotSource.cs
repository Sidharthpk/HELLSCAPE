using UnityEngine;

// Put on any AudioSource: its clip and volume come from the SoundLibrary slot named here ("Area/Slot name").
// An empty slot leaves the source as it is.
[RequireComponent(typeof(AudioSource))]
public class SoundSlotSource : MonoBehaviour
{
    public string slot = "Hell City/Hell ambience";

    void Awake() => Sounds.Apply(GetComponent<AudioSource>(), slot);
}
