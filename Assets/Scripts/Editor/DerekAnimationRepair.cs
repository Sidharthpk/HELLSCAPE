using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Rebuild the reference pose AFTER selecting the actor in a two-person take.
public static class DerekAnimationRepair
{
    public static void ConfigureReferencePose(ModelImporter importer, string suffix)
    {
        var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(importer.assetPath));
        try
        {
            var desc = importer.humanDescription;
            var bones = model.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name);
            Transform Bone(string name) => bones[name + suffix];
            void Aim(string from, string to, Vector3 direction)
            {
                var a = Bone(from);
                a.rotation = Quaternion.FromToRotation(Bone(to).position - a.position, direction) * a.rotation;
            }
            // The FBXs are exported in the first action pose, not a T pose. Merely changing the
            // Humanoid mapping leaves the second actor's bent arms as the avatar's zero angles.
            var hips = Bone("pelvis");
            Vector3 right = Bone("thigh_r").position - Bone("thigh_l").position;
            Vector3 up = Bone("spine_01").position - hips.position;
            hips.rotation = Quaternion.Inverse(Quaternion.LookRotation(Vector3.Cross(right, up), up)) * hips.rotation;
            Aim("pelvis", "spine_01", Vector3.up);
            Aim("spine_01", "spine_02", Vector3.up);
            Aim("spine_02", "spine_03", Vector3.up);
            Aim("spine_03", "neck_01", Vector3.up);
            Aim("neck_01", "neck_02", Vector3.up);
            Aim("neck_02", "head", Vector3.up);
            foreach (string side in new[] { "l", "r" })
            {
                Vector3 outward = side == "l" ? Vector3.left : Vector3.right;
                Aim("clavicle_" + side, "upperarm_" + side, outward);
                Aim("upperarm_" + side, "lowerarm_" + side, outward);
                Aim("lowerarm_" + side, "hand_" + side, outward);
                Aim("hand_" + side, "middle_01_" + side, outward);
                Aim("thigh_" + side, "calf_" + side, Vector3.down);
                Aim("calf_" + side, "foot_" + side, Vector3.down);
                Aim("foot_" + side, "ball_" + side, Vector3.forward);
            }
            var human = desc.human;
            for (int i = 0; i < human.Length; i++)
            {
                if (human[i].humanName == "Neck") human[i].boneName = "neck_01" + suffix;
                if (human[i].humanName == "LeftToes") human[i].boneName = "ball_l" + suffix;
                if (human[i].humanName == "RightToes") human[i].boneName = "ball_r" + suffix;
            }
            desc.human = human;
            desc.skeleton = desc.skeleton.Select(s =>
            {
                if (bones.TryGetValue(s.name, out var t))
                { s.position = t.localPosition; s.rotation = t.localRotation; s.scale = t.localScale; }
                return s;
            }).ToArray();
            importer.humanDescription = desc;
        }
        finally { UnityEngine.Object.DestroyImmediate(model); }
    }

    [MenuItem("HellScape/People: Repair Derek Animations")]
    public static void Repair()
    {
        foreach (string name in new[] { "Derek Stabbed", "Derek Kills Tomas" })
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath("Assets/Materials/Derek/" + name + ".fbx");
            ConfigureReferencePose(importer, name == "Derek Stabbed" ? "_2" : "");
            importer.SaveAndReimport();
        }
        ConfigureTapeProp();
    }

    public static void ConfigureTapeProp()
    {
        var tape = UnityEngine.Object.FindObjectsByType<StabCutscene>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(s => s.name == "Cutscene - Derek kills Tomas");
        if (tape == null || tape.actors == null) return;
        var clone = UnityEngine.Object.Instantiate(tape.actors);
        try
        {
            clone.SetActive(true);
            var replacement = clone.GetComponentsInChildren<Animator>().FirstOrDefault(a => a.isHuman);
            if (replacement == null) return;
            foreach (var a in clone.GetComponentsInChildren<Animator>())
            { a.cullingMode = AnimatorCullingMode.AlwaysAnimate; a.Rebind(); a.Play("Stab", 0, 0); a.Update(0); }
            var prop = clone.GetComponentsInChildren<Transform>(true).First(t => t.name == "weapon_KitchenKnife");
            var hand = replacement.GetBoneTransform(HumanBodyBones.RightHand);
            var source = prop.GetComponent<SkinnedMeshRenderer>();
            var mesh = new Mesh { name = "Held Knife" };
            source.BakeMesh(mesh, false);
            // The prop is skinned to the old actor. Bake it once into a rigid hand-local
            // mesh so the hidden skeleton can no longer pull it away from Derek's grip.
            Vector3 grip = prop.TransformPoint(mesh.bounds.center) - Vector3.forward * .1f;
            mesh.vertices = mesh.vertices.Select(v => hand.InverseTransformVector(prop.TransformPoint(v) - grip)).ToArray();
            mesh.normals = mesh.normals.Select(n => hand.InverseTransformDirection(prop.TransformDirection(n))).ToArray();
            mesh.RecalculateBounds();
            const string meshPath = "Assets/Materials/Derek/Held Knife.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, meshPath); saved = mesh; }
            else { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); }
            var liveHand = tape.actors.transform.Find(AnimationUtility.CalculateTransformPath(hand, clone.transform));
            var held = liveHand.Find("Derek held knife");
            if (held == null)
            {
                var go = new GameObject("Derek held knife", typeof(MeshFilter), typeof(MeshRenderer));
                Undo.RegisterCreatedObjectUndo(go, "Attach Derek's knife");
                held = go.transform;
                held.SetParent(liveHand, false);
            }
            held.GetComponent<MeshFilter>().sharedMesh = saved;
            held.GetComponent<MeshRenderer>().sharedMaterials = source.sharedMaterials;
            var original = tape.actors.transform.Find(AnimationUtility.CalculateTransformPath(prop, clone.transform)).GetComponent<Renderer>();
            Undo.RecordObject(original, "Hide original knife");
            original.enabled = false;
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(tape.gameObject.scene);
            EditorSceneManager.SaveScene(tape.gameObject.scene);
        }
        finally { UnityEngine.Object.DestroyImmediate(clone); }
    }

    public static void Preview(string label)
    {
        string police = AssetDatabase.FindAssets("Character_20_Police t:Model").Select(AssetDatabase.GUIDToAssetPath).First();
        var preview = new PreviewRenderUtility();
        var sheet = new Texture2D(1280, 640, TextureFormat.RGB24, false);
        try
        {
            preview.camera.backgroundColor = new Color(.15f, .17f, .2f);
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.nearClipPlane = .01f;
            preview.camera.farClipPlane = 100f;
            preview.lights[0].intensity = 1.3f;
            preview.lights[0].transform.rotation = Quaternion.Euler(30, 30, 0);
            preview.lights[1].intensity = .8f;
            for (int row = 0; row < 2; row++)
            {
                string name = row == 0 ? "Derek Stabbed" : "Derek Kills Tomas";
                var tape = UnityEngine.Object.FindObjectsByType<StabCutscene>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(s => s.name == "Cutscene - Derek kills Tomas");
                var g = UnityEngine.Object.Instantiate(row == 1 && tape != null ? tape.actors : AssetDatabase.LoadAssetAtPath<GameObject>(police));
                preview.AddSingleGO(g);
                g.SetActive(true);
                g.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                g.transform.localScale = Vector3.one * (row == 1 && tape != null ? tape.actors.transform.lossyScale.x : .38f);
                var a = g.GetComponentsInChildren<Animator>().First(an => an.isHuman);
                var previewMaterials = new System.Collections.Generic.List<Material>();
                foreach (var r in g.GetComponentsInChildren<Renderer>())
                {
                    if (r is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
                    r.sharedMaterials = r.sharedMaterials.Select(m => {
                        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                        mat.mainTexture = m.mainTexture;
                        previewMaterials.Add(mat);
                        return mat;
                    }).ToArray();
                }
                a.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Materials/Derek/" + name + ".controller");
                a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                a.applyRootMotion = false;
                a.Rebind();
                for (int col = 0; col < 4; col++)
                {
                    float time = new[] { 0f, .16f, .45f, .99f }[col];
                    foreach (var actor in g.GetComponentsInChildren<Animator>())
                    {
                        actor.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                        actor.Play("Stab", 0, time);
                        actor.Update(0);
                    }
                    // Multiple samples render within one editor frame. Bake each pose so
                    // the render pipeline cannot reuse the first sample's skinning buffer.
                    var visibleSkins = g.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s => s.enabled).ToArray();
                    var bakedObjects = new System.Collections.Generic.List<GameObject>();
                    foreach (var skin in visibleSkins)
                    {
                        var baked = new Mesh();
                        skin.BakeMesh(baked, false);
                        var rigid = new GameObject("Pose sample", typeof(MeshFilter), typeof(MeshRenderer));
                        rigid.transform.SetParent(skin.transform, false);
                        Vector3 scale = skin.transform.lossyScale;
                        rigid.transform.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
                        rigid.GetComponent<MeshFilter>().sharedMesh = baked;
                        rigid.GetComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                        skin.enabled = false;
                        bakedObjects.Add(rigid);
                    }
                    preview.camera.transform.position = new Vector3(3, 2, 4);
                    preview.camera.transform.LookAt(new Vector3(0, .8f, 0));
                    preview.camera.orthographic = true;
                    preview.camera.orthographicSize = 1.4f;
                    preview.BeginPreview(new Rect(0, 0, 320, 320), GUIStyle.none);
                    preview.Render(true);
                    var rt = (RenderTexture)preview.EndPreview();
                    RenderTexture.active = rt;
                    sheet.ReadPixels(new Rect(0, 0, 320, 320), col * 320, (1-row) * 320);
                    RenderTexture.active = null;
                    foreach (var rigid in bakedObjects)
                    {
                        UnityEngine.Object.DestroyImmediate(rigid.GetComponent<MeshFilter>().sharedMesh);
                        UnityEngine.Object.DestroyImmediate(rigid);
                    }
                    foreach (var skin in visibleSkins) skin.enabled = true;
                }
                UnityEngine.Object.DestroyImmediate(g);
                foreach (var material in previewMaterials) UnityEngine.Object.DestroyImmediate(material);
            }
            sheet.Apply();
            Directory.CreateDirectory("Temp/DerekReview");
            File.WriteAllBytes("Temp/DerekReview/" + label + ".png", sheet.EncodeToPNG());
        }
        finally { preview.Cleanup(); UnityEngine.Object.DestroyImmediate(sheet); }
    }
}
