using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// A repeatable placement pass that preserves the existing interactables and story events.
public static class OfficeDocumentBuilder
{
    const string Art = "Assets/Resources/OfficeDocuments/";
    const string Materials = "Assets/Generated/Office/Documents/";
    static readonly Vector3 Desk = new Vector3(982f, 3.4f, 892f);   // the open-plan floor of the v3 office (plan e 0, n 22)
    const float Top = .84f;

    [MenuItem("HellScape/Office/Place Readable Documents In Main Hall")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play before placing documents.");
        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var office = all.First(t => t.name == "Office" && t.parent != null && t.parent.name == "OfficeAct");
        Undo.RegisterFullObjectHierarchyUndo(office.gameObject, "Place main hall documents");
        System.IO.Directory.CreateDirectory(Materials);
        AssetDatabase.Refresh();
        foreach (var file in new[] { "AprilCalendar", "WitnessStatement", "DeskNote" })
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(Art + file + ".png");
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.maxTextureSize = 4096;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.filterMode = FilterMode.Bilinear;
            imp.mipmapEnabled = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
        }
        var station = office.Find("Your desk - main office hall");
        if (station == null)
        {
            station = new GameObject("Your desk - main office hall").transform;
            station.SetParent(office, false);
            Undo.RegisterCreatedObjectUndo(station.gameObject, "Create your desk");
        }
        station.position = Desk;
        station.rotation = Quaternion.identity;
        station.localScale = Vector3.one;
        var wood = Material("Desk walnut", new Color(.24f, .16f, .105f));
        var metal = Material("Desk dark steel", new Color(.075f, .085f, .09f));
        var cork = Material("Calendar backing", new Color(.28f, .22f, .15f));
        Part(station, "Desktop", new Vector3(0, Top - .045f, 0), new Vector3(2.2f, .09f, .95f), wood);
        foreach (float x in new[] { -.96f, .96f }) foreach (float z in new[] { -.34f, .34f })
            Part(station, "Leg " + x + " " + z, new Vector3(x, (Top - .09f) * .5f, z), new Vector3(.075f, Top - .09f, .075f), metal);
        Part(station, "Drawer unit", new Vector3(.76f, .49f, 0), new Vector3(.5f, .53f, .78f), wood);
        Part(station, "Drawer handle", new Vector3(.76f, .56f, .405f), new Vector3(.23f, .035f, .04f), metal);
        Part(station, "Calendar board", new Vector3(-.66f, 1.43f, -.43f), new Vector3(.74f, 1.08f, .065f), cork);
        Part(station, "Board support", new Vector3(-.66f, .98f, -.44f), new Vector3(.07f, .3f, .07f), metal);
        Part(station, "Lamp foot", new Vector3(.89f, Top + .025f, -.28f), new Vector3(.2f, .05f, .2f), metal);
        Part(station, "Lamp stem", new Vector3(.89f, Top + .25f, -.28f), new Vector3(.035f, .48f, .035f), metal);
        Part(station, "Lamp shade", new Vector3(.78f, Top + .49f, -.22f), new Vector3(.33f, .07f, .22f), metal);
        var lamp = station.Find("Reading lamp");
        if (lamp == null) { lamp = new GameObject("Reading lamp").transform; lamp.SetParent(station, false); }
        lamp.localPosition = new Vector3(.45f, 1.34f, .15f);
        if (!lamp.TryGetComponent<Light>(out var light)) light = lamp.gameObject.AddComponent<Light>();
        light.type = LightType.Point; light.color = new Color(1f, .88f, .68f);
        light.intensity = .8f; light.range = 3.6f; light.shadows = LightShadows.None;

        if (!station.TryGetComponent<NavMeshObstacle>(out var obstacle)) obstacle = station.gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box; obstacle.center = new Vector3(0, .7f, 0);
        obstacle.size = new Vector3(2.22f, 1.4f, .98f); obstacle.carving = true;
        var plaque = station.Find("Your desk label");
        if (plaque == null) { plaque = new GameObject("Your desk label").transform; plaque.SetParent(station, false); }
        Part(station, "Name plate", new Vector3(.5f, .74f, .486f), new Vector3(.7f, .12f, .018f), metal);
        plaque.localPosition = new Vector3(.5f, .74f, .5f);
        plaque.localRotation = Quaternion.Euler(0, 180, 0);
        if (!plaque.TryGetComponent<TextMeshPro>(out var label)) label = plaque.gameObject.AddComponent<TextMeshPro>();
        label.text = "YOUR DESK"; label.fontSize = .55f;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.color = new Color(.94f, .89f, .73f); label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta = new Vector2(.65f, .12f);

        Document(office, "Calendar", "AprilCalendar", Desk + new Vector3(-.66f, 1.43f, -.389f), Quaternion.Euler(0, 180, 0), new Vector2(.64f, .96f));
        Document(office, "IncidentReport", "WitnessStatement", Desk + new Vector3(-.04f, Top + .012f, -.025f), Quaternion.LookRotation(Vector3.down, Vector3.back), new Vector2(.43f, .645f));
        Document(office, "YourDesk_Note", "DeskNote", Desk + new Vector3(.55f, Top + .014f, .075f), Quaternion.LookRotation(Vector3.down, Vector3.back), new Vector2(.36f, .36f));
        var oldLabel = office.Find("Calendar label");
        if (oldLabel != null) oldLabel.gameObject.SetActive(false);
        var oldGlow = office.Find("Calendar glow");
        if (oldGlow != null) oldGlow.gameObject.SetActive(false);
        var oldSign = office.Find("Your desk sign");
        if (oldSign != null) oldSign.gameObject.SetActive(false);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(office.gameObject.scene);
        EditorSceneManager.SaveScene(office.gameObject.scene);
        Selection.activeGameObject = station.gameObject;
        Debug.Log("Main hall: desk, calendar, witness statement and new note placed with original clue wiring intact.");
    }

    static Material Material(string name, Color color)
    {
        string path = Materials + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
        material.color = color;
        material.SetFloat("_Smoothness", .2f);
        return material;
    }

    static void Part(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        var t = parent.Find(name);
        if (t == null) { t = GameObject.CreatePrimitive(PrimitiveType.Cube).transform; t.name = name; t.SetParent(parent, false); }
        t.localPosition = position; t.localRotation = Quaternion.identity; t.localScale = size;
        t.GetComponent<Renderer>().sharedMaterial = material;
    }

    static void Document(Transform office, string name, string file, Vector3 position, Quaternion rotation, Vector2 size)
    {
        var t = office.Find(name);
        if (t == null) throw new System.InvalidOperationException("Missing story prop: " + name);
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + file + ".png");
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        t.GetComponent<MeshFilter>().sharedMesh = quad.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(quad);
        t.SetPositionAndRotation(position, rotation);
        t.localScale = new Vector3(size.x, size.y, 1);
        var material = Material(file, Color.white);
        material.SetTexture("_BaseMap", sprite.texture);
        material.SetFloat("_Cull", 0);
        material.EnableKeyword("_EMISSION");
        material.SetTexture("_EmissionMap", sprite.texture);
        material.SetColor("_EmissionColor", Color.white * .15f);
        t.GetComponent<Renderer>().sharedMaterial = material;
        var col = t.GetComponent<BoxCollider>(); col.center = Vector3.zero; col.size = new Vector3(1, 1, .018f);
        var inter = t.GetComponent<Interactable>(); inter.once = false; inter.hideOnUse = false;
        inter.prompt = name == "Calendar" ? "Read the April calendar" : name == "IncidentReport" ? "Read the witness statement" : "Read the note on your desk";
        if (!t.TryGetComponent<ReadableNote>(out var note)) note = t.gameObject.AddComponent<ReadableNote>();
        note.artwork = sprite;
        if (name == "Calendar")
        {
            note.style = ReadableNote.Style.Calendar; note.month = "APRIL"; note.firstWeekday = 2; note.circledDay = 13; note.days = 30;
            note.dayNote = "Tomas accident"; note.scrawl = "";
        }
        else if (name == "IncidentReport")
        {
            note.style = ReadableNote.Style.Report; note.title = "SHIFT WITNESS STATEMENT";
            note.body = "13 APRIL - SILO 2\nT. ROURKE\n\nOFFICIAL ACCOUNT\nAccidental fall while unsupervised.\n\nSigning confirms that I witnessed this account.\nThis is not an attendance sheet.";
            note.footer = "Signed: Maya / Sam / Derek / [your signature]\nFiled by Vane.";
        }
        else
        {
            note.style = ReadableNote.Style.BloodySticky;
            note.body = "YOUR KEYS.\nSILO 2.\n\nBRING THE STATEMENT.\nREAD THE LAST NAME.";
            note.footer = "- ELIAS";
        }
        EditorUtility.SetDirty(t.gameObject); EditorUtility.SetDirty(material);
    }
}
