using UnityEngine;

// The dream hall (HellScape > Void: Dream Hall builds Resources/DreamHall.prefab): the cathedral nave from "interior test",
// and the Demon King's throne with the King seated in it. EndingSequence.DreamWalk plays the first death in it.
// The nave is the same every `bay` metres, so the walk is endless: whenever you have walked one bay the whole prefab is
// moved one bay with you and nothing you can see changes.
public class DreamHall : MonoBehaviour
{
    public Renderer[] hall;             // the cathedral (one mesh); its material is recoloured as the dream sours
    public Transform seat;              // throne + King, black, facing -Z, standing on the floor at the nave axis; off until the end
    public GameObject[] people;         // the stalkers: ordinary people, black, feet on the origin, switched off (a copy of one is made for each)
    public Transform kingHead;         // his head: where the camera looks
    public Transform kingBack;          // behind the throne: where the red glow goes
    public float bay = 6.09f;           // metres between two columns along Z
    public float armLength = 42.6f;     // the east arm from z 0 (its first bay) to its end at z = armLength; the seat stands near the end
    public float length = 82f;          // the whole model along the nave: another copy placed one length on continues it end to end
    public float width = 11f;           // how far from the axis the walkway is walled in
}
