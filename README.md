# REPO Delta Force Mod

中文：这是一个为 REPO 制作的哈夫克主题玩法模组，围绕特殊开局事件、军用信息终端、飞行记录仪和航空箱扩展原版搜刮、搬运与取舍体验。  
English: A Havoc-themed gameplay mod for REPO, expanding the base scavenging, hauling, and risk-reward loop with a special opening event, a military terminal, a flight recorder, and an air drop case.

## Origin | 创作缘起

### 中文

这个项目的灵感来自一个视频，视频内容大致是“下单三角洲护航，一起在 REPO 中摸金”。  
作为一个同时玩三角洲和 REPO 的玩家，我想把三角洲里那种特殊的摸金体验，以更适合 REPO 的方式做进游戏里，于是就开始了这个 mod。

我不是专业开发者，只是一名边做边学的新手，所以这个项目更像是一次玩家视角下的主题化玩法尝试。

### English

This project was inspired by a video built around the idea of “bringing Delta Force escort-style looting into REPO.”  
As a player of both Delta Force and REPO, I wanted to recreate part of that special extraction-loot feeling inside REPO in a way that fits the game, which is how this mod started.

I am not a professional developer. This is a beginner-made project built from a player perspective while learning along the way.

## Status | 当前状态

- `Playable`
- `Core gameplay implemented`
- `Repository cleaned for public source hosting`

### 中文

- 当前核心玩法已经完成并可正常游玩
- 仓库已完成一轮公开源码整理
- 后续是否继续长期更新，会看玩家反馈再决定

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
When the Havoc event triggers, Havoc company supplies are inserted into the map, and that event acts as the main delivery mechanism for the mod’s special content.

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
目前我比较明确的设想，是先继续把“事件驱动的特殊摸金体验”做厚，再慢慢扩展新的阵营与奖励结构。当前比较清晰的后续方向包括：

- 在哈夫克物资池中加入更多有趣物资
- 增加“收集一整套获得特殊奖励”之类的新玩法
- 后续加入阿萨拉事件
- 扩展阿萨拉集团物资池
- 等事件和物资逐渐丰富后，再去重新调整开局事件的触发概率

### English

If players enjoy this gameplay direction, I may continue updating the mod.  
The clearest current idea is to deepen the event-driven special looting experience first, then gradually expand into new factions and reward structures. The current roadmap direction includes:

- adding more interesting items to the Havoc supply pool
- introducing set-collection rewards or similar mechanics
- adding an Assala-themed event in the future
- building an Assala company supply pool
- adjusting opening event trigger rates later, once the event and item pools become richer

## Suggested Repository Description | 仓库简介建议

中文：为 REPO 制作的哈夫克主题玩法模组，灵感来自“三角洲护航一起在 REPO 中摸金”的视频设想，当前加入特殊开局事件、军用信息终端、飞行记录仪与航空箱。  
English: A Havoc-themed gameplay mod for REPO, inspired by the idea of bringing Delta Force-style extraction looting into REPO, featuring a special opening event, a military terminal, a flight recorder, and an air drop case.
