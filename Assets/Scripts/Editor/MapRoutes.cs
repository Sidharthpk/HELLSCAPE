using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// Where the van is driven in each scene: the line the map tools (audit, colliders) work along.
public static class MapRoutes
{
    // areas: discs (x, ground y, z, radius) driven over as well, off the line (the boss arena)
    public static Vector3[] DriveLine(Scene scene, out Vector4[] areas)
    {
        areas = null;
        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.gameObject.scene == scene).ToArray();
        var line = new List<Vector3>();

        // the hell city: the escape road, garage to bridge, and the arena at its end
        var spawner = all.Select(t => t.GetComponent<RoadObstacleSpawner>()).FirstOrDefault(s => s != null);
        if (spawner != null && spawner.roadPoints != null)
        {
            var park = all.FirstOrDefault(t => t.name == GarageBuilder.ParkName);
            line.AddRange(spawner.roadPoints.Where(p => p != null).Select(p => p.position));
            if (park != null && Flat(line[0] - park.position).magnitude > 1.5f) line.Insert(0, park.position);
            var arena = all.FirstOrDefault(t => t.name == "Boss arena");
            if (arena != null) areas = new[] { new Vector4(arena.position.x, arena.position.y + 0.7f, arena.position.z, 62f) };
            return line.ToArray();
        }

        // the prologue: the morning's delivery round (a child "Drive line" of Deliveries, in driving order)
        var drive = all.FirstOrDefault(t => t.name == "Drive line" && t.parent != null && t.parent.name == "Deliveries");
        if (drive != null) foreach (Transform p in drive) line.Add(p.position);
        return line.ToArray();
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    // the same line without the garage's own legs (bay, door, driveway): just the street, for looking sideways from
    public static Vector3[] StreetLine(Scene scene)
    {
        var line = DriveLine(scene, out _).ToList();
        var garage = GarageBuilder.Find(scene);
        if (garage == null || line.Count < 5) return line.ToArray();
        Vector3 bay = garage.Find(GarageBuilder.ParkName) != null ? garage.Find(GarageBuilder.ParkName).position : garage.position;
        if (Flat(line[0] - bay).magnitude < 1.5f) line.RemoveAt(0);                                  // the hell city: starts in the bay
        else if (Flat(line[line.Count - 1] - bay).magnitude < 1.5f)
        {
            // the prologue: ends mouth, door, bay; and the round itself ends at the garage (the home street beyond is
            // only on the line so the audit looks at it)
            Vector3 mouth = line[line.Count - 3];
            line.RemoveRange(line.Count - 3, 3);
            int near = 0;
            for (int i = 1; i < line.Count; i++) if (Flat(line[i] - mouth).sqrMagnitude < Flat(line[near] - mouth).sqrMagnitude) near = i;
            if (near + 1 < line.Count) line.RemoveRange(near + 1, line.Count - near - 1);
        }
        return line.ToArray();
    }
}
