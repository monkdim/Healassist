using HealAssist;
using HealAssist.Core;
using HealAssist.Data;

var config = new Configuration { LowestIncludeAlliance = true, LowestIncludeNearby = true,
    LowestIncludeSelf = false, LowestMaxHpPercent = 1, LowestMaxDistance = 1 };
var picker = new TargetPicker(config);
PartyMemberInfo Member(string name, uint hp, bool self = false, MemberSource source = MemberSource.Party,
    uint max = 100, int index = 1) => new()
{
    GameObject = null!, Name = name, CurrentHp = hp, MaxHp = max, IsSelf = self, Source = source,
    JobId = 32, Role = RoleType.Tank, Distance = 10, PartyIndex = index,
    HasRaisePending = false, IsBeingRaisedByOther = false,
};
int checks = 0;
void Check(bool ok, string label) { if (!ok) throw new Exception(label); ++checks; }
void Pick(string? expected, params PartyMemberInfo[] members)
    => Check(picker.PickBlackestNightTarget(members, _ => true).Target?.Name == expected, expected ?? "no target");

Pick("hurt", Member("me", 100, true), Member("hurt", 79), Member("alliance", 1, source: MemberSource.Alliance), Member("stranger", 1, source: MemberSource.Nearby));
Pick("me", Member("me", 100, true), Member("other", 80));
Pick("me", Member("me", 100, true), Member("other", 81));
Pick("me", Member("me", 100, true), Member("other", 100));
Pick("lowest", Member("me", 50, true), Member("lowest", 20), Member("higher", 30));
Pick("me", Member("me", 10, true), Member("other", 20));
Pick("me", Member("me", 100, true), Member("dead", 0), Member("invalid", 10, max: 0));
Pick("percentage", Member("me", 100, true), Member("raw", 5000, max: 10000), Member("percentage", 8000, max: 20000));
Pick("me", Member("me", 100, true));
Pick(null, Member("alliance", 1, true, MemberSource.Alliance), Member("stranger", 1, source: MemberSource.Nearby));
Pick(null);
Pick(null, Member("dead me", 0, true));
var candidates = new[] { Member("me", 100, true), Member("unreachable", 1), Member("reachable", 50) };
Check(picker.PickBlackestNightTarget(candidates, m => m.Name != "unreachable").Target?.Name == "reachable", "skip unusable injured member");
Check(picker.PickBlackestNightTarget(candidates, m => m.IsSelf).Target?.IsSelf == true, "self fallback when injured members cannot be targeted");
Check(!picker.PickBlackestNightTarget(candidates, _ => false).Success, "unavailable spell has no target");
Pick("first", Member("second", 30, index: 2), Member("first", 30, index: 1));
Console.WriteLine($"{checks} Blackest Night targeting checks passed.");
