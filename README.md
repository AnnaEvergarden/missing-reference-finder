# Missing Reference Finder

A Unity Editor tool that finds every broken asset reference and missing script in your project — in one click.

## Features

- **Missing object references** — serialized fields pointing to deleted/moved assets
- **Missing scripts** — MonoBehaviour components whose script file was deleted
- **Three scan scopes** — All assets, selected only, or current scene
- **Click-to-ping** — select and highlight the problematic asset directly
- **Export report** — save results as a timestamped `.txt` file
- **Non-blocking scan** — editor stays responsive during full-project scans

## Installation

### Via UPM (Git URL)

1. Open `Window > Package Manager`
2. Click `+` → `Add package from git URL...`
3. Enter: `https://github.com/AnnaEvergarden/missing-reference-finder.git`

### Manual

Copy `Editor/MissingReferenceFinder.cs` into any `Editor` folder in your project.

## Usage

1. `Tools > Missing Reference Finder`
2. Choose scope: **All Assets** / **Selected Only** / **Current Scene**
3. Click **Scan**
4. Click **Ping** on any result to jump to the asset
5. Click **Export** to save a text report

## Screenshot

```
┌──────────────────────────────────────────────────┐
│ [Scan] [Cancel]              [All Assets ▼] [By Asset ▼] [Clear] [Export] │
├──────────────────────────────────────────────────┤
│ Assets scanned: 1247   Props: 89523   Missing: 14   Scripts: 2           │
│ Filter: [________________]  ☑ Refs  ☑ Scripts  ☑ Stats                     │
├──────────────────────────────────────────────────┤
│ ⚠ _backgroundImage  in  Panel > TitlePanel                              │
│   Assets/UI/Prefabs/TitlePanel.prefab                              [Ping]│
│ ⚠ _cardFrame  in  CardSlot                                              │
│   Assets/Prefabs/Card.prefab                                      [Ping]│
│ ⚠ Missing Script  GameObject: BattleScene/Canvas/MainPanel              │
│   Assets/Scenes/BattleScene.unity                                 [Ping]│
└──────────────────────────────────────────────────┘
```

## Requirements

- Unity 2021.3 or later
- Works in all render pipelines (Built-in, URP, HDRP)

## License

MIT

