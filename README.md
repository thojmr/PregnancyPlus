# Introduction
This repository contains the KK_PregnancyPlus plugin, that adds additional belly sliders in Studio and Maker/Creator.  It is intended to compliment the [KK_Pregnancy](https://github.com/ManlyMarco/KoikatuGameplayMods) plugin, but can be used without it.   (For example: HS2 , AI)  Can be used in any game mode when the slider vales are saved to a character card.

<img src="https://github.com/thojmr/KK_PregnancyPlus/blob/master/images/result.png" height="575"></img>
<img src="https://github.com/thojmr/KK_PregnancyPlus/blob/master/images/P%2BMakerGUI.PNG" height="575"></img>
<img src="https://github.com/thojmr/KK_PregnancyPlus/blob/master/images/P%2BStudioGUI.PNG" width="476.5"></img>

## Features
- Adds a number of slider that will allow you to change the size and shape of the belly area in Studio and Maker/Creator.
    - Slider values can be saved to scene or card, but anyone that you share the scene or card with must have KK_PregnancyPlus installed to see the belly shape.
- Adds a Story Mode / Main Game mode config option.  Disable if you want to turn off this plugins features temporarily.
    - In Koikatsu the inflated belly effect will be added on top of the KK_Pregnancy effect if you have that plugin installed, hence you can use the F1 config sliders to adjust the results.
    - In HS2 and AI the belly size will be whatever is defined on the character card + the F1 config slider adjustments.
- Adds 3 configurable keybinds in plugin config that can be used to increase or decrease the belly size in Story Mode / Main Game Mode on the fly.   
- KK Timeline integration via blendshapes.  How to:
    - Set Sliders to the desired shape you want
    - Click "Create Timeline Blendshape" button
    - Open KKPE > Adv.mode > blendshape > select bone/cloth blendshape (Make sure one exists for each coordinate, and o_body_a.  Named like {mesh name}_x_KK_PregnancyPlus)
    - Open Timeline with Ctrl+T, search for "Blendshape" and middle click to add
    - From here treat it like any normal blendshape.  Also, once added to a timeline you can re-adjust the sliders at any time and click "Create Timeline Blendshape" again to update the timelines final shape.
- This plugin can be a substitute for stomach bulges as well, but it's original intent is pregnancy effects.    

## FAQ - Troubleshooting
- Q: Where do I put the KK_PregnancyPlus.dll?  
    - A: It should end up under <Root game folder>/BepinEx/Plugins/KK_PregnancyPlus.dll
- Q: Some of the sliders are not working?
    - A: Disable "Balloon" plugin config option since it disables some sliders.  Then try adjusting your 'Move Z' slider to make sure it is not outside your characters body.
- Q: The belly size is changing when the character moves, or I slightly adjust a slider.
    - A: The default belly size is calculated based on the hip and rib bone width.  In rare cases It can be due to strange character porportions.

## Notes
- Modding this game is new to me, so dont expect this to feel like a finished product.  More like an interesting way to learn C#
- There will be cloth clipping.  You can use the Cloth Offset slider to help with it
- This plugin works in Koikatsu, Honey Select 2, and AI.  Grab the KK zip for Koikatsu, HS2 zip for Honey Select 2, and AI zip for AI [here](https://github.com/thojmr/KK_PregnancyPlus/releases)

## How to download
You can grab the latest plugin release [here](https://github.com/thojmr/KK_PregnancyPlus/releases), or build it yourself.  Explained further below.

## How to install
Almost all plugins are installed in the same way. If there are any extra steps needed they will be added to the plugin descriptions below.
1. Make sure you have at least BepInEx 5.1 and latest BepisPlugins and KKAPI.
2. Download the latest release of the plugin you want.
3. Extract the archive into your game directory. The plugin .dll should end up inside your BepInEx\plugins\ directory.
4. Check if there are no warnings on game startup, if the plugin has settings it should appear in plugin settings.

## Compiling with Visual Studio 2019 (The official way)
Simply clone this repository to your drive and use the free version of Visual Studio 2019 for C# to compile it. Hit build and all necessary dependencies should be automatically downloaded. Check the following links for useful tutorials. If you are having trouble or want to try to make your own plugin/mod, feel free to ask for help in modding channels of either the [Koikatsu](https://discord.gg/hevygx6) or [IllusionSoft](https://discord.gg/F3bDEFE) Discord servers.
- https://help.github.com/en/github/creating-cloning-and-archiving-repositories/cloning-a-repository
- https://docs.microsoft.com/en-us/visualstudio/get-started/csharp/?view=vs-2019
- https://docs.microsoft.com/en-us/visualstudio/ide/troubleshooting-broken-references?view=vs-2019

## Compiling with Visual Studio Code (Not the suggested way, but my way)
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
- Instead of manipulating the bones like KK_Pregnancy does, this mod alters the mesh itself which has benifits and drawbacks
- Integrates with KK_Pregnancy in Story Mode for KK so that both plugins can work together, can be configured in KK_PregnancyPlus config

### Some of the drawbacks of manipulating the mesh instead of the bones directly
-  Right now clothing can be hit or miss, because of the way the belly grows, clothing will flatten and clip when the belly is set its largest size
    -  There are some clothing items in HS2 and AI that simply wont work with the sliders at all
-  Acessories won't automatically move out of the way of the mesh as they do when you manipulate bones
-  It has bigger impact on performance than a simple bone scale change, but doesn't affect the shape of bones which is a bonus!
- Why not use blendShapes?
    - Blendshapes are predefined mesh transitions that could be tied to the sliders in the same way.  However blend shapes depend on the base mesh having the same number of verticies as the original mesh it was created on.  Because this game relies heavily on Uncensor bodies and a wide verity of clothing, two meshed rarley have the same number of verticies, which limits where you can use pre made blend shapes.  Instead this plugin calculates the desired mesh position on the fly when sliders are changed, which doesn't depend on two meshes having the same vertex count/position.

## Some TODO items that may or mat not be implemented in the future (depending on interest)
-  Make accessories move along with the belly to prevent clipping
-  Fix clothing flattening at the largest belly sizes (Has been improved already)
-  There are certain clothing items that do not work in the current state
