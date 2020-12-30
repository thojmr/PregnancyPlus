# Introduction
This repository contains the KK_PregnancyPlus plugin, that adds additional belly sliders in Character Studio.  It is intended to compliment the [KK_Pregnancy](https://github.com/ManlyMarco/KoikatuGameplayMods) plugin, but can be used without it.

![ChaStudio GUI](https://github.com/thojmr/KK_PregnancyPlus/blob/master/images/P%2BGUI.PNG)

## Notes
- Modding this game is new to me, so dont expect this to feel like a finished product.  More like an interesting way to learn C#
- Clothing clips through at max size, so try it without clothing if you are experiencing issues
- This is currently only available in Character Studio, you should see additional sliders below the KK_Pregnancy slider

## How to download
You can grab the latest plugin release [here](https://github.com/thojmr/KK_PregnancyPlus/releases), or build it yourself.  Explained further below.

## How to install
Almost all plugins are installed in the same way. If there are any extra steps needed they will be added to the plugin descriptions below.
1. Make sure you have at least BepInEx 5.1 and latest BepisPlugins and KKAPI.
2. Download the latest release of the plugin you want.
3. Extract the archive into your game directory. The plugin .dll should end up inside your BepInEx\plugins directory.
4. Check if there are no warnings on game startup, if the plugin has settings it should appear in plugin settings.

## Compiling with Visual Studio 2019 (The official way)
Simply clone this repository to your drive and use the free version of Visual Studio 2019 for C# to compile it. Hit build and all necessary dependencies should be automatically downloaded. Check the following links for useful tutorials. If you are having trouble or want to try to make your own plugin/mod, feel free to ask for help in modding channels of either the [Koikatsu](https://discord.gg/hevygx6) or [IllusionSoft](https://discord.gg/F3bDEFE) Discord servers.
- https://help.github.com/en/github/creating-cloning-and-archiving-repositories/cloning-a-repository
- https://docs.microsoft.com/en-us/visualstudio/get-started/csharp/?view=vs-2019
- https://docs.microsoft.com/en-us/visualstudio/ide/troubleshooting-broken-references?view=vs-2019

## Compiling with Visual Studio Code (Not the suggested way, but my way)
Simply clone this repository to your drive and use Visual Studio Code.  
Install the C# extension for VSCode. 
Make sure the following directory exists `C:/Program Files (x86)/Microsoft Visual Studio/2019/Community/MSBuild/Current/Bin/msbuild.exe`.  If not you will need to install the VS2019 MS build tools (There may be other ways to build, but this is the one that eventually worked for me)
Install nuget.exe and set the environment path to it. 
Then use `nuget install -OutputDirectory ../packages` to install the dependancies from the \KK_PregnancyPlus\ directory.  
Finally create a build script with tasks.json in VSCode.
Example build task:
```json
{
    "label": "build-KK_PregnancyPlus",
    "command": "C:/Program Files (x86)/Microsoft Visual Studio/2019/Community/MSBuild/Current/Bin/msbuild.exe",
    "type": "process",
    "args": [
        "${workspaceFolder}/KK_PregnancyPlus/KK_PregnancyPlus.csproj",
        "/property:GenerateFullPaths=true",
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
If sucessfull you should see a KK_PregnancyPlus.dll file in \bin\

### Some of the KK_PregnancyPlus features
- Adds a number of sliders that can alter the size and shape of the belly.  More customizability overall
- Instead of manipulating the bones like KK_Pregnancy does, this mod alters the mesh itself which has benifits and drawbacks
- Integrates with KK_Pregnancy (in Character Studio only for now) so that all sliders can be used together

### Some of the drawbacks of manipulating the mesh instead of the bones directly
-  Right now clothing can be hit or miss, because of the way the belly grows, clothing will flatten and clip when the belly is set its largest size
-  Acessories won't automatically move out of the way of the mesh as they do when you manipulate bones
-  It has bigger impact on performance than a simple bone scale change, but not enough to notice in general

## Some TODO items that may or mat not be implemented in the future (depending on interest)
-  Make accessories move along with the belly to prevent clipping
-  Fix clothing flattening at the largest belly sizes
-  There are certain clothing items that do not work in the current state
-  Potentially add the "improved" belly shape into story mode, with optional config toggle
