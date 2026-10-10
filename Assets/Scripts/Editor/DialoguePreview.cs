using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Render the actual dialogue components in an isolated preview scene. Never edits an open scene.
[InitializeOnLoad]
public static class DialoguePreview
{
    const string Request = "Library/DialoguePreview.request";
    static DialoguePreview()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            Render();
        };
    }

    [MenuItem("HellScape/UI/Preview Cinematic Dialogue")]
    public static void Render()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Directory.CreateDirectory("docs/media/dialogue");
        try
        {
            Shot(1920, 1080, true, "choices-wide");
            Shot(1280, 720, true, "choices-720p");
            Shot(900, 1200, true, "choices-narrow");
            Shot(1920, 1080, false, "subtitle");
            File.WriteAllText("Library/DialoguePreview.result", "PASS: four response callbacks, text fit and screen bounds at 1920x1080, 1280x720, 900x1200; subtitle render.");
            Debug.Log("Cinematic dialogue previews and layout checks passed. See docs/media/dialogue.");
        }
        catch (Exception e)
        {
            File.WriteAllText("Library/DialoguePreview.result", e.ToString());
            Debug.LogException(e);
        }
    }

    static void Shot(int width, int height, bool choices, string name)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        RenderTexture target = null;
        Texture2D pixels = null;
        var previous = RenderTexture.active;
        try
        {
            var cameraObject = new GameObject("Preview Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = scene;
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.065f, .075f, .10f);
            target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            var canvasObject = new GameObject("Preview Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            // Use the same reference density as the game's 1920x1080 story canvas.
            canvas.scaleFactor = Mathf.Sqrt((width / 1920f) * (height / 1080f));
            var owner = new GameObject("Dialogue", typeof(RectTransform), typeof(CanvasGroup));
            owner.transform.SetParent(canvasObject.transform, false);
            Canvas.ForceUpdateCanvases();
            DialoguePresentation view;
            if (choices)
            {
                var controllerObject = new GameObject("Choice Controller", typeof(RectTransform));
                controllerObject.transform.SetParent(canvasObject.transform, false);
                owner.transform.SetParent(controllerObject.transform, false);
                var controller = controllerObject.AddComponent<ChoiceDialogue>();
                controller.panel = owner;
                view = DialoguePresentation.Choices(controller);
                int selected = -1;
                view.ShowChoices("ELIAS", "You read the report. Tell me what you left out.", new[]
                {
                    "I changed the statement.", "Derek said it was an accident.",
                    "Let Maya and Sam go. This is between us.", "..."
                }, DialoguePresentation.Blood, i => selected = i);
                var buttons = owner.GetComponentsInChildren<Button>();
                for (int i = 0; i < buttons.Length; i++)
                {
                    buttons[i].onClick.Invoke();
                    if (selected != i) throw new Exception("Response callback mismatch: " + i);
                }
            }
            else
            {
                var controller = owner.AddComponent<DialogueBox>();
                controller.group = owner.GetComponent<CanvasGroup>();
                view = DialoguePresentation.Subtitle(controller);
                controller.nameText.text = "ELIAS";
                controller.text.text = "You didn't have to kill him. You only had to make it look like nobody did.";
                controller.text.maxVisibleCharacters = int.MaxValue;
                controller.group.alpha = 1f;
            }
            Canvas.ForceUpdateCanvases();
            view.RefreshLine();
            Canvas.ForceUpdateCanvases();
            string issue = null;
            foreach (var text in owner.GetComponentsInChildren<TextMeshProUGUI>())
            {
                text.ForceMeshUpdate();
                if (text.preferredHeight > text.rectTransform.rect.height + 3f)
                    issue += name + ": text exceeds height: " + text.name + " " + text.preferredHeight + "/" + text.rectTransform.rect.height + "\n";
                var corners = new Vector3[4];
                text.rectTransform.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var point = RectTransformUtility.WorldToScreenPoint(camera, corner);
                    if (point.x < -1 || point.x > width + 1 || point.y < -1 || point.y > height + 1)
                        issue += name + ": text outside screen: " + text.name + "\n";
                }
            }
            camera.Render();
            RenderTexture.active = target;
            pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes("docs/media/dialogue/" + name + ".png", pixels.EncodeToPNG());
            if (issue != null) throw new Exception(issue);
        }
        finally
        {
            RenderTexture.active = previous;
            EditorSceneManager.ClosePreviewScene(scene);
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
            if (target != null) UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
