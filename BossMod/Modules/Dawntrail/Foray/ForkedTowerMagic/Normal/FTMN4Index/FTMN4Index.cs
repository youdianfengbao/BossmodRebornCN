// 魔之塔（The Forked Tower: Magic，国服"两岐塔 魔之塔"）Normal 第 4 战：目录（Index）。
// 场地中心 (0, -628)、boss 模型 0x4B5F（BNpcName 14717，国服"目录"/英文 Index）等实体数据来自
// 2026-08-06 国服回放实测（ZoneID 1346 新月岛北部）。OID/AID/SID 枚举由 The Combat Reborn Team (LTS)
// 数据导入生成。
namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Normal.FTMN4Index;

[ModuleInfo(BossModuleInfo.Maturity.Dummy, // 开发中屏蔽，机制完成后启用
    StatesType = typeof(IndexStates),
    ConfigType = null, // 如需要可替换为 typeof(IndexConfig)
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    StatusIDType = typeof(SID),
    TetherIDType = typeof(TetherID),
    IconIDType = typeof(IconID),
    PrimaryActorOID = (uint)OID.Index,
    Contributors = "The Combat Reborn Team (LTS)",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CFC,
    GroupID = 1093u,
    NameID = 14717u, // 目录（Index）的 BNpcName 行；原 14503 为惧死者误用，已修正
    SortOrder = 4,
    PlanLevel = 0)]
[SkipLocalsInit]
// 场地圆形 R28：2026-08-06 回放实测目录瞬移站位 R28 硬证据，原 Square(28f) 改为圆形。
// 异形轮廓待后续用 ReplayAnalysis 凹包工具补 Custom 边界。
public sealed class Index(WorldState ws, Actor primary) : BossModule(ws, primary, new(0f, -628f), new ArenaBoundsCircle(28f));
