// 双头怪鸟战状态机：相位拆分基于 2026-08-06 三场回放实测时序（时长取回放读条实测值）：
// 决战（49727，4.7s）→ 普通循环（剧毒 47615 → 风暴 47614 → 雷霜 47736 → 恐惧 50656/50657 ×2 顺序随机 → 诅咒 49723 → 风暴，约 100s）
// → 召唤阶段 ×3 轮（召唤 47705 → 雷簇 47643 → 雷霜 47736；第 2 轮后冰焰凝环 47655/47656 ×2，第 3 轮前随机插入风暴/剧毒）
// → 终局（魔法阵展开 49717 → 魔阵光 49720 → 剧毒/风暴收尾 → 本体死亡）
namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Normal.FTMN1TwoHeadedAevis;

[SkipLocalsInit]
sealed class TwoHeadedAevisStates : StateMachineBuilder
{
    public TwoHeadedAevisStates(BossModule module) : base(module)
    {
        // 相位 0：决战（开战）
        var p0 = SimplePhase(0x10000, id =>
        {
            Cast(id, (uint)AID.Ability_DecisiveClash1, 4.7f, 4.7f, "决战")
                .ActivateOnEnter<OpeningClash>()
                .ActivateOnEnter<PoisonBreath>()
                .ActivateOnEnter<StormBreath>()
                .ActivateOnEnter<ThunderfrostTempest>()
                .ActivateOnEnter<CursedTimer>()
                .ActivateOnEnter<TwoTerrors>()
                .ActivateOnEnter<Clusters>()
                .ActivateOnEnter<Orbs>()
                .ActivateOnEnter<BlazeFlames>()
                .ActivateOnEnter<BlazeLoop>()
                .ActivateOnEnter<ArcaneBeacon>();
        }, "决战");
        // 决战读条结束 → 切普通循环
        p0.Raw.Update = () => Module.PrimaryActor.CastInfo?.Action.ID != (uint)AID.Ability_DecisiveClash1;

        // 相位 1：普通循环（到第一次召唤 47705 为止）
        var p1 = SimplePhase(0x20000, id =>
        {
            Cast(id, (uint)AID.Ability_13, 6.9f, 6.9f, "剧毒吐息")
                .ActivateOnEnter<OpeningClash>()
                .ActivateOnEnter<PoisonBreath>()
                .ActivateOnEnter<StormBreath>()
                .ActivateOnEnter<ThunderfrostTempest>()
                .ActivateOnEnter<CursedTimer>()
                .ActivateOnEnter<TwoTerrors>()
                .ActivateOnEnter<Clusters>()
                .ActivateOnEnter<Orbs>()
                .ActivateOnEnter<BlazeFlames>()
                .ActivateOnEnter<BlazeLoop>()
                .ActivateOnEnter<ArcaneBeacon>();
            Cast(id + 0x10, (uint)AID.Ability_, 6.9f, 6.9f, "风暴吐息");
            Cast(id + 0x20, (uint)AID.Ability_ThunderfrostTempest, 4.7f, 4.7f, "雷霜暴风雨");
            CastMulti(id + 0x30, [(uint)AID.Ability_5, (uint)AID.Ability_6], 4.7f, 4.7f, "双头恐惧三列"); // 两侧/中间列顺序随机
            CastMulti(id + 0x40, [(uint)AID.Ability_5, (uint)AID.Ability_6], 4.7f, 4.7f, "双头恐惧三列");
            Cast(id + 0x50, (uint)AID.Ability_7, 2.7f, 2.7f, "定时诅咒");
            Cast(id + 0x60, (uint)AID.Ability_, 6.9f, 6.9f, "风暴吐息");
        }, "普通循环");
        // 第一次召唤读条开始 → 切召唤阶段
        p1.Raw.Update = () => Module.PrimaryActor.CastInfo?.Action.ID == (uint)AID.Ability_8;

        // 相位 2：召唤阶段（3 轮：召唤→雷簇→雷霜；第 2 轮后冰焰凝环 ×2；第 3 轮前随机插入风暴/剧毒）
        var p2 = SimplePhase(0x30000, id =>
        {
            Cast(id, (uint)AID.Ability_8, 2.7f, 2.7f, "召唤")
                .ActivateOnEnter<OpeningClash>()
                .ActivateOnEnter<PoisonBreath>()
                .ActivateOnEnter<StormBreath>()
                .ActivateOnEnter<ThunderfrostTempest>()
                .ActivateOnEnter<CursedTimer>()
                .ActivateOnEnter<TwoTerrors>()
                .ActivateOnEnter<Clusters>()
                .ActivateOnEnter<Orbs>()
                .ActivateOnEnter<BlazeFlames>()
                .ActivateOnEnter<BlazeLoop>()
                .ActivateOnEnter<ArcaneBeacon>();
            Cast(id + 0x10, (uint)AID.Ability_9, 7.1f, 7.1f, "雷簇/冰簇连线");
            Cast(id + 0x20, (uint)AID.Ability_ThunderfrostTempest, 4.7f, 4.7f, "雷霜暴风雨");
            Cast(id + 0x30, (uint)AID.Ability_8, 2.7f, 2.7f, "召唤");
            Cast(id + 0x40, (uint)AID.Ability_9, 7.1f, 7.1f, "雷簇/冰簇连线");
            Cast(id + 0x50, (uint)AID.Ability_ThunderfrostTempest, 4.7f, 4.7f, "雷霜暴风雨");
            CastMulti(id + 0x60, [(uint)AID.Ability_10, (uint)AID.Ability_Blazeloop4], 5.0f, 5.0f, "冰焰凝环"); // 本体两读条顺序随机
            CastMulti(id + 0x70, [(uint)AID.Ability_10, (uint)AID.Ability_Blazeloop4], 5.0f, 5.0f, "冰焰凝环");
            CastMulti(id + 0x80, [(uint)AID.Ability_, (uint)AID.Ability_13], 5.7f, 6.9f, "风暴/剧毒"); // 第 3 轮前随机填充
            Cast(id + 0x90, (uint)AID.Ability_8, 2.7f, 2.7f, "召唤");
            Cast(id + 0xA0, (uint)AID.Ability_9, 7.1f, 7.1f, "雷簇/冰簇连线");
            Cast(id + 0xB0, (uint)AID.Ability_ThunderfrostTempest, 4.7f, 4.7f, "雷霜暴风雨");
        }, "召唤阶段");
        // 魔法阵展开读条开始 → 切终局
        p2.Raw.Update = () => Module.PrimaryActor.CastInfo?.Action.ID == (uint)AID.Ability_12;

        // 相位 3：终局（魔法阵展开→魔阵光→收尾→本体死亡）
        DeathPhase(0x40000, id =>
        {
            Cast(id, (uint)AID.Ability_12, 2.7f, 2.7f, "魔法阵展开")
                .ActivateOnEnter<OpeningClash>()
                .ActivateOnEnter<PoisonBreath>()
                .ActivateOnEnter<StormBreath>()
                .ActivateOnEnter<ThunderfrostTempest>()
                .ActivateOnEnter<CursedTimer>()
                .ActivateOnEnter<TwoTerrors>()
                .ActivateOnEnter<Clusters>()
                .ActivateOnEnter<Orbs>()
                .ActivateOnEnter<BlazeFlames>()
                .ActivateOnEnter<BlazeLoop>()
                .ActivateOnEnter<ArcaneBeacon>();
            Condition(id + 0x10, 3.7f, () => Module.Enemies((uint)OID.ArcaneFont).Any(a => a.CastInfo?.Action.ID == (uint)AID.Ability_ArcaneBeacon), "魔阵光");
            CastMulti(id + 0x20, [(uint)AID.Ability_13, (uint)AID.Ability_], 6.9f, 6.9f, "剧毒/风暴收尾");
        });
    }
}
