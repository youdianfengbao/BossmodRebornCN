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
// 在盘内怪间轮换，场地中心跟随存活成员的平均位置（19.7y 方形围栏，与组件一致）。
[ModuleInfo(BossModuleInfo.Maturity.Contributed, PrimaryActorOID = 19638u, Contributors = "KanoNoUta", Expansion = BossModuleInfo.Expansion.Dawntrail, Category = BossModuleInfo.Category.Foray, GroupType = BossModuleInfo.GroupType.None, NameID = 14596u, SortOrder = 1)]
public sealed class CrucibleHigh1(WorldState ws, Actor primary) : BossModule(ws, primary, primary.Position, new ArenaBoundsSquare(19.7f))
{
    private const uint DummyOID = 9020; // 辅助体：发出部分命中事件，不属于盘内成员
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
            Arena.Center = new(sumX / count, sumZ / count);
    }

    private static bool IsLivingMember(Actor actor) => actor.Type == ActorType.Enemy
        && actor.OID != DummyOID && !actor.IsDeadOrDestroyed && actor.HPMP.CurHP != 0
        && MemberNameIDs.Contains(actor.NameID);
}
