// 魔之塔（The Forked Tower: Magic，国服"两岐塔 魔之塔"）Normal 第 3 战：惧死者（Necrophobia）。
// 场地中心 (100, 800)、boss 模型 0x4BE5（BNpcName 14503）等实体数据来自 2026-08-06 国服回放实测
// （ZoneID 1346 新月岛北部）。OID/AID/SID 枚举由 The Combat Reborn Team (LTS) 数据导入生成。
namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Normal.FTMN3Necrophobia;

[ModuleInfo(BossModuleInfo.Maturity.Dummy, // 开发中屏蔽，机制完成后启用
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
