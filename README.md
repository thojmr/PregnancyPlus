# Introduction
This repository contains the PregnancyPlus plugin, that adds additional belly sliders in Studio and Maker.  It is intended to compliment the [KK_Pregnancy](https://github.com/ManlyMarco/KoikatuGameplayMods) plugin, but can be used without it.   (For example: HS2 , AI)  
The belly shape will persist in any game mode when slider vales are saved to the characters card, or scene.

<img src="https://github.com/thojmr/KK_PregnancyPlus/blob/master/images/result.png" height="575"></img>
<img src="https://github.com/thojmr/KK_PregnancyPlus/blob/master/images/P%2BMakerGUI.PNG" height="575"></img>
<img src="https://github.com/thojmr/KK_PregnancyPlus/blob/master/images/P%2BStudioGUI.PNG" width="476.5"></img>

## Features
- Adds a number of sliders that will allow you to change the size and shape of a characters belly in Studio and Maker.
    - Slider values will save to scene or card.
    - In the main game you can further adjust all characters belly shape at once with the F1 plugin config sliders.
- Timeline (KK) and VNGE (HS2/AI) integration for animating the belly by creating blendshapes (see specific features below).
- Adds 3 configurable keybinds in plugin config that can be used to increase or decrease the belly size in Main Game, on the fly.   
- This plugin can be a substitute for stomach bulges/fat bellies as well, but it's original intent is pregnancy.    
- Adds a "Main Game" mode config option.  Disable if you want to turn off this plugins features temporarily while playing.
- This plugin is somewhat compatible with "[ddaa33] Pregnant plugin (ShapeKeyPlugin)" if you wish to combine the effects of both, you can.  But the effects applied by this other plugin will not save to the character card by default.

## Koikatsu Only Features
- In Koikatsu the "Inflation Size" belly slider will be added in addition to the KK_Pregnancy when both mods are installed.  You can use the F1 config sliders to adjust the results.  Ex: If the character is 40 weeks pregnant.  They will have the deafult KK_Pregnancy 40 week belly + ("Max Additional Belly Size" * 40) Inflation Size from KK_Pregnancy Plus.  If "Max Additional Belly Size" is set to a low number, the final result will be a slightly larger belly, if it's set high, it will be much much larger.
- Studio Timeline integration via blendshapes.  Here's how to:
    - Set the P+ character sliders to the desired shape you want.
    - Click "Open Blendshapes" button. You will see a popup that will show you any existing P+ blendshapes.  If none are found, then use the "Create New" button.
    - Move your blendshape sliders to the desired position (At least one blendshape slider must be green (touched) before the next step)
    - Open Timeline with Ctrl+T, search for "Blendshape" and middle click to add.
    - Follow Timeline guides for further info

## Honey Select 2 and AI Only Features
- Studio VNGE integration via blendshapes.  Here's how to:
    - Set the P+ character sliders to the desired shape you want.
    - Click "Open Blendshapes" button. You will see a popup that will show you any existing P+ blendshapes.  If none are found, then use the "Create New" button.
    - Move your blendshape sliders to the desired position (At least one blendshape slider must be green (touched) before the next step)
    - Open VNGE, and add it under "Clip Manager" 
     - To use BlendShapes in VNGE set ExportChara_XXPE_BlendShapes=1 in vnactor.ini everywhere it is found
    - Follow VNGE guides for further info

## Bigger!
- For additonal effect in HS2/AI you can mimic what KK_Pregnancy does to belly bones to make the final shape even larger/rounder
- Here's how 
    - In HS2 or AI: Studio > (HS2PE or AIPE) > Adv.mode > Bones
    - Bone: cf_J_kosi01_s  Set the following: PositionZ: 0.6, ScaleZ: 1.6, RotateX: 11
    - Bone: cf_spine01_s  Set the following: PositionZ: 0.6, ScaleZ: 1.6, RotateX: 351
    - Now apply any P+ sliders you want
- This will however make slight changes to the characters spine shape, so keep that in mind.  
- Also since this is altering bones, you may have some unintended cosequences down the road.

## FAQ - Troubleshooting
- Q: Where do I put the PregnancyPlus.dll?
    - A: It should end up under {Root game folder}/BepinEx/Plugins/xx_PregnancyPlus.dll
- Q: Why are some outfits not affected by the sliders?
    - A: Some outifts in Unity are marked as not readable, and the mesh of these outfits can not be altered at runtime.
- Q: Some of the sliders are not working?
    - A: First disable "Balloon" Plugin Config option since it ignores some sliders.  Then try adjusting your 'Move Y' slider to make sure it is not outside your characters body.  Third, make sure P+ gameplay is enabled in Plugin Config, and on the character card (It will be by default).  Worst case scenario you can try turning on Preg+ debug logging to look for any errors in the Plugin Config. 
- Q: The belly size is suddenly changing when the character moves, or the first time I adjust a slider.
    - A: The default belly size is calculated based on the hip and rib bone width.  In rare cases It can be due to strange character animations, or character size adjustments.   
- Q: There is no slider effect when the character has no legs.
    - A: The character must have a leg scale > 0 for the belly sliders to work correctly.

## Notes
- There will be cloth clipping.  You can use the Cloth Offset slider to help with it, but It's a difficult problem to solve.

## How to download
You can grab the latest plugin release [here](https://github.com/thojmr/KK_PregnancyPlus/releases), or build it yourself (developers only).  Explained further below.
This plugin works in Koikatsu, Honey Select 2, and AI.  Grab the KK zip for Koikatsu, HS2 zip for Honey Select 2, and AI zip for AI [here](https://github.com/thojmr/KK_PregnancyPlus/releases)

## How to install
1. Make sure you have at least BepInEx 5.1 and latest BepisPlugins and KKAPI.
2. Download the latest release of the plugin you want.
3. Extract the zip archive into your root game directory. The plugin .dll will end up inside your BepInEx\plugins\ directory.
    - Should look like {root game}/BepInEx/plugins/XX_PregnancyPlus.dll
4. Check if there are no warnings on game startup, if the plugin loaded it should appear in Plugin Config.

## (Developers only) Compiling with Visual Studio 2019 (The official way)
Simply clone this repository to your drive and use the free version of Visual Studio 2019 for C# to compile it. Hit build and all necessary dependencies should be automatically downloaded. Check the following links for useful tutorials. If you are having trouble or want to try to make your own plugin/mod, feel free to ask for help in modding channels of either the [Koikatsu](https://discord.gg/hevygx6) or [IllusionSoft](https://discord.gg/F3bDEFE) Discord servers.
- https://help.github.com/en/github/creating-cloning-and-archiving-repositories/cloning-a-repository
- https://docs.microsoft.com/en-us/visualstudio/get-started/csharp/?view=vs-2019
- https://docs.microsoft.com/en-us/visualstudio/ide/troubleshooting-broken-references?view=vs-2019

## (Developers only) Compiling with Visual Studio Code (Not the suggested way, but my way)
Simply clone this repository to your drive and use Visual Studio Code.  
Install the C# extension for VSCode. 
Make sure the following directory exists `C:/Program Files (x86)/Microsoft Visual Studio/2019/Community/MSBuild/Current/Bin/msbuild.exe`.  
If not you will need to install the VS2019 MS build tools (There may be other ways to build, but this is the one that eventually worked for me)
Install nuget.exe and set the environment path to it. 
Use `nuget install -OutputDirectory ../packages` to install the dependancies from the \KK_PregnancyPlus.csproj directory.  
Finally create a build script with tasks.json in VSCode.
If you see a .net version error, you will need to install that version of .net development kit (probably 3.5 for KK)
Example build task:
```json
{
    "label": "build-KK_PregnancyPlus",
    "command": "C:/Program Files (x86)/Microsoft Visual Studio/2019/Community/MSBuild/Current/Bin/msbuild.exe",
    "type": "process",
    "args": [
        "${workspaceFolder}/KK_PregnancyPlus/KK_PregnancyPlus.csproj",
        "/property:GenerateFullPaths=true;Configuration=Debug",
        "/consoleloggerparameters:NoSummary"
    ],
    "presentation": {
        "reveal": "silent"
    },
    "problemMatcher": "$msCompile",
},
{
    "label": "build-and-copy",
    "type": "shell",
    "command": "cp ./bin/KK_PregnancyPlus.dll '<KK_Install_DIR>/BepInEx/plugins/'",
    "dependsOn": "build-KK_PregnancyPlus",
    "group": {
        "kind": "build",
        "isDefault": true
    },
    "presentation": {
        "echo": true,
        "reveal": "silent",
        "focus": false,
        "panel": "shared",
        "showReuseMessage": true,
        "clear": false
    }
}
```
If sucessfull you should see a KK_PregnancyPlus.dll file nested in .\bin\

### Some KK_PregnancyPlus technical details
- Instead of manipulating the bones like KK_Pregnancy does, this mod alters the mesh itself which has benefits and drawbacks
- Integrates with KK_Pregnancy in Story Mode for KK so that both plugins can work together, can be configured in KK_PregnancyPlus config

### Some of the drawbacks of manipulating the mesh instead of the bones directly
-  Right now clothing can be hit or miss, because of the way the belly grows, clothing will flatten and clip when the belly is set to its largest size
    -  There are some clothing items in HS2 and AI that simply wont work with the sliders at all because they are marked as not readable in Unity
-  Acessories won't automatically move with the mesh as they do when you manipulate bones
-  It has bigger impact on performance (only when moving a slider), but doesn't affect the shape of bones which is nice!
- Why not use static blendShapes?
    - Blendshapes are predefined mesh transitions that could generate the same result.  However blend shapes depend on the base mesh having the same number of verticies as the original mesh it was created on.  Because this game relies heavily on Uncensor bodies and a wide verity of clothing, two meshes rarely have the same number of verticies, which limits where you can use pre made blendshapes.  Instead this plugin calculates the desired mesh positions on the fly when sliders are changed, which doesn't depend on two meshes having the same vertex count/position.  In short: this allows a wider variety of bodies and clothes.
