# 02 Scenes

← [[01 Folder Structure]] | [[Home]]

`Root` stays loaded. `MainMenu` and `Game` swap through `Loading`.

Play opens the menu. The Play button goes to the game. Escape in the game opens the mini menu and sets time scale to 0. Escape again closes it and sets time scale back to 1. Menu goes back to the main menu.

`ShellService` does the loads. It lives on `RootLifetimeScope`. The menu and game scopes are children of that.

Play, Loading, Game, and Menu read the `UI` string table. RU and EN sit on the `Localization` prefab. `LocalizationController` switches the locale. The choice is kept by the Player Pref locale selector. Master, SFX, and Music sit on the `Volume` prefab. `VolumeController` writes them to the audio manager. Those three are kept in PlayerPrefs.

`Settings/Game.inputactions` has `Game/Pause` on Escape. `PauseMenuController` listens to it.

| Scene | |
| --- | --- |
| `Root` | Camera, light, event system, `Startup`, `RootLifetimeScope`, `AudioManager` |
| `MainMenu` | Play, a `Localization` instance, and a `Volume` instance. The lit button is the current locale. |
| `Loading` | |
| `Game` | Mini menu. Menu, a `Localization` instance, and a `Volume` instance. Escape pauses. |

Press Play from `Root`.
