using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using ND_BehaviorTree;
using System.Linq;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using ND_BehaviorTree.Editor.CustomGraph;

namespace ND_BehaviorTree.Editor
{
    public class ND_BehaviorTreeEditorWindow : EditorWindow
    {
        [MenuItem("ND_BehaviorTree/ND Behavior Tree Window Editor")]
        public static void OpenWindow()
        {
            ND_BehaviorTreeEditorWindow window = GetWindow<ND_BehaviorTreeEditorWindow>();
            window.titleContent = new GUIContent("Behavior Tree Editor");
        }

        public static void Open(BehaviorTreeRunner runner)
        {
            ND_BehaviorTreeEditorWindow window = GetWindow<ND_BehaviorTreeEditorWindow>();
            window.titleContent = new GUIContent("Behavior Tree Editor");
            window.Load(runner);
        }

        public static void Open(BehaviorTree target)
        {
            ND_BehaviorTreeEditorWindow window = GetWindow<ND_BehaviorTreeEditorWindow>();
            window.titleContent = new GUIContent("Behavior Tree Editor");
            window.Load(target);
        }

        [SerializeField] private BehaviorTree m_currentGraph;
        [SerializeField] private SerializedObject m_serializeObject;
        [SerializeField] private ND_BTGraphCanvas m_currentView;
        [SerializeField] public BehaviorTreeRunner m_targetRunner;

        [SerializeField] private bool m_isLocked;
        private VisualElement m_contentContainer;
        private ToolbarToggle m_lockToggle;

        [SerializeField] private bool m_isBlackboardVisible = true;
        private ToolbarToggle m_blackboardToggle;

        private BlackboardView m_blackboardView;

        // --- Live debug ---
        [SerializeField] private bool m_isDebugPanelVisible;
        private ToolbarToggle m_debugPanelToggle;
        private ND_BTDebugPanel m_debugPanel;
        private ToolbarButton m_debugBackButton;
        private Label m_debugBreadcrumb;
        private bool m_liveWasActive;
        private double m_nextInfoRefresh;
        private double m_nextLiveUpdate;
        private bool m_isVisible = true;
        private ToolbarButton m_undoButton;
        private ToolbarButton m_redoButton;
        private ToolbarButton m_copyForAiButton;

        /// <summary>"Copy for AI" is shown only while AI features are enabled in the ND Behavior Tree settings.</summary>
        private void UpdateAiVisibility()
        {
            if (m_copyForAiButton == null) return;
            var display = Bridge.AiAccess.FeatureEnabled ? DisplayStyle.Flex : DisplayStyle.None;
            if (m_copyForAiButton.style.display != display) m_copyForAiButton.style.display = display;
        }

        /// <summary>Subtrees stepped into while live debugging (top = what the canvas shows). Play mode only.</summary>
        private readonly List<(BehaviorTree asset, BehaviorTree runtime, string label)> m_debugStack = new List<(BehaviorTree, BehaviorTree, string)>();

        public BehaviorTree currentGraph => m_currentGraph;
        public BehaviorTreeRunner WatchedRunner => m_targetRunner;

        /// <summary>The runtime tree that matches the graph on screen: the watched agent's tree, or a subtree clone stepped into.</summary>
        public BehaviorTree CurrentDebugRuntime =>
            m_debugStack.Count > 0 ? m_debugStack[m_debugStack.Count - 1].runtime : (m_targetRunner != null ? m_targetRunner.RuntimeTree : null);

        public bool IsLiveDebugging => Application.isPlaying && CurrentDebugRuntime != null;

        #region Saving (Live Save toggle / manual Save)

        private static string LiveSavePrefKey => $"ND_BehaviorTree.LiveSave.{PlayerSettings.productGUID}";
        private ToolbarToggle m_liveSaveToggle;
        private ToolbarButton m_saveButton;

        /// <summary>
        /// On: graph edits are written ~1 s after the last edit. Off: nothing is written automatically; use Save / Ctrl+S.
        /// Remembered per project (EditorPrefs).
        /// </summary>
        public bool LiveSave
        {
            get => EditorPrefs.GetBool(LiveSavePrefKey, true);
            set => EditorPrefs.SetBool(LiveSavePrefKey, value);
        }

        [UnityEditor.ShortcutManagement.Shortcut("ND Behavior Tree/Save Graph", typeof(ND_BehaviorTreeEditorWindow), KeyCode.S, UnityEditor.ShortcutManagement.ShortcutModifiers.Action)]
        private static void SaveShortcut(UnityEditor.ShortcutManagement.ShortcutArguments args) => (args.context as ND_BehaviorTreeEditorWindow)?.SaveGraph();

        /// <summary>Live Save preference for this project (same value the toolbar toggle shows).</summary>
        public static bool IsLiveSaveEnabled => EditorPrefs.GetBool(LiveSavePrefKey, true);

        /// <summary>
        /// A tree was changed outside the canvas (AI bridge). Open windows showing it redraw and save by their Live Save
        /// mode. Returns true if the tree ended up saved to disk.
        /// </summary>
        public static bool OnTreeChangedExternally(BehaviorTree tree, bool save)
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<ND_BehaviorTreeEditorWindow>())
            {
                if (window.m_currentGraph != tree || window.m_currentView == null) continue;
                window.m_currentView.SerializedTree.Update();
                window.m_currentView.Rebuild();
                window.m_blackboardView?.PopulateView();
                if (save) window.m_currentView.RequestSave();
                window.UpdateHistoryButtons();
            }
            if (!save || !IsLiveSaveEnabled) return false; // Live Save off: stays dirty until tree_save or Ctrl+S
            new Graph.BehaviorTreeGraphService(tree).Save();
            return true;
        }

        /// <summary>Edits made outside the canvas (e.g. the node Properties window) follow the same Live Save / manual rule.</summary>
        public static void NotifyTreeEdited(BehaviorTree tree)
        {
            if (tree == null) return;
            foreach (var window in Resources.FindObjectsOfTypeAll<ND_BehaviorTreeEditorWindow>())
            {
                if (window.m_currentGraph == tree) window.m_currentView?.RequestSave();
            }
        }

        public void SaveGraph()
        {
            if (m_currentView == null) return;
            m_currentView.SaveNow();
            ShowNotification(new GUIContent($"Saved {m_currentGraph.name}"), 0.8);
        }

        /// <summary>Unity calls this when the window is closed with unsaved changes and the user chooses Save.</summary>
        public override void SaveChanges()
        {
            m_currentView?.SaveNow();
            base.SaveChanges();
        }

        /// <summary>Before showing another tree: offer to save unsaved manual-mode changes of the current one.</summary>
        private void ConfirmLeaveCurrentGraph()
        {
            if (!hasUnsavedChanges || m_currentView == null || m_currentView.TreeData == null) return;
            if (EditorUtility.DisplayDialog("Unsaved Behavior Tree changes",
                    $"'{m_currentView.TreeData.name}' has unsaved changes (Live Save is off).", "Save", "Keep unsaved"))
            {
                m_currentView.SaveNow();
            }
            SetUnsavedChanges(false); // kept changes stay in memory (asset is dirty) until saved
        }

        #endregion

        private void OnEnable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;

            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;

            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;

            CreateUIStructure();
            OnSelectionChanged();
        }

        private void OnLostFocus() => m_currentView?.FlushPendingSave();

        private void OnDisable()
        {
            m_currentView?.FlushPendingSave();
            EditorApplication.update -= OnEditorUpdate;
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode) m_currentView?.FlushPendingSave();
            if (change != PlayModeStateChange.ExitingPlayMode) return;
            // Runtime clones are destroyed with Play mode: return to the edited asset.
            var rootAsset = m_debugStack.Count > 0 ? m_debugStack[0].asset : null;
            m_debugStack.Clear();
            m_targetRunner = null;
            if (rootAsset != null) m_currentGraph = rootAsset;
            DrawGraph();
            UpdateDebugToolbar();
        }

        private void OnSelectionChanged()
        {
            if (m_isLocked) return;
            if (this == null) return;

            BehaviorTree treeToLoad = null;
            BehaviorTreeRunner runnerToDebug = null;

            GameObject selectedObject = Selection.activeGameObject;

            if (selectedObject != null)
            {
                BehaviorTreeRunner runner = selectedObject.GetComponent<BehaviorTreeRunner>();
                if (runner != null && runner.treeAsset != null)
                {
                    treeToLoad = runner.treeAsset;
                    if (Application.isPlaying)
                    {
                        runnerToDebug = runner;
                    }
                }
            }

            if (treeToLoad == null)
            {
                treeToLoad = Selection.activeObject as BehaviorTree;
            }

            if (runnerToDebug != null)
            {
                if (runnerToDebug != m_targetRunner || m_debugStack.Count > 0) Load(runnerToDebug);
            }
            else if (treeToLoad != null)
            {
                if (m_currentGraph != treeToLoad || m_targetRunner != null)
                {
                    Load(treeToLoad);
                }
            }
            else
            {
                ClearView();
            }
        }

        private void CreateUIStructure()
        {
            rootVisualElement.Clear();
            CreateToolbar();
            m_contentContainer = new VisualElement { name = "ContentContainer" };
            m_contentContainer.style.flexGrow = 1;
            rootVisualElement.Add(m_contentContainer);

            // The debug panel lives outside the content so it stays visible when no graph is loaded.
            m_debugPanel = new ND_BTDebugPanel(this);
            rootVisualElement.Add(m_debugPanel);
            m_debugPanel.style.display = m_isDebugPanelVisible ? DisplayStyle.Flex : DisplayStyle.None;
            UpdateDebugToolbar();
            UpdateSaveButton();
        }

        private void CreateToolbar()
        {
            var toolbar = new Toolbar();

            m_blackboardToggle = new ToolbarToggle { text = "Blackboard" };
            m_blackboardToggle.tooltip = "Show or hide the Blackboard panel.";
            m_blackboardToggle.value = m_isBlackboardVisible;
            m_blackboardToggle.RegisterValueChangedCallback(evt => {
                m_isBlackboardVisible = evt.newValue;
                ToggleBlackboard(m_isBlackboardVisible);
            });
            toolbar.Add(m_blackboardToggle);

            m_liveSaveToggle = new ToolbarToggle { text = "Live Save", tooltip = "On: edits are saved ~1 s after the last change.\nOff: save manually with Save / Ctrl+S." };
            m_liveSaveToggle.SetValueWithoutNotify(LiveSave);
            m_liveSaveToggle.RegisterValueChangedCallback(evt =>
            {
                LiveSave = evt.newValue;
                if (evt.newValue && hasUnsavedChanges) m_currentView?.SaveNow(); // switching on: write what is pending
                UpdateSaveButton();
            });
            toolbar.Add(m_liveSaveToggle);
            m_saveButton = new ToolbarButton(SaveGraph) { text = "Save", tooltip = "Save this tree now (Ctrl+S)." };
            toolbar.Add(m_saveButton);

            m_undoButton = new ToolbarButton(UndoGraph) { text = "↶" };
            m_redoButton = new ToolbarButton(RedoGraph) { text = "↷" };
            toolbar.Add(m_undoButton);
            toolbar.Add(m_redoButton);

            m_copyForAiButton = new ToolbarButton(() =>
            {
                if (m_currentGraph == null) return;
                EditorGUIUtility.systemCopyBuffer = Bridge.CopyForAi.TreeText(m_currentGraph);
                ShowNotification(new GUIContent("Tree reference copied for AI"), 1.0);
            })
            {
                text = "Copy for AI",
                tooltip = "Copy this tree's path, GUID and the command to read it, to paste into an AI prompt."
            };
            toolbar.Add(m_copyForAiButton);
            UpdateAiVisibility();

            toolbar.Add(new ToolbarButton(() => m_currentView?.FocusOnRoot())
            {
                text = "Focus Root",
                tooltip = "Zoom back to 100% and centre the Root node (F frames the selection, or the Root)."
            });

            m_debugPanelToggle = new ToolbarToggle { text = "Live Debug", tooltip = "Show the Live Debug panel: pick which agent to watch in Play mode." };
            m_debugPanelToggle.value = m_isDebugPanelVisible;
            m_debugPanelToggle.RegisterValueChangedCallback(evt =>
            {
                m_isDebugPanelVisible = evt.newValue;
                if (m_debugPanel != null) m_debugPanel.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });
            toolbar.Add(m_debugPanelToggle);

            m_debugBackButton = new ToolbarButton(StepOutOfSubtree) { text = "◀ Back", tooltip = "Return to the parent tree." };
            toolbar.Add(m_debugBackButton);
            m_debugBreadcrumb = new Label { style = { unityTextAlign = TextAnchor.MiddleLeft, marginLeft = 6, color = new Color(1f, 0.8f, 0.2f) } };
            toolbar.Add(m_debugBreadcrumb);

            toolbar.Add(new ToolbarSpacer { style = { flexGrow = 1 } });

            m_lockToggle = new ToolbarToggle { text = "Lock" };
            m_lockToggle.tooltip = "Prevents the editor from changing the tree when you select a different object.";
            m_lockToggle.value = m_isLocked;
            m_lockToggle.RegisterValueChangedCallback(evt => {
                m_isLocked = evt.newValue;
                if (!m_isLocked)
                {
                    OnSelectionChanged();
                }
            });

            toolbar.Add(m_lockToggle);
            rootVisualElement.Add(toolbar);
        }

        public void Load(BehaviorTreeRunner runner)
        {
            if (runner == null || runner.treeAsset == null)
            {
                ClearView();
                return;
            }
            m_debugStack.Clear();
            m_targetRunner = runner;
            m_currentGraph = runner.treeAsset;
            titleContent = new GUIContent($"{runner.gameObject.name} ({runner.treeAsset.name})", EditorGUIUtility.ObjectContent(null, typeof(BehaviorTree)).image);
            DrawGraph();
            UpdateDebugToolbar();
        }

        public void Load(BehaviorTree target)
        {
            if (target == null)
            {
                ClearView();
                return;
            }
            m_debugStack.Clear();
            m_targetRunner = null;
            m_currentGraph = target;
            titleContent = new GUIContent($"{target.name}", EditorGUIUtility.ObjectContent(null, typeof(BehaviorTree)).image);
            DrawGraph();
            UpdateDebugToolbar();
        }

        /// <summary>Live debug panel: watch this agent. Turns on Lock so changing the Unity selection does not switch away.</summary>
        public void WatchRunner(BehaviorTreeRunner runner)
        {
            if (runner == null) return;
            m_isLocked = true;
            m_lockToggle?.SetValueWithoutNotify(true);
            Load(runner);
        }

        /// <summary>Shows a subtree asset driven by the given runtime clone (pushed on the debug stack).</summary>
        public void StepIntoSubtree(BehaviorTree subtreeAsset, BehaviorTree runtimeSubtree, string label)
        {
            if (subtreeAsset == null || runtimeSubtree == null) return;
            if (m_debugStack.Count == 0) m_debugStack.Add((m_currentGraph, m_targetRunner != null ? m_targetRunner.RuntimeTree : null, m_currentGraph.name));
            m_debugStack.Add((subtreeAsset, runtimeSubtree, $"{label} ({subtreeAsset.name})"));
            m_currentGraph = subtreeAsset;
            DrawGraph();
            UpdateDebugToolbar();
        }

        private void StepOutOfSubtree()
        {
            if (m_debugStack.Count < 2) return;
            m_debugStack.RemoveAt(m_debugStack.Count - 1);
            m_currentGraph = m_debugStack[m_debugStack.Count - 1].asset;
            if (m_debugStack.Count == 1) m_debugStack.Clear(); // back at the watched agent's own tree
            DrawGraph();
            UpdateDebugToolbar();
        }

        private void UpdateDebugToolbar()
        {
            if (m_debugBackButton == null) return;
            bool inSubtree = m_debugStack.Count > 1;
            m_debugBackButton.style.display = inSubtree ? DisplayStyle.Flex : DisplayStyle.None;

            string text = string.Empty;
            if (Application.isPlaying && m_targetRunner != null)
            {
                text = $"● Watching {m_targetRunner.gameObject.name}";
                if (inSubtree) text += "  ▸  " + string.Join("  ▸  ", m_debugStack.Skip(1).Select(e => e.label));
            }
            m_debugBreadcrumb.text = text;
        }

        /// <summary>Rebuilds the canvas so theme / node appearance changes take effect.</summary>
        public void RefreshGraph() => DrawGraph();

        #region Graph history (Ctrl+Z / Ctrl+Y)

        // Window-context shortcuts win over Unity's global Edit/Undo while this window has focus.
        [UnityEditor.ShortcutManagement.Shortcut("ND Behavior Tree/Undo Graph Edit", typeof(ND_BehaviorTreeEditorWindow), KeyCode.Z, UnityEditor.ShortcutManagement.ShortcutModifiers.Action)]
        private static void UndoShortcut(UnityEditor.ShortcutManagement.ShortcutArguments args) => (args.context as ND_BehaviorTreeEditorWindow)?.UndoGraph();

        [UnityEditor.ShortcutManagement.Shortcut("ND Behavior Tree/Redo Graph Edit", typeof(ND_BehaviorTreeEditorWindow), KeyCode.Y, UnityEditor.ShortcutManagement.ShortcutModifiers.Action)]
        private static void RedoShortcut(UnityEditor.ShortcutManagement.ShortcutArguments args) => (args.context as ND_BehaviorTreeEditorWindow)?.RedoGraph();

        [UnityEditor.ShortcutManagement.Shortcut("ND Behavior Tree/Redo Graph Edit (Shift+Z)", typeof(ND_BehaviorTreeEditorWindow), KeyCode.Z, UnityEditor.ShortcutManagement.ShortcutModifiers.Action | UnityEditor.ShortcutManagement.ShortcutModifiers.Shift)]
        private static void RedoShiftShortcut(UnityEditor.ShortcutManagement.ShortcutArguments args) => (args.context as ND_BehaviorTreeEditorWindow)?.RedoGraph();

        public void UndoGraph()
        {
            if (m_currentGraph == null || IsTyping()) return;
            if (!Graph.GraphHistory.For(m_currentGraph).Undo()) { ShowNotification(new GUIContent("Nothing to undo"), 1.0); return; }
            AfterHistoryChange();
        }

        public void RedoGraph()
        {
            if (m_currentGraph == null || IsTyping()) return;
            if (!Graph.GraphHistory.For(m_currentGraph).Redo()) { ShowNotification(new GUIContent("Nothing to redo"), 1.0); return; }
            AfterHistoryChange();
        }

        /// <summary>Ctrl+Z while editing a note/name text field must not undo the graph.</summary>
        private bool IsTyping() => rootVisualElement.panel?.focusController?.focusedElement is VisualElement focused && focused.GetFirstOfType<TextField>() != null;

        private void AfterHistoryChange()
        {
            if (m_currentView == null) return;
            m_currentView.SerializedTree.Update();
            m_currentView.Rebuild();
            m_currentView.RequestSave();
            m_blackboardView?.PopulateView();
            UpdateHistoryButtons();
        }

        private int m_shownHistoryState = int.MinValue;

        private void UpdateHistoryButtons()
        {
            if (m_undoButton == null) return;
            var history = m_currentGraph != null ? Graph.GraphHistory.For(m_currentGraph) : null;

            // Only rebuild tooltips when the stacks actually changed.
            int state = history == null ? -1 : unchecked(history.UndoCount * 7919 + history.RedoCount * 31 + history.Capacity + (history.NextUndoName?.GetHashCode() ?? 0));
            if (state == m_shownHistoryState) return;
            m_shownHistoryState = state;

            m_undoButton.SetEnabled(history != null && history.CanUndo);
            m_redoButton.SetEnabled(history != null && history.CanRedo);
            m_undoButton.tooltip = history != null && history.CanUndo ? $"Undo '{history.NextUndoName}' (Ctrl+Z) — {history.UndoCount}/{history.Capacity} steps" : "Nothing to undo";
            m_redoButton.tooltip = history != null && history.CanRedo ? $"Redo '{history.NextRedoName}' (Ctrl+Y)" : "Nothing to redo";
        }

        #endregion

        private void DrawGraph()
        {
            if (m_currentGraph == null || m_contentContainer == null) return;
            // Switching graphs: Live Save writes the previous one; manual mode asks first.
            m_currentView?.FlushPendingSave();
            if (m_currentView != null && m_currentView.TreeData != m_currentGraph) ConfirmLeaveCurrentGraph();

            m_serializeObject = new SerializedObject(m_currentGraph);
            m_currentView = new ND_BTGraphCanvas(m_serializeObject, this);

            m_contentContainer.Clear();
            m_contentContainer.Add(m_currentView);

            m_blackboardView = new BlackboardView(m_serializeObject);
            m_contentContainer.Add(m_blackboardView);
            ToggleBlackboard(m_isBlackboardVisible);
            m_liveWasActive = false;
        }

        private void ToggleBlackboard(bool isVisible)
        {
            if (m_blackboardView != null)
            {
                m_blackboardView.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void ClearView()
        {
            m_currentView?.FlushPendingSave();
            ConfirmLeaveCurrentGraph();
            m_currentGraph = null;
            m_serializeObject = null;
            m_currentView = null;
            m_targetRunner = null;
            m_blackboardView = null;
            m_debugStack.Clear();

            if (m_contentContainer == null) return;
            m_contentContainer.Clear();

            var prompt = new Label("Select a GameObject with a BehaviorTreeRunner or a BehaviorTree asset.");
            prompt.style.unityTextAlign = TextAnchor.MiddleCenter;
            prompt.style.fontSize = 14;
            m_contentContainer.Add(prompt);

            if (titleContent.text != "Behavior Tree Editor")
            {
                titleContent = new GUIContent("Behavior Tree Editor");
            }

            SetUnsavedChanges(false);
            UpdateDebugToolbar();
        }

        public void SetUnsavedChanges(bool unsaved)
        {
            this.hasUnsavedChanges = unsaved;
            saveChangesMessage = m_currentGraph != null ? $"'{m_currentGraph.name}' has unsaved Behavior Tree changes." : null;
            UpdateSaveButton();
        }

        private void UpdateSaveButton()
        {
            if (m_saveButton == null) return;
            // Visible when saving is manual; highlighted while there is something to save.
            m_saveButton.style.display = LiveSave ? DisplayStyle.None : DisplayStyle.Flex;
            m_saveButton.SetEnabled(hasUnsavedChanges);
            m_saveButton.text = hasUnsavedChanges ? "Save*" : "Save";
        }

        // EditorApplication.update fires hundreds of times per second; the graph only needs a fraction of that.
        private const double LiveUpdateInterval = 1.0 / 30.0;
        private const double InfoRefreshInterval = 0.5;

        private void OnBecameVisible() => m_isVisible = true;
        private void OnBecameInvisible() => m_isVisible = false;

        private void OnEditorUpdate()
        {
            if (m_currentView == null || !m_isVisible) return; // hidden tab: no work at all
            double now = EditorApplication.timeSinceStartup;

            if (IsLiveDebugging)
            {
                if (now >= m_nextLiveUpdate)
                {
                    m_nextLiveUpdate = now + LiveUpdateInterval;
                    m_currentView.UpdateLiveDebug(CurrentDebugRuntime);
                    m_liveWasActive = true;
                }
            }
            else if (m_liveWasActive)
            {
                m_currentView.ClearLiveDebug();
                m_liveWasActive = false;
            }

            if (now >= m_nextInfoRefresh && UnityEditorInternal.InternalEditorUtility.isApplicationActive)
            {
                // Event names, subtree assets, key slots and attachment summaries edited elsewhere (property window, blackboard).
                m_nextInfoRefresh = now + InfoRefreshInterval;
                m_currentView.RefreshInfoLabels();
                UpdateHistoryButtons();
                UpdateAiVisibility();
            }
        }
    }
}
