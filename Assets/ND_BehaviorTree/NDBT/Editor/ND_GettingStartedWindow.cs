using UnityEditor;
using UnityEngine;

namespace ND_BehaviorTree.Editor
{
    // This class uses InitializeOnLoad to show the window automatically when Unity opens
    [InitializeOnLoad]
    public static class ND_GettingStartedWindowAutoLoader
    {
        // Static constructor runs on editor load
        static ND_GettingStartedWindowAutoLoader()
        {
            // Use delayCall to ensure the editor is fully initialized before we try to open a window
            EditorApplication.delayCall += () =>
            {
                // Use SessionState to ensure the window only opens once per editor session
                if (!SessionState.GetBool("ND_BT_GettingStartedShown", false))
                {
                    SessionState.SetBool("ND_BT_GettingStartedShown", true);
                    ND_GettingStartedWindow.OpenWindow();
                }
            };
        }
    }

    public class ND_GettingStartedWindow : EditorWindow
    {
        private GUIStyle _headerStyle;
        private GUIStyle _subHeaderStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _wrappedLabelStyle;
        private Vector2 _scrollPosition;

        private const string DocumentationURL = "https://nddevvlog.github.io/ND_BehaviorTreeDocumentPage";
        private const string DiscordURL = "https://discord.gg/your-discord-invite"; // <-- TODO: Replace with your Discord link
        private const string AssetStoreURL = "https://assetstore.unity.com/packages/slug/your-asset-id"; // <-- TODO: Replace with your Asset Store link

        [MenuItem("ND_BehaviorTree/Getting Started", false, 0)]
        public static void OpenWindow()
        {
            ND_GettingStartedWindow window = (ND_GettingStartedWindow)GetWindow(typeof(ND_GettingStartedWindow), false, "ND Behavior Tree");
            window.minSize = new Vector2(400, 450);
            window.maxSize = new Vector2(400, 450);
            window.Show();
        }

        // We initialize styles inside OnGUI because it's the only place where
        // we can be sure that all GUI resources are loaded and available.
        private void InitializeStyles()
        {
            _headerStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(0, 0, 10, 10)
            };

            _subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                margin = new RectOffset(5, 0, 10, 5)
            };
            
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fixedHeight = 35
            };
            
            _wrappedLabelStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                margin = new RectOffset(5, 5, 0, 10)
            };
        }

        private void OnGUI()
        {
            // Lazy initialization of styles.
            if (_headerStyle == null)
            {
                InitializeStyles();
            }

            // Main Header
            GUILayout.Label("ND Behavior Tree", _headerStyle);
            DrawSeparator();

            // Main Content Area
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, false, false);
            
            EditorGUILayout.BeginVertical(new GUIStyle{ padding = new RectOffset(10, 10, 10, 10) });

            // --- Documentation Section ---
            DrawSection("📖 Documentation",
            "The official documentation provides a complete overview of the asset, including setup guides, component explanations, and API references.",
            "Open Online Documentation",
            () => Application.OpenURL(DocumentationURL));

            // --- Community Section ---
            //DrawSection("💬 Community & Support", "Have a question or want to show off your work? Join our Discord community! For bugs or feature requests, please use the GitHub issue tracker.", "Join our Discord", () => Application.OpenURL(DiscordURL));

            // --- Review Section ---
            //DrawSection("⭐ Enjoying ND Behavior Tree?", "If you find this asset helpful, please consider leaving a review on the Unity Asset Store. Your feedback is greatly appreciated and helps support future development!", "Leave a Review", () => Application.OpenURL(AssetStoreURL));
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Draws a reusable section with a header, description, and a call-to-action button.
        /// </summary>
        private void DrawSection(string title, string description, string buttonText, System.Action buttonAction)
        {
            GUILayout.Label(title, _subHeaderStyle);
            GUILayout.Label(description, _wrappedLabelStyle);
            
            if (GUILayout.Button(buttonText, _buttonStyle))
            {
                buttonAction?.Invoke();
            }
            DrawSeparator();
        }

        /// <summary>
        /// Draws a simple horizontal line.
        /// </summary>
        private void DrawSeparator()
        {
            EditorGUILayout.Space(10);
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.1f)); // A subtle separator line
            EditorGUILayout.Space(10);
        }
    }
}