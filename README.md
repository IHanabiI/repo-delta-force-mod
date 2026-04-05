# REPO Delta Force Mod

中文：这是一个为 REPO 制作的哈夫克主题玩法模组，围绕特殊开局事件、军用信息终端、飞行记录仪和航空箱，扩展原版搜刮、搬运与风险取舍体验。  
English: A Havoc-themed gameplay mod for REPO, expanding the base scavenging, hauling, and risk-reward loop with a special opening event, a military terminal, a flight recorder, and an air drop case.

## Origin | 创作缘起

### 中文

这个项目的灵感来自一个视频，视频内容大致是“把三角洲护航摸金体验带进 REPO”。

作为一个同时玩三角洲和 REPO 的玩家，我一直想把三角洲里那种带有事件感、风险感和高回报目标感的特殊摸金体验，以更适合 REPO 的方式做进游戏里，于是就有了这个 mod。

我不是专业开发者，这个项目更像是一个玩家作者边做边学完成的主题化玩法尝试。

### English

This project was inspired by a video built around the idea of bringing a Delta Force-style escort looting experience into REPO.

As a player of both Delta Force and REPO, I wanted to bring that sense of special event-driven looting, risk, and high-value target chasing into REPO in a way that better fits the game, which is how this mod started.

I am not a professional developer. This project is better described as a player-made themed gameplay experiment built while learning along the way.

## Status | 当前状态

- `Playable`
- `Core gameplay implemented`
- `Repository cleaned for public source hosting`

### 中文

- 当前核心玩法已经完成并可正常游玩
- 仓库已经完成一轮公开源码整理
- 后续是否继续长期更新，会根据玩家反馈再决定

### English

- The core gameplay loop is already implemented and playable
- The repository has been cleaned for public source hosting
- Longer-term updates will depend on player feedback and interest

## Gameplay Overview | 玩法概览

### Havoc Opening Event | 哈夫克开局事件

### 中文

当前版本采用事件触发的方式推进玩法。

当哈夫克事件触发时，地图中会投放哈夫克公司的特殊物资，用这套事件来承接模组的主要内容。

### English

The current version uses an event-driven structure.

When the Havoc event triggers, Havoc company supplies are inserted into the map, and that event acts as the main delivery mechanism for the mod's special content.

### Military Terminal | 军用信息终端

中文：军用信息终端是偏工具型物资，强调搜索、扫描与目标指引。  
English: The military terminal is a utility-focused item built around scanning, searching, and target guidance.

### Flight Recorder | 飞行记录仪

中文：飞行记录仪是高价值、高风险的特殊 valuable，强调携带过程中的压力与取舍。  
English: The flight recorder is a high-value, high-risk special valuable that adds pressure and tradeoffs during extraction.

### Air Drop Case | 航空箱

中文：航空箱是重型特殊 valuable，支持“直接开启换即时收益”与“完整搬运保留更高价值”两种玩法选择。  
English: The air drop case is a heavy special valuable with a core choice between opening it for instant payout or carrying it intact for higher value.

## Included In This Repository | 仓库内容

- `source/RepoDeltaForceMod.RuntimeRecovered`
  - Main BepInEx runtime source
  - 主运行时源码
- `docs`
  - Retained design notes for the released gameplay direction
  - 当前保留的正式设计文档
- `scripts`
  - Optional local helper scripts for content export and profile sync
  - 本地导出与同步时可选使用的辅助脚本

## Retained Docs | 保留文档

- [军用信息终端设计-2026-04-02.md](docs/军用信息终端设计-2026-04-02.md)
- [哈夫克物资设计-2026-04-02.md](docs/哈夫克物资设计-2026-04-02.md)
- [第一版正式实装范围-2026-04-05.md](docs/第一版正式实装范围-2026-04-05.md)
- [GitHub发布文案-2026-04-06.md](docs/GitHub发布文案-2026-04-06.md)

## Local Setup | 本地配置

### English

1. Copy `Directory.Repo.props.user.example` to `Directory.Repo.props.user`
2. Set `RepoGameDir` and `BepInExDirectory` for your machine
3. Build from the repository root:

```powershell
dotnet build .\source\RepoDeltaForceMod.RuntimeRecovered\RepoDeltaForceMod.csproj -c Release
```

### 中文

1. 将 `Directory.Repo.props.user.example` 复制为 `Directory.Repo.props.user`
2. 按你的本机环境填写 `RepoGameDir` 和 `BepInExDirectory`
3. 在仓库根目录执行：

```powershell
dotnet build .\source\RepoDeltaForceMod.RuntimeRecovered\RepoDeltaForceMod.csproj -c Release
```

## Configuration Notes | 配置说明

- `Directory.Repo.props` keeps only generic template values
- `Directory.Repo.props` 只保留通用模板配置
- Machine-specific paths should go into `Directory.Repo.props.user`
- 本机专用路径应写在 `Directory.Repo.props.user`
- PowerShell scripts inside `scripts/` expect explicit local path arguments
- `scripts/` 中的 PowerShell 脚本需要显式传入本地路径参数

## Future Direction | 后续方向

### 中文

如果后续有玩家喜欢这套玩法，我会再决定是否继续更新。

目前我比较明确的设想，是先继续把“事件驱动的特殊摸金体验”做厚，再慢慢扩展新的阵营、奖励结构和物资机制。比较清晰的方向包括：

- 继续扩展哈夫克物资池，加入更多有趣物资
- 增加高回报但带有特殊效果或额外风险的物资，比如会干扰环境、暴露玩家位置、改变搬运节奏，或者迫使玩家在短期收益和长期安全之间做选择
- 扩展特殊物资的玩法多样性，让不同物资不只是“值多少钱”，而是各自带有不同的机制压力、交互方式和取舍逻辑
- 增加“收集一整套获得特殊奖励”之类的新玩法
- 后续加入阿萨拉事件
- 扩展阿萨拉集团物资池
- 等事件和物资逐渐丰富后，再去调整开局事件触发概率

另外，当前的军用信息终端、飞行记录仪和航空箱虽然已经能形成完整玩法，但都还只是实现了部分功能。  
如果后续继续更新，我也希望继续把这三类核心物资往更完整、更有特色的方向实装和细化。

### English

If players enjoy this gameplay direction, I may continue updating the mod.

The clearest idea right now is to deepen the event-driven special looting experience first, then gradually expand into new factions, reward structures, and item mechanics. The current roadmap direction includes:

- expanding the Havoc supply pool with more interesting items
- introducing high-reward items that also come with special effects or additional risk, such as environmental interference, player exposure, altered hauling rhythm, or other tradeoffs between short-term profit and long-term safety
- making special supplies more mechanically diverse so they differ not only by value, but also by pressure, interaction style, and decision-making
- introducing set-collection rewards or similar mechanics
- adding an Assala-themed event in the future
- building an Assala company supply pool
- adjusting opening event trigger rates later, once the event and item pools become richer

Also, while the military terminal, flight recorder, and air drop case already form a working gameplay loop, each of them still represents only part of its full potential design.  
If development continues, I would like to keep expanding and refining these three core supplies as well.

## Suggested Repository Description | 仓库简介建议

中文：为 REPO 制作的哈夫克主题玩法模组，灵感来自“把三角洲护航摸金体验带进 REPO”的视频设想，当前版本通过哈夫克事件向地图投放特殊物资，包括军用信息终端、飞行记录仪与航空箱。  
English: A Havoc-themed gameplay mod for REPO, inspired by the idea of bringing Delta Force-style escort looting into REPO, currently using a Havoc event to inject special supplies into the map, including a military terminal, a flight recorder, and an air drop case.
