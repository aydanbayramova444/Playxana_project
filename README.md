# 🎮 Hackathon Game

Team Unity project for the PlayXana / Xsolla hackathon. Team of 4, everyone writes code.

---

## ⚙️ Unity version — the SAME for EVERYONE

| Setting | Value |
|---|---|
| **Unity** | **`6000.6.10f1`** (Unity 6.6) — exact version, including the patch |
| Template | `TODO: 2D (URP) / 3D (URP)` |
| Target platform | **Windows** (standalone `.exe`, no web build) |

> The source of truth is `ProjectSettings/ProjectVersion.txt`. Unity Hub will offer to install
> the right version if yours differs. **Never open the project in a different version and never
> accept an upgrade prompt** — it will break the project for everyone.

### Installation
1. Download **Unity Hub** → unity.com/download, sign in (Personal license).
2. **Installs → Install Editor** → pick **`6000.6.10f1`**.
   If it's not in the list — unity.com/releases/editor/archive → find `6000.6.10f1` → **Install**.
3. Modules:

| Module | Windows | Mac |
|---|---|---|
| Editor build | — | **Apple Silicon** (if M-series chip) |
| Microsoft Visual Studio Community | ✅ if you don't use Rider | — |
| Windows Build Support (Mono) | built in | optional (only to build an `.exe` from Mac) |
| Everything else (WebGL, Android, iOS, Linux, IL2CPP, Docs) | ❌ | ❌ |

4. Code editor: **Rider** (free for non-commercial use), **Visual Studio** (Windows),
   or **VS Code** + the *Unity* extension. Visual Studio for Mac is discontinued.
   In Unity: *Edit/Unity → Preferences → External Tools → External Script Editor*.

---

## 🔧 Git setup (once per machine)

### 1. Git LFS
Images, audio, models and fonts are stored in LFS (see `.gitattributes` for the full list).
```bash
git lfs install
```
> On Mac, if LFS is missing: `brew install git-lfs`. On Windows it ships with Git for Windows.

### 2. General settings
```bash
git config --global core.longpaths true      # Windows: long paths
git config --global pull.rebase true         # clean history without extra merge commits
```

### 3. UnityYAMLMerge (smart merging of scenes and prefabs)

**Windows:**
```bash
git config --global merge.unityyamlmerge.name "Unity SmartMerge"
git config --global merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.6.10f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -h -p --force %O %B %A %A"
git config --global merge.unityyamlmerge.recursive binary
```

**Mac:**
```bash
git config --global merge.unityyamlmerge.name "Unity SmartMerge"
git config --global merge.unityyamlmerge.driver "'/Applications/Unity/Hub/Editor/6000.6.10f1/Unity.app/Contents/Tools/UnityYAMLMerge' merge -h -p --force %O %B %A %A"
git config --global merge.unityyamlmerge.recursive binary
```

> If you installed Unity to a non-default location, adjust the path accordingly
> (Unity Hub → Installs → ⚙ → Show in Explorer/Finder).

### 4. Unity project settings (check once after the project is created)
*Edit → Project Settings → Editor*:
- **Asset Serialization → Mode: Force Text**
- **Version Control → Mode: Visible Meta Files**

---

## 📁 Project structure

```
Assets/
├── _Project/              ← everything of ours lives here (separate from plugins / Asset Store packages)
│   ├── Scenes/
│   │   ├── Main.unity     ← main scene (edited only by its owner, see below)
│   │   └── Dev/           ← personal test scenes: Sam.unity, ...
│   ├── Scripts/
│   │   ├── Core/          ← GameManager, events, saving, audio
│   │   ├── Player/        ← input, movement, camera
│   │   ├── Gameplay/      ← mechanics, enemies, levels
│   │   ├── UI/            ← menus, HUD
│   │   └── Integrations/  ← Xsolla SDK and other external services
│   ├── Prefabs/
│   ├── Art/               ← sprites, models, materials
│   ├── Audio/
│   └── ScriptableObjects/
└── Plugins/               ← third-party SDKs
```

---

## 👥 Who owns what

| Member | Area | Folders |
|---|---|---|
| `NAME 1` | Core / GameManager / final build | `Scripts/Core`, `Scenes/Main.unity` |
| `NAME 2` | Player / controls | `Scripts/Player` |
| `NAME 3` | Gameplay / enemies / levels | `Scripts/Gameplay` |
| `NAME 4` | UI / integrations (Xsolla) | `Scripts/UI`, `Scripts/Integrations` |

---

## 📏 Working rules

### Branches
- `main` — always a working build that runs.
- Work in your own branches: `feature/player-movement`, `fix/ui-crash`.
- **Merge into `main` often — every 1–2 hours.** Long-lived branches = pain.
- Before merging: `git pull` from `main` → open Unity → make sure the console has no errors.

### Scenes and prefabs — the #1 source of conflicts
- 🚫 **Two people never edit the same scene or prefab at the same time.** Need it? Post "taking Main.unity" in chat, then "released" when done.
- Test your features in **your own scene** under `Scenes/Dev/`.
- Make everything you can a **prefab** — then the main scene only needs the prefab dropped in.

### Code
- Modules talk via **events** (`event Action`, ScriptableObject events), not direct references to other people's classes.
- Don't change someone else's public API without a heads-up in chat.
- Namespaces follow folders: `Game.Core`, `Game.Player`, `Game.UI`…

### Files
- **Rename / move files only inside Unity**, never in Explorer/Finder (otherwise `.meta` files and references get lost).
- Always commit the `.meta` file together with its asset.
- Don't commit `Library/`, builds, or `.csproj`/`.sln` — they're in `.gitignore`.

### Before pushing
1. No errors in the Unity console.
2. Scenes are saved (`Ctrl/Cmd + S`).
3. The commit contains only what you actually changed.

---

## 🚀 Building
The final build is made by **one person on Windows** (see the ownership table): *File → Build Profiles* → **Windows** → Build.
