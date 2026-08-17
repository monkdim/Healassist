using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using HealAssist.Core;
using HealAssist.Data;

namespace HealAssist.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
    private static readonly Vector4 Good = ImGuiColors.HealerGreen;
    private static readonly Vector4 Warn = ImGuiColors.DalamudYellow;
    private static readonly Vector4 Bad = ImGuiColors.DalamudRed;
    private static readonly Vector4 Muted = ImGuiColors.DalamudGrey;

    /// <summary>The nearby sweep can return a whole field-operation zone; the table shows a slice.</summary>
    private const int MaxPartyRows = 24;

    private readonly Plugin plugin;
    private readonly CommandRunner runner;
    private readonly TargetPicker picker;
    private readonly HotkeyManager hotkeys;

    private string newPlayerName = string.Empty;
    private RoleType newRole = RoleType.Healer;
    private Hotkey? capturing;

    private Configuration Config => plugin.Config;

    public ConfigWindow(Plugin plugin, CommandRunner runner, TargetPicker picker, HotkeyManager hotkeys)
        : base("HealAssist###HealAssistConfig")
    {
        this.plugin = plugin;
        this.runner = runner;
        this.picker = picker;
        this.hotkeys = hotkeys;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(560, 460),
            MaximumSize = new Vector2(1400, 1200),
        };
    }

    public void Dispose()
    {
        if (capturing is not null)
        {
            capturing = null;
            hotkeys.SuppressForRebind = false;
        }
    }

    public override void OnClose()
    {
        capturing = null;
        hotkeys.SuppressForRebind = false;
    }

    public override void Draw()
    {
        if (!ImGui.BeginTabBar("##healassist-tabs"))
            return;

        DrawTab("Raise", DrawRaiseTab);
        DrawTab("Lowest HP", DrawLowestTab);
        DrawTab("Hotkeys", DrawHotkeyTab);
        DrawTab("Macros", DrawMacroTab);
        DrawTab("Party", DrawPartyTab);

        ImGui.EndTabBar();
    }

    private static void DrawTab(string label, Action body)
    {
        if (!ImGui.BeginTabItem(label))
            return;

        ImGui.Spacing();
        body();
        ImGui.EndTabItem();
    }

    // =====================================================================
    // Raise
    // =====================================================================

    private void DrawRaiseTab()
    {
        DrawPreviewLine("Would raise", plugin.Preview.Raise);
        ImGui.Spacing();

        if (ImGui.Button(Config.AutoCastRaise ? "Test now (will cast)" : "Test now (targets only)"))
            runner.TargetRaise();

        ImGui.Separator();
        ImGui.TextUnformatted("Priority list — the first line that matches a dead player wins.");
        Hint("Reorder with the arrows. Add a specific character name to always pull them to the front,\n"
           + "for example your co-healer or a friend who needs to be up first.");
        ImGui.Spacing();

        DrawPriorityList();

        ImGui.Spacing();
        DrawPriorityAdders();

        ImGui.Separator();
        DrawRaiseFilters();

        ImGui.Separator();
        DrawAutoCastSection();
    }

    private void DrawPriorityList()
    {
        var list = Config.RaisePriority;
        var removeAt = -1;
        var swapWith = (From: -1, To: -1);

        if (list.Count == 0)
        {
            ColoredWrapped(Muted, "The list is empty. With \"Only raise players on this list\" off, "
                         + "everyone is still raisable in party order.");
        }

        for (var i = 0; i < list.Count; i++)
        {
            var entry = list[i];
            ImGui.PushID(i);

            ImGui.BeginDisabled(i == 0);
            if (ImGui.ArrowButton("##up", ImGuiDir.Up))
                swapWith = (i, i - 1);
            ImGui.EndDisabled();

            ImGui.SameLine(0, 2);
            ImGui.BeginDisabled(i == list.Count - 1);
            if (ImGui.ArrowButton("##down", ImGuiDir.Down))
                swapWith = (i, i + 1);
            ImGui.EndDisabled();

            ImGui.SameLine();
            var enabled = entry.Enabled;
            if (ImGui.Checkbox("##enabled", ref enabled))
            {
                entry.Enabled = enabled;
                Config.Save();
            }
            Hint("Untick to skip this line without deleting it.");

            ImGui.SameLine();
            ImGui.TextUnformatted($"{i + 1}.");

            ImGui.SameLine();
            var color = entry.Kind == PriorityKind.Player ? ImGuiColors.ParsedGold : ColorForRole(entry.Role);
            ImGui.PushStyleColor(ImGuiCol.Text, entry.Enabled ? color : Muted);
            ImGui.TextUnformatted(entry.Label);
            ImGui.PopStyleColor();

            if (entry.Kind == PriorityKind.Player)
            {
                ImGui.SameLine();
                Colored(Muted, "(player)");
            }

            ImGui.SameLine();
            if (ImGui.SmallButton("Remove"))
                removeAt = i;

            ImGui.PopID();
        }

        if (swapWith.From >= 0)
        {
            (list[swapWith.From], list[swapWith.To]) = (list[swapWith.To], list[swapWith.From]);
            Config.Save();
        }

        if (removeAt >= 0)
        {
            list.RemoveAt(removeAt);
            Config.Save();
        }
    }

    private void DrawPriorityAdders()
    {
        ImGui.SetNextItemWidth(220f);
        if (ImGui.BeginCombo("##role-to-add", newRole.DisplayName()))
        {
            foreach (var role in RoleTypeExtensions.Selectable)
            {
                if (ImGui.Selectable(role.DisplayName(), role == newRole))
                    newRole = role;
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        if (ImGui.Button("Add role"))
        {
            Config.RaisePriority.Add(PriorityEntry.ForRole(newRole));
            Config.Save();
        }

        ImGui.SetNextItemWidth(220f);
        ImGui.InputTextWithHint("##player-to-add", "Character name, e.g. Eden Aphelion", ref newPlayerName, 64);

        ImGui.SameLine();
        ImGui.BeginDisabled(string.IsNullOrWhiteSpace(newPlayerName));
        if (ImGui.Button("Add player"))
        {
            Config.RaisePriority.Add(PriorityEntry.ForPlayer(newPlayerName));
            newPlayerName = string.Empty;
            Config.Save();
        }
        ImGui.EndDisabled();

        ImGui.SameLine();
        DrawAddFromPartyCombo();

        ColoredWrapped(Muted, "A one-word entry also matches that person's first name, so \"Eden\" finds \"Eden Aphelion\".");
    }

    private void DrawAddFromPartyCombo()
    {
        var members = plugin.Preview.Members;

        ImGui.BeginDisabled(members.Count == 0);
        if (ImGui.BeginCombo("##add-from-party", "Add from party", ImGuiComboFlags.NoArrowButton))
        {
            foreach (var member in members)
            {
                if (!ImGui.Selectable($"{member.Name} ({member.JobAbbreviation})"))
                    continue;

                Config.RaisePriority.Insert(0, PriorityEntry.ForPlayer(member.Name));
                Config.Save();
            }

            ImGui.EndCombo();
        }
        ImGui.EndDisabled();
        Hint("Adds the selected party member to the top of the list, spelled exactly right.");
    }

    private void DrawRaiseFilters()
    {
        CheckboxSetting("Only raise players on the list above", Config.RaiseOnlyListed,
            v => Config.RaiseOnlyListed = v,
            "On: anyone who matches no line is ignored entirely.\nOff: they are still raisable, just last.");

        CheckboxSetting("Skip corpses that already have a raise on them", Config.RaiseSkipPending,
            v => Config.RaiseSkipPending = v,
            "Skips anyone showing the Raise status, so you do not double up on a body\nthat is already waiting on an accept prompt.");

        CheckboxSetting("Skip corpses someone else is mid-cast on", Config.RaiseSkipBeingRaisedByOthers,
            v => Config.RaiseSkipBeingRaisedByOthers = v,
            "Watches other players' cast bars for raise spells and hands you the next body instead.");

        CheckboxSetting("Include the other alliance parties (24-player content)", Config.RaiseIncludeAlliance,
            v => Config.RaiseIncludeAlliance = v,
            "Your own party is always considered first.");

        CheckboxSetting("Include anyone nearby, party or not", Config.RaiseIncludeNearby,
            v => Config.RaiseIncludeNearby = v,
            "For field operations — Occult Crescent, Bozja, Eureka — where the people who need\n"
          + "raising are in the zone with you but not in any party of yours.\n"
          + "Your party is still considered first, then the alliance, then everyone else.");

        var distance = Config.RaiseMaxDistance;
        ImGui.SetNextItemWidth(220f);
        if (ImGui.SliderFloat("Max distance (yalms)", ref distance, 0f, 60f, "%.0f"))
        {
            Config.RaiseMaxDistance = distance;
            Config.Save();
        }
        Hint("Raise spells reach 30y. Set to 0 to ignore distance entirely.");
    }

    private void DrawAutoCastSection()
    {
        ImGui.TextUnformatted("Let HealAssist cast the raise itself");
        ColoredWrapped(Muted,
            "Optional, and off by default. The recommended setup is to leave this off and let your macro "
          + "do the casting — see the Macros tab, you still get one button. Turning this on means the "
          + "plugin sends the action for you, which is the kind of automation Square Enix's Terms of "
          + "Service prohibit. Your call, your account.");
        ImGui.Spacing();

        CheckboxSetting("Cast my raise spell immediately after targeting", Config.AutoCastRaise,
            v => Config.AutoCastRaise = v, null);

        ImGui.BeginDisabled(!Config.AutoCastRaise);
        ImGui.Indent();

        CheckboxSetting("Use Swiftcast first when it is available", Config.AutoCastUseSwiftcast,
            v => Config.AutoCastUseSwiftcast = v,
            "Skipped if you already have the Swiftcast buff or it is on cooldown.");

        if (ImGui.TreeNode("Raise spell IDs"))
        {
            ColoredWrapped(Muted, "Only touch these if a patch ever changes an action ID. Set one to 0 to go back to the built-in value.");
            foreach (var jobId in JobTable.RaiseCapableJobs)
            {
                var current = (int)Config.GetRaiseActionFor(jobId);
                ImGui.SetNextItemWidth(120f);
                if (ImGui.InputInt(JobTable.GetAbbreviation(jobId), ref current, 0, 0))
                {
                    Config.RaiseActionOverrides[jobId] = (uint)Math.Max(0, current);
                    Config.Save();
                }
            }

            if (ImGui.Button("Reset spell IDs to defaults"))
            {
                Config.RaiseActionOverrides.Clear();
                Config.Save();
            }

            ImGui.TreePop();
        }

        ImGui.Unindent();
        ImGui.EndDisabled();
    }

    // =====================================================================
    // Lowest HP
    // =====================================================================

    private void DrawLowestTab()
    {
        DrawPreviewLine("Would target", plugin.Preview.Lowest);
        ImGui.Spacing();

        if (ImGui.Button("Test now"))
            runner.TargetLowestHp();

        ImGui.Separator();

        CheckboxSetting("Include myself", Config.LowestIncludeSelf,
            v => Config.LowestIncludeSelf = v,
            "Off if you would rather never have this command target you.");

        CheckboxSetting("Include the other alliance parties (24-player content)", Config.LowestIncludeAlliance,
            v => Config.LowestIncludeAlliance = v, null);

        CheckboxSetting("Include anyone nearby, party or not##low", Config.LowestIncludeNearby,
            v => Config.LowestIncludeNearby = v,
            "Same field-operation case as the raise button. Worth pairing with a lower HP\n"
          + "threshold, or the button will keep finding a scratched stranger to heal.");

        CheckboxSetting("Keep my current target when nobody qualifies", Config.LowestKeepTargetIfNoneFound,
            v => Config.LowestKeepTargetIfNoneFound = v,
            "Off clears your target instead, which is rarely what you want mid-pull.");

        var threshold = Config.LowestMaxHpPercent;
        ImGui.SetNextItemWidth(220f);
        if (ImGui.SliderFloat("Only consider below (% HP)", ref threshold, 1f, 100f, "%.0f%%"))
        {
            Config.LowestMaxHpPercent = threshold;
            Config.Save();
        }
        Hint("At 100% this always finds someone. Lower it if you want the button to do nothing\nwhen the party is basically topped off.");

        var distance = Config.LowestMaxDistance;
        ImGui.SetNextItemWidth(220f);
        if (ImGui.SliderFloat("Max distance (yalms)##low", ref distance, 0f, 60f, "%.0f"))
        {
            Config.LowestMaxDistance = distance;
            Config.Save();
        }
        Hint("Most single-target heals reach 30y. Set to 0 to ignore distance.");

        ImGui.Separator();
        ColoredWrapped(Muted, "Dead players are never picked here — that is what the raise button is for.");
    }

    // =====================================================================
    // Hotkeys
    // =====================================================================

    private void DrawHotkeyTab()
    {
        ImGui.TextWrapped(
            "Hotkeys are optional. If you play on a controller, putting the macros on a crossbar slot "
          + "is usually easier — see the Macros tab. Use these if you have a spare keyboard key, or if "
          + "you remap a controller button to a keyboard key with Steam Input, JoyToKey or DS4Windows.");
        ImGui.Spacing();
        ColoredWrapped(Muted, "These read the game's own key state, so they only fire while FFXIV has focus, "
                            + "and only for keys the game itself recognises.");
        ImGui.Separator();

        DrawHotkeyRow("Raise target", Config.RaiseHotkey);
        DrawHotkeyRow("Lowest HP target", Config.LowestHotkey);

        ImGui.Separator();
        CheckboxSetting("Only fire hotkeys while in combat", Config.HotkeysOnlyInCombat,
            v => Config.HotkeysOnlyInCombat = v,
            "Stops a stray press from re-targeting you while you are running around town.");

        if (capturing is not null)
        {
            ImGui.Spacing();
            Colored(Warn, "Press a key now, or press Escape to cancel.");
            CaptureKey();
        }
    }

    private void DrawHotkeyRow(string label, Hotkey hotkey)
    {
        ImGui.PushID(label);

        var enabled = hotkey.Enabled;
        if (ImGui.Checkbox("##enabled", ref enabled))
        {
            hotkey.Enabled = enabled;
            Config.Save();
        }

        ImGui.SameLine();
        ImGui.TextUnformatted(label);

        ImGui.SameLine(190f);
        var isCapturing = ReferenceEquals(capturing, hotkey);
        ImGui.PushStyleColor(ImGuiCol.Text, isCapturing ? Warn : hotkey.IsBound ? Good : Muted);
        ImGui.TextUnformatted(isCapturing ? "Press a key..." : hotkey.Describe());
        ImGui.PopStyleColor();

        ImGui.SameLine(360f);
        if (ImGui.Button(isCapturing ? "Cancel" : "Set"))
        {
            capturing = isCapturing ? null : hotkey;
            hotkeys.SuppressForRebind = capturing is not null;
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(!hotkey.IsBound && hotkey.Key == VirtualKey.NO_KEY);
        if (ImGui.Button("Clear"))
        {
            hotkey.Clear();
            Config.Save();
        }
        ImGui.EndDisabled();

        ImGui.PopID();
    }

    private void CaptureKey()
    {
        var target = capturing;
        if (target is null)
            return;

        if (HotkeyManager.IsDown(VirtualKey.ESCAPE))
        {
            capturing = null;
            hotkeys.SuppressForRebind = false;
            return;
        }

        foreach (var key in Svc.KeyState.GetValidVirtualKeys())
        {
            if (key is VirtualKey.NO_KEY or VirtualKey.CONTROL or VirtualKey.MENU or VirtualKey.SHIFT
                or VirtualKey.LCONTROL or VirtualKey.RCONTROL or VirtualKey.LMENU or VirtualKey.RMENU
                or VirtualKey.LSHIFT or VirtualKey.RSHIFT or VirtualKey.ESCAPE)
                continue;

            if (!HotkeyManager.IsDown(key))
                continue;

            target.Key = key;
            target.Ctrl = HotkeyManager.IsDown(VirtualKey.CONTROL);
            target.Alt = HotkeyManager.IsDown(VirtualKey.MENU);
            target.Shift = HotkeyManager.IsDown(VirtualKey.SHIFT);
            target.Enabled = true;

            capturing = null;
            hotkeys.SuppressForRebind = false;
            Config.Save();
            return;
        }
    }

    // =====================================================================
    // Macros
    // =====================================================================

    private void DrawMacroTab()
    {
        ImGui.TextWrapped(
            "This is the recommended setup. HealAssist moves your target, the next macro line casts the "
          + "spell — so the game handles the cast exactly as if you had clicked the party member yourself.");
        ImGui.Spacing();
        ImGui.TextWrapped(
            "Make a macro in-game (User Macros), paste one of these in, give it an icon, then drag it "
          + "onto a hotbar or crossbar slot.");
        ImGui.Separator();

        var raiseSpell = CurrentRaiseSpellName();

        DrawMacroBlock(
            "Raise the priority target",
            $"/harez\n/ac \"{raiseSpell}\" <t>",
            "Swap the spell name for your job if you swap jobs, or make one macro per job.");

        DrawMacroBlock(
            "Raise with Swiftcast",
            $"/harez\n/ac \"Swiftcast\" <wait.1>\n/ac \"{raiseSpell}\" <t>",
            "Macros cannot queue, so the wait is there to give Swiftcast a beat to apply.");

        DrawMacroBlock(
            "Heal the lowest HP party member",
            "/halow\n/ac \"Cure II\" <t>",
            "Any single-target heal works. Benediction, Essential Dignity, Tetragrammaton, Haima, and so on.");

        DrawMacroBlock(
            "Just move the target, cast it yourself",
            "/harez",
            "Useful if you would rather keep full control of which spell goes out.");

        ImGui.Separator();
        ImGui.TextUnformatted("Getting these onto a controller");
        ImGui.Spacing();
        ImGui.TextWrapped(
            "The easiest route is the expanded crossbar: hold one trigger and double-tap the other (WXHB), "
          + "or use the Expanded Hold Controls in Character Configuration. Those slots are empty on most "
          + "setups, so you get two free buttons without giving anything up.");
        ImGui.Spacing();
        ImGui.TextWrapped(
            "If you want a dedicated physical button instead, map an unused one — L3, R3, or the touchpad "
          + "click — to a spare keyboard key with Steam Input, DS4Windows or JoyToKey, then bind that key "
          + "to the hotbar slot holding the macro. Insert, Delete, Home, End, Page Up, Page Down and the "
          + "numpad keys are all free in a default FFXIV keybind layout.");
    }

    private void DrawMacroBlock(string title, string macro, string note)
    {
        ImGui.PushID(title);
        ImGui.TextUnformatted(title);

        foreach (var line in macro.Split('\n'))
        {
            ImGui.Indent();
            Colored(ImGuiColors.ParsedGold, line);
            ImGui.Unindent();
        }

        if (ImGui.Button("Copy"))
            ImGui.SetClipboardText(macro);

        ImGui.SameLine();
        Colored(Muted, note);

        ImGui.Spacing();
        ImGui.PopID();
    }

    private static string CurrentRaiseSpellName()
    {
        var jobId = Svc.Me?.ClassJob.RowId ?? 0;
        return jobId switch
        {
            6 or 24 => "Raise",
            26 or 27 or 28 => "Resurrection",
            33 => "Ascend",
            35 => "Verraise",
            36 => "Angel Whisper",
            40 => "Egeiro",
            _ => "Raise",
        };
    }

    // =====================================================================
    // Party preview
    // =====================================================================

    private void DrawPartyTab()
    {
        var preview = plugin.Preview;

        DrawPreviewLine("Would raise", preview.Raise);
        DrawPreviewLine("Lowest HP", preview.Lowest);
        ImGui.Separator();

        if (preview.Members.Count == 0)
        {
            Colored(Muted, "Nobody to show. Log in and join a party, or turn on \"Include anyone nearby\".");
            return;
        }

        var raiseId = preview.Raise.Target?.GameObject.EntityId;
        var lowestId = preview.Lowest.Target?.GameObject.EntityId;

        // With the nearby sweep on this can be the whole zone, so show your own party first and
        // float corpses to the top of each group rather than dumping object-table order.
        var rows = preview.Members
            .OrderBy(m => m.Source)
            .ThenByDescending(m => m.IsDead)
            .ThenBy(m => m.PartyIndex)
            .ThenBy(m => m.Distance)
            .ToList();

        var hidden = 0;
        if (rows.Count > MaxPartyRows)
        {
            hidden = rows.Count - MaxPartyRows;
            rows = rows.Take(MaxPartyRows).ToList();
        }

        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp;
        if (!ImGui.BeginTable("##party", 6, flags))
            return;

        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 2f);
        ImGui.TableSetupColumn("Job", ImGuiTableColumnFlags.WidthStretch, 0.6f);
        ImGui.TableSetupColumn("HP", ImGuiTableColumnFlags.WidthStretch, 1.4f);
        ImGui.TableSetupColumn("Dist", ImGuiTableColumnFlags.WidthStretch, 0.6f);
        ImGui.TableSetupColumn("Rez rank", ImGuiTableColumnFlags.WidthStretch, 0.8f);
        ImGui.TableSetupColumn("Notes", ImGuiTableColumnFlags.WidthStretch, 1.6f);
        ImGui.TableHeadersRow();

        foreach (var member in rows)
        {
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            var marker = member.GameObject.EntityId == raiseId ? "> "
                       : member.GameObject.EntityId == lowestId ? "* "
                       : string.Empty;
            ImGui.PushStyleColor(ImGuiCol.Text, member.IsDead ? Bad : ColorForRole(member.Role));
            ImGui.TextUnformatted($"{marker}{member.Name}");
            ImGui.PopStyleColor();

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(member.JobAbbreviation);

            ImGui.TableNextColumn();
            if (member.IsDead)
            {
                Colored(Bad, "dead");
            }
            else
            {
                var fraction = member.HpPercent / 100f;
                ImGui.ProgressBar(fraction, new Vector2(-1f, ImGui.GetTextLineHeight()), $"{member.HpPercent:F0}%");
            }

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{member.Distance:F0}y");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(member.IsDead ? RankLabel(picker.RankFor(member)) : "-");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(NotesFor(member));
        }

        ImGui.EndTable();

        ImGui.Spacing();
        if (hidden > 0)
            Colored(Muted, $"{hidden} more not shown — the picks above still consider everyone.");
        Colored(Muted, "> next raise target      * lowest HP target");
    }

    private string RankLabel(int rank)
    {
        if (!TargetPicker.IsListed(rank))
            return Config.RaiseOnlyListed ? "excluded" : "unlisted";

        return $"#{TargetPicker.ListPosition(rank) + 1}";
    }

    private static string NotesFor(PartyMemberInfo member)
    {
        var notes = new List<string>(3);
        if (member.IsSelf) notes.Add("you");
        if (member.Source == MemberSource.Alliance) notes.Add("alliance");
        if (member.Source == MemberSource.Nearby) notes.Add("nearby");
        if (member.HasRaisePending) notes.Add("raise pending");
        if (member.IsBeingRaisedByOther) notes.Add("being raised");
        return notes.Count == 0 ? string.Empty : string.Join(", ", notes);
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private static void DrawPreviewLine(string label, PickResult result)
    {
        ImGui.TextUnformatted($"{label}:");
        ImGui.SameLine();

        if (result.Target is not null)
        {
            Colored(Good, $"{result.Target.Name} ({result.Target.JobAbbreviation}) — {result.Target.Distance:F0}y");
        }
        else
        {
            Colored(Muted, TargetPicker.DescribeFailure(result.Failure));
        }
    }

    private void CheckboxSetting(string label, bool value, Action<bool> setter, string? tooltip)
    {
        var local = value;
        if (ImGui.Checkbox(label, ref local))
        {
            setter(local);
            Config.Save();
        }

        if (tooltip is not null)
            Hint(tooltip);
    }

    private static void Hint(string text)
    {
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(text);
    }

    private static void Colored(Vector4 color, string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }

    private static void ColoredWrapped(Vector4 color, string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }

    private static Vector4 ColorForRole(RoleType role) => role switch
    {
        RoleType.Tank => ImGuiColors.TankBlue,
        RoleType.Healer => ImGuiColors.HealerGreen,
        RoleType.MeleeDps or RoleType.PhysicalRangedDps or RoleType.MagicalRangedDps => ImGuiColors.DPSRed,
        _ => ImGuiColors.DalamudWhite,
    };
}
