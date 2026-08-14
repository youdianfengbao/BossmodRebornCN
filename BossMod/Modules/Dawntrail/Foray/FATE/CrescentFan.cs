namespace BossMod.Dawntrail.Foray.FATE.CrescentFan;

public enum OID : uint {
    CrescentFan = 0x4D8D, // 新月风扇 (main target of FATE 2073)
    BigFan = 0x4D8E, // 大风扇 (spawn during fight)
    Helper = 0x233C,
    Pot = 0x4D8C, // 撒娇罐 (protected NPC, shared pot OID 0x47CB also used)
}

public enum AID : uint {
    AutoAttack = 40542, // CrescentFan/BigFan->player, no cast, single-target
    HighPressureTornado = 50221, // 高压龙卷, CrescentFan->self, 2.7s cast, range 15 width 4 rect
    HighPressureTornadoBig = 50222, // 高压龙卷, BigFan->self, 2.7s cast, range 15 width 4 rect
    Tempest = 50223, // 暴风, BigFan->self, 5.7s cast, raidwide (whole arena, no avoid)
}

sealed class HighPressureTornado(BossModule module) : Components.SimpleAOEGroups(module, [(uint)AID.HighPressureTornado, (uint)AID.HighPressureTornadoBig], new AOEShapeRect(15.0f, 2.0f));
sealed class Tempest(BossModule module) : Components.RaidwideCast(module, (uint)AID.Tempest);

[SkipLocalsInit]
sealed class CrescentFanStates : StateMachineBuilder {
    public CrescentFanStates(BossModule module) : base(module) {
        TrivialPhase()
            .ActivateOnEnter<HighPressureTornado>()
            .ActivateOnEnter<Tempest>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(CrescentFanStates),
    ConfigType = null,
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    StatusIDType = null,
    TetherIDType = null,
    IconIDType = null,
    // 主目标改用撒娇罐(4D8C)：它整个 FATE 波次全程存活且从不进入战斗，
    // 若以新月风扇(4D8D)为主目标，小怪全灭会触发 SimpleBossModule.CheckReset(!PrimaryActor.InCombat)
    // 卸载模块，导致 4D8E 大风扇阶段（高压龙卷 50222 / 暴风 50223）无雷达图无预警
    PrimaryActorOID = (uint)OID.Pot,
    Contributors = "KanoNoUta",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.ForayFATE,
    GroupID = 1093u,
    NameID = 2073u,
    SortOrder = 1,
    PlanLevel = 0)]
[SkipLocalsInit]
public sealed class CrescentFan(WorldState ws, Actor primary) : OpenWorldFate(ws, primary)
{
    // 大风扇(4D8E)是否出现过：出现过且当前场景中已不存在 → 判定 FATE 收尾，结束模块
    private bool _bigFanSeen;

    // 撒娇罐从不进入战斗，默认 CheckPull(要求 primary InCombat) 恒为 false；
    // 改为：FATE 2073 激活 + 玩家在 FATE 区域内 + 撒娇罐存在 时激活模块（玩家中途加入同样生效）
    protected override bool CheckPull()
    {
        var fate = WorldState.Client.ActiveFate;
        if (fate.ID != Info?.NameID || fate.Radius <= 0f)
        {
            return false;
        }

        var player = Raid.Player();
        return player != null
            && player.Position.InCircle(new WPos(fate.Center.XZ()), fate.Radius)
            && WorldState.Actors.Any(a => a.OID == (uint)OID.Pot);
    }

    // 生命周期控制：
    // - 大风扇(4D8E)曾出现且现已不存在 → FATE 收尾，卸载模块；
    // - FATE 2073 仍激活且玩家在区域内 → 不重置（即使小怪全灭、撒娇罐脱战），保证大风扇阶段持续有雷达图与技能预警；
    // - 其余情况（FATE 结束/玩家离开区域）走默认判定
    public override bool CheckReset()
    {
        var bigFanExists = WorldState.Actors.Any(a => a.OID == (uint)OID.BigFan);
        _bigFanSeen |= bigFanExists;
        if (_bigFanSeen && !bigFanExists)
        {
            return true;
        }

        var fate = WorldState.Client.ActiveFate;
        if (fate.ID == Info?.NameID && fate.Radius > 0f)
        {
            var player = Raid.Player();
            return player == null || !player.Position.InCircle(new WPos(fate.Center.XZ()), fate.Radius + 10f);
        }

        return base.CheckReset();
    }
}
