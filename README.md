# Stack Surge (Unity)

Hyper-casual match puzzle: tap a column to drop the current tile on a 7×9 grid (row 0 = bottom). Match 3+ in a row, column, or L-shape; survive rising rows; wild and bomb specials; local scoring and meta (daily best, streak, challenges).

## Unity version

Built and batch-tested with **Unity 6000.2.8f1** (Unity 6). Open the project folder in Unity Hub and load `Assets/Scenes/Main.unity`.

## Input

The project uses the **Input System** package (`com.unity.inputsystem`) for UI. `ProjectSettings` has **Active Input Handling: Both** so editor helpers keep working. The game replaces any legacy `StandaloneInputModule` at startup with `InputSystemUIInputModule`.

## Controls

- Tap the **numbered blue strips** at the bottom (columns 1–7) to drop your current tile into that column. Color blocks that **disappear** are normal: that’s a match clear or the bomb wild effect.
- For the first **6 seconds** of a run (configurable on `StackSurgeSettings.InitialRiseGraceSeconds`), the board does **not** auto-rise so you can read the on-screen “How to play” text.
- **Challenges** opens the local challenge list.
- **Copy score** copies a share line to the clipboard (stub).
- Editor: **B** slow-fill buff, **R** force one rise (requires new Input System keyboard).

## Project layout

- `Assets/Scripts/Core/` — grid, matching, difficulty curve, scoring
- `Assets/Scripts/Meta/` — JSON save, challenges
- `Assets/Scripts/Settings/` — `StackSurgeSettings` ScriptableObject (optional asset override)
- `Assets/Editor/CreateMainScene.cs` — one-shot scene builder (`CreateMainScene.Create`)
