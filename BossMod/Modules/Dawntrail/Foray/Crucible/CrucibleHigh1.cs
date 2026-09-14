namespace BossMod.Dawntrail.Foray.Crucible;

// 每组一个实例：HighCrucibleCastAOEs.ConfigFor 按 (Battle, actionID) 精确匹配，
// 非本组的 action 直接忽略，因此多个实例可整盘共存、互不干扰；
// 风险窗口只有 B22/B25/B29 收紧到 0.5 秒（见 HighCrucibleCastAOEs）。
sealed class Wave22CastAOEs(BossModule module) : HighCrucibleCastAOEs(module, Battle.B22);
sealed class Wave23CastAOEs(BossModule module) : HighCrucibleCastAOEs(module, Battle.B23);
sealed class Wave24CastAOEs(BossModule module) : HighCrucibleCastAOEs(module, Battle.B24);
sealed class Wave25CastAOEs(BossModule module) : HighCrucibleCastAOEs(module, Battle.B25);
sealed class Wave26CastAOEs(BossModule module) : HighCrucibleCastAOEs(module, Battle.B26);
sealed class Wave28CastAOEs(BossModule module) : HighCrucibleCastAOEs(module, Battle.B28);
sealed class Wave30CastAOEs(BossModule module) : HighCrucibleCastAOEs(module, Battle.B30);
sealed class Wave31CastAOEs(BossModule module) : HighCrucibleCastAOEs(module, Battle.B31);

sealed class CrucibleHigh1States : StateMachineBuilder
{
    public CrucibleHigh1States(CrucibleHigh1 module) : base(module)
    {
        SimplePhase(0, id => SimpleState(id, 10000f, "狂暴"), "盘结束")
            .ActivateOnEnter<Wave22CastAOEs>()
            .ActivateOnEnter<Wave23CastAOEs>()
            .ActivateOnEnter<Wave24CastAOEs>()
            .ActivateOnEnter<Wave25CastAOEs>()
            .ActivateOnEnter<Wave26CastAOEs>()
            .ActivateOnEnter<Wave28CastAOEs>()
            .ActivateOnEnter<Wave30CastAOEs>()
            .ActivateOnEnter<Wave31CastAOEs>()
            .ActivateOnEnter<CrucibleRottenStench>()
            .ActivateOnEnter<CrucibleMiasma>()
            .ActivateOnEnter<CrucibleRockRain>()
            .ActivateOnEnter<CrucibleRockWalls>()
            .ActivateOnEnter<CrucibleGargoyleKnockback>()
            .ActivateOnEnter<CrucibleGolemKnockback>()
            .ActivateOnEnter<CrucibleDragonPoison>()
            .ActivateOnEnter<CrucibleDragonLanding>()
            .ActivateOnEnter<CrucibleOwlGust>()
            .ActivateOnEnter<CrucibleOwlGustHint>()
            .ActivateOnEnter<CrucibleOwlGustPriority>()
            .ActivateOnEnter<CrucibleOwlChapterHint>()
            .ActivateOnEnter<CrucibleFlowerTrapHint>()
            .ActivateOnEnter<CrucibleGargoyleSlashHint>()
            .ActivateOnEnter<CrucibleGargoyleRingHint>()
            .ActivateOnEnter<CrucibleGargoyleKnockbackHint>()
            .ActivateOnEnter<CrucibleGolemKnockbackHint>()
            .ActivateOnEnter<CrucibleGolemSelfDestructHint>()
            .ActivateOnEnter<CrucibleDragonBreathHint>()
            .ActivateOnEnter<CrucibleDragonLandingHint>()
            .ActivateOnEnter<CrucibleDragonBaits>()
            // 以下组件对照 Kano 7.5.6.1 二进制补齐（实现见 CrucibleHigh1Enhancements.cs）。
            .ActivateOnEnter<CrucibleOwlTetherHint>()
            .ActivateOnEnter<CrucibleOwlMagicUpHint>()
            .ActivateOnEnter<CrucibleFlowerSpinningCones>()
            .ActivateOnEnter<CrucibleFlowerKnockback>()
            .ActivateOnEnter<CrucibleFlowerVoidzone>()
            .ActivateOnEnter<CrucibleSlimeTargetHint>()
            .ActivateOnEnter<CrucibleFlowerHeavyHint>()
            .ActivateOnEnter<CrucibleFlowerKnockbackHint>()
            .ActivateOnEnter<CrucibleFlowerParalysisHint>()
            .ActivateOnEnter<CrucibleFlowerPoisonHint>()
            .ActivateOnEnter<CrucibleFlowerTetherHint>()
            .ActivateOnEnter<CrucibleDevourHint>()
            .ActivateOnEnter<CrucibleDragonVoidzone>()
            .ActivateOnEnter<CrucibleGargoyleSteelDonut>()
            .ActivateOnEnter<CrucibleGargoyleDoubleCone>()
            .ActivateOnEnter<CrucibleGargoyleRushTether>()
            .ActivateOnEnter<CrucibleMinotaurChaseDonut>()
            .ActivateOnEnter<CrucibleBombFieldAOEs>()
            .ActivateOnEnter<CrucibleBombCharge>()
            .ActivateOnEnter<CrucibleBombVoidzone>()
            .ActivateOnEnter<CrucibleGrenadeBoomHint>()
            .ActivateOnEnter<CrucibleIceSpikesHint>()
            .ActivateOnEnter<CrucibleDragonPoisonTrace>()
            .ActivateOnEnter<CrucibleToxicSpikeVoidzone>()
            .ActivateOnEnter<CrucibleDeadlyPoisonHint>()
            .Raw.Update = () => !module.HasLivingMembers;
    }
}

// 高段第一盘（weekly board 1，2026-09-10/11 录制，groups 22/23/24/25/26/28/30/31）。
// 整盘连续多组怪共用一个模块：以 BNpcName 识别盘内成员，PrimaryActor 随当前波次
// 在盘内怪间轮换。场地解析对照 Kano 7.5.6.1 RecordedCrucibleModule（通道 A/B）：
// 先按 PrimaryActor 位置匹配静态场地注册表，命中即冻结 Center/Bounds（本盘命中
// 斗兽圆场 (920,-420) R19.75，Bounds 内缩 0.7 → R19.05，与 B31 注释一致）；miss
// 才用活成员平均位置且只计算一次。构造时的 19.7 方形仅作注册表命中前的占位。
[ModuleInfo(BossModuleInfo.Maturity.Contributed, PrimaryActorOID = 19638u, Contributors = "KanoNoUta", Expansion = BossModuleInfo.Expansion.Dawntrail, Category = BossModuleInfo.Category.Foray, GroupType = BossModuleInfo.GroupType.None, NameID = 14596u, SortOrder = 1)]
public sealed class CrucibleHigh1(WorldState ws, Actor primary) : BossModule(ws, primary, primary.Position, new ArenaBoundsSquare(19.7f))
{
    private const uint DummyOID = 9020; // 辅助体：发出部分命中事件，不属于盘内成员
    private const uint ExpectedNameID = 14596u; // 与 ModuleInfo.NameID 一致，供注册表 NameID 特例匹配
    // 盘内全部 BNpcName（与 CrucibleOIDResolver 的 XBMB22/23/24/25/26/28/30/31 映射一致）。
    private static readonly HashSet<uint> MemberNameIDs = [
        14596, // B22 奇子·博学林鸮
        14599, 14600, 14601, 14602, // B23 奇子·魔界花 / 大口花 / 腐汁 / 幼苗
        14603, 14605, // B24 奇子·尸生花 / 女王鹰蜂
        14606, 14607, // B25 奇子·冰龙 / 冰元精
        14608, // B26 奇子·石像鬼
        14617, // B28 奇子·牛头魔
        14623, 14624, 14625, 14626, 14627, // B30 爆弹家族 / 焰球
        14628, 14629, // B31 怨毒龙 博尔格尼 / 有毒物质
    ];

    public bool HasLivingMembers
    {
        get
        {
            foreach (var actor in WorldState.Actors)
                if (IsLivingMember(actor))
                    return true;
            return false;
        }
    }

    protected override bool CheckPull()
    {
        foreach (var actor in WorldState.Actors)
            if (MemberNameIDs.Contains(actor.NameID) && actor.IsTargetable && actor.InCombat)
                return true;
        return false;
    }

    public override bool CheckReset()
    {
        foreach (var actor in WorldState.Actors)
            if (MemberNameIDs.Contains(actor.NameID) && actor.InCombat)
                return false;
        return true;
    }

    private bool _arenaFrozen; // 场地只解析一次：命中注册表或算出均位后不再变（对照 DLL _frozen 字段）
    private CrucibleArenaEntry? _staticArena; // 命中的静态场地条目，供 AI hints 的寻路边界使用

    protected override void UpdateModule()
    {
        if (!IsLivingMember(PrimaryActor))
        {
            foreach (var actor in WorldState.Actors)
                if (IsLivingMember(actor))
                {
                    PrimaryActor = actor;
                    break;
                }
        }
        if (_arenaFrozen)
            return;

        // 通道 A：先按 PrimaryActor 位置 + NameID 匹配静态场地注册表，命中即冻结。
        _staticArena = CrucibleArenaRegistry.Find(PrimaryActor.Position, ExpectedNameID);
        if (_staticArena is { } entry)
        {
            Arena.Center = entry.Center;
            Arena.Bounds = entry.Bounds;
            _arenaFrozen = true;
            return;
        }

        // miss：活成员平均位置，只计算一次即冻结（原实现每帧重算，现对照 DLL 改正）。
        var sumX = 0f;
        var sumZ = 0f;
        var count = 0;
        foreach (var actor in WorldState.Actors)
        {
            if (MemberNameIDs.Contains(actor.NameID) && !actor.IsDeadOrDestroyed)
            {
                sumX += actor.Position.X;
                sumZ += actor.Position.Z;
                ++count;
            }
        }
        if (count > 0)
        {
            Arena.Center = new(sumX / count, sumZ / count);
            _arenaFrozen = true;
        }
    }

    protected override void CalculateModuleAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // 通道 B：BossModule.CalculateAIHints 已默认 PathfindMapBounds = Arena.Bounds（内缩 0.7 后的
        // R19.05）；对照 DLL RecordedCrucibleModule 改设注册表条目的 NavigationBounds（真实围栏
        // R19.75 = halfSize + boundaryWidth）。瘴气布防时优先 Miasma 的导航边界（与 DLL 一致）。
        if (_staticArena is { } entry)
            hints.PathfindMapBounds = entry.NavigationBounds;
        if (FindComponent<CrucibleMiasma>()?.NavigationBounds is { } miasmaBounds)
            hints.PathfindMapBounds = miasmaBounds;
    }

    private static bool IsLivingMember(Actor actor) => actor.Type == ActorType.Enemy
        && actor.OID != DummyOID && !actor.IsDeadOrDestroyed && actor.HPMP.CurHP != 0
        && MemberNameIDs.Contains(actor.NameID);
}

// 静态场地注册表（数据逐项对照 Kano 7.5.6.1 二进制 ArenaRegistry，条目字段原样搬运）。
// DLL 原型：record(WPos center, byte? index, bool circular, float halfWidth, float halfHeight,
//           float boundaryWidth = 3f, float wallMargin = 0f)。
// Index 为原插件的场地编号（本地仅保留元数据）；boundaryWidth>0 时原版会在围栏外沿画电网
// AOE（本盘命中条目为 0，不画），wallMargin 为模块边界相对真实围栏的内缩量。
internal readonly record struct CrucibleArenaEntry(WPos Center, byte? Index, bool Circular, float HalfWidth, float HalfHeight, float BoundaryWidth = 3f, float WallMargin = 0f)
{
    // 模块内部边界：按 wallMargin 内缩（对照 DLL：halfWidth - wallMargin）。
    public ArenaBounds Bounds => Circular
        ? new ArenaBoundsCircle(HalfWidth - WallMargin)
        : new ArenaBoundsRect(HalfWidth - WallMargin, HalfHeight - WallMargin);

    // 寻路边界：真实围栏位置，按 boundaryWidth 外扩（对照 DLL：halfWidth + boundaryWidth）。
    public ArenaBounds NavigationBounds => Circular
        ? new ArenaBoundsCircle(HalfWidth + BoundaryWidth)
        : new ArenaBoundsRect(HalfWidth + BoundaryWidth, HalfHeight + BoundaryWidth);
}

internal static class CrucibleArenaRegistry
{
    // 通用场地表：默认按位置匹配（距离 < 60y 即 LengthSq < 3600 命中第一条）。
    // 前三条 BoundaryWidth=3（原版用来画围栏电网），斗兽两条均为 0（不画）。
    public static readonly CrucibleArenaEntry[] Defaults =
    [
        new(new(120, -420), 0, true, 20, 20),                        // 120 圆场（蛮神系）
        new(new(120, 0), 1, false, 20, 20),                          // 120 方场
        new(new(520, 0), 2, false, 20, 15),                          // 520 方场（扁）
        new(new(520.1f, -420), null, false, 18.7f, 20.2f, 0, 0.7f),  // 低段斗兽方场
        new(new(920, -420), null, true, 19.75f, 19.75f, 0, 0.7f),    // 高段斗兽圆场 ← 本盘命中
    ];

    // 位置匹配（对照 DLL default 分支：FirstOrDefault(e => (pos - e.Center).LengthSq() < 3600f)）。
    public static CrucibleArenaEntry? MatchByPosition(WPos pos)
    {
        foreach (var entry in Defaults)
            if ((pos - entry.Center).LengthSq() < 3600f)
                return entry;
        return null;
    }

    // NameID 特例表（对照 DLL Register 的 switch，其余 NameID 走位置匹配）。
    public static CrucibleArenaEntry? Find(WPos pos, uint nameID) => nameID switch
    {
        14549 => new CrucibleArenaEntry(new(520, 0), 7, false, 20, 15),
        14561 or 14562 => new CrucibleArenaEntry(new(520, -420), null, false, 18, 19, 0, 0.7f),
        14564 or 14565 or 14566 or 14567 or 14568 or 14569 or 14570 or 14571
            or 14580 or 14581 or 14582 => new CrucibleArenaEntry(new(120, 0), 0, false, 20, 20),
        14572 or 14573 or 14574 or 14575 or 14576 or 14577 or 14578 or 14579
            or 14583 or 14584 or 14585 or 14586 => new CrucibleArenaEntry(new(120, -420), 1, true, 20, 20),
        14592 or 14593 or 14594 or 14595 => new CrucibleArenaEntry(new(520, -420), 22, false, 15, 25, 0, 0.6f),
        14618 or 14619 or 14620 or 14621 or 14622 => new CrucibleArenaEntry(new(120, -420), 0, true, 20, 20, 3, 0.3f),
        _ => MatchByPosition(pos),
    };
}
