# REPO Delta Force Mod

中文：这是一个为 REPO 制作的哈夫克主题玩法模组，围绕特殊开局事件、军用信息终端、飞行记录仪和航空箱扩展原版搜刮与搬运体验。  
English: A Havoc-themed gameplay mod for REPO, expanding the base scavenging loop with a special opening event, a military terminal, a flight recorder, and an air drop case.

## 项目特色 | Features

### 中文

- 哈夫克开局事件，作为本模组的核心主题入口
- 军用信息终端，提供手持扫描与目标指引能力
- 飞行记录仪，作为高价值高风险特殊 valuable
- 航空箱，支持开启取舍、即时收益与剩余价值玩法

### English

- Havoc opening event as the main thematic entry point
- Military terminal with held scanning and target guidance
- Flight recorder as a high-value, high-risk special valuable
- Air drop case with open-or-carry tradeoff, instant payout, and remaining value

## 当前仓库内容 | Repository Contents

### 中文

- `source/RepoDeltaForceMod.RuntimeRecovered`
  主运行时源码，基于 BepInEx 插件结构
- `docs`
  当前保留的核心设计文档
- `scripts`
  本地导出与同步内容包时可选使用的辅助脚本

### English

- `source/RepoDeltaForceMod.RuntimeRecovered`
  Main runtime source for the BepInEx plugin
- `docs`
  Retained core design and implementation notes
- `scripts`
  Optional helper scripts for local content export and profile sync

## 保留文档 | Retained Docs

- [军用信息终端设计-2026-04-02.md](docs/军用信息终端设计-2026-04-02.md)
- [哈夫克物资设计-2026-04-02.md](docs/哈夫克物资设计-2026-04-02.md)
- [第一版正式实装范围-2026-04-05.md](docs/第一版正式实装范围-2026-04-05.md)

## 本地构建 | Local Build

### 中文

1. 将 `Directory.Repo.props.user.example` 复制为 `Directory.Repo.props.user`
2. 根据你的机器环境填写 `RepoGameDir` 和 `BepInExDirectory`
3. 在仓库根目录执行：

```powershell
dotnet build .\source\RepoDeltaForceMod.RuntimeRecovered\RepoDeltaForceMod.csproj -c Release
```

### English

1. Copy `Directory.Repo.props.user.example` to `Directory.Repo.props.user`
2. Set `RepoGameDir` and `BepInExDirectory` for your local machine
3. Run this from the repository root:

```powershell
dotnet build .\source\RepoDeltaForceMod.RuntimeRecovered\RepoDeltaForceMod.csproj -c Release
```

## 配置说明 | Configuration Notes

### 中文

- `Directory.Repo.props` 只保留通用模板配置
- 本机专用路径应写在 `Directory.Repo.props.user`
- `scripts` 中的 PowerShell 脚本需要显式传入你的本地路径参数

### English

- `Directory.Repo.props` contains generic template values only
- Machine-specific paths should go into `Directory.Repo.props.user`
- PowerShell scripts in `scripts/` expect explicit local path arguments

## 仓库简介建议 | Suggested Repository Description

中文：为 REPO 制作的哈夫克主题玩法模组，加入特殊开局事件、军用信息终端、飞行记录仪与航空箱。  
English: A Havoc-themed gameplay mod for REPO featuring a special opening event, a military terminal, a flight recorder, and an air drop case.
