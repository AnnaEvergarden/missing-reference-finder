# Missing Reference Finder · 缺失引用查找器

[![Unity](https://img.shields.io/badge/Unity-2021.3%2B-black?logo=unity)](https://unity.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

A Unity Editor tool that finds every broken asset reference and missing script in your project — in one click.

一键扫描 Unity 项目中所有损坏的资源引用和丢失的脚本组件。

---

## Features · 功能

| English | 中文 |
|---------|------|
| **Missing object references** — serialized fields pointing to deleted/moved assets | **丢失的对象引用** — 序列化字段指向已删除/移动的资源 |
| **Missing scripts** — MonoBehaviour components whose script file was deleted | **丢失的脚本** — MonoBehaviour 组件对应的 .cs 文件已被删除 |
| **Three scan scopes** — all assets, selected only, or current scene | **三种扫描范围** — 全部资源、仅选中、当前场景 |
| **Click-to-ping** — select and highlight the problematic asset | **点击定位** — 在 Project 窗口中高亮异常资源 |
| **Export report** — save results as a timestamped `.txt` file | **导出报告** — 保存为带时间戳的 `.txt` 文本文件 |
| **Non-blocking scan** — editor stays responsive during full-project scans | **不阻塞编辑器** — 全项目扫描时编辑器保持响应 |

## Installation · 安装

### Via UPM (recommended) · 通过 UPM（推荐）

1. Open `Window > Package Manager`
2. Click `+` → `Add package from git URL...`
3. Enter: `https://github.com/AnnaEvergarden/missing-reference-finder.git`

### Manual · 手动

Copy `Editor/MissingReferenceFinder.cs` into any `Editor` folder in your project.

将 `Editor/MissingReferenceFinder.cs` 复制到项目的任意 `Editor` 文件夹中。

## Usage · 使用

1. `Tools > Missing Reference Finder`
2. Choose scope · 选择扫描范围
3. Click **Scan** · 点击扫描
4. Click **Ping** on any result to jump to the asset · 点击 Ping 跳转到资源
5. Click **Export** to save a text report · 点击 Export 导出报告

### Scan Scopes · 扫描范围

| Scope · 范围 | What it checks · 检查内容 |
|-------------|-------------------------|
| **All Assets** | 项目中所有 Prefab、Scene、ScriptableObject、Material 等 |
| **Selected Only** | 仅 Project 窗口当前选中的资源 |
| **Current Scene** | 当前打开场景中的 GameObject 及其组件 |

## Preview · 预览

```
┌──────────────────────────────────────────────────────────────┐
│ [Scan] [Cancel]        [All Assets ▼] [By Asset ▼] [Clear] [Export] │
├──────────────────────────────────────────────────────────────┤
│ Assets scanned: 1247   Props: 89523   Missing refs: 14   Scripts: 2 │
│ Filter: [_______________]  ☑ Refs  ☑ Scripts  ☑ Stats               │
├──────────────────────────────────────────────────────────────┤
│ ⚠ _backgroundImage  in  Panel > TitlePanel                         │
│   Assets/UI/Prefabs/TitlePanel.prefab                        [Ping]│
│ ⚠ Missing Script  GameObject: BattleScene/Canvas/MainPanel          │
│   Assets/Scenes/BattleScene.unity                           [Ping]│
└──────────────────────────────────────────────────────────────┘
```

## Requirements · 环境要求

- Unity 2021.3 or later · Unity 2021.3 或更高版本
- Works in all render pipelines (Built-in, URP, HDRP) · 兼容所有渲染管线

## How it works · 工作原理

The tool iterates every asset's `SerializedObject`, checking each `ObjectReference` property. The key indicator of a missing reference is:

工具遍历每个资源的 `SerializedObject`，检查每个 `ObjectReference` 属性。判定缺失引用的核心逻辑：

```csharp
// objectReferenceValue is null, but instance ID is non-zero
// → the asset this reference pointed to was deleted or moved
if (prop.objectReferenceValue == null &&
    prop.objectReferenceInstanceIDValue != 0)
{
    // Missing reference found!
}
```

For missing scripts, it checks `GameObject.GetComponents<Component>()` — if a component is `null` while its slot exists, the script file was removed.

对于丢失的脚本，检查 `GameObject.GetComponents<Component>()` —— 组件槽位存在但内容是 `null`，说明 .cs 文件已被删除。

## License · 许可证

MIT