// 惧死者战状态机：相位拆分基于 2026-08-06 三场国服回放实测时序（时长取回放读条/间隔实测值）：
// 开战核爆雨 47452 → 古代爆炎 47455 → 古代冰封 47456 → 碎尸 47459 → 魔力注入 47461 → 魔具展开 47463 → 魔具联动爆炎 47465
// → （魔力注入×2 → 魔具展开 → 联动爆炎 47465 → 联动冰封 47466 → 核爆雨）
// → 真空波 47473 → 灭亡射线 47475 ×2（第二波与真空波同步）→ 黑暗奔流 47476 → 古代暴雷 47457
// → （魔力注入×3 → 魔具展开 → 联动黑暗奔流 47479 ×2，第二次同步屏障头暴雷 47471 ×8 → 核爆雨 → 碎尸 → 本体死亡）
// 注：灭亡射线由 8 个屏障头（非本体）施放，CastMulti 只监听本体，故用 Condition 等待任意头读条开始。
namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Normal.FTMN3Necrophobia;

[SkipLocalsInit]
sealed class NecrophobiaStates : StateMachineBuilder
{
    public NecrophobiaStates(BossModule module) : base(module)
    {
        // 相位 1：开战 ~ 第一轮魔具联动爆炎
        var p0 = SimplePhase(0x10000, id =>
        {
            Cast(id, (uint)AID.HailOfHellflares, 4.0f, 4.7f, "核爆雨")
                .ActivateOnEnter<HailOfHellflares>()
                .ActivateOnEnter<AncientFireIII>()
                .ActivateOnEnter<AncientBlizzardIII>()
                .ActivateOnEnter<CorpseMangler>()
                .ActivateOnEnter<AncientThunderIII>()
                .ActivateOnEnter<SeveringHeadThunder>()
                .ActivateOnEnter<SeveredFire>()
                .ActivateOnEnter<SeveredBlizzard>()
                .ActivateOnEnter<DeathlyRay>()
                .ActivateOnEnter<VacuumWave>()
                .ActivateOnEnter<DarkCurrent>()
                .ActivateOnEnter<CenterGoal>();
            CastMulti(id + 0x10, [(uint)AID.AncientFireIII, (uint)AID.AncientBlizzardIII], 10.6f, 4.7f, "古代爆炎/冰封"); // 三场回放顺序随机（爆炎→冰封 或 冰封→爆炎）
            CastMulti(id + 0x20, [(uint)AID.AncientFireIII, (uint)AID.AncientBlizzardIII], 5.0f, 4.7f, "古代爆炎/冰封");
            Cast(id + 0x30, (uint)AID.CorpseMangler, 6.0f, 4.7f, "碎尸（死刑）");
            Cast(id + 0x40, (uint)AID.DeathShroud, 12.8f, 6.7f, "魔力注入");
            Cast(id + 0x50, (uint)AID.HeadsRoll, 2.4f, 2.7f, "魔具展开");
            Cast(id + 0x60, (uint)AID.SeveredFireIII, 8.4f, 5.2f, "魔具联动：爆炎");
        }, "开战循环");
        // 第二次魔力注入读条开始 → 切第二轮魔具
        p0.Raw.Update = () => Module.PrimaryActor.CastInfo?.Action.ID == (uint)AID.DeathShroud;

        // 相位 2：第二轮魔具（联动爆炎 → 联动冰封 → 核爆雨）
        var p1 = SimplePhase(0x20000, id =>
        {
            Cast(id, (uint)AID.DeathShroud, 11.3f, 6.7f, "魔力注入")
                .ActivateOnEnter<HailOfHellflares>()
                .ActivateOnEnter<AncientFireIII>()
                .ActivateOnEnter<AncientBlizzardIII>()
                .ActivateOnEnter<CorpseMangler>()
                .ActivateOnEnter<AncientThunderIII>()
                .ActivateOnEnter<SeveringHeadThunder>()
                .ActivateOnEnter<SeveredFire>()
                .ActivateOnEnter<SeveredBlizzard>()
                .ActivateOnEnter<DeathlyRay>()
                .ActivateOnEnter<VacuumWave>()
                .ActivateOnEnter<DarkCurrent>()
                .ActivateOnEnter<CenterGoal>();
            Cast(id + 0x10, (uint)AID.HeadsRoll, 2.4f, 2.7f, "魔具展开");
            CastMulti(id + 0x20, [(uint)AID.SeveredFireIII, (uint)AID.SeveredBlizzardIII], 8.4f, 5.2f, "魔具联动"); // 三场回放顺序随机（爆炎→冰封 或 冰封→爆炎）
            CastMulti(id + 0x30, [(uint)AID.SeveredFireIII, (uint)AID.SeveredBlizzardIII], 9.1f, 5.2f, "魔具联动");
            Cast(id + 0x40, (uint)AID.HailOfHellflares, 10.6f, 4.7f, "核爆雨");
        }, "第二轮魔具");
        // 真空波读条开始 → 切真空波/射线阶段
        p1.Raw.Update = () => Module.PrimaryActor.CastInfo?.Action.ID == (uint)AID.VacuumWave;

        // 相位 3：真空波 + 灭亡射线（8 头 ×2 波，第二波与第二次真空波同步）
        var p2 = SimplePhase(0x30000, id =>
        {
            Cast(id, (uint)AID.VacuumWave, 14.5f, 3.7f, "真空波")
                .ActivateOnEnter<HailOfHellflares>()
                .ActivateOnEnter<AncientFireIII>()
                .ActivateOnEnter<AncientBlizzardIII>()
                .ActivateOnEnter<CorpseMangler>()
                .ActivateOnEnter<AncientThunderIII>()
                .ActivateOnEnter<SeveringHeadThunder>()
                .ActivateOnEnter<SeveredFire>()
                .ActivateOnEnter<SeveredBlizzard>()
                .ActivateOnEnter<DeathlyRay>()
                .ActivateOnEnter<VacuumWave>()
                .ActivateOnEnter<DarkCurrent>()
                .ActivateOnEnter<CenterGoal>();
            Condition(id + 0x10, 6.9f, () => Module.Enemies((uint)OID.SeveringHead).Any(e => e.CastInfo?.Action.ID == (uint)AID.DeathlyRay), "灭亡射线×8", maxOverdue: 2f);
            Condition(id + 0x20, 9.2f, () => Module.Enemies((uint)OID.SeveringHead).Any(e => e.CastInfo?.Action.ID == (uint)AID.DeathlyRay), "灭亡射线×8", maxOverdue: 2f);
            Cast(id + 0x30, (uint)AID.VacuumWave, 0.9f, 3.7f, "真空波");
        }, "真空波与灭亡射线");
        // 黑暗奔流读条开始 → 切黑暗奔流阶段
        p2.Raw.Update = () => Module.PrimaryActor.CastInfo?.Action.ID == (uint)AID.DarkCurrent;

        // 相位 4：黑暗奔流（第一段 + 步进对）→ 古代暴雷
        var p3 = SimplePhase(0x40000, id =>
        {
            Cast(id, (uint)AID.DarkCurrent, 9.9f, 3.9f, "黑暗奔流")
                .ActivateOnEnter<HailOfHellflares>()
                .ActivateOnEnter<AncientFireIII>()
                .ActivateOnEnter<AncientBlizzardIII>()
                .ActivateOnEnter<CorpseMangler>()
                .ActivateOnEnter<AncientThunderIII>()
                .ActivateOnEnter<SeveringHeadThunder>()
                .ActivateOnEnter<SeveredFire>()
                .ActivateOnEnter<SeveredBlizzard>()
                .ActivateOnEnter<DeathlyRay>()
                .ActivateOnEnter<VacuumWave>()
                .ActivateOnEnter<DarkCurrent>()
                .ActivateOnEnter<CenterGoal>();
            Cast(id + 0x10, (uint)AID.AncientThunderIII, 5.4f, 3.9f, "古代暴雷");
        }, "黑暗奔流");
        // 第三次魔力注入读条开始 → 切最终阶段
        p3.Raw.Update = () => Module.PrimaryActor.CastInfo?.Action.ID == (uint)AID.DeathShroud;

        // 相位 5：最终魔具 + 黑暗奔流第二轮（两次联动黑暗奔流，第二次同步屏障头暴雷 ×8）→ 核爆雨 → 碎尸 → 本体死亡
        DeathPhase(0x50000, id =>
        {
            Cast(id, (uint)AID.DeathShroud, 12.3f, 6.7f, "魔力注入")
                .ActivateOnEnter<HailOfHellflares>()
                .ActivateOnEnter<AncientFireIII>()
                .ActivateOnEnter<AncientBlizzardIII>()
                .ActivateOnEnter<CorpseMangler>()
                .ActivateOnEnter<AncientThunderIII>()
                .ActivateOnEnter<SeveringHeadThunder>()
                .ActivateOnEnter<SeveredFire>()
                .ActivateOnEnter<SeveredBlizzard>()
                .ActivateOnEnter<DeathlyRay>()
                .ActivateOnEnter<VacuumWave>()
                .ActivateOnEnter<DarkCurrent>()
                .ActivateOnEnter<CenterGoal>();
            Cast(id + 0x10, (uint)AID.HeadsRoll, 2.4f, 2.7f, "魔具展开");
            Cast(id + 0x20, (uint)AID.SeveredDarkCurrent, 9.0f, 3.9f, "魔具联动：黑暗奔流"); // 同步屏障头冰封 ×2（组件自行绘制）
            Cast(id + 0x30, (uint)AID.SeveredDarkCurrent, 7.0f, 3.9f, "魔具联动：黑暗奔流"); // 同步屏障头暴雷 ×8
            Cast(id + 0x40, (uint)AID.HailOfHellflares, 7.5f, 4.7f, "核爆雨");
            Condition(id + 0x50, 8.5f, () => Module.PrimaryActor.CastInfo?.Action.ID == (uint)AID.CorpseMangler, "碎尸（死刑）", maxOverdue: 6f); // 第三场回放核爆雨后本体直接死亡，碎尸可选
        });
    }
}
