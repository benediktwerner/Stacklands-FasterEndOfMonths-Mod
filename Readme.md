# Stacklands FasterEndOfMonths Mod

Makes eating food instant and removes the "start month" button.

There also is a configuration option to reduce or disable saving at the end of the month which makes the cutscene almost instant.

## Manual Installation
This mod requires BepInEx to work. BepInEx is a modding framework which allows multiple mods to be loaded.

1. Download and install BepInEx from the [Thunderstore](https://stacklands.thunderstore.io/package/BepInEx/BepInExPack_Stacklands/).
4. Download this mod and extract it into `BepInEx/plugins/`
5. Launch the game

## Development
1. Install BepInEx
2. This mod uses publicized game DLLs to get private members without reflection
   - Use https://github.com/CabbageCrow/AssemblyPublicizer for example to publicize `Stacklands/Stacklands_Data/Managed/GameScripts.dll` (just drag the DLL onto the publicizer exe)
   - This outputs to `Stacklands_Data\Managed\publicized_assemblies\GameScripts_publicized.dll` (if you use another publicizer, place the result there)
3. Compile the project. This copies the resulting DLL into `<GAME_PATH>/BepInEx/plugins/`.
   - Your `GAME_PATH` should automatically be detected. If it isn't, you can manually set it in the `.csproj` file.
   - If you're using VSCode, the `.vscode/tasks.json` file should make it so that you can just do `Run Build`/`Ctrl+Shift+B` to build.

## Links
- Github: https://github.com/benediktwerner/Stacklands-FasterEndOfMonths-Mod
- Thunderstore: https://stacklands.thunderstore.io/package/benediktwerner/FasterEndOfMonths

## Changelog

- v1.1.1: Load this mod after StoneArch for compatibility
- v1.1:
  - Remove "start month" button
  - Add option to skip saving
- v1.0: Initial release
