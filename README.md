# dome-bt
BitTorrent client for use with `MAME-AO`.

![DOME-BT](https://raw.githubusercontent.com/sam-ludlow/dome-bt/main/images/dome-bt.png)

## Installation & Usage

`DOME-BT` is automatically managed by `MAME-AO`, ensure you are running the latest version.

https://github.com/sam-ludlow/mame-ao

## Building from Source
Not using GitHub actions yet.

### Preparing Monotorrent
DOME-BT uses the `monotorrent` library, offical GitHub here https://github.com/alanmcgovern/monotorrent

Monotorrent normally starts with all files set to download. We don't want this and changing them at startup takes too long.

So DOME-BT uses a modified version, here are the steps for preparing `monotorrent` to build DOME-BT.

![dome-bt monotorrent modification](https://raw.githubusercontent.com/sam-ludlow/dome-bt/main/images/dome-bt-monotorrent-modification.png)

- Clone latest stable release that works with .net Framework `git clone --branch release-v3.0.2 https://github.com/alanmcgovern/monotorrent.git` (you will have a detached HEAD, don't wrorry)
- Open solution `src\MonoTorrent.sln`
- Install required .net runtimes
- Search for `= Priority.Normal` you should find it in `src\MonoTorrent.Client\MonoTorrent.Client\Managers\TorrentFileInfo.cs`
- Make the code change `Priority.Normal` => `Priority.DoNotDownload`
- Change solution config to Release
- Rebuild solution
- Observe binaries for .net Framework in `src\MonoTorrent.Client\bin\Release\net472`



### Building DOME-BT
Clone DOME-BT repo and build it.
```
git clone git@github.com:sam-ludlow/dome-bt.git
```

Ensure the `monotorrent` directory is parallel, its just linking to the DLLs at the moment.

```
    <Reference Include="MonoTorrent">
      <HintPath>..\monotorrent\src\MonoTorrent.Client\bin\Release\net472\MonoTorrent.dll</HintPath>
    </Reference>
    <Reference Include="MonoTorrent.Client">
      <HintPath>..\monotorrent\src\MonoTorrent.Client\bin\Release\net472\MonoTorrent.Client.dll</HintPath>
    </Reference>
```
