# HealAssist

A Dalamud plugin for FFXIV that gives healers two buttons:

- **Raise button** — targets the highest-priority dead party member, using a priority list you build yourself.
- **Lowest HP button** — targets whoever in the party has the lowest HP percentage.

HealAssist moves your target cursor. Your macro casts the spell. That means one button press does
the whole target-and-raise combo, while the game handles the cast exactly as if you had clicked the
party member yourself.

---

## Installing

In game, open Dalamud Settings (`/xlsettings`) → **Experimental** → **Custom Plugin Repositories**,
paste this URL, hit the **+**, then **Save and Close**:

```
https://github.com/monkdim/Healassist/releases/latest/download/repo.json
```

HealAssist then shows up in the plugin installer (`/xlplugins`) under **All Plugins** — search for it
and hit Install. Updates come through the normal plugin updater from then on.

That URL always resolves to the newest release, so it never needs changing.

---

## The two commands

| Command | What it does |
| --- | --- |
| `/harez` | Target the highest-priority raisable dead party member |
| `/halow` | Target the party member with the lowest HP percentage |
| `/healassist` | Open the settings window (`/ha` also works) |

`/healassist rez` and `/healassist low` do the same thing as the short commands, if you prefer one
command to remember.

## Macros

Make these in-game under **User Macros**, give them an icon, and drag them to a hotbar or crossbar
slot. The **Macros** tab in the settings window has all of these with a Copy button, and it fills in
the right spell name for whatever job you are currently on.

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

**Heal the lowest HP party member**

```
/halow
/ac "Cure II" <t>
```

Swap `Raise` for your job's spell — `Resurrection` (SCH/SMN), `Ascend` (AST), `Egeiro` (SGE),
`Verraise` (RDM), `Angel Whisper` (BLU) — or make one macro per job.

Macros cannot queue actions the way a real hotbar press can, so on a raise the cast will start when
you press it rather than clipping into a previous cast. For a 8-second raise cast that is fine. It
is not something you would want for your regular DPS rotation.

## Getting this onto a controller

**The easy way — use a crossbar slot you are not using.** Hold one trigger and double-tap the other
to reach the WXHB, or turn on the Expanded Hold Controls in Character Configuration → Hotbar
Settings → Custom. Those slots are empty on most setups, so you get two free buttons without giving
anything up. Drag the macros there and you are done.

**The dedicated-button way.** If you want a real physical button, map an unused one — L3, R3, or the
touchpad click — to a spare keyboard key using Steam Input, DS4Windows or JoyToKey. Then either:

- bind that key to the hotbar slot holding the macro, in Character Configuration → Keybind, or
- bind it directly in HealAssist's **Hotkeys** tab, which skips the hotbar entirely (though then you
  still press your raise button yourself, since only the macro route casts for you).

Good spare keys in a default FFXIV layout: `Insert`, `Delete`, `Home`, `End`, `Page Up`, `Page Down`,
and the numpad. Avoid F13–F24 — some remappers emit them, but the game does not always recognise
them, and HealAssist can only read keys the game itself tracks.

## The raise priority list

Settings → **Raise** tab. The list is checked top to bottom, and the first line that matches a dead
player wins. Two kinds of line:

- **A role** — Healer, Tank, Melee DPS, Physical Ranged DPS, Magical Ranged DPS.
- **A specific character** — type a name, e.g. `Eden Aphelion`. A one-word entry also matches that
  person's first name, so `Eden` finds `Eden Aphelion`. There is an **Add from party** dropdown that
  fills the name in for you, spelled exactly right.

Put a named line above the role lines to always pull that person up first. Default order is
Healer → Tank → Melee → Physical Ranged → Magical Ranged.

Ties inside one line break by party list order. If two people match nothing on the list, they go
last, unless **Only raise players on the list above** is on, in which case they are skipped entirely.

### Filters

- **Skip corpses that already have a raise on them** — ignores anyone showing the Raise status, so
  you do not double up on a body that is already waiting on an accept prompt.
- **Skip corpses someone else is mid-cast on** — watches other players' cast bars for raise spells
  and hands you the next body instead.
- **Include the other alliance parties** — for 24-player content. Your own party always sorts first.
- **Max distance** — 30 yalms by default, which is raise range. Set to 0 to ignore distance.

## Lowest HP settings

- Include or exclude yourself.
- **Only consider below X% HP** — at 100% the button always finds someone. Lower it if you want the
  button to do nothing when the party is basically topped off.
- **Keep my current target when nobody qualifies** — on by default, so a stray press mid-pull does
  not drop your target.
- Max distance, and an alliance toggle.

Dead players are never picked here. That is what the raise button is for.

## The Party tab

A live table of your party while you play: HP bars, distance, which priority line each dead player
matched, and whether someone else is already raising them. `>` marks the next raise target, `*`
marks the lowest HP target. Useful for checking your priority list is doing what you meant before
you rely on it in a raid.

## Optional: let the plugin cast the raise

There is a toggle under Settings → Raise → *Let HealAssist cast the raise itself*, with an optional
Swiftcast-first option. It is **off by default**, and it is the one place this plugin presses a
button on your behalf.

Worth being straightforward about the tradeoff: setting your target is the same thing you do by
clicking a party list slot, but having software send the action for you is the kind of automation
Square Enix's Terms of Service prohibit, and third-party plugins are unsupported regardless. The
macro route above gets you the same one-button result without that. Your call.

---

## Building

Requirements:

- .NET SDK 10.0.101 or newer
- XIVLauncher/Dalamud installed, so the SDK can find the Dalamud assemblies
- Targets **Dalamud API 15** (`Dalamud.NET.Sdk/15.0.0`)

```
dotnet build -c Release
```

The built plugin lands in `HealAssist/bin/Release/HealAssist/`. To load it in game, add that folder
under Dalamud Settings → Experimental → **Dev Plugin Locations**, then enable HealAssist in the
plugin installer.

### CI

Two workflows, both building against the current Dalamud release on a Windows runner:

- **`build.yml`** runs on every pull request and uploads the built plugin as a run artifact. Useful
  for testing a branch: download it from the [Actions tab](https://github.com/monkdim/Healassist/actions),
  unzip somewhere permanent, and point Dev Plugin Locations at that folder.
- **`release.yml`** runs on pushes to `main` (or on demand). It builds, generates `repo.json` from
  `HealAssist.json` plus the `<Version>` in the csproj, and publishes both that manifest and
  `latest.zip` as assets on a `v{version}` GitHub release. Because the manifest's download links use
  the `/releases/latest/download/` redirect, the repository URL above is permanent.

To cut a new version for users: bump `<Version>` in `HealAssist.csproj`, then push a matching tag.

```
git tag v1.1.0.0 && git push origin v1.1.0.0
```

The release job refuses to run if the tag and the csproj version disagree, so the release name and
the `AssemblyVersion` Dalamud compares against cannot drift apart. Re-running without a version bump
just replaces the assets on the existing release, which will not prompt anyone to update.

If you build against an older Dalamud API level, the one thing that needs changing is `Svc.Me` in
`HealAssist/Svc.cs` — `LocalPlayer` moved from `IClientState` to `IObjectTable` in API 14 and was
removed from `IClientState` entirely in API 15. That is isolated to a single line on purpose.

### Project layout

```
HealAssist/
  Plugin.cs              entry point, commands, framework tick
  Svc.cs                 injected Dalamud services
  Configuration.cs       saved settings, priority entries, hotkeys
  Core/
    PartyScanner.cs      reads the party into a safe snapshot
    TargetPicker.cs      priority ranking and selection rules
    CommandRunner.cs     sets the target, optional auto-cast
    HotkeyManager.cs     optional in-game hotkeys
    PartyMemberInfo.cs   one party member, flattened
  Data/
    JobTable.cs          job → role and job → raise spell
    RoleType.cs
  Windows/
    ConfigWindow.cs      settings UI
```

## Notes

- Third-party plugins are not supported by Square Enix. Use at your own risk.
- Action IDs are hard-coded and verified against XIVAPI, but the settings window lets you override
  any of them without a rebuild if a patch ever changes one.
