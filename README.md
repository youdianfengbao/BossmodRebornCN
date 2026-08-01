# [![](https://raw.githubusercontent.com/FFXIV-CombatReborn/RebornAssets/main/IconAssets/BMR_Icon.png)](https://github.com/KanoNoUta/BossmodRebornCN)

**BossMod Reborn CN**

![Github Latest Releases](https://img.shields.io/github/downloads/KanoNoUta/BossmodRebornCN/latest/total.svg?style=for-the-badge)
![Github License](https://img.shields.io/github/license/KanoNoUta/BossmodRebornCN.svg?label=License&style=for-the-badge)

BossMod Reborn CN 是由 KanoNoUta 维护的国服适配版本，当前对应国服 7.55。插件提供战斗雷达、机制范围、移动提示与职业辅助。

## 国服 7.55 新月岛北岛

- 已适配现有录像覆盖的 CE50–CE63（CE49 提蔛暂无录像，按当前范围暂缓）。
- 新增古术魔典、卡洛菲斯提莉二重身、赤龙与新月阿剌克涅的完整机制模块。
- 已处理高倍速回放重复包、CastInfo 重同步、迟到事件与残留范围清理。
- 惨白魔人的死亡轮盘按实测 5–12m/12–20m 极坐标扇区绘制，并跟随 helper 的实时坐标、朝向与极性换位。
- 阿尔戈尔旋转拉拽、连续半场、圆形击退与横向击退均按 replay/客户端 ActionEffect 的实际时序处理。
- 7.5.5.8 修正雪石膏之剑与宝石兽半场方向、卡洛菲斯提莉左右刀、魔亡灵法师直条和古术魔典圆形场地，并补强阿尔戈尔旋转吸引 AI 与诱拐魔冰花提示。
- 7.5.5.13 Replay 界面中文化；CE205 回转吸引全圈预警与死亡墙边界校准；CE203 分身技能提前预警。
- 7.5.5.12 开启北岛四 FATE 绘制，补齐惨白魔人电网与轮盘预览，修正阿尔戈尔地火/场地、负隅宝石兽与魔亡灵法师方形电网、连续击退、变形法师冲刺与扩散圈，重做禁书知见规则/翻页/墨阵/踩塔。
- 补全统领奇美拉三连吐息、玛琦塔八连挥击与唤雷者 Freefall 三段落点。

第三方插件库：`https://raw.githubusercontent.com/KanoNoUta/DalamudPlugins/main/pluginmaster.json`

## Features

- **Advanced Radar System**: A sophisticated on-screen map displaying player and boss positions, imminent AOEs, and other crucial mechanics. This system helps players visualize the battlefield, simplifying decision-making processes.
- **Mechanic Descriptors**: Near the radar, you’ll find clear, concise descriptions of upcoming mechanics, global hints for resolving current challenges, and personalized player advice to optimize your response to each situation.
- **Cooldown Planner**: A tool for meticulous planning of ability usage, ensuring optimal timing for cooldowns and abilities in coordination with raid strategies.
- **User-Friendly Interface**: The module viewer and configuration interface are designed for quick access during combat preparation.
- **Regular Updates**: Committed to staying current with the latest game patches, class updates, and community feedback. PRs will be reviewed, tested, and approved.

## Contributing

- Create a fork
- Make your changes
- Test the changes
- Create a PR and point it to main
