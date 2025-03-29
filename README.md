# Multi-Map Packer & Uploader
Inspired by [map_batch_updater](https://github.com/ficool2/map_batch_updater) by ficool2

This program is a configurable CLI wrapper for bspzip.exe that can handle multiple maps in one go with the option of uploading them to the workshop if they already exist. 

This tool has only been tested on Team Fortress 2 maps, however it should work for all other Source 1 games.

## Usage
- The `settings.json` file contains settings which will affect all bsp configs
- Within the `configs/` folder you will find `example.json`. You may create as many configs as you'd like in this folder.
- Make sure that `steam_api64.dll` and `steam_appid.txt` are both in the same folder as `MultiMapPackerAndUploader.exe`
- If you intend on uploading maps to the workshop, make sure `steam_appid.txt` contains the game app id that your maps were made for
  * The app id for Team Fortress 2 (440) is there by default
- Run `MultiMapPackerAndUploader.exe`
- You'll be asked to confirm that all settings which were read from your configs are correct
- If `"uploadMapsToWorkshop": true`, you'll be asked to confirm the upload of all maps found from your workshop items
  * If maps with `"upload": true` have an invalid workshop `id`, you'll be asked to confirm and continue the upload of maps that **_were_** found on the workshop

## Configuration Settings

**All keys must be specified unless (optional)**
* Within `settings.json` under the `settings` key
  * `gameinfoPath` - The directory path that contains your `gameinfo.txt`
  * `bspzipPath` - The absolute path to `bspzip.exe`
  * `outputPath` - The location of bsps will be placed after operations
  * `forceMapCompression` - Force all maps to be compressed
  * `uploadMapsToWorkshop` - All maps with their workshop settings properly configured will go through the upload process
  * `verboseLogging` - If `true`, print and log extra information about assets to console
  * `extensionWhitelist` - File extensions that aren't specified in this array will be ignored unless this array is empty
  * `bspConfigFileNames` - Add the names of all the configs within your `configs/` folder that you'd like to operate on
  * `globalAssets` - Assets listed here will be packed into every map

* Within each config within your `configs/` folder under the `maps` key
  *  `name` - The name of the outputted map (`"name": "example"` will output `example.bsp`)
  *  `enabled` - If `true`, operations will be performed on this map
  *  `compress` - If `true`, the map will be compressed. This option can be overridden by `forceMapCompression` in `settings.json`
  *  `sourcePath` - The absolute path to the map which operations will be performed on
  *  (optional) `ignoreAssets` - `false` By default, if `true`, ignores all assets in the `assets`, `sharedAssets`, and `globalAssets` arrays
     * This option can be used to solely upload maps to the workshop without packing assets
  *  (optional) `assets` - An array of absolute asset paths
     * You must include a pair of either `//` or `\\` to denote which files/folders you want to pack into the map
     * This example will pack the `materials` folder `C:/dir//materials`
     * This example will pack all files/folders within the `materials` folder `C:/dir/materials//`
     * This example will pack `asset.txt` into the map without a folder `C:/dir//asset.txt`
  *  (optional) `workshop`  - An object for configuring workshop upload settings
     * `id` - The map's ugc id on the workshop (can be found in the workshop page url)
     * `upload` - If `true`, this map will go through the upload process after all other operations are completed
     * `visibility` - Ranging from [0,3], 0 = Public, 1 = Friends Only, 2 = Private, 3 = Unlisted
     * (optional) `changelog` - Self-explanatory, you may use newlines, will upload a blank changelog by default
  * Within `sharedAssets`
    * Same rules apply here as with the `assets` array within a map
    * These are assets which will be packed into all maps unless the map's `ignoreAssets: true`
    * This array must exist in the config, but including assets here is (optional)
