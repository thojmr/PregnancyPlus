# Pregnancy Plus Introduction
This repository contains the PregnancyPlus plugin, that adds additional belly sliders in Studio and Maker for Illusion games.  It is intended to compliment the [KK_Pregnancy](https://github.com/ManlyMarco/KoikatuGameplayMods) plugin, but can be used without it.

** No new features currently planned.  We'll see if I get the urge to add more later

See [How To Install](#how-to-install) for installation instructions
</br>
See [Features](#features) for all plugin features
</br>
See [Plugin Tips](#some-tips) for tips and tricks


<img src="https://github.com/thojmr/KK_PregnancyPlus/blob/master/images/P+ All Menus.png" height="900"></img>

## Latest Features (I will update this occasionally)
- 7.0+
    - Dedicated timeline slider
- 6.0+:    
    - Inflation Speed config option for HS2
    - Fixed Skirt and Jackets clipping below the belly
    - Added Preset Belly Shape dropdown
    - Individual clothing offsets GUI in Studio and Maker
    - Major performance improvements
    - Fat Fold Gap slider                                                    <- (Last Vanilla KK version)
    - All skinned accessories will work with Preg+
- 5.0+:
    - Preg+ works with all normal clothing types now


</br>


## How to install
1. Requires BetterRepack or HF Patch (Preg+ is included with these, but probably not the latest version)
3. Download the latest release of Preg+ [here](https://github.com/thojmr/KK_PregnancyPlus/releases).
4. Then right click the zip and select "Extract Here".  Copy that `BepInEx/` folder to your root game directory. The plugin `.dll` will end up inside your `BepInEx/plugins/` directory like this:
    -> `{root game}/BepInEx/plugins/XX_PregnancyPlus.dll`
5. That's it.  If the plugin loaded it should appear in the F1 Plugin Config.
    - If you see warnings in game about KKAPI or BepInEx versions, you probably need to download the latest BetterRepack or HFPatch

</br>

## Some Tips
- To increase the poly count of meshes making them appear more smooth, you can use [Shader Tesselation](#tesselation-shader) or a [High Poly Mesh](#high-poly-mesh)
- The more Multiplier you apply, the more -StretchX and -StretchY you should apply.  Otherwise the belly gets too wide and tall for the body.
- The Taper sliders are good at making the shape more egg like.
- Too much Roundness slider will cause clothes to clip at the top/bottom of the belly.  Maybe I'll fix this one day...
- If you see stretched skin near the edges, use the "Smooth Belly Mesh" button to correct it, or reduce your Stretch sliders if that doesn't help.
  - Optionally you can try [Shader Tesselation](#tesselation-shader) or a [High Poly Mesh](#high-poly-mesh) as a more permanent solution
- There will always be some amount of cloth clipping at extreme sizes.  You can use the Cloth Offset slider to help, but It's a difficult problem to solve.
- You can use the Individual Clohting Offset GUI to offset a single piece of clothing at a time now.  Great for overlaping or tight fitting clothing
- **Hover over any of the F1 Plugin Config options for more detailed descriptions**

<br>

## Features
- Adds a number of sliders that will allow you to change the size and shape of a characters belly in Studio, Maker, and Main Gameplay.
    - In Main Game you can further bulk tweak all character's belly shapes with the F1 plugin config sliders.
    - Optionally the "Preset Belly Shape" dropdown is a quick way to select a belly shape if you dont want to mess with sliders.  You can also select one of these preset shapes as the default Main Game belly progression shape.
- Adds Timeline and VNGE integration for belly animations. Short guides below. [Timeline](#timeline-integration) | [VNGE](#vnge-integration)
- Adds 3 configurable keybinds in plugin config that can be used to increase or decrease the belly size in Main Game, on the fly.   
- Adds a Preset Belly Shape dropdown to Studio and Maker that allows you to quickly set a base shape for the belly from a variety of styles.  Keep in mind it was tuned  for normal size characters, and will appear different based on character body scales.
- Adds an additional Fat Fold slider, just make sure the Preg+ slider is above 0 to see the effect.
- Adds an "Override KK_Pregnancy belly shape" toggle, that lets you use the Preg+ belly shape as the default one in Main Gameplay (Instead of combining both plugin's shapes).
- Adds a "Mesh Smoothing" button in Studio and Maker, that allows you to smooth the belly mesh and reduce any stretched skin and hard shadows.
    - The smoothing will reset on slider change or character load, so it's mostly for screenshots, and animations.
    - The smoothed mesh can be saved as a blendshape!    
    - It's a slow prcess, so watch the timer below the button to see when its done. (extremely slow when using a high poly mesh)

</br>

## Honey Select 2 Only Features
- Because I'm too lazy to port KK_Pregnancy to HS2, belly inflation logic has been added to Main Game HScenes when finishing inside (Thanks to Crescent696).  Similar to how KK_Pregnancy inflation works.  It is off by default, turn it on in Preg+ plugin config.

</br>

## Koikatsu and AI Only Features
- This was way too confusing, so I added the option to disable the KK_Pregnancy belly shape in favor of the Pregnancy+ belly in HScenes and Main Gameplay.  Both plugins will still work together, and pregnancy will progress.  Look for "Override KK_Pregnancy belly shape" in plugin config.  It's just less to think about now!

<del>
- When using the KK/AI_Pregnancy `inflation` or `pregnancy` features, any saved Pregnancy+ belly sliders will be added in addition to KK/AI_Pregnancy's shape, but only both pregnancy plugins are installed.  You can control the amount of Pregnancy+ belly shape applied on top of the KK/AI_Pregnancy belly with the "Max Additional Belly Size" slider in plugin config. 0 being no additional Preg+ slider effect, and max being the full Preg+ slider effect.  You can use the plugin config sliders to further adjust the results for all pregnant characters at once.
    - This can be used to tweak the final pregnant shape, or make the max size larger than what KK/AI_Pregnancy allows.
    - Toggle this feature off by setting "Max Additional Belly Size" to 0.
- *Key thing to note*: If you just want to alter the KK/AI_Pregnancy shape.  Make sure to set the "Inflation Size" slider to 0 before saving character card.  When it is above 0 it will always be visible in Main Game even when not pregnant.
</del>

</br>
</br>
</br>

## Timeline Integration
### New way
- Studio Timeline integration via the new timeline Preg+ slider.  Here's how:
  
  `Keep in mind you will need KKSAPI or HS2API v1.30 and BepInEx v5.4.15 to see this feature`
    - Set the Preg+ character sliders to the desired shape you want
    - Open timeline, find the PregnancyPlus section and add timeline nodes.  Set value to desired level.
    - That's It!
### Old way [sill works, but not recomended]
- Studio Timeline integration via blendshapes.  Here's how:
  - Set the P+ character sliders to the desired shape you want (including belly smoothing).
  - Click "Open Blendshapes" button. You will see a popup that will show you any existing P+ blendshapes.  If none are found, then use the "Create New" button.
    - Do not alter the Preg+ blendshapes ending in \[temp\] They are temporary and will not be saved.
  - Move your new blendshape sliders to the desired position.  At least one blendshape slider must be green (touched) before the next step
  - Open Timeline with Ctrl+T, search for "Blendshape" and middle click to add.
  - Follow Timeline guides for further info
  - See [The BlendShape GUI](#the-blendshape-gui) below for a longer description of the GUI and it's behaviors

</br>
</br>

## VNGE Integration
- Studio VNGE integration in HS2 and AI via blendshapes.  Here's how:
  - Set the P+ character sliders to the desired shape you want (including belly smoothing).
  - Click "Open Blendshapes" button. You will see a popup that will show you any existing P+ blendshapes.  If none are found, then use the "Create New" button.
    - Do not alter the Preg+ blendshapes ending in \[temp\] They are temporary and will not be saved.
  - Move your new blendshape sliders to the desired position.  At least one blendshape slider must be green (touched) before the next step
  - Open VNGE, and add the blendshape(s) under "Clip Manager" 
    - To use BlendShapes in VNGE set ExportChara_XXPE_BlendShapes=1 in vnactor.ini everywhere it is found (may become obsolete)
  - Follow VNGE guides for further info
  - See [The BlendShape GUI](#the-blendshape-gui) below for a longer description of the GUI and it's behaviors

</br>

## The BlendShape GUI
In the bottom right corner of the banner image, you can see the blendshape GUI.  The purpose of this GUI is to allow you to capture the current pregnancy+ sliders and save their shape as a permanent blendshape to the character card/scene.  That way, if a character's belly is animated via Timeline or VNGE, the animation will be persistent and shareable with anyone else automatically. `(Although you should use the new Preg+ timeline interpolable instead)`

Note:
- Keep in mind that a blendshape is tied directly to a specific uncensor or clothing.  If you change either you will need to recreate the blendshapes in most cases.
- If you plan on loading a scene made in KK to KKS, the uncensor you used in KK MUST exist in KKS.  Otherwise you will have to re-create the blendshape in KKS.
- This is why the new timeline interpolable is better.  It's not dependent on any mesh, and will work after swaping characters.

Tips:
- You CAN replace characters in the scene after the blendshape is saved. Pregnancy+ will automatically load the same uncensor to the newly replaced character.  However clothing will not retain its' shape when changed.
- Do not try to animate blendshapes ending in `PregnancyPlus_[temp]` as these exist only temporarily.
- Once the blendshape has been made, you can opt to use the HSPE/KKPE blendshape window also.  They both do the same thing at this point.
- If you're feeling really crazy you can actually multiply the effect of any slider by captureing it as a blendshape first, setting the blendshape to 100%, then going back to the normal Pregnancy+ sliders and altering them again.  Rince and repeat to amplify the effect.  You can make some unusual shapes this way.

</br>

## Bigger!
- For additonal effect in HS2 you can mimic what KK_Pregnancy does to belly bones to make the final shape larger/rounder
- Here's how 
    - In HS2 or AI: Studio > HS2PE > Adv.mode > Bones
    - Bone: cf_J_kosi01_s  Set the following: PositionZ: 0.6, ScaleZ: 1.6, RotateX: 11
    - Bone: cf_spine01_s  Set the following: PositionZ: 0.6, ScaleZ: 1.6, RotateX: 351
    - Now apply any P+ sliders you want
- This will however make slight changes to the characters spine shape, so keep that in mind.  
- Also since this is altering bones, you may have some unintended cosequences down the road.

</br>

## FAQ - Troubleshooting
- Q: Where do I put the PregnancyPlus.dll?
    - A: It should end up under {Root game folder}/BepinEx/Plugins/xx_PregnancyPlus.dll
- Q: Why are some outfits not affected by the sliders?
    - A: Most likley the unskinned mesh was imported in an unusual way.  Feel free to send me a character card with the clothing on to debug it.  (As of 6.0+ this should be mostly fixed)
- Q: There are no slider effects when the character has no legs.
    - A: The character must have a leg scale > 0 for the belly sliders to work correctly.
- Q: The belly sliders suddenly stoped working!?
    - A: Most of the time this is because KKPE/HSPE sometimes locks the belly blendshapes.  You can try opening KKPE > Advanced > Blendshapes > and "Reset" the Pregnancy+ blendshapes on each mesh that is modified (purple).  Otherwise your output_log.txt may have more details if an error occured.
    - A: Alternatively try changing the uncensor.  Sometimes they are just poorly imported into the game.
- Q: What the heck is a BlendShape?
    - A: Put simply a blendshape is a "copy" of the mesh that has some deformation that you want to be able to ease into.  Like visually morphing from originalMesh -> morphedMesh.

</br>
</br>

## Mesh Smoothing Options
The default mesh in KK and KKS are quite low poly.  Here are some ways to correct that.

### Tesselation Shader
This can affect skin or clothing mesh
- There are a few shaders you can apply to a character that have tesselation sliders.  Tesselation subdivides the mesh, making the mesh appear more smooth.  And unlike the HighPoly mesh, it is better on performance and comes default with all Repacks.
- These are the shaders with tesselation sliders (that I know of):
    - The Vanilla+ shaders by Xukmi 
    - The KKUTS shaders by Haruka 
- If you have the ShaderSwapper pluggin latest release you can automatically apply tesselation with `CTRL + P` if you configure it first.
    - Otherwise you can apply them manually via Material Editor
- I'm not sure if tesselation shaders exist for HS2/AI.

### High Poly Mesh
This only affects the skin mesh
- A high poly uncensor is a body mesh that contains more verticies than the normal mesh, which makes the mesh more smooth overall.
- If you want my recomendation [Look for [KK][Female]Highpoly_vX.X.zip Here](https://ux.getuploader.com/nHaruka_KK/)  They've done a decent job to prevent clipping with the latest versions. 
  - You can alter the xml in the zipmod to get it working in KKS by changing the game name from "Koikatsu" to "Koikatsu Sunshine" if it's not available in KKS yet. 
  - To use it in studio/maker just extract that zip into `<root game folder>/mods/MyMods/`.   Then in game, find the Uncensor dropdown and select "High Poly".
- High Poly meshes exist for HS2/AI as well if you look around, but generally there are plenty of polygons in those games.
- The downsides are:
    - The performance cost is higher than a tesselation shader, since a shader is handled almost entirely on the GPU.
    - While an uncensor does save to the character card, anyone you share the card with must also have downloaded the same high poly mesh.    

>right is HighPoly
<img src="https://github.com/thojmr/KK_PregnancyPlus/blob/master/images/HighPoly.png" height="200"></img>

### Lapacian Smoothing (Preg+ Belly Mesh Smoothing button)
This affects skin and clothing mesh
- Finally there is the Mesh Smoothing button in Pregnancy+.  This will perform a Lapacian Smoothing pass over the existing mesh to help smooth out rough areas.  It's very slow to process, but great at reducing skin stretching near the edge of the belly.
- This type of smoothing does not save to the character card unless you save it as a Blendshape in the BlendShape GUI.  But that has its own drawbacks.  See [BlendShape GUI](#the-blendshape-gui)
- I recomend this when all you want to do is fix some problem areas around the belly for screenshots.

</br>
</br>
</br>

## Some PregnancyPlus technical details
- Instead of manipulating the bones like KK_Pregnancy does, this mod alters the mesh itself with blendshapes computed at runtime, which has benefits and drawbacks.
    - The sliders alter the blendshape weight and or create a new blendshape if one does not already exist. 
- Integrates with KK/AI_Pregnancy in Story Mode so that both plugins can work together.  This can be configured in plugin config

### Some of the drawbacks of generating blendshapes instead of manipulating bones directly
- Right now clothing can be hit or miss, because of the way the belly grows the mesh loses its local positional data causing clipping. On the other hand, when adjusting bone scale, clothing shifts automagically which usually results in less clipping, but less shape controll overall.
- Acessories won't automatically move with the mesh as they do when you manipulate bones unless they are "Skinned Accessories".  And Preg+ works with skinned accessorries on v6.0+
- Generating BlendShapes has a bigger impact on performance (only when changing a slider) because of the computations it has to perform. However once the shape is calculated the performance is equally as fast as bone manipulation.
- Unity doesn't have great blendshape support in older versions like KK is running on, se we have to hack it a bit.
- Since blendshapes are tied to a single mesh, if the mesh is changed (like uncensors), any saved blendshape will become invalid, and a new blendshape will need to be made. Bone manipulation on the other hand doesn't care about the specific mesh as long as it is skinned properly.

</br>
</br>
</br>


## (Developers only) Compiling with Visual Studio 2019 (The official way)
<details>
  <summary>Click to expand</summary>
 
 Simply clone this repository to your drive and use the free version of Visual Studio 2019 for C# to compile it. Hit build and all necessary dependencies should be automatically downloaded. Check the following links for useful tutorials. If you are having trouble or want to try to make your own plugin/mod, feel free to ask for help in modding channels of either the [Koikatsu](https://discord.gg/hevygx6) or [IllusionSoft](https://discord.gg/F3bDEFE) Discord servers.
- https://help.github.com/en/github/creating-cloning-and-archiving-repositories/cloning-a-repository
- https://docs.microsoft.com/en-us/visualstudio/get-started/csharp/?view=vs-2019
- https://docs.microsoft.com/en-us/visualstudio/ide/troubleshooting-broken-references?view=vs-2019
    
Note: You may need to manually copy a few DLL's from the game's directory to `<project_root>/packages/` if you are noticing some missing references (HS2PE, KKPE, etc...).
 </details>

## (Developers only) Compiling with Visual Studio Code (My way)
<details>
  <summary>Click to expand</summary>

### Setup:

- Clone this repository to your drive and use Visual Studio Code.  
- Install the C# extension for VSCode. 
- Make sure the following directory exists `C:/Program Files (x86)/Microsoft Visual Studio/2019/Community/MSBuild/Current/Bin/msbuild.exe`
  - If not you will need to install the VS2019 MS build tools (There may be other ways to build, but this is the one that eventually worked for me)
- Install nuget.exe and add it to your enviroment variables PATH.  (You can probably use the VS Code Nuget extension too, I just prefer command line)
- Run `nuget install -OutputDirectory ../../packages` to install the dependancies from the `./PregnancyPlus/KK_PregnancyPlus.csproj` directory.  
- Finally create a build script with tasks.json in VSCode, to automate builds and releases.

Note: If you see a .net version error, you will need to install that version of .net development kit (3.5 for KK, and 4.6 for others)

Note: You may need to manually copy a few DLL's from the game's directory to `<project_root>/packages/` if you are noticing some missing references (HS2PE, KKPE, etc...).

Example build task:  Debug and Release Tasks
```json
{
    "label": "_debug",
    "command": "C:/Program Files (x86)/Microsoft Visual Studio/2019/Community/MSBuild/Current/Bin/msbuild.exe",
    "type": "process",
    "args": [
        "${workspaceFolder}/PregnancyPlus.sln",
        "/property:GenerateFullPaths=true;Configuration=Debug",
        "/consoleloggerparameters:NoSummary"
    ],
    "presentation": {
        "reveal": "silent"
    },
    "problemMatcher": "$msCompile"
},
{
    "label": "build-debug+copy-output",
    "type": "shell",
    "command": "cp ./bin/KK_PregnancyPlus/BepInEx/plugins/KK_PregnancyPlus.dll 'C:/<game directory>/BepInEx/plugins/'",
    "dependsOn": "_debug",
    "presentation": {
        "echo": true,
        "reveal": "silent",
        "focus": false,
        "panel": "shared",
        "showReuseMessage": true,
        "clear": false
    },
    "group": {
        "kind": "build",
        "isDefault": true
    },
    "problemMatcher": []
},
{
    "label": "_release",
    "command": "C:/Program Files (x86)/Microsoft Visual Studio/2019/Community/MSBuild/Current/Bin/msbuild.exe",
    "type": "process",
    "args": [
        "${workspaceFolder}/PregnancyPlus.sln",
        "/property:GenerateFullPaths=true;Configuration=Release",
        "/consoleloggerparameters:NoSummary"
    ],
    "presentation": {
        "reveal": "silent"
    },
    "problemMatcher": "$msCompile"
},
{
    "label": "build-release",
    "type": "shell",
    "command": "./release.cmd",
    "dependsOn": "_release",
    "problemMatcher": []
}
```
If sucessfull you should see a KK_PregnancyPlus.dll file nested in `.\bin\` after building (Ctrl + SHIFT + B)
    
### Additional:
    
One drawback of using VSCode is that you have to manage adding/updating new packages to .config and .csproj files.  Maybe it can be automated, but im too lazy to check.
    
</details>
