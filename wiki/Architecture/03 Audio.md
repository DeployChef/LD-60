# 03 Audio

← [[02 Scenes]] | [[Home]]

A button does not play a clip itself. `UiButtonSound` publishes `PlaySound` on `IMessageBus`. `AudioService` is the listener. It asks `IAudioManager` to play that id.

`AudioCatalog` is the list of sounds. `AudioManager` stays on `Root` and keeps the `AudioSource` list. Playback goes through those sources.

`Volume` is the settings prefab. Three sliders: master, SFX, and music. Master is the listener volume. SFX covers effect and UI sources. Music covers the music channel.

`UiMove` is the hover clip. `UiClick` is the click clip. Both are the button sounds from Futboloid. Hover and click still play while the game is paused.
