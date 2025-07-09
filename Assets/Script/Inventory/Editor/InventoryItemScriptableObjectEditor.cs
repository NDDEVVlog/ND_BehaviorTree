using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using System.Collections.Generic;

namespace ND.Inventory
{
    [CustomEditor(typeof(InventoryItemScriptableObject)), CanEditMultipleObjects]
    public class InventoryItemScriptableObjectEditor : Editor
    {
        private static InventoryItemScriptableObject Target;

        void OnEnable()
        {
            Target = target as InventoryItemScriptableObject;
        }

        [OnOpenAsset]
        public static bool OnOpenAsset(int instanceId, int line)
        {
            var obj = EditorUtility.InstanceIDToObject(instanceId);

            if (obj != null && obj is InventoryItemScriptableObject inventory)
            {
                OpenDatabaseEditor(inventory);
                return true;
            }

            return false;
        }

        public static void OpenDatabaseEditor(InventoryItemScriptableObject database)
        {
            if (database != null)
            {
                InventoryEditorWindow itemsEditor = EditorWindow.GetWindow<InventoryEditorWindow>(false, database.name, true);
                itemsEditor.minSize = new Vector2(800, 450);
                itemsEditor.Show(database);
            }
            else
            {
                Debug.LogError("[OpenDatabaseEditor] Database object is not initialized!");
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox("Contains a database of inventory items.", MessageType.Info);
            EditorGUILayout.Space(2);
            EditorGUILayout.HelpBox("Assign this asset to Inventory script to enable item picker with this asset.", MessageType.Warning);
            EditorGUILayout.Space(10);

            var rect = GUILayoutUtility.GetRect(1f, 30f);
            if (GUI.Button(rect, "Open Inventory Database Editor"))
            {
                OpenDatabaseEditor(Target);
            }

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Items Count: " + Target.itemMappers.Count, EditorStyles.miniBoldLabel);
            EditorGUILayout.EndVertical();

            string[] items = Target.itemMappers.Select(x => x.Name).ToArray();

            if (items.Length > 0)
            {
                if (items.Length < 50)
                {
                    EditorGUILayout.HelpBox(string.Join(", ", items), MessageType.None);
                }
                else
                {
                    string[] items_short = items.Take(50).ToArray();
                    string itemsText = /*string.Join(", ", items_short) +*/ " etc.";

                    EditorGUILayout.HelpBox(itemsText, MessageType.None);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }

    public class InventoryEditorWindow : EditorWindow
    {
        private InventoryItemScriptableObject database;
        private int selectedItemIndex = -1;
        private Vector2 scrollPosition; // Scroll position for the right pane

        // Foldout states
        private bool showItemDetails = true;
        private bool showToggles = true;
        private bool showSounds = true;
        private bool showCustomActionSettings = true;
        private bool showCombineSettings = true;
        private bool showAdditionalFields = true;
        private bool show1DArray = false;
        public void Show(InventoryItemScriptableObject database)
        {
            this.database = database;
            titleContent = new GUIContent(database.name);
            Show();
        }

        private void OnGUI()
        {
            if (database == null)
            {
                EditorGUILayout.HelpBox("No database selected.", MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("Inventory Database Editor", EditorStyles.boldLabel);

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();

            // Left Pane: Item List and Buttons
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            EditorGUILayout.LabelField("Items", EditorStyles.boldLabel);
            if (GUILayout.Button("Add New Item"))
            {
                database.itemMappers.Add(new ItemMapper { Name = "New Item" });
                UpdateItemIDs(); // Update IDs after adding a new item
            }

            for (int i = 0; i < database.itemMappers.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                string buttonLabel = $"{database.itemMappers[i].Name} [{database.itemMappers[i].ID}]";
                if (GUILayout.Button(buttonLabel))
                {
                    selectedItemIndex = i;
                }
                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    database.itemMappers.RemoveAt(i);
                    UpdateItemIDs(); // Update IDs after removing an item
                    selectedItemIndex = -1; // Deselect the item
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            // Right Pane: Item Details with Scroll View
            EditorGUILayout.BeginVertical();

            // Display item image, name, and description
            if (selectedItemIndex >= 0 && selectedItemIndex < database.itemMappers.Count)
            {
                ItemMapper selectedItem = database.itemMappers[selectedItemIndex];

                GUILayout.BeginHorizontal();
                if (selectedItem.itemSprite != null)
                {
                    GUILayout.Label(selectedItem.itemSprite.texture, GUILayout.Width(64), GUILayout.Height(64));
                }
                else
                {
                    GUILayout.Label("No Image", GUILayout.Width(64), GUILayout.Height(64));
                }
                GUILayout.BeginVertical();
                GUILayout.Label("Name: " + selectedItem.Name, EditorStyles.boldLabel);
                GUILayout.Label("Description: " + selectedItem.Description, EditorStyles.wordWrappedLabel);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("Select an item to view its details.", MessageType.Info);
            }

            EditorGUILayout.Space();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Item Details", EditorStyles.boldLabel);

            if (selectedItemIndex >= 0 && selectedItemIndex < database.itemMappers.Count)
            {
                ItemMapper selectedItem = database.itemMappers[selectedItemIndex];

                showItemDetails = EditorGUILayout.Foldout(showItemDetails, "Item Details");
                if (showItemDetails)
                {
                    EditorGUILayout.BeginVertical("box");
                    selectedItem.Name = EditorGUILayout.TextField("Name", selectedItem.Name);
                    selectedItem.Description = EditorGUILayout.TextField("Description", selectedItem.Description);
                    selectedItem.itemType = (ItemType)EditorGUILayout.EnumPopup("Item Type", selectedItem.itemType);
                    selectedItem.useActionType = (ItemAction)EditorGUILayout.EnumPopup("Use Action Type", selectedItem.useActionType);
                    selectedItem.itemSprite = (Sprite)EditorGUILayout.ObjectField("Item Sprite", selectedItem.itemSprite, typeof(Sprite), false);
                    EditorGUILayout.EndVertical();
                }

                // Toggles
                EditorGUILayout.Space();
                showToggles = EditorGUILayout.Foldout(showToggles, "Toggles");
                if (showToggles)
                {
                    EditorGUILayout.BeginVertical("box");
                    selectedItem.itemToggles.isStackable = EditorGUILayout.Toggle("Is Stackable", selectedItem.itemToggles.isStackable);
                    selectedItem.itemToggles.isUsable = EditorGUILayout.Toggle("Is Usable", selectedItem.itemToggles.isUsable);
                    selectedItem.itemToggles.isCombinable = EditorGUILayout.Toggle("Is Combinable", selectedItem.itemToggles.isCombinable);
                    selectedItem.itemToggles.isDroppable = EditorGUILayout.Toggle("Is Droppable", selectedItem.itemToggles.isDroppable);
                    selectedItem.itemToggles.isRemovable = EditorGUILayout.Toggle("Is Removable", selectedItem.itemToggles.isRemovable);
                    selectedItem.itemToggles.restoreStamina = EditorGUILayout.Toggle("Restore Stamina", selectedItem.itemToggles.restoreStamina);
                    selectedItem.itemToggles.canExamine = EditorGUILayout.Toggle("Can Examine", selectedItem.itemToggles.canExamine);
                    selectedItem.itemToggles.canBindShortcut = EditorGUILayout.Toggle("Can Bind Shortcut", selectedItem.itemToggles.canBindShortcut);
                    selectedItem.itemToggles.combineAddItem = EditorGUILayout.Toggle("Combine Add Item", selectedItem.itemToggles.combineAddItem);
                    selectedItem.itemToggles.combineKeepItem = EditorGUILayout.Toggle("Combine Keep Item", selectedItem.itemToggles.combineKeepItem);
                    selectedItem.itemToggles.combineShowItem = EditorGUILayout.Toggle("Combine Show Item", selectedItem.itemToggles.combineShowItem);
                    selectedItem.itemToggles.showItemOnUse = EditorGUILayout.Toggle("Show Item On Use", selectedItem.itemToggles.showItemOnUse);
                    selectedItem.itemToggles.bagDescription = EditorGUILayout.Toggle("Bag Description", selectedItem.itemToggles.bagDescription);
                    selectedItem.itemToggles.doActionUse = EditorGUILayout.Toggle("Do Action Use", selectedItem.itemToggles.doActionUse);
                    selectedItem.itemToggles.doActionCombine = EditorGUILayout.Toggle("Do Action Combine", selectedItem.itemToggles.doActionCombine);
                    EditorGUILayout.EndVertical();
                }
                // Sounds
                EditorGUILayout.Space();
                showSounds = EditorGUILayout.Foldout(showSounds, "Sounds");
                if (showSounds)
                {
                    EditorGUILayout.BeginVertical("box");
                    selectedItem.itemSounds.useSound = (AudioClip)EditorGUILayout.ObjectField("Use Sound", selectedItem.itemSounds.useSound, typeof(AudioClip), false);
                    selectedItem.itemSounds.useVolume = EditorGUILayout.Slider("Use Volume", selectedItem.itemSounds.useVolume, 0f, 1f);
                    selectedItem.itemSounds.combineSound = (AudioClip)EditorGUILayout.ObjectField("Combine Sound", selectedItem.itemSounds.combineSound, typeof(AudioClip), false);
                    selectedItem.itemSounds.combineVolume = EditorGUILayout.Slider("Combine Volume", selectedItem.itemSounds.combineVolume, 0f, 1f);
                    EditorGUILayout.EndVertical();
                }

                // Custom Action Settings
                EditorGUILayout.Space();
                showCustomActionSettings = EditorGUILayout.Foldout(showCustomActionSettings, "Custom Action Settings");
                if (showCustomActionSettings)
                {
                    EditorGUILayout.BeginVertical("box");
                    selectedItem.useActionSettings.triggerValue = EditorGUILayout.IntField("Trigger Value", selectedItem.useActionSettings.triggerValue);
                    selectedItem.useActionSettings.triggerItemID = EditorGUILayout.IntField("Trigger Item ID", selectedItem.useActionSettings.triggerItemID);
                    selectedItem.useActionSettings.triggerCustomValue = EditorGUILayout.TextField("Trigger Custom Value", selectedItem.useActionSettings.triggerCustomValue);
                    selectedItem.useActionSettings.actionRemove = EditorGUILayout.Toggle("Action Remove", selectedItem.useActionSettings.actionRemove);
                    selectedItem.useActionSettings.actionAddItem = EditorGUILayout.Toggle("Action Add Item", selectedItem.useActionSettings.actionAddItem);
                    selectedItem.useActionSettings.actionRestrictUse = EditorGUILayout.Toggle("Action Restrict Use", selectedItem.useActionSettings.actionRestrictUse);
                    selectedItem.useActionSettings.actionRestrictCombine = EditorGUILayout.Toggle("Action Restrict Combine", selectedItem.useActionSettings.actionRestrictCombine);
                    EditorGUILayout.EndVertical();
                }

                // Combine Settings
                // Combine Settings
                EditorGUILayout.Space();
                //EditorGUILayout.LabelField("Combine Settings", EditorStyles.boldLabel);
                showCombineSettings = EditorGUILayout.Foldout(showCombineSettings, "Combine Settings");
                if (showCombineSettings)
                {
                    EditorGUILayout.BeginVertical("box");
                    if (GUILayout.Button("Add Combine Setting"))
                    {
                        AddCombineSetting();
                    }

                    if (selectedItem.combineSettings != null)
                    {
                        for (int i = 0; i < selectedItem.combineSettings.Count; i++)
                        {
                            EditorGUILayout.LabelField("Combine Setting " + (i + 1), EditorStyles.boldLabel);
                            selectedItem.combineSettings[i].combineWithID = EditorGUILayout.IntField("Combine With ID", selectedItem.combineSettings[i].combineWithID);
                            selectedItem.combineSettings[i].resultCombineID = EditorGUILayout.IntField("Result Combine ID", selectedItem.combineSettings[i].resultCombineID);
                            selectedItem.combineSettings[i].combineSwitcherID = EditorGUILayout.IntField("Combine Switcher ID", selectedItem.combineSettings[i].combineSwitcherID);
                            EditorGUILayout.Space();
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("No combine settings defined.", MessageType.Info);
                    }
                    EditorGUILayout.EndVertical();
                }

               /* if (selectedItem.combineSettings != null)
                {
                    for (int i = 0; i < selectedItem.combineSettings.Count; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("Combine Setting " + (i + 1), EditorStyles.boldLabel);
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("Combine With ID", GUILayout.Width(100));
                        selectedItem.combineSettings[i].combineWithID = EditorGUILayout.IntField(selectedItem.combineSettings[i].combineWithID);
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("Result Combine ID", GUILayout.Width(100));
                        selectedItem.combineSettings[i].resultCombineID = EditorGUILayout.IntField(selectedItem.combineSettings[i].resultCombineID);
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("Combine Switcher ID", GUILayout.Width(100));
                        selectedItem.combineSettings[i].combineSwitcherID = EditorGUILayout.IntField(selectedItem.combineSettings[i].combineSwitcherID);
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.Space();
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No combine settings defined.", MessageType.Info);
                }*/

                // Additional Fields for BoolArray2D, Columns, Rows, and ObjectReferences
                EditorGUILayout.Space();
                showAdditionalFields = EditorGUILayout.Foldout(showAdditionalFields, "Additional Fields");
                if (showAdditionalFields)
                {
                    EditorGUILayout.BeginVertical("box");
                    selectedItem.columns = EditorGUILayout.IntField("Columns", selectedItem.columns);
                    selectedItem.rows = EditorGUILayout.IntField("Rows", selectedItem.rows);

                    if (selectedItem.boolArray2D == null || selectedItem.boolArray2D.Values == null || selectedItem.boolArray2D.Values.GetLength(0) != selectedItem.rows || selectedItem.boolArray2D.Values.GetLength(1) != selectedItem.columns)
                    {
                        selectedItem.boolArray2D = new BoolArray2D(selectedItem.rows, selectedItem.columns);
                        selectedItem.SyncTo2DArray();
                    }

                    EditorGUILayout.LabelField("Bool Array 2D");
                    for (int row = 0; row < selectedItem.boolArray2D.Values.GetLength(0); row++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        for (int col = 0; col < selectedItem.boolArray2D.Values.GetLength(1); col++)
                        {
                            bool value = selectedItem.boolArray2D.Values[row, col];
                            bool newValue = EditorGUILayout.Toggle(value, GUILayout.Width(20), GUILayout.Height(20));
                            if (newValue != value)
                            {
                                selectedItem.boolArray2D.Values[row, col] = newValue;
                                EditorUtility.SetDirty(database);
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                    }

                    selectedItem.bool1DData = new List<bool>();
                    selectedItem.SyncTo1DData();
                    show1DArray = EditorGUILayout.Foldout(show1DArray, "1DARray");
                    if(show1DArray)
                    if (selectedItem.bool1DData.Count > 1)
                    {
                        
                        EditorGUILayout.LabelField("Bool 1D Data");
                        for (int i = 0; i < selectedItem.bool1DData.Count; i++)
                        {
                            bool value = selectedItem.bool1DData[i];
                            bool newValue = EditorGUILayout.Toggle(value);
                            if (newValue != value)
                            {
                                selectedItem.bool1DData[i] = newValue;
                                selectedItem.SyncTo2DArray();
                                EditorUtility.SetDirty(database);
                            }
                        }
                    }
                    if (selectedItem.DropObject == null)
                    {
                        selectedItem.DropObject = new ObjectReference();
                    }
                    if (selectedItem.PackDropObject == null)
                    {
                        selectedItem.PackDropObject = new ObjectReference();
                    }

                    EditorGUILayout.LabelField("Drop Object", EditorStyles.boldLabel);
                    selectedItem.DropObject.GUID = EditorGUILayout.TextField("GUID", selectedItem.DropObject.GUID);
                    selectedItem.DropObject.Object = (GameObject)EditorGUILayout.ObjectField("Object", selectedItem.DropObject.Object, typeof(GameObject), false);

                    EditorGUILayout.LabelField("Pack Drop Object", EditorStyles.boldLabel);
                    selectedItem.PackDropObject.GUID = EditorGUILayout.TextField("GUID", selectedItem.PackDropObject.GUID);
                    selectedItem.PackDropObject.Object = (GameObject)EditorGUILayout.ObjectField("Object", selectedItem.PackDropObject.Object, typeof(GameObject), false);

                    EditorGUILayout.EndVertical();
                }

            }
            else
            {
                EditorGUILayout.HelpBox("Select an item to view or edit its details.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(database);
            }
        }
        private void AddCombineSetting()
        {
            if (selectedItemIndex >= 0 && selectedItemIndex < database.itemMappers.Count)
            {
                ItemMapper selectedItem = database.itemMappers[selectedItemIndex];

                if (selectedItem.combineSettings == null)
                {
                    selectedItem.combineSettings = new List<ItemMapper.CombineSettings>(1);
                }
                else
                {
                    selectedItem.combineSettings.Add(new ItemMapper.CombineSettings());
                }

                selectedItem.combineSettings[selectedItem.combineSettings.Count - 1] = new ItemMapper.CombineSettings();
            }
        }
        private void UpdateItemIDs()
        {
            for (int i = 0; i < database.itemMappers.Count; i++)
            {
                database.itemMappers[i].ID = i;
            }
        }
    }
}
