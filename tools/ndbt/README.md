# ndbt — AI bridge for ND Behavior Tree

`ndbt` lets an AI assistant (Claude Code via MCP) or a terminal read and edit behavior trees **through the Unity
Editor**, using the same graph service, validator and undo history as the graph window. Nothing edits `.asset`
YAML directly.

```
Claude Code ──MCP stdio──▶ ndbt ──HTTP 127.0.0.1 + session token──▶ Unity Editor (ND_BehaviorTree bridge)
```

## 1. Enable access in Unity (owner password)

1. **Project Settings → ND Behavior Tree**: tick **Enable AI Access (CLI / MCP)** (off by default; the settings page
   may ask for its own unlock first). Until this is on, every AI feature is hidden: the AI Access page, the Copy for
   AI actions and the bridge.
2. Open **ND Behavior Tree → AI Access** (or the "Open AI Access settings →" button). The first time, set the
   **owner password**. Only a salted hash is stored in the settings asset.
3. Choose the **access level**:
   * **Off**: no AI access (default).
   * **Read-only**: inspect trees, validate, watch running agents.
   * **Read + Write**: also create and edit trees, manage keys, undo/redo, send events.
4. Every user must **unlock with the password in each editor session**. There is no command that unlocks access
   remotely. While unlocked, Unity writes `Library/NDBT/bridge.json` (port + session token) for `ndbt` to read.

This is a guardrail against accidental AI edits, not security against someone who can edit the project files.

## 2. Build the bridge

Requires the .NET 8 SDK.

```bash
dotnet build tools/ndbt        # → tools/ndbt/bin/ndbt.dll
```

## 3. Connect Claude Code

In the AI Access page click **Copy MCP config** and paste it into the project's `.mcp.json`:

```json
{
  "mcpServers": {
    "ndbt": {
      "command": "dotnet",
      "args": ["D:/ND_BehaviorTree/tools/ndbt/bin/ndbt.dll", "mcp", "--project", "D:/ND_BehaviorTree"]
    }
  }
}
```

## 4. Use it from a terminal

```bash
dotnet tools/ndbt/bin/ndbt.dll status
dotnet tools/ndbt/bin/ndbt.dll node-list --category decorator
dotnet tools/ndbt/bin/ndbt.dll tree-get --tree Wolf --format outline
dotnet tools/ndbt/bin/ndbt.dll tree-apply --tree Assets/AI/Guard.asset --ops-file ops.json --dryRun true
```

The CLI parses argument values as JSON when possible. `--<name>-file <path>` loads an argument from a JSON file, and
`--json '{...}'` passes several arguments at once.

## Commands

| Command | Access | Purpose |
|---|---|---|
| `status`, `help` | read | Bridge state; command list |
| `node_list {category?, search?}` | read | Node types (title, type, kind, menu, children none/one/many) |
| `node_info {type}` | read | Fields (defaults, descriptions), key slots (required types), how to add it |
| `tree_list {folder?}` | read | BehaviorTree assets |
| `tree_get {tree, format?: outline\|json}` | read | Read a tree (outline = compact, json = complete) |
| `tree_validate {tree}`, `validate_all` | read | Validation issues |
| `blackboard_get {tree}` | read | Keys and available key types |
| `find_usages {type? \| key?, tree?}` | read | Where a node type / key is used |
| `agent_list`, `agent_state {agent}` | read | Play mode: running agents, live running path |
| `tree_create {path, blackboard?, ops?}` | write | New tree (optionally with content) |
| `tree_duplicate {tree, path}` | write | Copy a tree |
| `tree_apply {tree, ops, dryRun?, label?}` | write | Batch edit: all-or-nothing, one undo step |
| `subtree_extract {tree, nodes, path}` | write | Branch → new subtree asset |
| `blackboard_key {tree, action, name, type?, newName?, value?}` | write | add / rename / set / remove keys |
| `history {tree, action: undo\|redo}` | write | Same history as Ctrl+Z in the graph window |
| `tree_save {tree}` | write | Save when Live Save is off |
| `agent_send_event {agent, event}` | write | Play mode: SendEvent on an agent |
| `menu_run {path}` | write | Run one of this package's menu items (path must start with `ND_BehaviorTree/`), e.g. a demo scene builder |

Write commands are refused in Play mode, except `agent_send_event`. Saving follows the graph window's **Live Save**
toggle: when it is off, changes stay in memory until `tree_save` or Ctrl+S.

### Example: `tree_apply` operations

```json
[
  { "op": "create", "type": "Selector", "ref": "sel", "parent": "root" },
  { "op": "create", "type": "Wait", "ref": "wait", "parent": "$sel", "position": [0, 400], "fields": { "duration": 2 } },
  { "op": "add-decorator", "node": "$wait", "type": "Cooldown", "fields": { "cooldownDuration": 5 } },
  { "op": "create", "type": "MoveToTransform", "ref": "move", "parent": "$sel", "position": [250, 400] },
  { "op": "link-key", "node": "$move", "path": "targetTransformKey", "key": "Target" }
]
```

* Node references: `"root"`, a full node id, the 8-character short id shown in outlines, or a `ref` declared
  earlier in the same batch (`"$sel"`).
* Children of a composite run **left to right by x position**.
* If any operation fails, the whole batch is rolled back and the error names the failing operation.

## Copy for AI

* Select a tree asset and click **Copy for AI** in its Inspector, or use the **Copy for AI** toolbar button in the
  graph window. This copies the path, GUID and the command to read the tree.
* Right-click nodes → **Copy Node IDs for AI**, then refer to them in your prompt.
