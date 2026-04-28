# Stack Surge (Unity)

Hyper-casual match puzzle: tap a column to drop the current tile on a 7×9 grid (row 0 = bottom). Match 3+ in a row, column, or L-shape; survive rising rows; wild and bomb specials; local scoring and meta (daily best, streak, challenges).

## Unity Version

Built and batch-tested with **Unity 6000.2.8f1** (Unity 6). Open the project folder in Unity Hub and load `Assets/Scenes/Main.unity`.

## UI & Aesthetics

The game features a modern, high-performance UI system designed for clarity and visual impact:
- **Decoupled Architecture**: UI logic is separated from the game state using a dedicated `GameView` class, allowing for dynamic layout creation and cleaner code.
- **Smooth Animations**:
  - **Gravity**: Tiles fall with realistic acceleration when matches are cleared.
  - **Row Rising**: The entire grid slides up smoothly using quadratic easing when new rows appear.
  - **Visual Feedback**: Screen shake on clears and blinking effects for bomb explosions.
- **Premium Design**: Dark-themed UI with semi-transparent overlays, professional color palettes, and crisp typography powered by **TextMeshPro**.
- **Animation Engine**: Integrated **DOTween** for smooth UI transitions and juice.

## Input & Controls

- **Input System**: Uses the **Input System** package for responsive cross-platform interaction.
- **Controls**:
  - Tap the **semi-transparent blue strips** (columns 1–7) to drop tiles.
  - **Grace Period**: No auto-rise for the first 6 seconds to allow players to read the "How to play" instructions.
  - **Challenges**: View and track in-game goals.
  - **Social**: "Copy score" feature for easy sharing.
- **Editor Debugging**:
  - `B`: Activate slow-fill buff.
  - `R`: Force a row rise.

## Project Layout

- `Assets/Scripts/Core/` — Grid logic, matching algorithms, difficulty curves.
- `Assets/Scripts/UI/` — `GameView` management, animations, and visual effects.
- `Assets/Scripts/Meta/` — Save systems, challenge tracking.
- `Assets/Scripts/Settings/` — `StackSurgeSettings` ScriptableObject configurations.
- `Assets/Plugins/` — Third-party libraries like **DOTween**.

