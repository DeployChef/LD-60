# 01 Folder Structure

← [[Home]] | Next → [[02 Scenes]]

Game stuff lives in `Assets/_Project/`. The `_` keeps it at the top of the Project window.

URP settings, the volume profile, and `InputSystem_Actions` stay in `Assets/`. Game actions are `Settings/Game.inputactions`. Scripts, sprites, and audio for the game go under `_Project`.

```
Assets/_Project/
├── Code/
│   ├── LD60.Core/
│   ├── LD60.Main/
│   ├── LD60.Gameplay/
│   ├── LD60.UI/
│   └── LD60.Editor/
├── Scenes/
├── Prefabs/
│   └── Systems/
├── Art/
│   ├── Sprites/
│   ├── Animations/
│   ├── Materials/
│   └── Fonts/
├── Audio/
│   ├── Music/
│   ├── SFX/
│   └── Mixers/
├── Data/
└── Settings/
```

| Assembly | |
| --- | --- |
| `LD60.Core` | Shared types, message bus, audio catalog. UniTask. |
| `LD60.Main` | Core, Gameplay, UI, UniTask, VContainer. |
| `LD60.Gameplay` | Core, UniTask, VContainer. |
| `LD60.UI` | Core, Gameplay, UniTask, VContainer, UGUI, TextMeshPro, Input System, Localization. |
| `LD60.Editor` | The other four, VContainer. Editor only. |

`Core` does not reference `Main`, `Gameplay`, or `UI`.
