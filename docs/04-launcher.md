# Build and install Quiet Home

Quiet Home is a native NUI/.NET application, with Home/startup and remote-key
routing adjusted to make it act as the launcher. It does not replace the
kernel or the whole Tizen shell. Samsung's settings, app framework and media
stack remain available underneath.

Home leads with the app you were last in, shown live, with Resume as its one
main action. Inputs and Settings are pills above it, and the dock of chosen
apps runs along the bottom. Your Apps, Inputs and Settings are pages of their
own, with Home, Apps, Inputs and Settings as pill tabs along the top. Every
page is flat black. The app tiles adapt Free My TV. All UI text here is
English. See [attribution](../NOTICE.md).

## How Home works

Home picks what to lead with from two values the TV keeps,
`memory/appfw/current_fullscreen_appid` and
`memory/appfw/previous_fullscreen_appid`. While Quiet Home is in front it is
the current app, so the previous one is the app you came from. The TV viewer
(`org.tizen.tv-viewer`) stands for the last input. Settings, Samsung's own
screens and anything outside the app library leave the earlier choice in
place. The choice is read again whenever Quiet Home comes back and is kept in
memory only. `launcher/src/Recent.cs` holds these rules and
`tests/RecentTests.cs` checks them.

The preview is the app itself, live. Like Samsung's home, Quiet Home asks the
virtual screen linker (`/usr/lib/libvirtualscreen-linker.so`) to scale the
app's own window into the preview area beneath Quiet Home's window, and leaves
that area of its own window transparent so the app shows through. Quiet Home
never reads or stores the app's picture. Settings shows the same app, smaller,
beside its list. The app is let go when another app comes to the front, when
you leave Home and Settings, and when Quiet Home closes. If the app
is not running or any linker call fails, the preview shows the app's tile
instead. An input always shows its card art. The linker is a private Samsung
library observed on the tested build; inspect it before reuse.

Resume, or Back on Home, returns to that app with the extra Samsung's home
sends for a recent app (`Entry=recent`, never `SkipPIN`, so a locked app still
asks for its PIN), or switches to that
input. Until there is something to resume, Home says so and Back switches to
the last input. Up from Resume reaches the pills and Down reaches the dock,
where focus returns to the tile it last left. Back on any other page returns
Home, and the Home button always lands on Resume.

In `/tmp/quiet-home-state.txt`, `recent` names the choice, `preview` reports
`live`, `still` or `none`, and each `vs` line records the linker's results.

## Obtain matching private framework references

Build against the TV's own compatible `Tizen.NUI.dll`,
`Tizen.Applications.Common.dll`, `Tizen.System.dll`, `Tizen.dll` and
`Tizen.TV.DownloadableApplications.dll`.
The shipped project files use `Private=false`, so those references are not
copied into the app package.

Find the paths on the target:

```sh
tv exec --command 'find /usr/share/dotnet.tizen -name Tizen.NUI.dll -o -name Tizen.Applications.Common.dll -o -name Tizen.System.dll -o -name Tizen.dll -o -name Tizen.TV.DownloadableApplications.dll'
```

Inspect the results and pull the five matching runtime assemblies individually
with `tv pull REMOTE_PATH .local/tv-framework/NAME.dll`. Do not use a random
desktop/mobile SDK version just because it has a file with the same name.
Do not commit the resulting assemblies.

```sh
python3 tools/build.py launcher --framework .local/tv-framework
```

This builds `BixbyPromotion.dll`, `QuickLaunch.dll` and `Probe.dll`, then
assembles `.local/package/`. Public source cards are neutral text placeholders;
[optional publisher artwork](06-artwork.md) can be fetched before rebuilding.

## Adapt inputs and apps

The Inputs page shows one card per external input in the TV's source list
(`get_source_list()` in `libsource-api.so`). A card's label is the name
set in Samsung's Edit Source, then the name the device reports, then "HDMI N".
Recognized PlayStation and Nintendo Switch names get the bundled illustration;
otherwise a specific Samsung device icon is used, and a bare port gets the
generic HDMI card. `launcher/src/Sources.cs` holds these rules.

Upload and run Probe to inspect sources and the current main-operation handler:

```sh
tv push .local/build/Probe/Probe.dll Probe.dll
tv exec --command '/usr/bin/dotnet /home/owner/share/tmp/sdk_tools/quiet-tizen/Probe.dll'
```

The app library is the TV's installed store apps plus Samsung's preinstalled
media apps, read at launch through the application manager. `apps.json` is the
curated layer on top. Its entries come first in catalog order and supply
publisher artwork (see [app configuration](06-artwork.md)). Installed apps
without curated art use the square tile Samsung's own home draws, read from
its app service; many package icons are only a placeholder. Catalog entries for
apps that are not installed are hidden. If the query fails, the catalog alone
is shown. `library_source` and `store_source` in `/tmp/quiet-home-state.txt`
report which happened.

Selecting an app opens it when Select is released. Holding Select opens its
options: Open, Add to Home or Remove from Home, Move (on the Home dock) and
Delete app when Samsung's app service marks it removable. Move works like
Samsung's reorder: the rest of the screen dims, the neighboring tiles part
around the app and arrows flank it, Left and Right slide it one slot in 300 ms,
Select keeps the order and Back cancels.
Apps draw like the app row in Samsung's home: 112 px squircle
tiles 16 px apart, a dock aligned with Home's left edge and a 1.2x focus scale with the TV's own
focus-stroke artwork. The dock holds up to ten apps; until it is edited it
shows the first three, and the edited order is saved in the app's data directory as
`dock.json`. Delete asks for confirmation, with Cancel focused, then uses
Samsung's uninstall.

## Install without changing Home yet

The tested native-app container is **`com.samsung.tv.bixby-promotion`**.
Its `BixbyPromotion.dll` is replaced with our assembly, while the package's
existing identity, metadata and framework integration are retained. This
replaces Bixby Promotion's function. It is not a store-signed standalone TPK.

Inspect the installed package, its owner/labels, original entry assembly and
current registry flags first. This release's recipe requires the observed
owner `201:0`. If the layout differs, stop and adapt the profile.

```sh
tv exec --script recipes/snapshot.sh --arg home-01
tar -czf .local/package.tar.gz -C .local/package .
tv push .local/package.tar.gz package.tar.gz
tv exec --script recipes/install-launcher.sh --arg home-01
```

The recipe backs up the original package before replacing the assembly,
moves the cached native image out of the execution path, installs resources,
applies the package's observed SMACK label and requests a manual launch.
It does not change Home/startup.

The new release recipes are a generalized form of the operations used on the
original TV. Filesystem steps are not a single atomic transaction. If a step
fails, inspect the saved state and use recovery; do not rerun by deleting
the original backup. The snapshot and backup folder is
`/opt/usr/share/quiet-tizen-backups/home-01`.

To install a newer build over an existing Quiet Home, stage the new package
and run the update recipe with the same snapshot name. It refuses unless the
live assembly is the one that snapshot recorded, replaces only the assembly and
`res/quiet-home`, and records the new hash so restoration still works. It does
not change Home/startup. Once Quiet Home is the Home app, the TV restarts it
about 0.35 seconds after it is killed, so the recipe labels the new files beside
the live ones and swaps them in by rename before the kill. Copying after the
kill let the restarted instance find an empty `fonts` folder; it then drew
every label in the TV's fallback font while still reporting `font_loaded=True`.

```sh
tv push .local/package.tar.gz package.tar.gz
tv exec --script recipes/update-launcher.sh --arg home-01
```

Verify visibly:

- All pages render; the D-pad, Select and Back behave correctly.
- Home shows the recent app live, and Resume and Back return to it in full
  screen.
- HDMI cards switch the intended input, with picture and audio.
- Each settings row changes the TV and reads back the same value.
- All settings and Network still open Samsung's own screens.
- Chosen app shortcuts launch the correct installed apps.
- The Controllers shortcut reaches the Bluetooth device list.

The `build_ms` value in `/tmp/quiet-home-state.txt` measures scene construction,
not input-to-photon latency or complete rendering time.

## Make it open on Home and startup

Save the **three original values** privately first: the main-operation Home
handler printed by Probe, `db/appfw/home_appid`, and
`db/appfw/startup_appid`. They all happened to be `com.samsung.tv.csfs` on the
case-study device; restoration must use the actual saved values.

Only after the visible verification above:

```sh
tv exec --command '/usr/bin/dotnet /opt/usr/share/quiet-tizen-backups/home-01/Probe.dll set-home com.samsung.tv.bixby-promotion'
tv exec --command 'vconftool set -f -t string db/appfw/home_appid com.samsung.tv.bixby-promotion; vconftool set -f -t string db/appfw/startup_appid com.samsung.tv.bixby-promotion'
```

Read all three selectors back. Let temporary root cleanup finish, then
restart with the remote. Check full boot, the Home button, standby/wake
and return from a streaming app separately. Automatic startup was verified
on the original TV; an OS update can change routing.

## Replace the slow settings/sidebar path

The Settings page lists picture mode, AI picture (Samsung's Intelligent
Mode), brightness, color tone, motion smoothing (Picture Clarity), volume,
sound mode, captions, sleep timer, energy saving and Game Mode. Left and Right
change the focused row at once, and the recent app beside the list shows the
result. `launcher/src/Settings.cs` holds the rows and `tests/SettingsTests.cs`
checks them.

Each row is a Samsung settings item, read through `libsettings-api.so`. The
TV lists an item's options as `value,name;` pairs and repeats a name for each
source type, so the page shows each name once and drops the options the TV
marks unavailable. The tested build reported picture mode as disabled yet
accepted changes, so a row stays open while the TV reports it either visible
or enabled. Every change is read back. A row the TV is not offering, such as
Game Mode while an app plays, shows its value and says so. Sound output is
left out on purpose. Controllers, Network and All settings open Samsung's own
screens. Item IDs and option values are observations for the tested build;
read them again before reuse.

The small QuickLaunch bridge exits on vendor `launch_type=preload` requests.
For interactive requests it opens Quiet Home's settings page and exits.
It does not keep a second heavy settings process running.

After quick settings work, take a new snapshot and install the bridge:

```sh
tv exec --script recipes/snapshot.sh --arg shell-01
tv exec --script recipes/install-settings-bridge.sh --arg shell-01
```

This replaces `/opt/usr/apps/com.samsung.tv.quicklaunch/bin/QuickLaunch.dll`
and updates two `key_filter` rules:

| Rule owner / key | New destination |
|---|---|
| `com.samsung.tv.quicklaunch` / `XF86More` | Quiet Home, `quiet_page:settings` |
| `org.tizen.menu` / `XF86SysMenu` | Quiet Home, `quiet_page:settings` |

The recipe selects semantic keys and checks the saved values. It does not
copy another TV's row numbers. It notifies
`memory/appfw/app_context_db_change` after the transaction.

Test both settings buttons, then generate/apply the **`light-shell`** cleanup
group from the `shell-01` registry snapshot. That disables the native sidebar
and browser. Keep the QuickLaunch bridge and `org.tizen.menu` enabled.

Retain rollback files and remove only the known staged files after verification:

```sh
tv exec --command 'rm -f /home/owner/share/tmp/sdk_tools/quiet-tizen/package.tar.gz /home/owner/share/tmp/sdk_tools/quiet-tizen/Probe.dll; rm -rf /home/owner/share/tmp/sdk_tools/quiet-tizen/unpacked'
```

## Performance choices in the launcher

App art is local. Real `ImageView` objects are created ahead of use and moved
between the hidden cache parent and visible cards. `ReleasePolicy=Destroyed`
keeps decoded textures alive while those views live. The cache is bounded
by catalog size and the grid/dock variants, not an ever-growing screenshot
cache. Image loading is asynchronous, so a first launch still needs loading
time; subsequent page visits avoid recreating image views.

The clock updates every 30 seconds and stops while the launcher is paused.
There is no always-running controller-poll loop. Quiet Home acts on every
remote press; an earlier 180 ms filter on repeated keys dropped every other
press when tapping quickly, so fast taps scrolled slower than steady ones.
Local diagnostic files retain only current launcher state and are overwritten;
the launcher has no telemetry/network client.
