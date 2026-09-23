# DeepSeek Balance Widget（macOS 版）

> 简体中文 | [English](README.en.md)

> ⚠️ **本仓库只维护 macOS 端**（Avalonia / .NET 8）。Windows（WPF）端已拆分为**独立仓库**，此处不再维护，Windows 相关改动请提交到 Windows 仓库。
>
> DeepSeek 余额 & ChatGPT Plus 用量监控桌面悬浮窗 · macOS 12+ · 基于 .NET 8
>
> _DeepSeek balance & ChatGPT Plus usage monitor — a desktop widget for macOS._

一个面向 macOS 的 DeepSeek API 余额与 ChatGPT Plus 用量监控小工具。它支持余额轮询、Plus 剩余额度、开机自启和异常状态提示，并提供贴边自动隐藏、迷你胶囊和菜单栏常驻。

这是一个面向中文用户的 macOS 桌面工具，界面基于 Avalonia。下面的标签是 GitHub 使用的技术分类，中文含义见[标签说明](#标签说明)。

[![Release](https://img.shields.io/github/v/release/wanghoufan/DeepSeekBalanceWidget-Mac?display_name=tag)](https://github.com/wanghoufan/DeepSeekBalanceWidget-Mac/releases/latest)
[![Platform](https://img.shields.io/badge/platform-macOS-000000?logo=apple)](https://github.com/wanghoufan/DeepSeekBalanceWidget-Mac)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)

![DeepSeek 余额监控 v0.5.0](artifacts/ui-audit/02-after.png)

## 下载

前往 [Releases](https://github.com/wanghoufan/DeepSeekBalanceWidget-Mac/releases/latest) 下载 macOS 安装包：

| 文件 | 适用平台 |
| --- | --- |
| `DeepSeekBalanceWidget-v0.5.0-macos-arm64.zip` | macOS Apple Silicon（M 系列） |
| `DeepSeekBalanceWidget-v0.5.0-macos-x64.zip` | macOS Intel |

**macOS**：解压后把 `DeepSeekBalanceWidget.app` 拖入“应用程序”文件夹即可，无需安装 .NET。

> 需要 Windows 版？请前往 Windows 端独立仓库（本仓库不含 Windows 发布包）。

> 第一次启动后，请在设置中填写自己的 DeepSeek API Key。macOS 版 API Key 存入登录钥匙串，不会上传到 GitHub。

## 使用

macOS 版是独立的原生 `.app` 菜单栏应用。它支持 Apple Silicon 和 Intel Mac，API Key 保存到当前用户的 macOS 登录钥匙串。

首次运行未签名的应用时，若 Gatekeeper 阻止打开，请在 Finder 中按住 Control 点击应用并选择“打开”。

macOS 版在设置中可启用“登录时自动启动”；这会写入当前用户的 `~/Library/LaunchAgents/com.deepseekbalancewidget.plist`。ChatGPT Plus 用量读取本机 `~/.cc-switch/codex_oauth_auth.json`。

### 在 Mac 上自行构建

在安装了 .NET 8 SDK 的 Mac 上执行：

```bash
chmod +x scripts/publish-macos.sh
./scripts/publish-macos.sh arm64   # Apple Silicon（M 系列）
# 或 ./scripts/publish-macos.sh x64  # Intel Mac
open release/macos-arm64/DeepSeekBalanceWidget.app
```

生成的应用是自包含的，最终使用者不需要安装 .NET（发布包会包含运行时，因此体积会较大）。

要像其他 App 一样从 Launchpad 启动，可在打包后安装到当前用户的“应用程序”目录：

```bash
bash scripts/install-macos.sh arm64
```

脚本会自动打开应用，使其注册到 Launchpad；之后可直接从 Launchpad 或 Finder 的“应用程序”中启动。同名旧版本会被移动为带时间戳的备份。

## 主要功能

- 实时显示 DeepSeek API 总余额、充值余额和有效赠送余额，并显示与上一次成功刷新的金额和百分比变化
- 通过本机 Codex 登录状态持续显示 **ChatGPT Plus 用量**：以对齐表格呈现两个账号的 5 小时滚动窗口与周窗口剩余额度、重置倒计时（v0.4.0 起逐列对齐）
- 通过官方用量接口持续显示 **OpenCode Go 额度**：5 小时 / 周 / 月三窗口剩余百分比、距恢复倒计时与进度条；未配置 Key / Key 无效 / 网络失败等状态直接显示在区块上（替代原 WorkBuddy 占位）
- 余额与额度预警：DeepSeek 低余额、ChatGPT / OpenCode 额度降到设定档位（默认 20% / 10%）时弹出常驻警报并循环警报声，额度恢复后弹出恢复通知；同一档位每个周期仅提醒一次
- **迷你胶囊单行宽布局**：DeepSeek 余额（含变动小字）｜GPT 双账号四列对齐｜OpenCode Go 三窗口进度条｜贴边 / 置顶 / 最小化 / 关闭按钮垂直居中贴最右（刷新时间已从胶囊移除，展开卡片保留）
- **胶囊区块顺序自定义**（v0.4.0）：设置内上移/下移调整 DeepSeek / ChatGPT / OpenCode / WorkBuddy 的渲染顺序，保存即生效
- 低余额及异常下降提醒，带冷却机制避免重复打扰
- macOS 版提供完整卡片与迷你胶囊模式，可自由拖动、记忆位置，并可选贴边自动隐藏
- 在菜单栏实时显示余额、Plus 用量百分比和高峰时段指示
- 菜单栏状态、置顶、隐藏、开机自启；按北京时间显示官方峰值时段参考
- API Key 存入 macOS 登录钥匙串，不落明文、不上传

## v0.5.0 更新亮点

- **GPT 额度恢复提醒**：5 小时 / 周额度只要回到满额（默认 100%）即弹出绿色恢复通知（环形恢复箭头图标 + 绿色边框，8 秒自动消失、不响铃），同时胶囊 GPT 区块绿色边框高亮 2 分钟；与橙色预警一眼可辨
- **Mac 端正式接入 GPT / OpenCode 额度预警弹窗**：此前评估器虽已编译但主窗口未调用，Mac 上预警与恢复弹窗均不会显示，本次补齐接线
- **OpenCode Go 额度监测**（替代原 WorkBuddy 占位）：读取官方用量接口，5 小时 / 周 / 月三窗口全量展示，胶囊内每行含剩余百分比、距恢复倒计时与进度条
- **预警系统重构**：低量预警改为常驻弹窗 + 循环警报声，需点击「知道了」关闭，位置可配（默认右上角）；OpenCode 只保留低量预警、不播报恢复
- **设置页改版**：左侧导航 + 监测项 2×2 卡片，各项独立开关与「测试连接」行式布局

**开发中（未发布）**：OpenCode Go **双账号监测**（胶囊 OC1/OC2 并排迷你卡、菜单栏独立 OC2 状态项、详情卡分组显示）、胶囊月额度恢复天数（月行纯天数显示）、菜单栏 GPT 5 小时恢复倒计时、设置保存稳定性修复、菜单栏状态项稳定性修复（只在启动时创建一次 + SIGTERM 收尾摘除）——详见 [CHANGELOG.md](CHANGELOG.md)「未发布」章节。

详见 [CHANGELOG.md](CHANGELOG.md)。

<details>
<summary>查看当前界面（v0.5.0）</summary>

| 迷你胶囊（单行宽布局） | 展开卡片 |
| --- | --- |
| <img src="artifacts/ui-audit/01-before.png" width="480"> | <img src="artifacts/ui-audit/02-after.png" width="280"> |

</details>

完整变更见 [CHANGELOG.md](CHANGELOG.md)。

## 标签说明

| GitHub 标签 | 中文解释 |
| --- | --- |
| `deepseek` | 对接 DeepSeek 开放平台 API |
| `api-monitoring` | 监控 API 余额、变化和可用状态 |
| `desktop-widget` | 桌面悬浮小工具 |
| `macos` | 面向 macOS 12+ 使用 |
| `avalonia` | macOS 版使用 Avalonia 构建界面 |
| `dotnet` | 基于 .NET 8 开发 |

仓库右侧的 About 区域提供项目简介和这些技术标签；README 负责提供完整的中文使用说明。

## 日常启动

日常使用请从 Launchpad 或“应用程序”中启动 `DeepSeekBalanceWidget.app`。不要从 `src/.../bin/Debug/...` 启动；该目录属于开发构建缓存，路径和文件会随编译变化。

## 发布

在 macOS 上运行 `scripts/publish-macos.sh` 生成 `.app` 包，见上文“在 Mac 上自行构建”。产物为：

```text
release/macos-arm64/DeepSeekBalanceWidget.app
release/macos-x64/DeepSeekBalanceWidget.app
```

自包含版本无需目标电脑预先安装 .NET Runtime，因此体积会明显大于 Debug 目录里的 apphost，这是正常现象。`bash scripts/install-macos.sh arm64` 可一步完成打包并安装到用户级应用程序目录 `~/Applications`（不需要管理员权限，装完即可在 Launchpad 中看到）。

打上 `v*` 标签推送后，GitHub Actions 会自动构建 macOS（arm64 / x64）安装包并发布到 Releases 页面。

> Windows 端的构建与发布在 Windows 独立仓库中进行，不在本仓库维护。

## 开发

环境要求：

- macOS 12+（Apple Silicon 或 Intel）
- .NET 8 SDK
- Rider 或 VS Code（可选）

构建与运行 macOS 项目：

```bash
dotnet build src/DeepSeekBalanceWidget.Mac/DeepSeekBalanceWidget.Mac.csproj
dotnet run --project src/DeepSeekBalanceWidget.Mac/DeepSeekBalanceWidget.Mac.csproj
```

> 解决方案 `DeepSeekBalanceWidget.sln` 里仍挂着 Windows（WPF）项目，在 macOS 上构建会失败（缺少 WindowsDesktop SDK）。在 Mac 上请直接构建上面的 `.Mac` 项目，不要跑整个 sln。

## 项目结构

```text
.
├─ src/
│  ├─ DeepSeekBalanceWidget/           Windows WPF 应用（已迁至 Windows 独立仓库，此处不再维护）
│  └─ DeepSeekBalanceWidget.Mac/       macOS Avalonia 应用（本仓库唯一维护目标）
├─ tests/                  自动化测试
├─ artifacts/ui-audit/     UI 前后对照截图
├─ scripts/                构建与发布脚本
│  ├─ governance/          治理入口与 Fail-Closed 接口模板
│  ├─ runtime/macos/       macOS Runtime 适配与审计入口
│  └─ workers/              Worker 启动接口模板
├─ docs/                   项目治理、进度、QA、Review、Runtime 与交接记录
│  ├─ governance/          Origin/Applied、权限、状态机、迁移审计
│  ├─ progress/            当前阶段、权威状态、Evidence、用户决定
│  ├─ runtime/             Runtime Health 与运行注册表
│  └─ roles/               八个固定治理角色职责
├─ skills/                 Skill 候选登记
├─ tests/governance/       治理契约测试（自动发现 tests/governance/test-*）
├─ release/                本地发布产物，不提交 Git
├─ DeepSeekBalanceWidget.sln
└─ README.md
```

## 治理模板

本项目已按 `01_治理模板/AI-Governance-Template` 的 ORCA V2.3.1 / Delivery V1.10 结构迁移。模板源保留在该目录，项目当前吸收状态记录在 `docs/governance/PROJECT_GOVERNANCE_APPLIED.yaml`，不可变来源记录在 `docs/governance/PROJECT_GOVERNANCE_ORIGIN.yaml`。

治理脚本默认 Fail Closed；脚本文件存在不等于本机 ORCA、OpenCode、Codex、Computer Use 或发布运行链路已经通过真实验证。项目专属迁移结论见 `docs/governance/PROJECT_MIGRATION_AUDIT.md`。

## 配置与安全

普通配置位于：

```text
~/Library/Application Support/DeepSeekBalanceWidget/config.json
```

API Key 不写入该配置文件，而是存入 macOS 登录钥匙串。配置文件、API Key 和本地发布产物不会提交到 GitHub。

本项目不会把 API Key 写入源代码、日志或 GitHub Actions。请不要把上述 `config.json` 提交或发送给其他人。

## 开机自启

macOS 在设置中启用“登录时自动启动”后，会写入 `~/Library/LaunchAgents/com.deepseekbalancewidget.plist`，记录当前正在运行的 App 路径。建议先安装/运行“应用程序”目录里的 `DeepSeekBalanceWidget.app`，再启用该选项，避免 plist 指向 Debug 构建目录。

## 文档

版本变化记录见 [CHANGELOG.md](CHANGELOG.md)。
