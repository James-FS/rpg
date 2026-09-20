# Bloodlines UI — Dark/Gothic UI Kit for Unity

A comprehensive interface collection in dark fantasy/gothic aesthetics featuring deep black backgrounds, contrasting red accents, and runic motifs. Ready for quick implementation in Unity through prefabs and textures.

## Features

- **Buttons**: 6 prefabs
  - 3 designs × 2 color themes (Gray/Red)
  - Interactive states for each design: Default, Hover, Pressed, Disabled
  - Textures: `Textures/Button/Button1..Button3`
- **Sliders**: 13 prefabs
  - 5 horizontal (`Slider 1..5 (Horizontal)`)
  - 4 round (`Slider 1..2 (Round)` and `With Runes` variants)
  - 4 segmented (`Slider 1..4 (Segments)`, including handle variant)
  - Additional textures: `Textures/Progress_Bar` (rect/rod/diamond, empty/full, toggler)
- **Toggles**: 6 prefabs
  - Round and square variants, 3 styles each (`Toggle 1..3`)
- **Icons**: 4 prefabs (`Prefabs/Icon`)
  - Additional icon sets in `Textures/Icon`
- **Frames & Backgrounds**: Decorative frame and background elements in `Textures/Frame`
- **Progress Bars** (textures):
  - Rectangular: `Textures/Progress_Bar/Rectangle` (empty/full v1–v5)
  - Round: `Textures/Progress_Bar/Round` (empty/full v1–v4)
- **Fonts**: `Fonts/`
  - Manufacturing Consent (TTF + TMP SDF) — decorative headings/titles
  - MedievalSharp (TTF + TMP SDF) — buttons, sliders and general UI text
- **Demo Scene**: `Scenes/Demo Scene (Bloodlines UI).unity`
- **Animations**: Preview controllers and clips for sliders in `Animations/`

## Scripts and Components

The kit includes several **optional** utility scripts (in `Scripts/`) for enhanced functionality. They are demo helpers — the art, prefabs and fonts work without them, so the `Scripts/` folder can be removed if you only need the visuals:

### Core Components
- **`SoundManager`**: Global audio management with persistent settings
  - Volume control and sound enable/disable
  - Hover and click sound scaling
  - PlayerPrefs integration for settings persistence
- **`SliderTextSynchronizer`**: Synchronizes slider values with text display
  - Real-time percentage display
  - Customizable text format
  - Automatic value monitoring
- **`ToggleSliderController`**: Links toggle state to slider control
  - Stores/restores original slider values
  - Zero-out functionality when toggle is disabled

### UI Enhancement Scripts
- **`ButtonSFX`**: Button sound effect management
  - Hover and click sound support
  - Integration with SoundManager
- **`ToggleSFX`**: Toggle sound effect management
  - Similar to ButtonSFX but for toggle elements
- **`ButtonTextColorChanger`**: Dynamic text color management for buttons
  - State-based color changes (default, highlighted, pressed, disabled)
  - Automatic interaction detection

### Scene Management
- **`LocalSceneManager`**: Local scene switching (GameObject-based)
  - Loading scene support
  - Scene index management
- **`LoadingSceneManager`**: Loading animation controller
  - Progress bar and text animation
  - Customizable timing and duration
- **`FastDontDestroyOnLoad`**: Quick persistent object utility

### Compatibility
- **`InputModuleBootstrap`**: Keeps the demo scene's `EventSystem` working under
  any **Active Input Handling** setting (Input Manager, Input System, or Both).
  The scene ships with the legacy `StandaloneInputModule`; when a project has the
  **new Input System as the only active backend**, that module throws an
  `InvalidOperationException`. This helper detects that case at runtime and swaps
  in an `InputSystemUIInputModule`, so the demo just works with no manual setup.
  It is attached to the `EventSystem` in the demo scene and is safe to leave in
  place regardless of which input backend you use.

## Style and Purpose

- **Aesthetic**: Dark backgrounds, saturated red accents, gothic/vampiric decorative elements
- **Application**: RPG, horror, dark fantasy, dungeon crawler, grim settings

## How to Use

1. Add desired prefabs from `Prefabs/` to your `Canvas`
2. Replace sprites in `Image` components with alternatives from `Textures/` if needed
3. For progress bars, use `Image` type = Filled (linear/radial) with corresponding sprites from `Textures/Progress_Bar/Rectangle` and `Textures/Progress_Bar/Round`
4. Round sliders can be styled with rune variants or without by replacing sprites/prefabs
5. Add sound effects by attaching `ButtonSFX` or `ToggleSFX` components to interactive elements
6. Use `SoundManager` singleton for global audio control
7. Implement `SliderTextSynchronizer` for automatic slider value display

## Project Structure

```
Bloodlines UI/
├── Prefabs/          # Ready-to-use UI prefabs
├── Textures/         # Sprite assets organized by component type
├── Fonts/            # TTF + TMP SDF font assets (Manufacturing Consent, MedievalSharp)
├── Scripts/          # Optional utility scripts and components
├── Animations/       # Animation controllers and clips
├── Audio/            # SFX clips and audio mixer
└── Scenes/           # Demo scene
```

## Requirements & Dependencies

- Unity **2022.3 LTS** or newer
- TextMeshPro (`com.unity.textmeshpro`) — for text components
- Unity UI / uGUI (`com.unity.ugui`) — built-in
- Unity Audio — for the optional sound effects
- **Input handling**: works with **Input Manager (old)**, **Input System (new)**,
  or **Both**. No package is required by the kit itself. The demo scene adapts
  automatically via the `InputModuleBootstrap` helper, so the new-Input-System
  `EventSystem` crash does not occur and no manual changes are needed.

## Performance Notes

- All scripts are optimized for runtime performance
- SoundManager uses singleton pattern for efficient memory usage
- Text synchronization uses value change detection to minimize updates
- Audio clips are played through pooled AudioSource components

## Troubleshooting

### Text is missing / invisible (TextMesh Pro)

If the demo scene or your own UI shows no text, your project is missing the
**TextMesh Pro essential resources** (the TMP shader and base materials that every
TMP font asset — including this kit's `MedievalSharp` and `Manufacturing Consent`
SDF fonts — relies on).

Fix it once per project: **Window → TextMeshPro → Import TMP Essential Resources**
(you do *not* need "Import TMP Examples & Extras"). Unity also offers this in a
popup the first time a TMP component is opened.

These resources are intentionally **not** bundled with the kit: they are tied to
the TextMesh Pro version in your project, and shipping them would cause duplicate
shader/material and GUID conflicts. Unity imports them for you with the menu item
above.

## Credits & Third-Party Assets

### Fonts

The bundled fonts are open-source typefaces licensed under the **SIL Open Font
License 1.1 (OFL)**, which permits embedding and redistribution inside this
package. Each font keeps its original `OFL.txt` license file in its folder, and
all third-party components are listed in `Third-Party Notices.txt`.

- **Manufacturing Consent** — © 2019 The Manufacturing Consent Project Authors, OFL 1.1
- **MedievalSharp** — © 2011 wmk69, OFL 1.1 (Reserved Font Name "MedievalSharp")

If you redistribute or repackage this kit, keep these `OFL.txt` files and the
`Third-Party Notices.txt` with it.

### Everything else

All UI artwork, prefabs, textures, scripts, and sound effects are original work by
**xGaida and Shieldomirs**.