using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// This namespace ends in "EditorTools", not "Editor". A namespace named "...Editor" hides
// Unity's own UnityEditor.Editor class, which causes confusing errors in custom inspectors later.
namespace ShepherdsJourney.Menus.EditorTools
{
    /// <summary>
    /// Editor-only tool that builds the Main Menu UI in MainMenu.unity.
    ///
    /// How to use: in Unity, choose <c>Tools &gt; Shepherds Journey &gt; Build Main Menu</c>.
    ///
    /// The tool is safe to run again. Each run deletes the old "MainMenuCanvas" and builds a
    /// fresh one. Changes you made by hand to that canvas will be lost, so make lasting
    /// changes here in the script instead.
    ///
    /// Because this file is inside a folder named "Editor", Unity only compiles it for the editor.
    /// It is never included in a player build.
    /// </summary>
    public static class MainMenuSceneBuilder
    {
        // ---------------------------------------------------------------------------------
        // Settings. Change these to restyle the menu, then run the tool again.
        // ---------------------------------------------------------------------------------

        private const string MenuPath = "Tools/Shepherds Journey/Build Main Menu";
        private const string ScenePath = "Assets/_Z_Menus/Scenes/MainMenu.unity";
        private const string CanvasName = "MainMenuCanvas";
        private const string GameTitle = "Shepherds Journey";

        /// <summary>The screen size the UI is designed for. The CanvasScaler scales from this size.</summary>
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        private static readonly Color BackgroundColor = new Color(0.10f, 0.12f, 0.10f, 1f);
        private static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.06f, 0.96f);
        private static readonly Color ButtonColor = new Color(0.85f, 0.80f, 0.68f, 1f);
        private static readonly Color ButtonTextColor = new Color(0.15f, 0.12f, 0.08f, 1f);
        private static readonly Color TitleColor = new Color(0.96f, 0.93f, 0.84f, 1f);

        private const float ButtonWidth = 500f;
        private const float ButtonHeight = 90f;
        private const float ButtonSpacing = 24f;

        // These names must match the private [SerializeField] fields in MainMenuController.
        // We set them through SerializedObject, the same way the Inspector does (see WireController).
        private const string FieldMainPanel = "mainPanel";
        private const string FieldOptionsPanel = "optionsPanel";
        private const string FieldCreditsPanel = "creditsPanel";

        // ---------------------------------------------------------------------------------
        // Menu item
        // ---------------------------------------------------------------------------------

        /// <summary>
        /// Builds the whole Main Menu. The [MenuItem] attribute adds this method to Unity's top menu bar.
        /// </summary>
        [MenuItem(MenuPath)]
        public static void BuildMainMenu()
        {
            // 1. Make sure TextMesh Pro is ready. Without its settings asset, text has no default font.
            //    We use Resources.Load rather than TMP_Settings.instance because the instance getter
            //    can open the TMP import window. We only want to check.
            if (Resources.Load<TMP_Settings>("TMP Settings") == null)
            {
                EditorUtility.DisplayDialog("Build Main Menu",
                    "TextMesh Pro Essential Resources were not found.\n\n" +
                    "Import them with Window > TextMeshPro > Import TMP Essential Resources, then try again.",
                    "OK");
                return;
            }

            // 2. Let the user save unsaved work before we switch scenes.
            //    This returns false if they press Cancel. Then we stop and change nothing.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            // 3. Open the Main Menu scene. "Single" closes the other open scenes.
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[MainMenuSceneBuilder] Could not open scene at '{ScenePath}'.");
                return;
            }

            // 4. Group everything below into ONE undo step, so a single Ctrl+Z undoes the whole build.
            Undo.SetCurrentGroupName("Build Main Menu");
            int undoGroup = Undo.GetCurrentGroup();

            // 5. Delete the canvas from an earlier run, so running the tool twice can't make duplicates.
            RemoveExistingCanvas(scene);

            // 6. Build the new UI.
            GameObject canvas = BuildCanvas();
            MainMenuController controller = canvas.AddComponent<MainMenuController>();

            CreateBackground(canvas.transform);
            GameObject mainPanel = CreateMainPanel(canvas.transform, controller);
            GameObject optionsPanel = CreateInfoPanel(canvas.transform, "OptionsPanel", "Options",
                "Options are coming soon.", controller);
            GameObject creditsPanel = CreateInfoPanel(canvas.transform, "CreditsPanel", "Credits",
                "Made by the Shepherds Journey team.", controller);

            WireController(controller, mainPanel, optionsPanel, creditsPanel);

            // Record the canvas as "created" only now, once its whole hierarchy exists.
            // Undo then saves the canvas and all its children together, so redo can rebuild all of it.
            Undo.RegisterCreatedObjectUndo(canvas, "Create Main Menu Canvas");

            // 7. Make sure the scene can receive input from the new Input System.
            EnsureEventSystem(scene);

            Undo.CollapseUndoOperations(undoGroup);

            // 8. Save. MarkSceneDirty tells Unity the scene changed. Without it, SaveScene may
            //    decide there is nothing to save.
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeGameObject = canvas;
            Debug.Log($"[MainMenuSceneBuilder] Main Menu built and saved in '{ScenePath}'.");
        }

        /// <summary>
        /// Validation for the menu item. Unity calls this method (the "true" in the attribute
        /// marks it as validation) to decide whether the menu item is enabled. We gray it out
        /// in Play mode, because changes made then are not saved.
        /// </summary>
        [MenuItem(MenuPath, true)]
        private static bool BuildMainMenuValidate()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        // ---------------------------------------------------------------------------------
        // Scene-level steps
        // ---------------------------------------------------------------------------------

        /// <summary>
        /// Deletes any root GameObject named <see cref="CanvasName"/> from an earlier run.
        /// </summary>
        private static void RemoveExistingCanvas(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == CanvasName)
                {
                    // Use Undo instead of Object.DestroyImmediate, so Ctrl+Z can bring the old canvas back.
                    Undo.DestroyObjectImmediate(root);
                }
            }
        }

        /// <summary>
        /// Creates the Canvas root with the components every screen-space UI needs.
        /// </summary>
        private static GameObject BuildCanvas()
        {
            // Passing component types to the GameObject constructor adds them right away.
            GameObject canvasObject = new GameObject(CanvasName,
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.layer = LayerMask.NameToLayer("UI");

            // Overlay draws the UI on top of everything and needs no camera. That's the usual choice for menus.
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // "Scale With Screen Size" keeps the layout the same on any resolution. We design at
            // 1920x1080. A match of 0.5 balances width and height for screens with other shapes.
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // GraphicRaycaster (added above) lets the mouse and touch click UI on this canvas.
            return canvasObject;
        }

        /// <summary>
        /// Makes sure the scene has exactly what the new Input System needs to drive the UI:
        /// an EventSystem with an InputSystemUIInputModule.
        /// </summary>
        private static void EnsureEventSystem(Scene scene)
        {
            EventSystem eventSystem = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                eventSystem = root.GetComponentInChildren<EventSystem>(true);
                if (eventSystem != null)
                {
                    break;
                }
            }

            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
                Undo.RegisterCreatedObjectUndo(eventSystemObject, "Create EventSystem");
                eventSystem = eventSystemObject.GetComponent<EventSystem>();
            }

            // The project uses the new Input System only (Project Settings > Player > Active Input
            // Handling). The old StandaloneInputModule reads the old input system, which would throw
            // errors every frame, so we swap it out.
            StandaloneInputModule oldModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (oldModule != null)
            {
                Undo.DestroyObjectImmediate(oldModule);
            }

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                InputSystemUIInputModule module = Undo.AddComponent<InputSystemUIInputModule>(eventSystem.gameObject);

                // Give the module the Input System's default UI actions (navigate, submit, point,
                // click...). Then mouse, keyboard and gamepad all work without extra setup.
                module.AssignDefaultActions();
            }
        }

        /// <summary>
        /// Gives the controller its panel references. The fields are private [SerializeField]s,
        /// so normal code can't set them. SerializedObject edits the saved data directly, the same
        /// way the Inspector does. That keeps the fields private, which is good encapsulation.
        /// </summary>
        private static void WireController(MainMenuController controller,
            GameObject mainPanel, GameObject optionsPanel, GameObject creditsPanel)
        {
            SerializedObject serialized = new SerializedObject(controller);

            SetObjectField(serialized, FieldMainPanel, mainPanel);
            SetObjectField(serialized, FieldOptionsPanel, optionsPanel);
            SetObjectField(serialized, FieldCreditsPanel, creditsPanel);

            // "WithoutUndo" is fine here: the controller is brand new, and the canvas's
            // created-object undo record already covers it.
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Sets one object-reference field by name. Logs a clear error if the field doesn't exist
        /// (usually because it was renamed in MainMenuController).
        /// </summary>
        private static void SetObjectField(SerializedObject serialized, string fieldName, Object value)
        {
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"[MainMenuSceneBuilder] MainMenuController has no serialized field " +
                               $"'{fieldName}'. Was it renamed? Update the constant in MainMenuSceneBuilder.");
                return;
            }

            property.objectReferenceValue = value;
        }

        // ---------------------------------------------------------------------------------
        // Building the panels
        // ---------------------------------------------------------------------------------

        /// <summary>
        /// Full-screen background image. It's created first, so it draws behind everything
        /// (UI draws in hierarchy order, top to bottom).
        /// </summary>
        private static void CreateBackground(Transform parent)
        {
            RectTransform background = CreateUIObject("Background", parent);
            StretchToParent(background);

            Image image = background.gameObject.AddComponent<Image>();
            image.color = BackgroundColor;
            image.raycastTarget = false; // Decoration only. It shouldn't block clicks.
        }

        /// <summary>
        /// Builds the main panel: the game title and a column of four buttons.
        /// </summary>
        private static GameObject CreateMainPanel(Transform parent, MainMenuController controller)
        {
            RectTransform panel = CreateUIObject("MainPanel", parent);
            StretchToParent(panel);

            // Title, anchored to the top-center of the screen.
            TextMeshProUGUI title = CreateText("Title", panel, GameTitle, 120f, FontStyles.Bold, TitleColor);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(1600f, 200f));

            // Button column. VerticalLayoutGroup stacks the children for us, so adding a fifth button
            // later just means calling CreateButton once more. No positions to work out.
            RectTransform column = CreateUIObject("ButtonColumn", panel);
            Place(column, new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), new Vector2(ButtonWidth, 0f));

            VerticalLayoutGroup layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = ButtonSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;       // Buttons fill the column width...
            layout.childControlHeight = true;      // ...and use their LayoutElement height.
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // ContentSizeFitter grows the column's height to fit its buttons exactly.
            ContentSizeFitter fitter = column.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Create each button and connect it to the matching controller method.
            // AddPersistentListener saves the connection into the scene, so it shows in the Button's
            // On Click () list, the same as dragging it in by hand. (onClick.AddListener would only
            // last until the scene closes. It's for connections made at runtime.)
            Button startButton = CreateButton("StartGameButton", column, "Start Game");
            UnityEventTools.AddPersistentListener(startButton.onClick, controller.StartGame);

            Button optionsButton = CreateButton("OptionsButton", column, "Options");
            UnityEventTools.AddPersistentListener(optionsButton.onClick, controller.OpenOptions);

            Button creditsButton = CreateButton("CreditsButton", column, "Credits");
            UnityEventTools.AddPersistentListener(creditsButton.onClick, controller.OpenCredits);

            Button quitButton = CreateButton("QuitButton", column, "Quit");
            UnityEventTools.AddPersistentListener(quitButton.onClick, controller.Quit);

            return panel.gameObject;
        }

        /// <summary>
        /// Builds a simple full-screen panel with a heading, body text, and a Back button.
        /// Used as a placeholder for Options and Credits. The panel starts hidden.
        /// </summary>
        private static GameObject CreateInfoPanel(Transform parent, string objectName, string heading,
            string body, MainMenuController controller)
        {
            RectTransform panel = CreateUIObject(objectName, parent);
            StretchToParent(panel);

            // This Image also blocks clicks from reaching the main buttons behind it
            // (raycastTarget stays on, the default).
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = PanelColor;

            TextMeshProUGUI headingText = CreateText("Heading", panel, heading, 80f, FontStyles.Bold, TitleColor);
            Place(headingText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(1200f, 140f));

            TextMeshProUGUI bodyText = CreateText("Body", panel, body, 40f, FontStyles.Normal, TitleColor);
            Place(bodyText.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 400f));

            Button backButton = CreateButton("BackButton", panel, "Back");
            Place((RectTransform)backButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 160f),
                new Vector2(ButtonWidth * 0.8f, ButtonHeight));
            UnityEventTools.AddPersistentListener(backButton.onClick, controller.ClosePanels);

            // Hidden at start. MainMenuController shows it when its button is pressed.
            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }

        // ---------------------------------------------------------------------------------
        // Small UI helpers
        // ---------------------------------------------------------------------------------

        /// <summary>
        /// Creates an empty UI GameObject (with a RectTransform) under <paramref name="parent"/>.
        /// </summary>
        private static RectTransform CreateUIObject(string objectName, Transform parent)
        {
            GameObject uiObject = new GameObject(objectName, typeof(RectTransform));
            uiObject.layer = LayerMask.NameToLayer("UI");

            RectTransform rectTransform = (RectTransform)uiObject.transform;
            // "false" = keep the local position and scale. With true, Unity would try to keep the
            // world position, which gives odd scales and offsets on UI.
            rectTransform.SetParent(parent, false);
            return rectTransform;
        }

        /// <summary>
        /// Creates a TextMesh Pro UI text object.
        /// </summary>
        private static TextMeshProUGUI CreateText(string objectName, Transform parent, string content,
            float fontSize, FontStyles style, Color color)
        {
            RectTransform rectTransform = CreateUIObject(objectName, parent);
            TextMeshProUGUI text = rectTransform.gameObject.AddComponent<TextMeshProUGUI>();

            // Use the project's default TMP font (set in TMP Settings), so every menu shares one font.
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false; // Text shouldn't catch clicks meant for buttons.
            return text;
        }

        /// <summary>
        /// Creates a Button with a background Image and a TextMesh Pro label.
        /// </summary>
        private static Button CreateButton(string objectName, Transform parent, string label)
        {
            RectTransform rectTransform = CreateUIObject(objectName, parent);

            // The Image is the button's clickable area and its look. Unity's built-in rounded "UISprite"
            // is the same sprite you get from GameObject > UI > Button. Sliced keeps its corners sharp
            // at any size.
            Image image = rectTransform.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = ButtonColor;

            Button button = rectTransform.gameObject.AddComponent<Button>();
            button.targetGraphic = image; // The Image is what tints on hover and press.

            // The VerticalLayoutGroup reads this to size the button.
            LayoutElement layoutElement = rectTransform.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = ButtonWidth;
            layoutElement.preferredHeight = ButtonHeight;

            TextMeshProUGUI text = CreateText("Label", rectTransform, label, 44f, FontStyles.Bold, ButtonTextColor);
            StretchToParent(text.rectTransform);

            return button;
        }

        /// <summary>
        /// Makes a RectTransform fill its whole parent (anchors at the corners, no offsets).
        /// </summary>
        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Anchors a RectTransform to one point of its parent (for example top-center = (0.5, 1)),
        /// then offsets and sizes it. The pivot matches the anchor, so the offset is measured
        /// from that edge. For example, y = -180 at the top means "180 units below the top".
        /// </summary>
        private static void Place(RectTransform rectTransform, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            rectTransform.anchorMin = anchor;
            rectTransform.anchorMax = anchor;
            rectTransform.pivot = anchor;
            rectTransform.anchoredPosition = offset;
            rectTransform.sizeDelta = size;
        }
    }
}
