# Bongo Cat Auto Claim

Bongo Cat Auto Claim is a small, open-source Windows utility that automatically
performs the game's normal chest claim when a chest becomes ready.

It keeps Bongo Cat's normal 30-minute cooldown intact. It does **not** speed up
chest timers, automate extra clicks or taps, change Steam inventory/drop
timing, or repeatedly redeem the same chest. Both the normal cosmetic chest and
the emote/secondary chest are handled on supported game builds.

> [!IMPORTANT]
> This is an unofficial community project. Bongo Cat, Steam, and all game files
> remain the property of their respective owners. No game DLL or asset is
> distributed here.

## Features

- One-click **Install Auto Claim** and **Restore Original** actions.
- Automatic Steam library detection, with manual folder selection as fallback.
- Supports both current chest/shop instances.
- Preserves the normal 1,800-second cooldown and standard reward flow.
- Creates and verifies a pristine backup before modifying anything.
- Rejects unknown DLL hashes, module identities, and IL layouts.
- Verifies the exact accepted patched hash before installation.
- Self-contained, portable Windows GUI with no console window.
- No telemetry, analytics, network access, or downloaded replacement DLLs.

## Supported versions

| Platform | Steam build | Original `Assembly-CSharp.dll` SHA-256 | Accepted patched SHA-256 |
| --- | --- | --- | --- |
| Windows / Steam | `25562987` | `BA56E528A5B4B5AF997960E623A90ED0A2A9201ECADE50FA5B6B8B352FDCD953` | `3670F83F99C7E5CFB8906EFD6C3EB8E615D530609C96694CFAE29ACF0DF7CBA2` |

Support for this build was accepted after three natural dual-chest claim
cycles. Unknown or updated builds are deliberately refused until they have
been manually audited and runtime tested.

## Installation

No public binary release is attached yet. Once v1.0.0 release validation is
complete:

1. Download the portable executable from this repository's Releases page.
2. Place it in a writable folder of your choice; it does not need to be inside
   the Bongo Cat installation.
3. Close Bongo Cat normally.
4. Run `BongoCatAutoClaim.exe` without administrator privileges.
5. Confirm the detected game folder and supported status.
6. Select **Install Auto Claim**.
7. Launch Bongo Cat normally through Steam.

The utility patches the user's own legitimate local installation. It does not
download or install somebody else's game assembly.

## Restore / uninstall

1. Close Bongo Cat normally.
2. Keep the portable application's `Backups` folder beside the executable.
3. Run the utility and select **Restore Original**.

The backup's SHA-256 is verified before restoration. Steam's **Verify integrity
of game files** can also restore official files, but may replace other local
modifications and is not the preferred first recovery method.

## Updating Bongo Cat

A Steam update may replace `Assembly-CSharp.dll`, removing the patch. This is
expected. Do not force an older patch onto a newer DLL, and do not copy an old
backup over an updated game build.

Run the utility after an update. If the build or hash is unknown, it will refuse
to modify the file. A maintainer must audit and validate that exact build before
support can be added. See [Adding a supported build](docs/ADDING-A-SUPPORTED-BUILD.md).

## Safety / transparency

- The complete application source is published in this repository.
- Users can inspect it and compile it themselves.
- No Bongo Cat DLL, patched DLL, backup, Steam file, or game asset is included.
- Only allowlisted originals are patched; unknown hashes and structures fail
  closed.
- A pristine, hash-verified backup is created before installation.
- The program requests no administrator elevation and contains no network code.
- Diagnostics are stored locally beside the app and avoid recording unnecessary
  personal information.

Small game-modding and binary-patching utilities can occasionally trigger
heuristic antivirus warnings. Treat any warning seriously: verify the download,
scan it with tools you trust, inspect the source, or build the program yourself.
An alert should be investigated rather than automatically dismissed.

For the precise component and patch boundaries, read
[Architecture](docs/ARCHITECTURE.md).

## Building from source

Requirements:

- Windows 10 or later, x64
- [.NET SDK 8.0.425](https://dotnet.microsoft.com/download/dotnet/8.0)
- Git

The repository pins SDK `8.0.425` with roll-forward disabled in `global.json`
and Mono.Cecil `0.11.4` in
the project file. From the repository root:

```powershell
dotnet restore BongoCatAutoClaim.sln --use-lock-file
dotnet build BongoCatAutoClaim.sln -c Release --no-restore
dotnet run --project tests/BongoCatAutoClaim.Tests -c Release --no-build
dotnet publish src/BongoCatAutoClaim.App -c Release --no-build -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/win-x64
```

The portable executable will be under `artifacts/win-x64`. Build output and
game files are ignored by Git.

Maintainers with a legally obtained pristine supported DLL can additionally
run the local compatibility fixture:

```powershell
dotnet run --project tests/BongoCatAutoClaim.Tests -c Release -- --assembly "X:\path\to\pristine\Assembly-CSharp.dll"
```

The fixture writes only to a temporary directory and verifies that the output
exactly matches the accepted patched SHA-256. Never commit the fixture DLL.

## Troubleshooting

### The tool says the build is unsupported

Do not bypass the warning. Steam may have updated Bongo Cat, or the selected
folder may not be the expected installation. Refresh detection and compare the
displayed build/hash with the supported-version table.

### Bongo Cat is running

Close the game normally before installing or restoring. The utility refuses to
replace a loaded assembly.

### The backup cannot be found

Keep the generated `Backups` directory beside the portable executable. If it
was deleted, Steam's file verification can restore official files.

### Steam reports an error or a timer stops

Close Bongo Cat, restore the original through the utility, and report the exact
game build, displayed hash, and local diagnostic message. Do not shorten the
timer or repeatedly retry claims.

## License

The original source code in this repository is available under the
[MIT License](LICENSE). Third-party notices and ownership boundaries are listed
in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
