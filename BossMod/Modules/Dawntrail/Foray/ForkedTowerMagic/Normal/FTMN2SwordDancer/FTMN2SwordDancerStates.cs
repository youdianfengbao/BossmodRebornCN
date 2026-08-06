// 剑舞者战状态机：骨架阶段仅 TrivialPhase（boss 进入战斗即激活、脱离/死亡即结束），
// 后续按回放（2026-08-06）补充具体机制阶段与组件。
namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Normal.FTMN2SwordDancer;

[SkipLocalsInit]
sealed class SwordDancerStates : StateMachineBuilder
{
    public SwordDancerStates(BossModule module) : base(module)
    {
        TrivialPhase();
    }
}
