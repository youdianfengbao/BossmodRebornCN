// 斗兽奇弈按盘组织：Board1~3 = 低段三盘，Board4~5 = 高段两盘。
// 每盘一个子目录 + 子命名空间（保留每模块独立 namespace 以隔离各自的 OID/AID 枚举）；共享基类与槽位表在本文件。
namespace BossMod.Dawntrail.BeastChess;

// 斗兽奇弈（BST，单人 Roguelike 爬塔，ZONE 1339，CFC 1088）共享模块基类。
// 激活方式与 Foray/CriticalEngagement 模块相同：按 boss actor 出现（OID 匹配）自动加载，无 CheckPull/CalculateBounds 覆写。
// 场中心只落在少数几个离散槽位上（槽位间距 400y，boss 活动半径 <15y），构造时按 primary actor 位置吸附最近槽位即可。
// 各槽形状独立：A 槽圆形 R20；B 槽（魅惑女妖场）实证为矩形 24x38；C 槽（上级恶魔场）实证为矩形 40x30（用户 2026-09-12 进本确认）。
public abstract class BeastChessModule(WorldState ws, Actor primary) : BossModule(ws, primary, SlotFor(primary.Position).Center, SlotFor(primary.Position).Bounds)
{
    private static readonly (WPos Center, ArenaBounds Bounds)[] Slots =
    [
        (new WPos(120f, -420f), new ArenaBoundsCircle(20f)), // A 槽：食人魔 / 骑士+主教 / 祸蛛蝎
        (new WPos(520f, -420f), new ArenaBoundsRect(12f, 19f)), // B 槽：魅惑女妖·帕德索，矩形 24x38（用户 2026-09-12 实测场域约 (507,-400)~(533,-440)，边缘有凹凸墙，半宽自 13x20 各缩 1 避墙后按中心对称绘制）
        (new WPos(520f, 0f), new ArenaBoundsRect(20f, 15f)), // C 槽：上级恶魔，矩形 40x30 (x 500-540, z -15..15)
        (new WPos(120f, 0f), new ArenaBoundsSquare(20f)), // D 槽：刺鱼魔（BC6），方 20x20——上游 PiscodemonPiece 同 (120,0)+方 20，Kano 通用场地表一致；参数为上游值未回放验证
    ];

    private static (WPos Center, ArenaBounds Bounds) SlotFor(WPos pos)
    {
        var best = Slots[0];
        var bestDistSq = float.MaxValue;
        foreach (var slot in Slots)
        {
            var distSq = (slot.Center - pos).LengthSq();
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = slot;
            }
        }
        return best;
    }

    // 本场需要全部击杀的敌人 OID 列表（含 primary）；双 boss / 多怪场由派生类列出全部成员
    public abstract uint[] EnemiesOfInterest { get; }

    // 全部敌人（含 primary）死亡或销毁 = 战斗结束
    public bool EnemiesAllDead()
    {
        foreach (var oid in EnemiesOfInterest)
        {
            foreach (var enemy in Enemies(oid))
            {
                if (!enemy.IsDeadOrDestroyed)
                    return false;
            }
        }
        return true;
    }

    // 双 boss 场 primary（如骑士 0x4B86）先死时战斗仍在继续：正常卸载路径由各 States 的
    // TrivialPhase Raw.Update 覆写阻止（默认谓词只查 PrimaryActor 死亡，见 StateMachineBuilder.DeathPhase:160，
    // phase 结束后 BossModuleManager.Update 即按 wasActive 卸载模块）；本覆写作为语义显式化的双保险——
    // primary 已死但场内仍有存活敌人时不允许重置，全部敌人死亡才允许（单 boss 场 primary 死=全灭，行为不变）。
    // 注意：CheckReset 仅在模块 active 时被查询（BossModuleManager.Update），phase 已结束后不起作用。
    public override bool CheckReset() => PrimaryActor.IsDeadOrDestroyed && EnemiesAllDead();
}
