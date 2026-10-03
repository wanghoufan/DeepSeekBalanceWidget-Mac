# AGENTS.md — DeepSeek Balance Widget（macOS 版仓库）

<!-- ORCA-RULES-BLOCK:BEGIN -->
<!-- 本区块由治理母版 scripts/sync-old-projects.sh 于 2026-09-29 注入；只增不删，可重复运行原地更新。 -->
<!-- 本项目 AGENTS.md 的其余内容（项目专属规矩）保持原样，冲突时以本区块为准。 -->
## ORCA 规则增量（母版 2026-09-29-产品验收）

> 本区块只写**对外通用**的机制增量；派工细节见 `docs/roles/`，账本口径见下方条目。

- **产品验收（2026-09-28 定）**：开发完成的判断来自**用户可见要求的覆盖证据**，不只看单测／构建／代码审查／工具调用成功。
  - 验收标准写在计划里：Phase1 给每条用户可见要求编一条可观察可测的**验收条目（AC）**，并标出**关键 AC**（对应 P0／blocking P1／核心用户路径／必要视觉交互呈现）；**关键 AC 集合不得为空**。
  - 证据落 `docs/qa/` 的**产品验收追踪矩阵**（照 `docs/qa/BUGS.template.md` 同名节）。
  - **不可放行三情形**（命中任一不得判 `PASS`）：①关键产品 DoD／AC 未测；②关键任务涉及的**每个**可见操作控件未实际点击并观察到页面／锚点／状态变化（只验 `href` 存在不算）；③验收证据缺失。
  - 视觉验收最小覆盖：关键用户任务逐条走通、按项目要求检查桌面与窄屏、用边界样本（奇偶条目数／长标题长正文／空状态）检验对齐·换行·裁切·溢出·可读性、留真实浏览器截图。
  - **用户签收**：发布类型为**首次发布**的，用户签收通过才算完成（签收前状态记未完成）；迭代更新与局部修复不强制签收。签收属 **Human Gate 范畴（用户参与）**，**不是新增 QA Gate**。
- **体系更新三件套（2026-09-29 定）**：①本项目规则文件改动后与母版对齐（用 `bash scripts/sync-old-projects.sh` 或按《迁移整理提示词》取包，**备份不覆盖**）；②账本内容**不重写**（实绩历史），只做 schema 校验 `node scripts/model/check-ledger.mjs docs/model`（须 `LEDGER-OK`）；③**HANDOFF 记一行**。**老项目无两包概念，故母版的「同步两包＋更新对外概览」不适用。**
- **派工跨目录禁令（2026-09-29 定）**：派 opencode 通道角色（supervisor／neat-freak／experience-recorder）时，任务里读写本仓以外目录（如 `/tmp`、`1.Active/` 等）会被 `external_directory` 权限自动拒、步骤静默失败，可能让角色误报已做也易反复盲试烧额度（禁盲试）；派单前处置二选一——①临时文件改到仓内已 gitignore 的 `temp/`，②先取得用户授权；codebuddy／codex 通道无此限制。
- **红线（2026-09-29 增补）**：产品验收未落盘或关键 AC 未测、不得报完工/收工；首次发布未取得用户签收、不得报完工/收工。
- **本项目迁移状态**：`docs/model/GOVERNANCE-STATE.json`（`rules_version`／`synced_at`／`project_phase_field`／`task_ledger_rows`／`agents_needs_manual_merge`／`product_acceptance_ac_added`）。
- **存量项目待办（不自动做，需项目 TM 判断）**：本项目实绩 Plan 需补「视觉与交互验收标准（AC 编号）＋关键 AC 集合＋发布类型」，否则新规则下收尾会被判**计划缺项**；完成后把 `product_acceptance_ac_added` 置 `true`。
<!-- ORCA-RULES-BLOCK:END -->


> ⚠️ **仓库边界**：本仓库（GitHub: `wanghoufan/DeepSeekBalanceWidget-Mac`）**只维护 macOS 端**。
> Windows（WPF）端已拆分为独立仓库，两仓库不再互相合并。
> - 在这里改 `src/DeepSeekBalanceWidget.Mac/**`，以及被 Mac 端 `<Compile Include>` 链接的共享 `Models/`、`Services/`（这两个目录物理上仍位于 `src/DeepSeekBalanceWidget/` 下）；
> - WPF 专属文件（窗口、DPAPI 存储、`publish.ps1` 等）不属本仓库范围，改动请去 Windows 仓库；
> - 两侧各自维护一份共享逻辑副本，改的时候只保证本仓库编译通过，不要跨仓库 cherry-pick。

桌面悬浮小工具：监控 DeepSeek API 余额、ChatGPT Plus 用量与 OpenCode Go 额度。本仓库维护 macOS 端（Avalonia），Windows 端（WPF）在独立仓库；Mac 端链接编译 WPF 目录下的平台无关 `Models/` 与 `Services/`。

## 怎么跑起来

- 开发构建：`dotnet build src/DeepSeekBalanceWidget.Mac/DeepSeekBalanceWidget.Mac.csproj`
- 不要跑 `dotnet build DeepSeekBalanceWidget.sln`：sln 里挂着 WPF 项目，macOS 缺 WindowsDesktop SDK 会直接失败
- macOS 打包：`./scripts/publish-macos.sh arm64`（或 x64）→ `release/macos-arm64/DeepSeekBalanceWidget.app`
- 一键安装并打开：`bash scripts/install-macos.sh arm64`（会装到 `~/Applications` 并注册 Launchpad）
- 日常使用从「应用程序」或 Launchpad 启动 `DeepSeekBalanceWidget.app`，不要从 `src/.../bin/Debug/` 启动
- ⚠️ 排查「闪退」先读崩溃报告（`~/Library/Logs/DiagnosticReports/*.ips`）里的 `app_version` 与 `procPath`：`install-macos.sh` 每次会把旧包备份成 `~/Applications/DeepSeekBalanceWidget.backup-*.app`，Launchpad 也可能指着旧包，旧版本的崩溃会被误判成当前代码的问题

## 技术栈

- .NET 8 + Avalonia 11（`net8.0`，macOS 原生 `.app`）
- 余额源：DeepSeek 开放平台 API；Plus 用量源：本机 `~/.cc-switch/codex_oauth_auth.json`
- 私钥（API Key）：macOS 登录钥匙串（Keychain），不落明文、不上传仓库

## 目录与约定

- `src/DeepSeekBalanceWidget.Mac`（Avalonia）是本仓库唯一维护目标；它通过 `<Compile Include="../...">` 链接 `src/DeepSeekBalanceWidget` 下的 `Models/`、`Services/`
- `src/DeepSeekBalanceWidget`（WPF）随 Windows 端拆出，其中 WPF 专属部分（窗口、DPAPI、`publish.ps1`）**只读不维护**：改这些文件请去 Windows 仓库
- `docs/` 下为治理与进度文档；`CHANGELOG.md` 是当前功能真相来源
- `release/`、`*.exe`、`*.zip`、`*.app`、`*.dmg`、`artifacts/runtime/`、`config.json` 已 gitignore，不入库

## ⚠️ 发布前版本一致性（易漏）

打 tag 前必须把 macOS 侧两处版本一起升到与 `CHANGELOG.md` 顶部一致，否则 app 内部版本与 GitHub tag 不符：
1. `src/DeepSeekBalanceWidget.Mac/DeepSeekBalanceWidget.Mac.csproj` 的 `<Version>`
2. `src/DeepSeekBalanceWidget.Mac/Info.plist` 的 `CFBundleShortVersionString` 与 `CFBundleVersion`

Release 包的 zip 文件名由 git tag（`v*`）决定，README 下载表里 macOS 两行的版本号也要同步改。Windows 的 `DeepSeekBalanceWidget.csproj` 版本归 Windows 仓库管，这里不再同步。

发布资产由 `.github/workflows/release-macos.yml` 生成：**push `v*` tag 就会触发 CI 构建 arm64 + x64 两个包并 `gh release upload --clobber` 覆盖同名资产**，所以本地 `release/*.zip` 不是发布物、不必手工上传（手工 `gh release create` 只有正文会保留，资产会被 CI 覆盖）。想自定义 Release 正文，先 `gh release create <tag> --notes-file …` 再 push tag，或事后 `gh release edit <tag> --notes-file …`。

## 当前状态与下一步

- 当前已发布版本：**0.6.0**（OpenCode Go 双账号监测、菜单栏 GPT 5 小时恢复倒计时 + OC1 标签、胶囊月额度恢复天数、GPT 预警事件日志 `AlertEventLogger`、Codex 原生凭据刷新、菜单栏状态项稳定性一批修复）
- 未发布（见 CHANGELOG「未发布」）：菜单栏 OC 状态项月额度恢复天数后缀（如 `OC1 99/89/45% 20D`，随刷新按 `ResetsAt` 重算）。待办见下方「下一步」
- 测试工程在 macOS 上不可运行（引用 WPF csproj，缺 WindowsDesktop SDK）；共享代码验证以 `dotnet build src/DeepSeekBalanceWidget.Mac/...` 为准
- 下一步：① 补齐 WorkBuddy 实际额度接入（当前仍是占位）；② README 的界面截图仍是 v0.6.0 之前的版本（本机缺屏幕录制权限，无法重截胶囊新布局），有权限时补；③ 视 Windows 新仓库落地情况，决定是否把 `src/DeepSeekBalanceWidget/`（WPF）从本仓库移除

## 治理模板已应用

本项目按 `01_治理模板/AI-Governance-Template` 的 ORCA V2.3.1 / Delivery V1.10 结构治理。完整角色职责、状态机、权限边界和学习闭环以 `docs/governance/`、`docs/workflow/`、`docs/roles/` 为准。

- 固定角色仅限 Planner、Builder、Code Reviewer、QA、Product Reviewer、Task Manager、Experience Recorder、neat-freak。
- Task Manager 只负责记录、分类、排序、Dispatch、Evidence 消费和 Continuation 判断，不直接修改业务源码、模型/启动注册表、运行时健康或权威治理状态。
- 高影响范围、Stage/P0、Human Gate、权限、Resource Policy、Production Model Mapping 等决定必须由用户确认。
- 真实 macOS Runtime 为第一验证环境；当前项目仅完成治理结构迁移，未将模板机器的 ORCA/OpenCode/Codex 运行凭据当作本项目证据。
- 权威机器状态包括 `docs/progress/governance-state.yaml`、`docs/runtime/runtime-health.json`、`docs/governance/PROJECT_GOVERNANCE_APPLIED.yaml`；Markdown 文件是可读视图。
