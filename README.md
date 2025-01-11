# dome-bt
BitTorrent client for Pleasuredome. For use with `MAME-AO`.

![DOME-BT](https://raw.githubusercontent.com/sam-ludlow/dome-bt/main/images/dome-bt.png)

## Installation & Usage

`DOME-BT` is automatically managed by `MAME-AO`, ensure you are running the latest version.

https://github.com/sam-ludlow/mame-ao

## System requirements
- Windows with .net framework 4.8
- 32 bit / 64 bit
- 0.5 Gb RAM free
- Lots Gb DISK free

## Building from Source
If you don't trust me you can build from source.

### monotorrent
DOME-BT uses the `monotorrent` library, offical GitHub here https://github.com/alanmcgovern/monotorrent

When monotorrent starts Torrents all the files are set to download by default. We don't want this as the Torrents are massive.

As there are so many files in some Torrents then setting the priority at startup takes ages.

So DOME-BT uses a modified version here https://github.com/sam-ludlow/monotorrent/tree/default-file-priority

Only change is at startup all files priority is set to `Priority.DoNotDownload`.

NOTE: DOME-BT can use the default `monotorrent` library, it will just take longer to start up the Torrents.

### Building

Clone these to repos, ensure `monotorrent` is using the `default-file-priority` branch.

```
git clone -b default-file-priority git@github.com:sam-ludlow/monotorrent.git
```

```
git clone git@github.com:sam-ludlow/dome-bt.git
```
Do a `Release` Build of `monotorrent` first, solution file `monotorrent\src\MonoTorrent.sln`.

You can now build `dome-bt`, solution file `dome-bt\dome-bt.sln`.

Its just linking to the DLLs at the moment

```
    <Reference Include="MonoTorrent">
      <HintPath>..\monotorrent\src\MonoTorrent.Client\bin\Release\net472\MonoTorrent.dll</HintPath>
    </Reference>
    <Reference Include="MonoTorrent.Client">
      <HintPath>..\monotorrent\src\MonoTorrent.Client\bin\Release\net472\MonoTorrent.Client.dll</HintPath>
    </Reference>
```
