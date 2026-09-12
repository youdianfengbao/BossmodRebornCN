namespace BossMod.Dawntrail.BeastChess.BC1Ogre;

// 骨架模块：仅场地吸附 + 敌人显示，机制组件待补
public enum OID : uint
{
    Boss = 0x4B8D, // 奇子·食人魔（HP 12074，槽位 A）
    FaerieFire = 0x4B8E, // 妖火 ×6
    GreaterFaerieFire = 0x4DD4, // 大妖火 ×2
    Fireball = 0x4B8F, // 火球
    Helper = 0x233C,
}

// AID 台账占位（2026-09-12 回放），机制待鉴定
public enum AID : uint
{
    Shared49682 = 49682, // ???: 食人魔/上级恶魔共用
    A50396 = 50396, // ???: 归属与机制待鉴定
    A50398 = 50398, // ???: 归属与机制待鉴定
}

sealed class BC1OgreStates : StateMachineBuilder
{
    public BC1OgreStates(BossModule module) : base(module)
    {
        TrivialPhase();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(BC1OgreStates),
    ConfigType = null,
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Uncategorized,
    GroupType = BossModuleInfo.GroupType.CFC,
    GroupID = 1088u,
    NameID = 14538u, // 奇子·食人魔（回放 ACT+ 提取）
    SortOrder = 1,
    PlanLevel = 0)]
public sealed class BC1Ogre(WorldState ws, Actor primary) : BeastChessModule(ws, primary)
{
    public static readonly uint[] EnemiesOfInterest = [(uint)OID.Boss, (uint)OID.FaerieFire, (uint)OID.GreaterFaerieFire, (uint)OID.Fireball];

    protected override void DrawEnemies(int pcSlot, Actor pc) => Arena.Actors(this, EnemiesOfInterest);
}
