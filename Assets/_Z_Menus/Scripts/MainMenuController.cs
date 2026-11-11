using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShepherdsJourney.Menus
{
    /// <summary>
    /// Runs the Main Menu at play time. The menu buttons call the public methods on this
    /// component (Start Game, Options, Credits, Quit, Back).
    ///
    /// The menu has three "panels" (groups of UI). Only one is shown at a time:
    ///   - the main panel (title and the four buttons),
    ///   - the Options panel,
    ///   - the Credits panel.
    ///
    /// The button connections are made by the editor tool
    /// <c>Tools &gt; Shepherds Journey &gt; Build Main Menu</c> (see MainMenuSceneBuilder.cs),
    /// but you can also connect them by hand in each Button's On Click () list.
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuController : MonoBehaviour
    {
        // NOTE: The editor tool sets these private fields by name through SerializedObject.
        // If you rename a field here, update the matching name in MainMenuSceneBuilder.cs too.

        [Header("Scenes")]
        [Tooltip("Name of the scene that Start Game loads. The scene must be in the build scene list " +
                 "(File > Build Profiles), or Unity cannot load it.")]
        [SerializeField] private string gameSceneName = "MainLevel";

        [Header("Panels")]
        [Tooltip("The panel with the title and the main buttons.")]
        [SerializeField] private GameObject mainPanel;

        [Tooltip("The panel shown when the player presses Options.")]
        [SerializeField] private GameObject optionsPanel;

        [Tooltip("The panel shown when the player presses Credits.")]
        [SerializeField] private GameObject creditsPanel;

        /// <summary>
        /// Unity calls Start once, before the first frame. By then every OnEnable has run,
        /// including the EventSystem's, so EventSystem.current is ready and we can select a button.
        /// </summary>
        private void Start()
        {
            // Always open on the main panel, even if a panel was left on in the scene by mistake.
            ShowOnly(mainPanel);
        }

        /// <summary>
        /// Loads the game scene. The Start Game button calls this.
        /// </summary>
        public void StartGame()
        {
            if (string.IsNullOrWhiteSpace(gameSceneName))
            {
                Debug.LogError("[MainMenu] No game scene name is set on MainMenuController.", this);
                return;
            }

            // Check first, so a missing scene gives a clear message
            // instead of Unity's more confusing load error.
            if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
            {
                Debug.LogError($"[MainMenu] Cannot load scene '{gameSceneName}'. Check the name, and make sure " +
                               "the scene is in the build scene list (File > Build Profiles).", this);
                return;
            }

            SceneManager.LoadScene(gameSceneName);
        }

        /// <summary>
        /// Shows the Options panel and hides the others. The Options button calls this.
        /// </summary>
        public void OpenOptions()
        {
            ShowOnly(optionsPanel);
        }

        /// <summary>
        /// Shows the Credits panel and hides the others. The Credits button calls this.
        /// </summary>
        public void OpenCredits()
        {
            ShowOnly(creditsPanel);
        }

        /// <summary>
        /// Goes back to the main panel. The Back buttons on the Options and Credits panels call this.
        /// </summary>
        public void ClosePanels()
        {
            ShowOnly(mainPanel);
        }

        /// <summary>
        /// Quits the game. The Quit button calls this.
        /// </summary>
        public void Quit()
        {
            // Application.Quit() does nothing in the Unity Editor, so in the editor we stop Play mode
            // instead. "#if UNITY_EDITOR" means this code only exists in the editor. Without it,
            // the UnityEditor reference would break player builds.
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// Turns on <paramref name="panelToShow"/>, turns off every other panel, and selects
        /// that panel's first button.
        /// </summary>
        /// <param name="panelToShow">The panel that should be visible.</param>
        private void ShowOnly(GameObject panelToShow)
        {
            SetPanelActive(mainPanel, mainPanel == panelToShow);
            SetPanelActive(optionsPanel, optionsPanel == panelToShow);
            SetPanelActive(creditsPanel, creditsPanel == panelToShow);

            SelectFirstSelectable(panelToShow);
        }

        /// <summary>
        /// Turns a panel on or off. Skips it if the reference was never set,
        /// so a missing panel can't throw an error.
        /// </summary>
        private static void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
            {
                panel.SetActive(active);
            }
        }

        /// <summary>
        /// Selects the first button (or other Selectable) inside a panel.
        /// Why: gamepad and keyboard players move around a menu from the selected button.
        /// If nothing is selected, they can't use the menu at all.
        /// </summary>
        private static void SelectFirstSelectable(GameObject panel)
        {
            if (panel == null || EventSystem.current == null)
            {
                return;
            }

            Selectable first = panel.GetComponentInChildren<Selectable>();
            if (first != null)
            {
                EventSystem.current.SetSelectedGameObject(first.gameObject);
            }
        }
    }
}
