# REPO Delta Force Mod

中文：这是一个为 REPO 制作的哈夫克主题玩法模组，围绕特殊开局事件、军用信息终端、飞行记录仪和航空箱扩展原版搜刮、搬运与取舍体验。  
English: A Havoc-themed gameplay mod for REPO, expanding the base scavenging, hauling, and risk-reward loop with a special opening event, a military terminal, a flight recorder, and an air drop case.

## Status | 当前状态

- `Playable`
- `Core gameplay implemented`
- `Repository cleaned for public source hosting`

中文：
- 当前核心玩法已经完成并可正常游玩
- 仓库已完成一轮公开源码整理
- 现阶段更偏向持续优化与后续扩展

English:
- Core gameplay is already implemented and playable
- The repository has been cleaned for public source hosting
- The current focus is polish and future expansion

## Gameplay Overview | 玩法概览

### Havoc Opening Event | 哈夫克开局事件

中文：模组会在开局阶段引入哈夫克主题事件，用来承接整套特殊物资的出现逻辑。  
English: The mod introduces a Havoc-themed opening event that serves as the entry point for the special supply set.

### Military Terminal | 军用信息终端

中文：军用信息终端是偏工具型物资，强调搜索、扫描与路线指引。  
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

## Roadmap | 后续方向

中文：
- 继续优化正式事件投放体验
- 继续打磨物资平衡与反馈表现
- 为后续主题扩展保留结构空间

English:
- Further polish the formal event spawn flow
- Continue tuning balance and gameplay feedback
- Keep the structure ready for future thematic expansions

## Suggested Repository Description | 仓库简介建议

中文：为 REPO 制作的哈夫克主题玩法模组，加入特殊开局事件、军用信息终端、飞行记录仪与航空箱。  
English: A Havoc-themed gameplay mod for REPO featuring a special opening event, a military terminal, a flight recorder, and an air drop case.
