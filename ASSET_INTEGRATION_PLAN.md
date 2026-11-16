# Asset Integration Plan for Craquez v2.31.0

## Overview
This document outlines free assets to download and integrate to improve the game experience.

## Asset Categories

### 1. 🏟️ Stadium & Environment Models

#### Football Stadium (CC0)
- **Source**: [Grab3D Stadium](https://grab3d.com/models/buildings/stadium/) - CC0, GLB, FBX
- **Alternative**: [Creazilla Stadium](https://creazilla.com/media/3d-model/67469/football-stadium) - Free 3D model
- **Integration**: Replace/enhance current stadium in `Assets/Craquez/Resources/Venues/`

#### Football Field (CC0)
- **Source**: [Sketchfab Football Field](https://sketchfab.com/3d-models/football-field-9752185eebef49d78b3ae529ea2235cf) - CC0
- **Integration**: Enhanced pitch with better grass texture

### 2. 🥅 Goal & Net Models

#### Soccer Goals with Net (CC0)
- **Source 1**: [Innerscene Soccer Goal](https://www.innerscene.com/tools/library/3d-parts/soccer-goal-with-net-dfa5e359) - CC0, GLB
- **Source 2**: [Grab3D Goal Net](https://grab3d.com/es/models/sports/goal-net/) - CC0, GLB, FBX
- **Source 3**: [Sketchfab Soccer Goal 1](https://sketchfab.com/3d-models/soccer-goal-dd82dcb8975543658f4ee5097637cb39)
- **Source 4**: [Sketchfab Soccer Goal 2](https://sketchfab.com/3d-models/soccer-goal-10d30382596a43f29e569fbe26e3966d)
- **Integration**: Replace current goal posts in `Assets/Craquez/Resources/Goals/`

### 3. 🎯 Ball Models

#### Soccer Balls (CC0)
- **Source**: [Sketchfab Soccer Balls](https://sketchfab.com/tags/football) - Various CC0 models
- **Poly Pizza**: [Poly by Google Soccer Ball](https://poly.pizza/m/7t4hmicGHV4) - CC0
- **Integration**: Enhanced ball model in `Assets/Craquez/Resources/Balls/`

### 4. 🎭 Animations

#### Soccer Animation Packs
- **Unity Asset Store Free**: [Soccer Animations Pack](https://assetstore.unity.com/packages/3d/animations/soccer-animations-pack-289758)
- **Alternative**: [Football Animations Pack](https://assetstore.unity.com/packages/3d/animations/football-animations-pack-241444)
- **itch.io**: [Soccer Animations](https://itch.io/game-assets/free/tag-football/tag-unity)
- **Integration**: Enhanced player animations in `Assets/Craquez/Animations/`

### 5. 👥 Crowd & Stadium Atmosphere

#### Crowd Models (CC0)
- **Source**: [Innerscene Crowd Models](https://www.innerscene.com/tools/library/3d-parts) - Various CC0 people models
- **Integration**: Enhanced crowd in `VenueBuilder.cs` and `VenueLife.cs`

#### Stadium Props
- **Source**: [Innerscene 3D Parts Library](https://www.innerscene.com/tools/library/3d-parts) - Benches, lights, flags, etc.
- **Integration**: Enhanced stadium details in `VenueBuilder.cs`

### 6. 🔊 Sound Effects (CC0)

#### Crowd & Stadium Sounds
- **Selekt Audio**: [Free Crowd Sound Effects](https://selektaudio.com/sound-lab/textures/crowd) - CC0
- **Pixabay**: [Stadium Crowd Sounds](https://pixabay.com/sound-effects/search/stadium%20crowd/) - CC0
- **Signature Sounds**: [Football Stadium Recordings](https://signaturesounds.org/store/p/football-stadium-recordings-free-download-cc0-wav-field-recording-sample-pack-) - CC0
- **Uppbeat**: [Stadium Sounds](https://uppbeat.io/sfx/category/sports/stadium) - Free
- **SFXMint**: [Free Sound Effects](https://sfxmint.com/) - CC0

#### Game Sounds
- **Pixabay**: [Whistle, Kick, Ball Impact](https://pixabay.com/sound-effects/search/football/) - CC0
- **Freesound**: [Various football sounds](https://freesound.org/search/?q=football) - CC0/CC-BY
- **GitHub**: [Free-Sound-Effects](https://github.com/hipdiscovery/Free-Sound-Effects) - CC0

### 7. ✨ Particle Effects (CC0)

#### Grass/Dust/Smoke
- **Unity Asset Store**: [Free Stylized Smoke Effects](https://assetstore.unity.com/packages/vfx/particles/fire-explosions/free-stylized-smoke-effects-pack-226406)
- **Unity Asset Store**: [Dust Particles](https://assetstore.unity.com/packages/vfx/particles/dust-125959)
- **OpenGameArt**: [Particle Pack 80+ sprites](https://opengameart.org/content/particle-pack-80-sprites) - CC0
- **OpenGameArt**: [Smoke Particle Assets](https://opengameart.org/content/smoke-particle-assets) - CC0
- **Summer Engine**: [Smoke Particles Pack](https://www.summerengine.com/asset-store/pack/smoke-particles) - CC0
- **Unity Blog**: [Free VFX Image Sequences](https://unity.com/blog/engine-platform/free-vfx-image-sequences-flipbooks)

### 8. 🎨 UI & HUD Assets

#### Free UI Packs
- **Unity Asset Store**: [Free UI Pack](https://assetstore.unity.com/packages/2d/gui/icons/free-ui-pack-139587)
- **itch.io**: [Free UI Packs](https://itch.io/game-assets/free/tag-ui/tag-unity)
- **Kenney.nl**: [UI Pack](https://kenney.nl/assets/ui-pack) - CC0

---

## Integration Priority

### High Priority (Immediate Impact)
1. **Goal models with nets** - Visual improvement
2. **Crowd sounds** - Atmosphere
3. **Ball impact/kick sounds** - Feedback
4. **Grass/dust particles** - Visual feedback on kicks/slides

### Medium Priority
1. **Stadium model** - Environment
2. **Crowd models** - Atmosphere
3. **Animations** - Gameplay feel
4. **Ball model** - Visual

### Low Priority (Nice to Have)
1. **Full stadium model** - Background
2. **Detailed crowd models** - Background
3. **Advanced UI** - Polish

---

## Integration Instructions

### For 3D Models (GLB/FBX)
1. Download the `.glb` or `.fbx` file
2. Place in appropriate `Assets/Craquez/Resources/` subfolder
3. Import in Unity - materials will be created automatically
4. Update relevant scripts (`VenueBuilder.cs`, `Goal.cs`, `Ball.cs`)

### For Sound Effects (WAV/OGG)
1. Download `.wav` or `.ogg` files
2. Place in `Assets/Craquez/Resources/Audio/`
3. Update `Sfx.cs` or `AudioManager.cs`

### For Particle Effects
1. Download particle textures/sprites
2. Place in `Assets/Craquez/Resources/Particles/`
3. Create Particle Systems in Unity
4. Reference in `MatchView.cs` or `TurfMarks.cs`

### For Animations
1. Download `.fbx` with animations
2. Place in `Assets/Craquez/Animations/`
3. Configure in Unity Animation window
4. Update `AthleteAnimator.cs` and `AthleteView.cs`

---

## Download Script

Run this PowerShell script to download high-priority assets:

```powershell
# Create directories
$dirs = @(
    "Assets/Craquez/Resources/Goals",
    "Assets/Craquez/Resources/Balls",
    "Assets/Craquez/Resources/Audio",
    "Assets/Craquez/Resources/Particles",
    "Assets/Craquez/Resources/Venues",
    "Assets/Craquez/Animations"
)
foreach ($d in $dirs) { if (-not (Test-Path $d)) { New-Item -ItemType Directory -Path $d } }

# High priority downloads (requires manual download due to site restrictions)
Write-Host "Manual downloads needed:"
Write-Host "1. Goal with net: https://www.innerscene.com/tools/library/3d-parts/soccer-goal-with-net-dfa5e359"
Write-Host "2. Crowd sounds: https://selektaudio.com/sound-lab/textures/crowd"
Write-Host "3. Stadium sounds: https://pixabay.com/sound-effects/search/stadium%20crowd/"
Write-Host "4. Ball model: https://poly.pizza/m/7t4hmicGHV4"
Write-Host "5. Particle pack: https://opengameart.org/content/particle-pack-80-sprites"
```

---

## Legal Notes

All listed assets are marked as **CC0 (Public Domain)** or **Free for commercial use**. However:
- Always verify license on the source page before use
- Some "free" assets on Unity Asset Store may have restrictions
- CC0 = No attribution required, but appreciated
- CC-BY = Attribution required
- When in doubt, check the license file or contact creator

---

## Next Steps

1. Run the download script to see manual download list
2. Download high-priority assets manually
2. Place in correct folders
3. Update Unity project
4. Test integration
5. Create version 2.31.0