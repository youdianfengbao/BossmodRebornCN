using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.Foray.CriticalEngagement.CE209WhatGoesAround;

public enum OID : uint
{
    Boss = 0x4BC1, // R3.0, BNpcName 14512, undead mage
    AncientExplorerSpirit = 0x4BC2, // R1.0, untargetable circle casters
    AncientPirateSpirit = 0x4BC3, // R1.0, untargetable cross casters
    Controller = 0x4C75, // arena controller at (224, -860)
    Helper = 0x233C
}

public enum AID : uint
{
    ArenaControl = 0xB845, // controller->self, no cast, persistent encounter control pulse
    Necromancy = 0xB846, // boss->self, 3.0s cast, summons spirits
    SpiritExplosionCircle = 0xB847, // explorer spirit->self, 2.0s cast, range 8 circle
    SpiritExplosionCross = 0xB848, // pirate spirit->self, 4.0s cast, range 80 width 7 cross
    MarchOfTheDead = 0xB849, // boss->self, 3.0s cast, visual
    GrudgeRelease = 0xB84A, // spirit->self, 5.0s cast, range 50 width 5 rect
    DeployMagicCircle = 0xB84B, // boss->self, 3.0s cast, visual
    GloomCurrent = 0xB84C, // helper->self, 7.0s cast, range 70 width 12 rect, lane centered on the helper
    Gloom = 0xB84D, // boss->self, 5.0s cast, range 50 width 50 rect
    DarkIV = 0xB84E, // boss->self, 5.0s cast, raidwide visual
    DarkIVHit = 0xB84F, // helpers->players, raidwide damage
    AutoAttack = 0xC649
}

// Every avoidable hit in the recording exposes a real cast-start packet from the actor that owns
// the shape. This includes the staggered 8y grid, the large crosses and all three Gloom Current
// lanes, so no position or rotation needs to be inferred from the boss visuals.
sealed class WhatGoesAroundAOEs(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCircle SpiritCircle = new(8f);
    private static readonly AOEShapeCross SpiritCross = new(80f, 3.5f);
    private static readonly AOEShapeRect Grudge = new(50f, 2.5f);
    // Replay hits land up to ~26y on both sides of the casting helper (projections -16.2..+25.6 on
    // the lane axis, lateral offsets within 6y), and the packet rotation points opposite to the
    // visual flow. Draw each lane as a symmetric line through the helper so all three lanes of a
    // wave cover the arena correctly regardless of the packet rotation sign.
    private static readonly AOEShapeRect Current = new(35f, 6f, 35f);
    private static readonly AOEShapeRect Gloom = new(50f, 25f);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);

        // Replay has three distinct B84C helpers in every wave. The lines leave safe pockets near
        // the rim, but forbidden zones alone give the pathfinder no reason to leave a currently
        // safe central pocket early. Prefer the outer two yalms while the triple-line wave is
        // visible; the three actual line shapes still decide which part of the rim is safe.
        var currentVisible = false;
        foreach (ref readonly var aoe in ActiveAOEs(slot, actor))
        {
            if (ReferenceEquals(aoe.Shape, Current))
            {
                currentVisible = true;
                break;
            }
        }
        if (currentVisible)
        {
            var center = Arena.Center;
            hints.GoalZones.Add(position => (position - center).LengthSq() is >= 289f and <= 361f ? 5f : 0f);
        }
    }

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.SpiritExplosionCircle => new(SpiritCircle),
        (uint)AID.SpiritExplosionCross => new(SpiritCross),
        (uint)AID.GrudgeRelease => new(Grudge),
        (uint)AID.GloomCurrent => new(Current),
        (uint)AID.Gloom => new(Gloom),
        _ => null
    };
}

// B84F is split across three helpers. The boss cast is the stable advance warning.
sealed class DarkIV(BossModule module) : Components.RaidwideCast(module, (uint)AID.DarkIV);

// 2026-08-07: 场地边界 21.5y 外的 21~22y 方环带是即死电网（用户实测 AI 出界踩电网死亡后增加）。
// 用 AOEShapeCustom 表示：外方形半宽 22y 减去内方形半宽 21y，即半宽 21~22y 的方环带。
// 永久常驻危险区（activation 为空），risky=true，AI 视为禁区回避并红色显示。
sealed class WhatGoesAroundKillZone(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCustom KillZone = new(
        [new Rectangle(new(224f, -860f), 22f, 22f)],
        [new Rectangle(new(224f, -860f), 21f, 21f)]);
    private static readonly AOEInstance[] KillZoneAOEs = [new(KillZone, new(224f, -860f), risky: true)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => KillZoneAOEs;
}

// 2026-08-03: the upstream ElectricBoundary class (ARR BFD0 deaths ~24.4y) was NOT restored -
// CN in-game observation shows the instakill boundary is a 21y SQUARE (see the module below);
// the 24.5y square + fence overlay only drew dead zone between the fence and the kill boundary.
sealed class WhatGoesAroundStates : StateMachineBuilder
{
    public WhatGoesAroundStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<WhatGoesAroundAOEs>()
            .ActivateOnEnter<DarkIV>()
            .ActivateOnEnter<WhatGoesAroundKillZone>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(WhatGoesAroundStates),
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Contributors = "KanoNoUta",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CriticalEngagement,
    GroupID = 1093u,
    NameID = 57u,
    SortOrder = 8)]
// The instakill boundary is a square of 21y (confirmed by in-game observation; the kill zone is
// square, not circular, center 224,-860). Draw the battle area right up to that boundary; the old
// 24.5y square and its electric fence overlay only showed dead zone between the fence and the kill
// boundary.
// 2026-08-07: arena bounds widened to 21.5f. The 21~22y band right outside the new bounds is a
// permanent forbidden zone (WhatGoesAroundKillZone) - user observed AI deaths from pathing out of
// bounds into the electric fence.
public sealed class WhatGoesAround(WorldState ws, Actor primary) : BossModule(ws, primary, new(224f, -860f), new ArenaBoundsSquare(21.5f));
