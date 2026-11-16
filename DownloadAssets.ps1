#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Download helper for Craquez free asset integration
.DESCRIPTION
    Opens browser tabs for high-priority free asset downloads and creates directory structure.
    Note: Most sites require manual download due to JavaScript/redirects.
#>

# Create directory structure
$dirs = @(
    "Assets/Craquez/Resources/Goals",
    "Assets/Craquez/Resources/Balls",
    "Assets/Craquez/Resources/Audio/Crowd",
    "Assets/Craquez/Resources/Audio/Gameplay",
    "Assets/Craquez/Resources/Audio/UI",
    "Assets/Craquez/Resources/Particles",
    "Assets/Craquez/Resources/Venues",
    "Assets/Craquez/Resources/Stadium",
    "Assets/Craquez/Resources/Props",
    "Assets/Craquez/Animations/Player",
    "Assets/Craquez/Animations/GK",
    "Assets/Craquez/Animations/Gandula",
    "Assets/Craquez/UI/HUD",
    "Assets/Craquez/UI/Menus"
)

Write-Host "Creating directory structure..." -ForegroundColor Green
foreach ($d in $dirs) {
    if (-not (Test-Path $d)) {
        New-Item -ItemType Directory -Path $d | Out-Null
        Write-Host "  Created: $d" -ForegroundColor Cyan
    }
}

# High-priority download URLs (require manual download due to site restrictions)
$downloads = @(
    @{
        Name = "Goal with Net (CC0, GLB)"
        Url = "https://www.innerscene.com/tools/library/3d-parts/soccer-goal-with-net-dfa5e359"
        Category = "Goals"
        Priority = "HIGH"
    }
    @{
        Name = "Soccer Goal (CC0, GLB) - Sketchfab 1"
        Url = "https://sketchfab.com/3d-models/soccer-goal-dd82dcb8975543658f4ee5097637cb39"
        Category = "Goals"
        Priority = "HIGH"
    }
    @{
        Name = "Soccer Goal (CC0, GLB) - Sketchfab 2"
        Url = "https://sketchfab.com/3d-models/soccer-goal-10d30382596a43f29e569fbe26e3966d"
        Category = "Goals"
        Priority = "MEDIUM"
    }
    @{
        Name = "Soccer Ball (CC0, GLB) - Poly Pizza"
        Url = "https://poly.pizza/m/7t4hmicGHV4"
        Category = "Balls"
        Priority = "HIGH"
    }
    @{
        Name = "Football Stadium (CC0, GLB/FBX) - Grab3D"
        Url = "https://grab3d.com/models/buildings/stadium/"
        Category = "Venues"
        Priority = "MEDIUM"
    }
    @{
        Name = "Football Field (CC0, GLB) - Sketchfab"
        Url = "https://sketchfab.com/3d-models/football-field-9752185eebef49d78b3ae529ea2235cf"
        Category = "Venues"
        Priority = "MEDIUM"
    }
    @{
        Name = "Crowd Sound Effects (CC0) - Selekt Audio"
        Url = "https://selektaudio.com/sound-lab/textures/crowd"
        Category = "Audio/Crowd"
        Priority = "HIGH"
    }
    @{
        Name = "Stadium Crowd Sounds (CC0) - Pixabay"
        Url = "https://pixabay.com/sound-effects/search/stadium%20crowd/"
        Category = "Audio/Crowd"
        Priority = "HIGH"
    }
    @{
        Name = "Stadium Recordings (CC0) - Signature Sounds"
        Url = "https://signaturesounds.org/store/p/football-stadium-recordings-free-download-cc0-wav-field-recording-sample-pack-"
        Category = "Audio/Crowd"
        Priority = "MEDIUM"
    }
    @{
        Name = "Football Sound Effects (CC0) - Pixabay"
        Url = "https://pixabay.com/sound-effects/search/football/"
        Category = "Audio/Gameplay"
        Priority = "HIGH"
    }
    @{
        Name = "Particle Pack 80+ sprites (CC0) - OpenGameArt"
        Url = "https://opengameart.org/content/particle-pack-80-sprites"
        Category = "Particles"
        Priority = "HIGH"
    }
    @{
        Name = "Smoke Particle Assets (CC0) - OpenGameArt"
        Url = "https://opengameart.org/content/smoke-particle-assets"
        Category = "Particles"
        Priority = "MEDIUM"
    }
    @{
        Name = "Free Stylized Smoke Effects (Unity Asset Store)"
        Url = "https://assetstore.unity.com/packages/vfx/particles/fire-explosions/free-stylized-smoke-effects-pack-226406"
        Category = "Particles"
        Priority = "MEDIUM"
    }
    @{
        Name = "Dust Particles (Unity Asset Store)"
        Url = "https://assetstore.unity.com/packages/vfx/particles/dust-125959"
        Category = "Particles"
        Priority = "MEDIUM"
    }
    @{
        Name = "Soccer Animations Pack (Unity Asset Store)"
        Url = "https://assetstore.unity.com/packages/3d/animations/soccer-animations-pack-289758"
        Category = "Animations"
        Priority = "MEDIUM"
    }
    @{
        Name = "Football Animations Pack (Unity Asset Store)"
        Url = "https://assetstore.unity.com/packages/3d/animations/football-animations-pack-241444"
        Category = "Animations"
        Priority = "MEDIUM"
    }
    @{
        Name = "Soccer League 3D Template (itch.io)"
        Url = "https://blackrosedevelopers.itch.io/soccer-league"
        Category = "Reference"
        Priority = "LOW"
    }
    @{
        Name = "Free UI Pack - Kenney (CC0)"
        Url = "https://kenney.nl/assets/ui-pack"
        Category = "UI"
        Priority = "MEDIUM"
    }
    @{
        Name = "Free UI Pack - Unity Asset Store"
        Url = "https://assetstore.unity.com/packages/2d/gui/icons/free-ui-pack-139587"
        Category = "UI"
        Priority = "LOW"
    }
    @{
        Name = "CC0 Music Pack - itch.io"
        Url = "https://itch.io/game-assets/assets-cc0/free/tag-music"
        Category = "Music"
        Priority = "HIGH"
    }
    @{
        Name = "Free Music Tag - itch.io"
        Url = "https://itch.io/game-assets/free/tag-music"
        Category = "Music"
        Priority = "HIGH"
    }
    @{
        Name = "Free Music - OpenGameArt"
        Url = "https://opengameart.org/content/free-music-0"
        Category = "Music"
        Priority = "HIGH"
    }
    @{
        Name = "CC0 Music - OpenGameArt"
        Url = "https://opengameart.org/content/cc0-music-0"
        Category = "Music"
        Priority = "HIGH"
    }
    @{
        Name = "CC0 Music - Pixabay"
        Url = "https://pixabay.com/music/search/cc0/"
        Category = "Music"
        Priority = "HIGH"
    }
    @{
        Name = "Game Assets Music - Pixabay"
        Url = "https://pixabay.com/music/search/game%20assets/"
        Category = "Music"
        Priority = "HIGH"
    }
    @{
        Name = "Free Music Loop Bundle - Tallbeard Studios"
        Url = "https://tallbeard.itch.io/music-loop-bundle"
        Category = "Music"
        Priority = "MEDIUM"
    }
    @{
        Name = "Free Game Audio - Hackingtons"
        Url = "https://www.hackingtons.com/free-game-audio"
        Category = "Music"
        Priority = "MEDIUM"
    }
    @{
        Name = "Free Crowd Cheering - Unity Asset Store"
        Url = "https://marketplace.unity.com/packages/audio/sound-fx/free-crowd-cheering-sounds-225494"
        Category = "Music"
        Priority = "MEDIUM"
    }
}

Write-Host "`n==========================================" -ForegroundColor Yellow
Write-Host "  CRAQUEZ FREE ASSET DOWNLOAD HELPER" -ForegroundColor Yellow
Write-Host "==========================================`n" -ForegroundColor Yellow

Write-Host "Directory structure created. Now opening high-priority downloads...`n" -ForegroundColor Green

# Group by priority
$priorityOrder = @("HIGH", "MEDIUM", "LOW")
foreach ($priority in $priorityOrder) {
    $items = $downloads | Where-Object { $_.Priority -eq $priority }
    if ($items.Count -gt 0) {
        Write-Host "`n=== $priority PRIORITY ===" -ForegroundColor ($priority == "HIGH" ? "Red" : ($priority == "MEDIUM" ? "Yellow" : "Green"))
        foreach ($item in $items) {
            Write-Host "`n[$item.Category] $item.Name" -ForegroundColor Cyan
            Write-Host "  URL: $item.Url" -ForegroundColor Gray
            Write-Host "  Save to: Assets/Craquez/Resources/$($item.Category)/" -ForegroundColor Green

            # Open in browser
            try {
                Start-Process $item.Url
                Write-Host "  [Opened in browser]" -ForegroundColor DarkGreen
            } catch {
                Write-Host "  [Could not auto-open - copy URL manually]" -ForegroundColor DarkRed
            }
        }
    }
}

Write-Host "`n==========================================" -ForegroundColor Yellow
Write-Host "DOWNLOAD INSTRUCTIONS:" -ForegroundColor Yellow
Write-Host "==========================================" -ForegroundColor Yellow
Write-Host @"
1. For each link opened in browser:
   - Look for "Download" button (usually .glb, .fbx, .obj for models)
   - For audio: download .wav or .ogg files
   - For particles: download .png sprites or .unitypackage

2. Save files to the correct folder:
   - Models (.glb, .fbx): Assets/Craquez/Resources/[Category]/
   - Audio: Assets/Craquez/Resources/Audio/[Category]/
   - Particles: Assets/Craquez/Resources/Particles/
   - Animations: Assets/Craquez/Animations/[Category]/

3. After downloading, in Unity:
   - Assets will auto-import
   - Check import settings (Model tab for .glb/.fbx)
   - Assign to prefabs/scripts as needed

4. Recommended import settings for models:
   - Animation Type: Generic (for static) or Humanoid (for animated)
   - Materials: External (Legacy) or Internal
   - Scale Factor: 1.0
   - Normal Calculation: Import

5. For audio:
   - Force To Mono: true (for 3D spatial)
   - Load Type: Compressed In Memory
   - Compression Format: Vorbis

6. After integration, test and create v2.31.0!
"@

Write-Host "`nDone! Check browser tabs for downloads.`n" -ForegroundColor Green