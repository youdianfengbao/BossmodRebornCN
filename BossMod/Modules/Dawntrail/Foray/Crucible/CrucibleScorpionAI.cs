namespace BossMod.Dawntrail.Foray.Crucible;

// 斗兽盘共享的 AI 避让入口：把已确认的 risky AOE 登记为导航禁区。
// 优先复用组件构造 AOEInstance 时算好的 ShapeDistance，避免每帧重复计算形状距离。
static class CrucibleScorpionAI
{
    public static void Avoid(AIHints hints, in Components.GenericAOEs.AOEInstance aoe)
        => hints.AddForbiddenZone(aoe.ShapeDistance ?? aoe.Shape.Distance(aoe.Origin, aoe.Rotation), aoe.Activation);
}
