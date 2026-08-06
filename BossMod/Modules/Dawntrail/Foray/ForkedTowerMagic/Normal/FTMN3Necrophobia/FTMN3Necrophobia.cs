// 魔之塔（The Forked Tower: Magic，国服"两岐塔 魔之塔"）Normal 第 3 战：惧死者（Necrophobia）。
// 场地中心 (100, 800)、boss 模型 0x4BE5（BNpcName 14503）等实体数据来自 2026-08-06 国服回放实测
// （ZoneID 1346 新月岛北部）。OID/AID/SID 枚举由 The Combat Reborn Team (LTS) 数据导入生成。
namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Normal.FTMN3Necrophobia;

[ModuleInfo(BossModuleInfo.Maturity.Dummy, // boss3 恢复开发隐藏（2026-08-07：boss1 实测通过，boss3 待实测）
    StatesType = typeof(NecrophobiaStates),
    ConfigType = null, // 如需要可替换为 typeof(NecrophobiaConfig)
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    StatusIDType = typeof(SID),
    TetherIDType = typeof(TetherID),
    IconIDType = typeof(IconID),
    PrimaryActorOID = (uint)OID.Necrophobia,
    Contributors = "The Combat Reborn Team (LTS)",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CFC,
    GroupID = 1093u,
    NameID = 14503u,
    SortOrder = 3,
    PlanLevel = 0)]
[SkipLocalsInit]
// 场地圆形 R24：2026-08-06 回放实测确认，与现有定义一致。
public sealed class Necrophobia(WorldState ws, Actor primary) : BossModule(ws, primary, new(100f, 800f), new ArenaBoundsCircle(24f));

// ==================== 组件（形状/时机均来自 2026-08-06 三场回放实测） ====================
// 角度换算说明：游戏内 rotation 0=北、方向向量 (-sinθ,-cosθ)，BossMod 角度 0=南、方向 (sin a, cos a)，
// 二者差 180°。但 Rect/Cross 类形状以施法落点（loc）为对称中心绘制，loc 自带方向偏移，读条 rotation 可直接使用；
// Cone 类形状以施法者为圆心，需对读条 rotation +180° 才与游戏方向一致（与上游 Hunt boss 扇形实现一致）。
//
// 对照 ACT 触发器参数（2026-08-07，魔之塔后半触发器日志"Boss 3 惧死者"简易播报 13 键表）：
// ACT 的 omen Scale 与米制为 1:1 映射（已验证：FTMN4 爱之歌 ACT Scale 15 ↔ BossMod R15、盯准 Scale 11 ↔ R11，
// 以及本模块古代爆炎 Scale 18 ↔ R18、冰封 Rect2 7.5×45 ↔ Cross(22.5,7.5) 均吻合）；
// Circle/Fan 的 Scale=(半径,半径,1)，Rect/Rect2 的 Scale=(半宽, 全长, 1)（cross 表示十字）。
// 下方各组件形状/尺寸均与 ACT 表一致，无需修改，仅补对照说明。

// 核爆雨：全屏伤害（本体 47452 读条 4.7s，回放三场均出现 3 次：开战、中盘、收尾）
sealed class HailOfHellflares(BossModule module) : Components.RaidwideCast(module, (uint)AID.HailOfHellflares, "核爆雨：全屏伤害");

// 古代爆炎：本体读条 4.7s 的钢铁（R18）。对照 ACT（2026-08-07）：B96C/B969 omen=Circle Scale=18,18,1（1:1 = R18，t=5.2）
sealed class AncientFireIII(BossModule module) : Components.SimpleAOEs(module, (uint)AID.AncientFireIII, 18f);

// 古代冰封：本体读条 4.7s 的十字（range 45 width 15 cross：半长 22.5、半宽 7.5）。对照 ACT（2026-08-07）：B96D/B96A omen=Rect2 Scale=7.5,45,1 cross
sealed class AncientBlizzardIII(BossModule module) : Components.SimpleAOEs(module, (uint)AID.AncientBlizzardIII, new AOEShapeCross(22.5f, 7.5f));

// 碎尸：死刑（读条 4.7s，回放实测目标为 MT 且带 218 号锁定图标）
sealed class CorpseMangler(BossModule module) : Components.SingleTargetCast(module, (uint)AID.CorpseMangler, "碎尸：死刑");

// 古代暴雷：boss 读条 47457 引导后，Helper 47458 在 boss 处放 4 个 45° 扇形（Fan45 R60，间隔 90°）。对照 ACT（2026-08-07）：B96F omen=Fan45 Scale=60,60,1（半角 22.5°，t=5.2）
sealed class AncientThunderIII(BossModule module) : Components.SimpleAOEs(module, (uint)AID.AncientThunderIII1, new AOEShapeCone(60f, 22.5f.Degrees()));

// 屏障头暴雷：黑暗奔流第二轮同步，Helper 47471 放 8 个 45° 扇形（Fan45 R60；东北据点 4 个 + 正南据点 4 个，读条 5.2s）。对照 ACT（2026-08-07）：C4B5 omen=Fan45 Scale=60,60,1
sealed class SeveringHeadThunder(BossModule module) : Components.SimpleAOEs(module, (uint)AID.AncientThunderIII3, new AOEShapeCone(60f, 22.5f.Degrees()));

// 魔具联动：爆炎（本体 47465 R18 钢铁 + 屏障头 47468 R18，同步读条 5.2s）。对照 ACT（2026-08-07）：B969 omen=Circle Scale=18,18,1（t=5.2）
sealed class SeveredFire(BossModule module) : Components.SimpleAOEGroups(module, [(uint)AID.SeveredFireIII, (uint)AID.AncientFireIII1], 18f);

// 魔具联动：冰封（本体 47466 十字 + 屏障头 47469 十字，同步读条 5.2s）。对照 ACT（2026-08-07）：B96A omen=Rect2 Scale=7.5,45,1 cross
sealed class SeveredBlizzard(BossModule module) : Components.SimpleAOEGroups(module, [(uint)AID.SeveredBlizzardIII, (uint)AID.AncientBlizzardIII1], new AOEShapeCross(22.5f, 7.5f));

// 灭亡射线：8 个屏障头同时读条 4.7s，各自发射 Rect 3x30（range 30 width 6：半长 15、半宽 3，中心在头位置）。对照 ACT（2026-08-07）：B973 omen=Rect Scale=3,30,1（t=4.7）
sealed class DeathlyRay(BossModule module) : Components.SimpleAOEs(module, (uint)AID.DeathlyRay, new AOEShapeRect(15f, 3f, 15f));

// 真空波：本体读条 3.7s 的 180° 扇形（R30），朝面前方向覆盖半场，需站 boss 背后躲避；
// AI 引导：扇形禁区（避让）+ boss 背后站位 Goal
sealed class VacuumWave(BossModule module) : Components.GenericAOEs(module, (uint)AID.VacuumWave, "真空波：站 boss 背后！")
{
    private static readonly AOEShapeCone _shape = new(30f, 90f.Degrees());
    private readonly List<AOEInstance> _aoes = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_aoes);

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction)
        {
            // 扇形以施法者为中心：读条 rotation（游戏角度 0=北）需 +180° 转 BossMod（0=南）方向
            _aoes.Add(new(_shape, caster.Position, spell.Rotation + 180f.Degrees(), Module.CastFinishAt(spell), actorID: caster.InstanceID));
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction)
        {
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction)
        {
            ++NumCasts;
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints); // 禁区 = 扇形本身（正面即危险区）
        var len = _aoes.Count;
        for (var i = 0; i < len; ++i)
        {
            var aoe = _aoes[i];
            // 引导站位：boss 背后（危险方向的反向），让 AI 就近就位而非贴着禁区边缘
            hints.GoalZones.Add(AIHints.GoalSingleTarget(aoe.Origin - aoe.Rotation.ToDirection() * 8f, 3f, 0.5f));
        }
    }
}

// 黑暗奔流：本体引导 47476/47479（3.9s）与 47477 第一段（5.2s）同步开始；
// 第一段 Rect(60x10) 中心在 boss 前方 30（近端即 boss）；随后 47478 步进对 Rect(10x60)
// 沿垂直方向 ±5→±15 推进（每 ~2.1s 一对，0.7s 快读条）；6s 后古代暴雷（或第二轮同步屏障头暴雷）。
// 组件在 47477 开始时预测步进对（时间对齐回放实测），47478 实测读条到达时以实际落点替换。
sealed class DarkCurrent(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeRect _shapeFirst = new(30f, 5f, 30f); // 第一段：长 60 宽 10（中心在施法点）
    private static readonly AOEShapeRect _shapeStep = new(5f, 30f, 5f); // 步进：长 10 宽 60（中心在施法点）
    private readonly List<AOEInstance> _aoes = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var time = WorldState.CurrentTime;
        _aoes.RemoveAll(a => a.Activation.AddSeconds(0.5d) < time); // 爆炸后清除（含预测对）
        return CollectionsMarshal.AsSpan(_aoes);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.DarkCurrent1) // 47477 第一段
        {
            _aoes.Add(new(_shapeFirst, spell.LocXZ, spell.Rotation, Module.CastFinishAt(spell), actorID: caster.InstanceID));
            // 预测步进对：垂直方向 ±5（第一段结束 +1.2s）与 ±15（+3.3s），时间来自回放实测（47477 生效后 ~1.1s 与 ~3.2s）
            var perp = spell.Rotation + 90f.Degrees();
            var perpDir = perp.ToDirection();
            var center = Module.PrimaryActor.Position;
            var t1 = Module.CastFinishAt(spell, 1.2d);
            var t2 = Module.CastFinishAt(spell, 3.3d);
            _aoes.Add(new(_shapeStep, center + perpDir * 5f, perp, t1));
            _aoes.Add(new(_shapeStep, center - perpDir * 5f, perp, t1));
            _aoes.Add(new(_shapeStep, center + perpDir * 15f, perp, t2));
            _aoes.Add(new(_shapeStep, center - perpDir * 15f, perp, t2));
        }
        else if (spell.Action.ID == (uint)AID.DarkCurrent2) // 47478 步进（实测替换预测）
        {
            _aoes.RemoveAll(a => a.Shape == _shapeStep && a.Origin.AlmostEqual(spell.LocXZ, 0.5f));
            _aoes.Add(new(_shapeStep, spell.LocXZ, spell.Rotation, Module.CastFinishAt(spell), actorID: caster.InstanceID));
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID.DarkCurrent1 or (uint)AID.DarkCurrent2)
        {
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.DarkCurrent1 or (uint)AID.DarkCurrent2)
        {
            ++NumCasts;
        }
    }
}
