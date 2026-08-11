// 魔之塔（The Forked Tower: Magic，国服"两岐塔 魔之塔"）Normal 第 3 战：惧死者（Necrophobia）。
// 场地中心 (100, 800)、boss 模型 0x4BE5（BNpcName 14503）等实体数据来自 2026-08-06 国服回放实测
// （ZoneID 1346 新月岛北部）。OID/AID/SID 枚举由 The Combat Reborn Team (LTS) 数据导入生成。
namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Normal.FTMN3Necrophobia;

[ModuleInfo(BossModuleInfo.Maturity.Contributed, // 恢复显示继续测试（2026-08-09）
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
// 场地圆形 R23.7：merge codex 后按用户修正（原 24f 略大）。
public sealed class Necrophobia(WorldState ws, Actor primary) : BossModule(ws, primary, new(100f, 800f), new ArenaBoundsCircle(23.7f));

// ==================== 组件（形状/时机均来自 2026-08-06 三场回放实测） ====================
// 角度说明：读条 rotation（spell.Rotation）即 BossMod 使用的实际朝向（ToDirection 方向 (sinθ, cosθ)，
// 与游戏朝向一致，参照回放验证的 CE214/ReplayValidatedCastAOEs 惯例），所有形状（Rect/Cross/Cone）直接使用、无需换算。
// 真空波组件曾误加 +180° 致危险区画到 boss 背后，2026-08-09 已修正为直接用读条朝向。
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

// 古代冰封：本体读条 4.7s 的十字（半臂 45、半宽 7.5、全长 90，实测确认十字更大）。对照 ACT（2026-08-07）：B96D/B96A omen=Rect2 Scale=7.5,45,1 cross（原 45 为全长，现半臂 45 全长 90，以国服实测为准）
sealed class AncientBlizzardIII(BossModule module) : Components.SimpleAOEs(module, (uint)AID.AncientBlizzardIII, new AOEShapeCross(45f, 7.5f));

// 碎尸：死刑（读条 4.7s，回放实测目标为 MT 且带 218 号锁定图标）
sealed class CorpseMangler(BossModule module) : Components.SingleTargetCast(module, (uint)AID.CorpseMangler, "碎尸：死刑");

// 古代暴雷：boss 读条 47457 引导后，Helper 47458 在 boss 处放 4 个 45° 扇形（Fan45 R60，间隔 90°）。对照 ACT（2026-08-07）：B96F omen=Fan45 Scale=60,60,1（半角 22.5°，t=5.2）
sealed class AncientThunderIII(BossModule module) : Components.SimpleAOEs(module, (uint)AID.AncientThunderIII1, new AOEShapeCone(60f, 22.5f.Degrees()));

// 屏障头暴雷：黑暗奔流第二轮同步，Helper 47471 放 8 个 45° 扇形（Fan45 R60；东北据点 4 个 + 正南据点 4 个，读条 5.2s）。对照 ACT（2026-08-07）：C4B5 omen=Fan45 Scale=60,60,1
sealed class SeveringHeadThunder(BossModule module) : Components.SimpleAOEs(module, (uint)AID.AncientThunderIII3, new AOEShapeCone(60f, 22.5f.Degrees()));

// 魔具联动：爆炎（本体 47465 R18 钢铁 + 屏障头 47468 R18，同步读条 5.2s）。对照 ACT（2026-08-07）：B969 omen=Circle Scale=18,18,1（t=5.2）
sealed class SeveredFire(BossModule module) : Components.SimpleAOEGroups(module, [(uint)AID.SeveredFireIII, (uint)AID.AncientFireIII1], 18f);

// 魔具联动：冰封（本体 47466 十字 + 屏障头 47469 十字，同步读条 5.2s，半臂 45、半宽 7.5、全长 90，实测确认十字更大）。对照 ACT（2026-08-07）：B96A omen=Rect2 Scale=7.5,45,1 cross（原 45 为全长，现半臂 45 全长 90，以国服实测为准）
sealed class SeveredBlizzard(BossModule module) : Components.SimpleAOEGroups(module, [(uint)AID.SeveredBlizzardIII, (uint)AID.AncientBlizzardIII1], new AOEShapeCross(45f, 7.5f));

// 魔具联动：暴雷（本体 47467 读条 + 4 个 Helper 50357 在 boss 位置放 4 个 R60 45° 扇形、rotation 间隔 90°，同步读条 5.2s；
// 屏障头 47471 扇形由 SeveringHeadThunder 处理。2026-08-11 回放实测：4 Helper 同毫秒读条、CastLocation=boss 位置，玩家受击确认）
sealed class SeveredThunder(BossModule module) : Components.SimpleAOEs(module, (uint)AID.SeveredThunder, new AOEShapeCone(60f, 22.5f.Degrees()), maxCasts: 4);

// 灭亡射线：8 个屏障头同时读条 4.7s，各自发射 Rect 3x30（range 30 width 6：向前 30、半宽 3、向后 0，从屏障头位置沿朝向延伸 30 米）。对照 ACT（2026-08-07）：B973 omen=Rect Scale=3,30,1（t=4.7，总长 30 宽 6 吻合）
sealed class DeathlyRay(BossModule module) : Components.SimpleAOEs(module, (uint)AID.DeathlyRay, new AOEShapeRect(30f, 3f, 0f));

// 真空波：本体读条 3.7s 的 180° 扇形（R30），朝面前方向覆盖半场，需站 boss 背后躲避；
// AI 避让由 GenericAOEs 基类 AddAIHints 按 Risky 项自动生成扇形禁区（原 boss 背后站位 Goal 绿圈已删除，2026-08-09）
sealed class VacuumWave(BossModule module) : Components.GenericAOEs(module, (uint)AID.VacuumWave, "真空波：站 boss 背后！")
{
    private static readonly AOEShapeCone _shape = new(30f, 90f.Degrees());
    private readonly List<AOEInstance> _aoes = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_aoes);

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction)
        {
            // 扇形以施法者（boss）为中心，危险区为 boss 面向方向的 180° 半圆（安全区在 boss 背后）；
            // 读条 rotation 即 boss 实际面向方向，直接使用（曾误加 +180° 致画到背后，已修正）
            _aoes.Add(new(_shape, caster.Position, spell.Rotation, Module.CastFinishAt(spell), actorID: caster.InstanceID));
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

}

// 黑暗奔流：本体引导 47476/47479（3.9s）与 47477 第一段（5.2s）同步开始；
// 第一段 Rect(60x10)（forward 60/back 0）：47477 由 Helper 施放，CastLocation 在 boss 前方 30（沿游戏面向方向），
// spell.Rotation 直接用 = 游戏面向反方向（BossMod 角度 0=南 vs 游戏 0=北，差 180°），二者抵消后实际覆盖
// = boss 位置前后各 30 米（长 60 宽 10，穿过整个 R24 场地）。
// 随后 47478 步进对 Rect(10x60) 沿垂直方向 ±10→±20 推进（每 ~2.1s 一对，0.7s 快读条；实测落点 ±10/±20，2026-08-09 回放确认）。
// 分阶段显示：P0（47477 读条预警期）只显示第一段（高危 Danger）+ ±10 预测对（普通 AOE 色、不参与 AI）；
// P1（47477 结算，OnCastFinished 触发）±10 转高危、添加 ±20 预测对（普通）；P2（第一批 47478 读条结束，OnCastFinished 事件触发）±20 转高危。
// 任何时刻 AI 只看到当前即将生效的一组矩形（GenericAOEs.AddAIHints 只对 Risky 项加禁区）。
// 47478 由 Helper 施放，omen 矩形中心 = Helper 位置（= 预测 origin，误差 <0.02m）；spell.LocXZ（CastLocation）
// 是 Helper 沿自身面向朝 boss 前移 5m 的落点、不是矩形中心，故用 caster.Position 匹配/绘制（0.5m 容差替换 100% 成功），
// 替换项保持当前阶段颜色/风险；未匹配（兜底）则按当前阶段新增。
sealed class DarkCurrent(BossModule module) : Components.GenericAOEs(module)
{
    private enum Phase { P0, P1, P2 }

    private static readonly AOEShapeRect _shapeFirst = new(60f, 5f, 0f); // 第一段：长 60 宽 10（覆盖 boss 前后各 30）
    private static readonly AOEShapeRect _shapeStep = new(5f, 30f, 5f); // 步进：长 10 宽 60（中心在施法点）
    private readonly List<AOEInstance> _aoes = [];
    private Phase _phase;
    private Angle _perp;
    private WPos _center;
    private DateTime _t1;
    private DateTime _t2;

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
            _phase = Phase.P0;
            _aoes.Clear();
            // 第一段矩形：高危预警（深黄 Danger），读条期间 AI 只看它
            _aoes.Add(new(_shapeFirst, spell.LocXZ, spell.Rotation, Module.CastFinishAt(spell), Colors.Danger, actorID: caster.InstanceID));
            // 预测步进对：垂直方向 ±10（t1）与 ±20（t2），实测落点 ±10/±20；时间相对 47477 结束 +1.2s/+3.3s
            _perp = spell.Rotation + 90f.Degrees();
            _center = Module.PrimaryActor.Position;
            _t1 = Module.CastFinishAt(spell, 1.2d);
            _t2 = Module.CastFinishAt(spell, 3.3d);
            var perpDir = _perp.ToDirection();
            // P0 只添加 ±10 预测对（普通 AOE 色、不参与 AI）；±20 到 P1 才添加
            _aoes.Add(new(_shapeStep, _center + perpDir * 10f, _perp, _t1, risky: false));
            _aoes.Add(new(_shapeStep, _center - perpDir * 10f, _perp, _t1, risky: false));
        }
        else if (spell.Action.ID == (uint)AID.DarkCurrent2) // 47478 步进（实测替换预测）
        {
            // 47478 由 Helper 施放，omen 矩形中心 = Helper 位置（= 预测 origin，误差 <0.02m）；
            // spell.LocXZ（CastLocation）是 Helper 沿自身面向朝 boss 前移 5m 的落点、不是矩形中心，故用 caster.Position 匹配/绘制
            var wasRisky = false;
            var wasColor = default(uint);
            var idx = _aoes.FindIndex(a => a.Shape == _shapeStep && a.Origin.AlmostEqual(caster.Position, 0.5f));
            if (idx >= 0)
            {
                // 匹配到预测项：替换并保持该项当前阶段的状态（颜色/风险）
                wasRisky = _aoes[idx].Risky;
                wasColor = _aoes[idx].Color;
                _aoes.RemoveAt(idx);
            }
            else
            {
                // 未匹配（兜底）：按当前阶段设定（P2 起高危）
                wasRisky = _phase >= Phase.P2;
                wasColor = wasRisky ? Colors.Danger : default;
            }
            _aoes.Add(new(_shapeStep, caster.Position, spell.Rotation, Module.CastFinishAt(spell), color: wasColor, risky: wasRisky, actorID: caster.InstanceID));
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.DarkCurrent1)
        {
            if (_phase == Phase.P0)
            {
                // P0 → P1：显式移除已结算的第一段矩形；±10 预测对转高危；添加 ±20 预测对（普通）
                _phase = Phase.P1;
                _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
                var len = _aoes.Count;
                for (var i = 0; i < len; ++i)
                {
                    if (_aoes[i].Shape == _shapeStep)
                    {
                        var a = _aoes[i];
                        a.Risky = true;
                        a.Color = Colors.Danger;
                        _aoes[i] = a;
                    }
                }
                var perpDir = _perp.ToDirection();
                _aoes.Add(new(_shapeStep, _center + perpDir * 20f, _perp, _t2, risky: false));
                _aoes.Add(new(_shapeStep, _center - perpDir * 20f, _perp, _t2, risky: false));
            }
            else
            {
                _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
            }
        }
        else if (spell.Action.ID == (uint)AID.DarkCurrent2)
        {
            if (_phase == Phase.P1)
            {
                // 第一批（±10）47478 读条结束 → P2：未到期的 ±20 预测转高危（±10 已结算 activation 已过，不转）
                _phase = Phase.P2;
                var time = WorldState.CurrentTime;
                var len = _aoes.Count;
                for (var i = 0; i < len; ++i)
                {
                    if (_aoes[i].Shape == _shapeStep && _aoes[i].Activation > time)
                    {
                        var a = _aoes[i];
                        a.Risky = true;
                        a.Color = Colors.Danger;
                        _aoes[i] = a;
                    }
                }
            }
            // 第二批（±20）47478 结束：_phase 已为 P2，仅移除实测项
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

// 场地中心弱引导：AI 尽量靠近场地中心（半径 15，权重 0.1，不强制）
sealed class CenterGoal(BossModule module) : BossComponent(module)
{
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        hints.GoalZones.Add(AIHints.GoalSingleTarget(Module.Arena.Center, 15f, 0.1f));
    }
}
