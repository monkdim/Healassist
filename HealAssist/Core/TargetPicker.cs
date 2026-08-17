namespace HealAssist.Core;

/// <summary>Why a pick failed, so the plugin can say something more useful than "nothing found".</summary>
public enum PickFailure
{
    None,
    NotLoggedIn,
    NoPartyData,
    NobodyDead,
    AllDeadFilteredOut,
    NobodyInRange,
    NobodyHurt,
}

public readonly record struct PickResult(PartyMemberInfo? Target, PickFailure Failure)
{
    public static PickResult Found(PartyMemberInfo member) => new(member, PickFailure.None);
    public static PickResult Fail(PickFailure reason) => new(null, reason);
    public bool Success => Target is not null;
}

/// <summary>Turns a party snapshot plus the user's settings into a single "target this one" answer.</summary>
public sealed class TargetPicker(Configuration config)
{
    /// <summary>Rank pushed onto alliance members so your own party always sorts first.</summary>
    private const int AllianceRankPenalty = 1000;

    /// <summary>Rank for a member that matched nothing in the priority list.</summary>
    private const int UnrankedPenalty = 500;

    public PickResult PickRaiseTarget(IReadOnlyList<PartyMemberInfo> members)
    {
        if (members.Count == 0)
            return PickResult.Fail(PickFailure.NoPartyData);

        var dead = members.Where(m => m.IsDead && !m.IsSelf).ToList();
        if (dead.Count == 0)
            return PickResult.Fail(PickFailure.NobodyDead);

        var inRange = config.RaiseMaxDistance > 0
            ? dead.Where(m => m.Distance <= config.RaiseMaxDistance).ToList()
            : dead;

        if (inRange.Count == 0)
            return PickResult.Fail(PickFailure.NobodyInRange);

        var eligible = inRange.Where(m =>
            (!config.RaiseSkipPending || !m.HasRaisePending) &&
            (!config.RaiseSkipBeingRaisedByOthers || !m.IsBeingRaisedByOther));

        var ranked = eligible
            .Select(m => (Member: m, Rank: RankFor(m)))
            .Where(x => !config.RaiseOnlyListed || x.Rank < UnrankedPenalty)
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Member.PartyIndex)
            .ToList();

        return ranked.Count == 0
            ? PickResult.Fail(PickFailure.AllDeadFilteredOut)
            : PickResult.Found(ranked[0].Member);
    }

    public PickResult PickLowestHpTarget(IReadOnlyList<PartyMemberInfo> members)
    {
        if (members.Count == 0)
            return PickResult.Fail(PickFailure.NoPartyData);

        var alive = members.Where(m => !m.IsDead);

        if (!config.LowestIncludeSelf)
            alive = alive.Where(m => !m.IsSelf);

        if (config.LowestMaxDistance > 0)
            alive = alive.Where(m => m.Distance <= config.LowestMaxDistance);

        var candidates = alive.ToList();
        if (candidates.Count == 0)
            return PickResult.Fail(PickFailure.NobodyInRange);

        var hurt = candidates
            .Where(m => m.HpPercent <= config.LowestMaxHpPercent)
            .OrderBy(m => m.HpPercent)
            .ThenBy(m => m.CurrentHp)
            .ThenBy(m => m.PartyIndex)
            .ToList();

        return hurt.Count == 0
            ? PickResult.Fail(PickFailure.NobodyHurt)
            : PickResult.Found(hurt[0]);
    }

    /// <summary>
    /// Index of the first enabled priority entry this member matches. Lower wins. Members that
    /// match nothing land after every listed member but still ahead of the alliance.
    /// </summary>
    public int RankFor(PartyMemberInfo member)
    {
        var basePenalty = member.IsAllianceMember ? AllianceRankPenalty : 0;
        var list = config.RaisePriority;

        for (var i = 0; i < list.Count; i++)
        {
            var entry = list[i];
            if (!entry.Enabled)
                continue;

            var matches = entry.Kind switch
            {
                PriorityKind.Player => NameMatches(entry.PlayerName, member.Name),
                PriorityKind.Role => entry.Role == member.Role,
                _ => false,
            };

            if (matches)
                return basePenalty + i;
        }

        return basePenalty + UnrankedPenalty;
    }

    /// <summary>
    /// Full character names match case-insensitively. A single-word entry ("Eden") also matches
    /// that member's first name, so you do not have to type the surname if it is unambiguous.
    /// </summary>
    public static bool NameMatches(string entryName, string memberName)
    {
        var entry = entryName.Trim();
        if (entry.Length == 0)
            return false;

        if (string.Equals(entry, memberName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (entry.Contains(' '))
            return false;

        var space = memberName.IndexOf(' ');
        var firstName = space < 0 ? memberName : memberName[..space];
        return string.Equals(entry, firstName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True when the rank came from an actual priority line rather than the fallback.</summary>
    public static bool IsListed(int rank) => rank % AllianceRankPenalty < UnrankedPenalty;

    /// <summary>Zero-based index of the priority line a rank came from.</summary>
    public static int ListPosition(int rank) => rank % AllianceRankPenalty;

    public static string DescribeFailure(PickFailure failure) => failure switch
    {
        PickFailure.NotLoggedIn => "not logged in.",
        PickFailure.NoPartyData => "no party members found.",
        PickFailure.NobodyDead => "nobody is dead.",
        PickFailure.AllDeadFilteredOut => "every corpse is already being raised or is filtered out.",
        PickFailure.NobodyInRange => "nobody is in range.",
        PickFailure.NobodyHurt => "nobody is below your HP threshold.",
        _ => "no target found.",
    };
}
