# ND Core (`Assets/ND_Core`)

Shared code of the ND plugins, so plugins dropped into the same project never define the same type twice.

| Assembly | What |
|---|---|
| `ND.Core` | `ND.UIText` — writes text into a `UI.Text` or a TextMeshPro component without depending on TextMeshPro |
| `ND.Core.Editor` | `ND.Editor.ScriptingDefines` (set/clear a define on every build target), `ND.Editor.OptionalIntegration` (the gate between plugins) |

## How ND plugins stay independent but work together

Each ND system is a plugin folder in `Assets/` (copy the folder into a project, or export it as a `.unitypackage`).

1. **Shared code lives here.** Every plugin that needs it requires `ND_Core`, so there is one copy.
2. **Code that joins two plugins** (Dialogue ↔ Behavior Tree, Dialogue ↔ FMOD…) lives in its own assembly with
   `defineConstraints: ["<DEFINE>"]` and a reference to the other plugin's assembly. Without the define the assembly
   is not compiled, so a project without the other plugin never breaks on it.
3. **`OptionalIntegration` sets that define** only while the other plugin's assembly exists (asmdef `versionDefines`
   cannot see plugins in `Assets/`, only Unity packages):
   ```csharp
   // on by itself when the ND Behavior Tree plugin is in the project
   new OptionalIntegration("ND Behavior Tree", "ND_DIALOGUE_NDBT", "ND_BehaviorTree.Runtime", automatic: true);
   // a settings switch, only while FMOD for Unity is imported
   new OptionalIntegration("FMOD", "ND_DIALOGUE_FMOD", "FMODUnity");
   ```
   If the other plugin is deleted, the define is removed after the next compilation and the project builds again.
   Defines are written to Player Settings → Scripting Define Symbols (every common build target).

## Which plugins a plugin needs
| Plugin | Needs |
|---|---|
| ND_Localization | — |
| ND_GraphUI | — |
| ND_BehaviorTree | ND_GraphUI |
| ND_Quests | ND_Core, ND_Localization |
| ND_Dialogue | ND_Core, ND_Localization, ND_Quests, ND_GraphUI |
