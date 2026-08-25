# HealAssist

A Dalamud plugin for FFXIV that gives healers two buttons:

* **Raise** targets the highest priority dead player, using a priority list you build yourself.
* **Lowest HP** targets whoever has the lowest HP percentage.

HealAssist moves your target. Your macro casts the spell. One button press does the whole
target-and-raise combo, and the game handles the cast exactly as if you had clicked the party
member yourself.

## Requirements

* Final Fantasy XIV on Windows
* XIVLauncher with Dalamud (this is a Dalamud plugin, it will not run on its own)
* Dalamud API 15 or newer

## Installing

Open Dalamud settings in game with `/xlsettings`, go to **Experimental**, and find **Custom Plugin
Repositories**. Paste this URL into the empty box, press the **+**, then **Save and Close**:

```
https://github.com/monkdim/Healassist/releases/latest/download/repo.json
```

Now open the plugin installer with `/xlplugins`, search for HealAssist under **All Plugins**, and
press Install. Future updates arrive through the normal plugin updater.

That URL always points at the newest release, so you never need to change it.

## The two commands

| Command | What it does |
| --- | --- |
| `/harez` | Target the highest priority raisable dead player |
| `/halow` | Target the player with the lowest HP percentage |
| `/healassist` | Open the settings window (`/ha` works too) |

If you would rather remember one command, `/healassist rez` and `/healassist low` do the same thing.

## Macros

Make these under **User Macros** in game, give them an icon, then drag them onto a hotbar or
crossbar slot. The **Macros** tab in the settings window has all of them with a Copy button, and it
fills in the correct spell name for whatever job you are currently playing.

**Raise the priority target**

```
/harez
/ac "Raise" <t>
```

**Raise with Swiftcast**

```
/harez
/ac "Swiftcast" <wait.1>
/ac "Raise" <t>
```

**Heal the lowest HP player**

```
/halow
/ac "Cure II" <t>
```

Swap `Raise` for whatever your job uses: `Resurrection` on SCH and SMN, `Ascend` on AST, `Egeiro` on
SGE, `Verraise` on RDM, `Angel Whisper` on BLU. Or make one macro per job.

Macros cannot queue actions the way a real hotbar press can, so the cast starts when you press the
button rather than clipping into a previous one. For an eight second raise that makes no practical
difference. Do not build your DPS rotation this way.

## Getting this onto a controller

**The easy way is a crossbar slot you are not using.** Hold one trigger and double tap the other to
reach the WXHB, or turn on Expanded Hold Controls under Character Configuration, Hotbar Settings,
Custom. Those slots sit empty on most setups, so you get two free buttons without giving anything up.
Drag the macros there and you are done.

**If you want a real physical button**, map an unused one such as L3, R3, or the touchpad click to a
spare keyboard key using Steam Input, DS4Windows, or JoyToKey. Then either bind that key to the
hotbar slot holding the macro under Character Configuration, Keybind, or bind it directly in
HealAssist's **Hotkeys** tab to skip the hotbar entirely.

Good spare keys in a default FFXIV layout: `Insert`, `Delete`, `Home`, `End`, `Page Up`, `Page Down`,
and the numpad. Avoid F13 through F24. Some remappers emit them, but the game does not reliably
recognise them, and HealAssist can only read keys the game itself tracks.

## The raise priority list

Settings, **Raise** tab. The list is checked from the top down and the first line that matches a dead
player wins. Two kinds of line:

* **A role.** Healer, Tank, Melee DPS, Physical Ranged DPS, or Magical Ranged DPS.
* **A specific character.** Type a name such as `Eden Aphelion`. A one word entry also matches that
  person's first name, so `Eden` finds `Eden Aphelion`. The **Add from party** dropdown fills the
  name in for you with the correct spelling.

Put a named line above the role lines to always bring that person up first. The default order is
Healer, Tank, Melee, Physical Ranged, Magical Ranged.

Ties inside one line break by party list order. Anyone matching nothing on the list goes last, unless
you turn on **Only raise players on the list above**, which skips them entirely.

### Filters

* **Skip corpses that already have a raise on them.** Ignores anyone showing the Raise status so you
  do not waste a cast on a body already waiting at the accept prompt.
* **Skip corpses someone else is mid cast on.** Watches other players' cast bars for raise spells and
  hands you the next body instead.
* **Include the other alliance parties.** For 24 player content.
* **Include anyone nearby, party or not.** For field operations such as Occult Crescent, Bozja, and
  Eureka, where the people who need raising share the zone with you but are in no party of yours.
* **Max distance.** 30 yalms by default, which is raise range. Set it to 0 to ignore distance.

### How candidates are ordered

Everyone is grouped before the priority list is applied: your own party first, then the alliance,
then unaffiliated players nearby. An unmatched party member still outranks a matched stranger, so
turning on the nearby option cannot pull your button away from your own team. Inside the nearby
group, closest wins.

## Lowest HP settings

* Include or exclude yourself.
* **Only consider below X% HP.** At 100% the button always finds someone. Lower it if you want the
  button to do nothing while the party is basically topped off.
* **Keep my current target when nobody qualifies.** On by default, so a stray press mid pull does not
  drop your target.
* **Include anyone nearby, party or not.** Same field operation case as above. Pair it with a lower
  HP threshold or the button will keep finding a lightly scratched stranger to heal.
* Max distance and an alliance toggle.

Dead players are never picked here. Use the raise button for those.

## The Party tab

A live table of everyone in scope while you play: HP bars, distance, which priority line each dead
player matched, and whether someone else is already raising them. `>` marks the next raise target and
`*` marks the lowest HP target. Your party sorts first and corpses float to the top of each group. In
a busy field operation the table shows a slice rather than the whole zone, but the picks still
consider everyone.

Use it to check your priority list does what you meant before you rely on it in a raid.

## Optional: let the plugin cast the raise

Settings, Raise tab, *Let HealAssist cast the raise itself*, with an optional Swiftcast first option.
This is **off by default** and it is the only place the plugin presses a button for you.

Setting your target is the same thing you do by clicking a party list slot. Having software send the
action for you is the kind of automation Square Enix prohibits, and third party plugins are
unsupported regardless. The macro route above gets you the same one button result without that.
Your call.

## Red Mage

Verraise is a ten second cast, the slowest resurrection in the game. Dualcast makes the next spell
instant, so the fast route is to spend one two second cast on anything and let Verraise ride the
Dualcast it generates.

| Route | Time to rez |
| --- | --- |
| Hard cast Verraise | 10.0s |
| Jolt or Vercure, then Verraise | about 2.0s plus a GCD |
| Swiftcast, then Verraise | instant |

Turn on **Generate an instant cast before Verraise** under Settings, Raise. It needs the auto cast
option enabled, since this is the plugin sending actions. The order it tries:

1. Dualcast or Swiftcast already up, cast Verraise straight away
2. Swiftcast off cooldown, use it, then Verraise
3. Something attackable in range, Jolt it, then Verraise off the Dualcast
4. Nothing attackable, Vercure yourself, then Verraise
5. None of the above worked, fall back to the plain ten second cast

Jolt is preferred over Vercure because both cost the same global cooldown, but Jolt deals damage and
builds mana rather than wasting it. **Your target never moves.** Actions are sent with an explicit
target ID, so the plugin hits the enemy with Jolt while your cursor stays on the body.

The sequence gives up if the body disappears, you die, or the filler cast gets interrupted. The
timeout is configurable and defaults to six seconds.

Worth knowing: **Acceleration does not help here.** It only applies to Verthunder III, Veraero III
and Impact, so it cannot make Verraise instant. Dualcast and Swiftcast are the only two options.

## Troubleshooting

**HealAssist does not appear in the plugin installer.** Press the refresh icon in `/xlplugins`.
Dalamud caches the repository listing and will happily show you stale data. Also check the URL was
saved: reopen `/xlsettings`, Experimental, and confirm it is listed and enabled.

**It appears but will not install.** Usually an API level mismatch. HealAssist targets API 15. If
your Dalamud is older, update XIVLauncher.

**The raise button says "nobody is dead" when someone clearly is.** Check your max distance, and
check whether the corpse already has a raise on it or is being raised by someone else. Both are
filtered out by default. The Party tab shows you exactly which of these applies.

**Nothing happens on a controller press.** The macro needs to be on a hotbar or crossbar slot. If you
mapped a controller button to a keyboard key, confirm the game itself sees that key by binding it to
something obvious first.

**Chat feedback is noisy.** Turn off the success or failure messages in settings.

## Building

You need the .NET SDK 10.0.101 or newer, and XIVLauncher installed so the SDK can find the Dalamud
assemblies. The project targets Dalamud API 15 through `Dalamud.NET.Sdk/15.0.0`.

```
dotnet build -c Release
```

The built plugin lands in `HealAssist/bin/Release/HealAssist/`. To load it in game, add that folder
under Dalamud settings, Experimental, **Dev Plugin Locations**, then enable HealAssist in the plugin
installer.

### CI

Two workflows, both building against the current Dalamud release on a Windows runner.

`build.yml` runs on pull requests and uploads the built plugin as a run artifact. Handy for testing a
branch: download it from the Actions tab, unzip it somewhere permanent, and point Dev Plugin
Locations at that folder.

`release.yml` runs on version tags and on pushes to `main`. It builds, generates `repo.json` from
`HealAssist.json` plus the `<Version>` in the csproj, checks the manifest is a flat array of plugin
entries, and publishes it alongside `latest.zip` on a `v{version}` GitHub release. The download links
inside the manifest use the `/releases/latest/download/` redirect, which is what makes the repository
URL permanent.

To cut a new version, bump `<Version>` in `HealAssist.csproj` and push a matching tag:

```
git tag v1.2.0.0 && git push origin v1.2.0.0
```

The release job refuses to publish if the tag and the csproj version disagree, so the release name
and the `AssemblyVersion` that Dalamud compares against cannot drift apart.

### Project layout

```
HealAssist/
  Plugin.cs              entry point, commands, framework tick
  Svc.cs                 injected Dalamud services
  Configuration.cs       saved settings, priority entries, hotkeys
  Core/
    PartyScanner.cs      reads party, alliance and nearby players into a snapshot
    TargetPicker.cs      grouping, priority ranking, selection rules
    CommandRunner.cs     sets the target, optional auto cast
    HotkeyManager.cs     optional in game hotkeys
    PartyMemberInfo.cs   one candidate, flattened
  Data/
    JobTable.cs          job to role, job to raise spell
    RoleType.cs
  Windows/
    ConfigWindow.cs      settings UI
```

## Notes

Third party plugins are not supported by Square Enix. Use at your own risk.

Action IDs are hard coded and were verified against XIVAPI, but the settings window lets you override
any of them without a rebuild if a patch ever changes one.

## License

MIT. See [LICENSE](LICENSE).
