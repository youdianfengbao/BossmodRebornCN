namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Normal.FTMN3Necrophobia;

// OID/AID/SID 由 LTS 数据导入生成；注释按 2026-08-06 三场国服回放实测修正/补充。
public enum OID : uint
{
    Necrophobia = 0x4BE5, // R5.001, x1：惧死者本体（PrimaryActor，可目标化，HP 归零出战斗）
    Necrophobia2 = 0x4BE9, // R1.000, x1：场地投影实体（回放中 47454 重复播放特效，无实际威胁）
    NecrophobiaHelper = 0x233C, // R0.500, x25, Helper type：核爆雨/黑暗奔流/古代暴雷等效果载体

    SeveringHead = 0x4BE6, // R1.410, x8：屏障头（回放实测 8 个，随魔具联动在 4 个据点间移动：东北/东南/西南 (R17)、正北 (R17)；灭亡射线前于场地中心南北列成列）

    Actor1e8fb8 = 0x1E8FB8, // R2.000, x2, EventObj type
    Actor1e8f2f = 0x1E8F2F, // R0.500, x1, EventObj type
    Actor1ebfaa = 0x1EBFAA, // R0.500, x0 (spawn during fight), EventObj type
}

public enum AID : uint
{
    AutoAttack = 47451, // Necrophobia->player, no cast, single-target：平砍

    HailOfHellflares = 47452, // Necrophobia->self, 4.7s cast, 全屏伤害（回放：开战/中盘/收尾共 3 次）
    HailOfHellflares1 = 47453, // NecrophobiaHelper->self, no cast：核爆雨特效
    HailOfHellflares2 = 48956, // NecrophobiaHelper->self, no cast：核爆雨特效
    HailOfHellflares3 = 48957, // NecrophobiaHelper->self, no cast：核爆雨特效

    AncientFireIII = 47455, // Necrophobia->self, 4.7s cast, range 18 circle：古代爆炎（钢铁）
    AncientFireIII1 = 47468, // SeveringHead->self, 5.2s cast, range 18 circle：屏障头版（与魔具联动同步）

    AncientBlizzardIII = 47456, // Necrophobia->self, 4.7s cast, range 45 width 15 cross：古代冰封（十字）
    AncientBlizzardIII1 = 47469, // SeveringHead->self, 5.2s cast, range 45 width 15 cross：屏障头版（与魔具联动同步）

    AncientThunderIII = 47457, // Necrophobia->self, 3.9s cast：古代暴雷引导（无伤害，伤害来自 Helper 扇形）
    AncientThunderIII1 = 47458, // NecrophobiaHelper->self, 4.7s cast, range 60 45.000-degree cone：4 个扇形（间隔 90°）
    AncientThunderIII2 = 47470, // SeveringHead->self, 4.4s cast：屏障头暴雷引导（无伤害，伤害来自 Helper 扇形）
    AncientThunderIII3 = 47471, // NecrophobiaHelper->self, 5.2s cast, range 60 45.000-degree cone：8 个扇形（东北据点 4 个 + 正南据点 4 个）

    Capitation = 47460, // Necrophobia->self, no cast：魔具召唤（回放 CST! 实名为"魔具召唤"，召唤屏障头机制标记）
    CorpseMangler = 47459, // Necrophobia->player, 4.7s cast：碎尸（死刑，目标为 MT）
    DeathShroud = 47461, // Necrophobia->self, 6.7s cast：魔力注入（机制标记，屏障头移动/就位）
    HeadsRoll = 47463, // Necrophobia->self, 2.7s cast：魔具展开（机制标记）
    HeadsRoll1 = 47474, // Necrophobia->self, no cast：魔具展开（特效）

    SeveredFireIII = 47465, // Necrophobia->self, 5.2s cast, range 18 circle：魔具联动：爆炎（钢铁，与屏障头 47468 同步）
    SeveredBlizzardIII = 47466, // Necrophobia->self, 5.2s cast, range 45 width 15 cross：魔具联动：冰封（十字，与屏障头 47469 同步）
    SeveredThunderIII = 47467, // Necrophobia->self, 4.4s cast：魔具联动：暴雷（本体读条，无直接伤害；伤害来自 Helper 50357 四扇形，2026-08-11 回放实测）
    SeveredThunder = 50357, // NecrophobiaHelper->self, 5.2s cast, range 60 45.000-degree cone：魔具联动：暴雷（4 个 Helper 同毫秒读条、均在 boss 位置，rotation 间隔 90° 四方向，伤害本体；与本体 47467 同步，2026-08-11 回放实测）

    DarkCurrent = 47476, // Necrophobia->self, 3.9s cast：黑暗奔流引导（无伤害）
    DarkCurrent1 = 47477, // NecrophobiaHelper->self, 5.2s cast, range 60 width 10 rect：黑暗奔流第一段（中心在 boss 前方 30）
    DarkCurrent2 = 47478, // NecrophobiaHelper->self, 0.7s cast, range 10 width 60 rect：黑暗奔流步进（一对，垂直方向 ±5→±15，每 ~2.1s）
    SeveredDarkCurrent = 47479, // Necrophobia->self, 3.9s cast：魔具联动：黑暗奔流引导（与 47477/47478 同模式）

    DeathlyRay = 47475, // SeveringHead->self, 4.7s cast, range 30 width 6 rect：灭亡射线（8 个头同时）

    VacuumWave = 47473, // Necrophobia->self, 3.7s cast, range 30 180.000-degree cone：真空波（朝面前 180°，需站 boss 背后）

    UnknownAbility1 = 47454, // Necrophobia2->self, no cast：场景特效
    UnknownAbility2 = 47450, // Necrophobia->location, no cast：场景特效
    UnknownAbility3 = 47462, // SeveringHead->location, no cast：屏障头就位/移动标记
    UnknownAbility4 = 47464, // SeveringHead->location, no cast：屏障头展开标记
    UnknownAbility5 = 47472, // SeveringHead->location, no cast：屏障头收束标记
}
public enum SID : uint
{
    Invincibility = 1570, // none->player, extra=0x0：无敌（屏障头相关）
    UnknownStatus1 = 2552, // none->Necrophobia, extra=0x45A/0x45B/0x45C：本体状态（魔具联动标记）
    VulnerabilityUp = 2347, // Necrophobia/SeveringHead/NecrophobiaHelper->player, extra=0x1/0x2/0x3：受伤加重
    UnknownStatus2 = 4956, // none->SeveringHead, extra=0x2C4：屏障头状态
    UnknownStatus3 = 3558, // none->SeveringHead, extra=0x47C/0x47D/0x47E：屏障头状态（就位标记）

}
public enum IconID : uint
{
    Icon_tank_lockon02k1 = 218, // player->self：死刑锁定（碎尸）
}
public enum TetherID : uint
{
    Tether_chn_m0475_mr_c0x = 400, // SeveringHead->Necrophobia：屏障头连线（魔具联动标记）
    Tether_chn_m0475_mr_c1x = 401, // SeveringHead->Necrophobia：屏障头连线（魔具联动标记）
    Tether_chn_m0475_mr_c2x = 402, // SeveringHead->Necrophobia：屏障头连线（魔具联动标记）
}
