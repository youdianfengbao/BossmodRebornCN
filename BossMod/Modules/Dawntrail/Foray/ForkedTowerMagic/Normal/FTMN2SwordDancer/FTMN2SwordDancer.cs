// 魔之塔（The Forked Tower: Magic，国服"两岐塔 魔之塔"）Normal 第 2 战：剑舞者（Sword Dancer）。
// 场地中心 (600, 704)、boss 模型 0x4D76（BNpcName 14820）等实体数据来自 2026-08-06 国服回放实测
// （ZoneID 1346 新月岛北部）。OID/AID/SID 枚举由 The Combat Reborn Team (LTS) 数据导入生成。
namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Normal.FTMN2SwordDancer;

[ModuleInfo(BossModuleInfo.Maturity.Dummy, // 开发中屏蔽，机制完成后启用
    StatesType = typeof(SwordDancerStates),
    ConfigType = null, // 如需要可替换为 typeof(SwordDancerConfig)
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    StatusIDType = typeof(SID),
    TetherIDType = typeof(TetherID),
    IconIDType = null, // 如需要可替换为 typeof(IconID)
    PrimaryActorOID = (uint)OID.SwordDancer,
    Contributors = "The Combat Reborn Team (LTS)",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CFC,
    GroupID = 1093u,
    NameID = 14820u,
    SortOrder = 2,
    PlanLevel = 0)]
[SkipLocalsInit]
// 场地圆形 R24：2026-08-06 回放实测，原 Circle(25f) 外扩 1y，按实测修正。
public sealed class SwordDancer(WorldState ws, Actor primary) : BossModule(ws, primary, new(600f, 704f), new ArenaBoundsCircle(24f));
