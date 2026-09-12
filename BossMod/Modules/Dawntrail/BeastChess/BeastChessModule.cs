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
}
