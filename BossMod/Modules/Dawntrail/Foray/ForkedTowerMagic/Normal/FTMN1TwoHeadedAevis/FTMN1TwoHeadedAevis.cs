// 魔之塔（The Forked Tower: Magic，国服"两岐塔 魔之塔"）Normal 第 1 战：双头怪鸟（Two-Headed Aevis）。
// 场地中心 (-900, 700)、boss 模型 0x4C11（BNpcName 14489）等实体数据来自 2026-08-06 国服回放实测
// （ZoneID 1346 新月岛北部）。OID/AID/SID 枚举由 The Combat Reborn Team (LTS) 数据导入生成。
namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Normal.FTMN1TwoHeadedAevis;

[ModuleInfo(BossModuleInfo.Maturity.Dummy, // 开发中屏蔽，机制完成后启用
    StatesType = typeof(TwoHeadedAevisStates),
    ConfigType = null, // 如需要可替换为 typeof(TwoHeadedAevisConfig)
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    StatusIDType = typeof(SID),
    TetherIDType = typeof(TetherID),
    IconIDType = typeof(IconID),
    PrimaryActorOID = (uint)OID.TwoHeadedAevis,
    Contributors = "The Combat Reborn Team (LTS)",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CFC,
    GroupID = 1093u,
    NameID = 14489u,
    SortOrder = 1,
    PlanLevel = 0)]
[SkipLocalsInit]
// 主 boss 0x4C11 为双头怪鸟本体（回放实测）；0x4C12/0x4C13 是绿/蓝头（战斗开始前即在场，为可战斗实体）。
// 回放实测（2026-08-06 三场）：本体 0x4C11 全程不可目标化（tgt=False），默认 CheckPull 永不成立，
// 故 override CheckPull 用绿/蓝头可目标化作为拉怪条件；出战斗端本体 DIE+（HP 归零）触发 DeathPhase 结束，
// 无需改 States、无需改回 PrimaryActorOID（0x4C12 无 DIE+/ACT-，改回反而出战斗不识别）。
// 场地 35×35 方形（半宽 17.5f）：2026-08-06 回放实测四边贴边验证，原 Square(20f) 外扩过多。
public sealed class TwoHeadedAevis(WorldState ws, Actor primary) : BossModule(ws, primary, new(-900f, 700f), new ArenaBoundsSquare(17.5f))
{
    protected override bool CheckPull() => PrimaryActor.InCombat && (PrimaryActor.IsTargetable || IsAnyActorTargetable((uint)OID.GreenHead1) || IsAnyActorTargetable((uint)OID.BlueHead1));
}

// ==================== 组件（形状/时机均来自 2026-08-06 三场回放实测） ====================

// 决战（开战全屏 AoE）：本体 49727 + 双头 49726 同步读条 4.7s，回放确认全屏无落点
sealed class OpeningClash(BossModule module) : Components.RaidwideCast(module, (uint)AID.Ability_DecisiveClash1, "决战：全屏伤害");

// 剧毒吐息：Helper 47617 在场地中心放 R18 大圈（回放实测 loc=中心 (-900,700)，R18>半宽 17.5，四角安全）
sealed class PoisonBreath(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Ability_PoisonBreath, 18f);

// 风暴吐息击退：绿头 Helper 48243 以中心为原点向外击退（回放实测位移约 10.7y，取 11；R30 覆盖全场）
sealed class StormBreath(BossModule module) : Components.SimpleKnockbacks(module, (uint)AID.Ability_1, 11f, stopAtWall: true);

// 雷霜暴风雨：全屏 AoE（本体 47736，双头 47735 同步读条 4.7s）
sealed class ThunderfrostTempest(BossModule module) : Components.RaidwideCast(module, (uint)AID.Ability_ThunderfrostTempest, "雷霜暴风雨：全屏伤害");

// 定时诅咒：全屏 AoE（本体 49723，双头 49722 同步读条 2.7s）
sealed class CursedTimer(BossModule module) : Components.RaidwideCast(module, (uint)AID.Ability_7, "定时诅咒：全屏伤害");

// 双头恐惧三列：Helper 50658 在列中心画南北向 Rect 5x40（回放实测两侧列 x=-915/-885 或中间列 x=-905/-895 交替，顺序随机）
sealed class TwoTerrors(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Ability_TwoTerrors1, new AOEShapeRect(20f, 2.5f));

// 雷簇/冰簇连线：Helper 50697（雷）/50698（冰）在连线处 R15（回放实测与小头 4C14/4C15 施法位置一致，绿雷蓝冰）
sealed class Clusters(BossModule module) : Components.SimpleAOEGroups(module, [(uint)AID.Ability_LightningCluster, (uint)AID.Ability_IceCluster1], 15f);

// 球爆炸：冰球 47707（冰碎）/雷球 47706（放电）在球自身位置 R15（4 角 ±10 分两批，读条 1.7s）
sealed class Orbs(BossModule module) : Components.SimpleAOEGroups(module, [(uint)AID.Ability_HypothermalCombustion, (uint)AID.Ability_Shock], 15f);

// 冰焰凝环-小圈：Helper 50703/50704/50705 在落点 R5（钢铁，先炸 5.7s 读条）
sealed class BlazeFlames(BossModule module) : Components.SimpleAOEGroups(module, [(uint)AID.Ability_Blaze1, (uint)AID.Ability_Blaze3, (uint)AID.Ability_Blaze5], 5f);

// 冰焰凝环-大环：Helper 47660 donut 5-60（月环，延迟 ~6s 后 2.2s 读条，落点中心 5y 内安全，先站小圈外再进圈躲月环）
sealed class BlazeLoop(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Ability_Blazeloop1, new AOEShapeDonut(5f, 60f));

// 魔阵光（终局）：16 个立体魔法阵 4B73 立于场地中心十字线（z=700 行朝南、x=-900 列朝西，各 8 个）发射 Rect 5x60 光束，
// 回放实测覆盖南半+西半，东北 1/4（x>-900 且 z<700）安全；方向用 cast 落点与 Font 位置推导（回放 rotation 为游戏原值，不宜直用）
sealed class ArcaneBeacon(BossModule module) : Components.GenericAOEs(module)
{
    private readonly List<AOEInstance> _aoes = [];
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_aoes);

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != (uint)AID.Ability_ArcaneBeacon)
        {
            return;
        }

        var dir = spell.LocXZ - caster.Position;
        if (dir.LengthSq() < 1f)
        {
            return;
        }

        _aoes.Add(new(new AOEShapeRect(60f, 2.5f), caster.Position, Angle.FromDirection(dir), Module.CastFinishAt(spell), actorID: caster.InstanceID));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.Ability_ArcaneBeacon)
        {
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.Ability_ArcaneBeacon)
        {
            ++NumCasts;
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
        }
    }
}

