# Flow Launcher Codebase Finder Plugin — Architecture

## Overview

A Flow Launcher plugin that leverages Everything's CLI (`es.exe`) to instantly find and open codebases in your editor of choice.

## Core Behavior

**Trigger**: User activates Flow Launcher and types the plugin action keyword (e.g., `code <query>`)

**Search targets**:
1. `.git` folders → result is the parent directory (the repo root)
2. `*.code-workspace` files → result is the file itself

**Action on select**: Open the target in the configured editor (Cursor or VS Code)

---

## Components

```
Flow.Launcher.Plugin.Codebases/
├── plugin.json                 # Plugin metadata, keyword, author, etc.
├── Main.cs                     # Entry point, implements IPlugin, ISettingProvider
├── Settings.cs                 # Settings model
├── SettingsControl.xaml        # Settings UI
├── SettingsControl.xaml.cs     # Settings UI codebehind
├── EverythingSearch.cs         # Wrapper around es.exe CLI
├── ResultBuilder.cs            # Transforms search results into Flow results
└── Images/
    └── icon.png                # Plugin icon
```

---

## Data Flow

```
User types query
       │
       ▼
┌─────────────────┐
│    Main.cs      │
│    Query()      │
└────────┬────────┘
         │
         ▼
┌─────────────────────────────────────────────────┐
│            EverythingSearch.cs                  │
│  - Shells out to es.exe                         │
│  - Query 1: folder:.git under search paths      │
│  - Query 2: ext:code-workspace under paths      │
│  - Parses stdout lines                          │
└────────┬────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────┐
│            ResultBuilder.cs                     │
│  - .git results: take Parent directory          │
│  - .code-workspace results: use file path       │
│  - Deduplicate (repo may have both)             │
│  - Fuzzy match against user query               │
│  - Build List<Result> with icons + actions      │
└────────┬────────────────────────────────────────┘
         │
         ▼
    Flow Launcher displays results
         │
         ▼
    User selects result
         │
         ▼
┌─────────────────────────────────────────────────┐
│  Action: launch editor                          │
│  - Cursor: `cursor <path>`                      │
│  - VS Code: `code <path>`                       │
│  - For .code-workspace: `cursor <file>`         │
│  - For repo folder: `cursor <folder>`           │
└─────────────────────────────────────────────────┘
```

---

## Settings

| Setting          | Type     | Default                          | Description                                      |
|------------------|----------|----------------------------------|--------------------------------------------------|
| `Editor`         | enum     | `Cursor`                         | Which editor to open results in                  |
| `SearchPaths`    | string[] | `[Environment.UserProfile]`      | Root paths to scope Everything search            |
| `EsExePath`      | string   | `es.exe` (assumes in PATH)       | Path to Everything CLI executable                |

### Editor enum

```csharp
public enum Editor
{
    Cursor,
    VSCode
}
```

Maps to CLI commands:
- `Cursor` → `cursor`
- `VSCode` → `code`

---

## Everything CLI Usage

**Find .git folders under a path:**
```
es.exe -path "C:\Users\Name" folder:.git
```

**Find workspace files under a path:**
```
es.exe -path "C:\Users\Name" ext:code-workspace
```

**Multiple paths**: Run one query per path, aggregate results. Alternatively use Everything's path syntax if supported.

**Performance**: es.exe returns results in milliseconds even for thousands of matches. No caching layer needed initially.

---

## Result Construction

### For `.git` match at `C:\Users\Name\projects\myrepo\.git`

```csharp
new Result
{
    Title = "myrepo",
    SubTitle = "C:\\Users\\Name\\projects\\myrepo",
    IcoPath = "Images\\icon.png",  // or grab folder icon
    Action = _ => OpenInEditor("C:\\Users\\Name\\projects\\myrepo")
}
```

### For `.code-workspace` match at `C:\Users\Name\projects\myworkspace.code-workspace`

```csharp
new Result
{
    Title = "myworkspace.code-workspace",
    SubTitle = "C:\\Users\\Name\\projects\\myworkspace.code-workspace",
    IcoPath = "Images\\icon.png",
    Action = _ => OpenInEditor("C:\\Users\\Name\\projects\\myworkspace.code-workspace")
}
```

---

## Edge Cases

| Case | Handling |
|------|----------|
| `es.exe` not found | Return single result: "Everything CLI not found. Install from voidtools.com" |
| No results | Return single result: "No codebases found" |
| Repo has both `.git` and `.code-workspace` | Show both; user can pick. No auto-dedup needed — different actions. |
| Query is empty | Return all results (possibly capped at N for performance) |
| Path no longer exists | Skip result or show with warning subtitle |

---

## Future Enhancements (Out of Scope for v1)

- Switch to Everything SDK for marginal perf gain
- Cache results with filesystem watcher invalidation
- Modifier key to open in alternate editor
- "Open in terminal" action
- Git branch / status preview in subtitle
- Frecency sorting (recently/frequently opened first)

---

## Dependencies

- .NET Framework (whatever Flow Launcher plugin SDK targets)
- Everything + es.exe installed and in PATH (or configured path)
- Cursor and/or VS Code installed and in PATH

---

## Build & Install

1. Build the project targeting the correct .NET version
2. Output folder structure must match Flow Launcher plugin format
3. Copy to `%APPDATA%\FlowLauncher\Plugins\Codebases\`
4. Restart Flow Launcher or use `reload` command
