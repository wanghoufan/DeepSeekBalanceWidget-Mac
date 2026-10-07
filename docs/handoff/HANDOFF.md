# HANDOFF｜交接（暂停/恢复用，先读我）

> 旧版字段（governance-state / Evidence / Human Gate / Promotion / Dispatch ID）已废弃，不填。

- Captured at（YYYY-MM-DD HH:MM）：2026-10-07 10:30
- PROJECT_PHASE：DEVELOP（小活/缺陷修复批次，无独立 Plan 文档）
- PLAN_VERSION：（空）
- PLAN_READINESS_SCORE：（空）
- PLAN_GATE：（空）
- DEV_BASELINE：（空）
- CHANGE_REQUEST：NONE
- Stage ID：STAGE-大交接2（0.7.0 功能已开发完、未发版）
- 剩 P0（没完的才列，多一条都不行）：无
- 当前 Task（正干到哪）：大交接 2 五步（neat-freak 收尾 → 本 HANDOFF → skill 审查清单 → commit+push → 恢复提示词），已完成
- 执行链/Session：（空）
- 未闭环评审意见（code-reviewer/qa 留的还没改的）：无
- 产品验收（追踪矩阵落点 / 未测 AC / 是否待用户签收）：`docs/qa/`；本批三项功能真机证据全绿（probe12 全绿、通知屏内实拍、菜单栏含 Builder 实拍、148 tests 0 错、build 0 错），AC 覆盖见 CHANGELOG 未发布 4 条与 README；发布类型＝迭代更新，不强制用户签收
- 用户签收（触发依据 / 证据包 / 签收人 / 结论）：不需要（迭代更新，非首次发布）
- docs 落盘清单（本轮新增/改了哪几个 docs 文件）：`docs/handoff/HANDOFF.md`（本次重写，旧版备份为 `HANDOFF.md.旧版-2026-08-30`）、`docs/skill-reveiw/SKILL-REVIEW.md`（新建）、`AGENTS.md`（下一步待办更新）
- 下一步（Next Single Action）：见下方「下一步任务」第 1 条（0.7.0 发版，待用户拍板）
- 人要拍什么板（列出来问，不问不许开工）：① 是否发 0.7.0；② `docs/skill-reveiw/SKILL-REVIEW.md` 候选 skill 逐条审查；③ 大残留是否大清理（tests/deprecated、release/ 旧包、各 *.旧版-* 备份——本批只列未删）
- permission_request：2026-10-07 大交接 2 用户明确授权 commit + push（分支以远端实际为准＝master）✓
- 收尾记一笔（neat-freak）：文档已对齐（AGENTS 未发布/待办、CHANGELOG 4 条与代码一致，build 复验 0 错）；temp 仅剩运行中 app 持有 fd 的 `app-stderr.log`；会话残留已清（.DS_Store、系统临时区、截图探针）；未决＝上面「人要拍什么板」

## 当前工作进展（本阶段实绩）

**功能（全部未发布，见 CHANGELOG「未发布」4 条）**

1. 启动不弹卡片：初始化完成后立即隐藏主窗；启动 60 秒内「窗口未显示 + 用户从未打开过」才抑制 Reopen 激活（覆盖 LaunchServices Reopen 晚到 30s+ 实测）；用户打开过一次后照常还原窗口
2. 通知弹窗屏外与透明修复：`RepositionAll` 统一按逻辑点定位（不再二次乘 `RenderScaling`）；淡入淡出改直接属性插值、结束时显式落地目标透明度 + 1.5s 兜底
3. 菜单栏 Builder 推荐状态项：独立 `NSStatusItem`（余额项左侧）、固定 UTC+8 工作日/周末两张表输出 `trae/open/work·ds` 等短文本、Tooltip 原因与下次切换、变化时复用 `MacToastService` 弹 8s 轻通知；规则 SSOT＝共享 `BuilderRecommendationCalculator` + 全量边界测试
4. 菜单栏 OC 月额度恢复天数后缀（如 `OC1 99/89/45% 20D`，随刷新按 `ResetsAt` 重算）

**验证**：148 tests 0 错；probe12 全绿（定位 1644,46 / 134 屏内、全屏帧右上两张通知实拍、fade 日志正常）；`dotnet build` Mac csproj 0 错（4 个既有 warning 不阻塞）；测试后已还原 config `[20,10]`、alert-events 104 行

**对外同步（均已上线复验）**

- README 中英：hero 换 2026-10-07 实拍（邮箱已像素化）、菜单栏实拍条、Builder 标「开发中，未发布」、仓名统一 `p010-deepseek-balance-mac`、标签表补齐 11＝About 实测；`validate_readme.py` → DOCUMENTATION_READY(0)；commit `6404a65` 已推 master
- 站点封面：GitHub hero 同源合成 1600×900@16:9 → 站点仓 `0189147` → Pages run `37559720663` success → 线上 sha256 与本地一致
- 中央 skill `github-homepage-showcase-local-zh` 升 2.1.0（新增「封面与 README hero 同源一致」条款），`validate_skill.py` VALID，`SKILL-REGISTRY.md` 已登记（含 2.0.0 补记）
- 反向对账（v1.2.0 新规首跑）：`audit-remotes` PASS——远端 56 仓、`unregistered=0`、9 条 ghost 全有 renamed/deleted note、NO-HOME 空

**已发布**：0.6.0（tag 与 CI 双架构产物在）

## 下一步任务

1. 【待用户拍板】0.7.0 发版：CHANGELOG 未发布 4 条转版号 → 三处版本一致性（csproj `<Version>` + `Info.plist` CFBundleShortVersionString/CFBundleVersion + README 下载表）→ tag `v0.7.0` push 触发 CI → 真机装包验证
2. WorkBuddy 实际额度接入（AGENTS 待办①，当前仍是占位）
3. 界面截图持续更新：2026-10-07 hero/菜单栏实拍已上 README，胶囊细节图可在 0.7.0 发版时顺带重截
4. 视 Windows 新仓库落地情况，决定是否把 `src/DeepSeekBalanceWidget/`（WPF）从本仓库移除
5. `docs/skill-reveiw/SKILL-REVIEW.md` 候选 skill 逐条审查拍板

## 注意事项及相关规矩（下一 agent 必读）

**仓库边界**

- 只维护 macOS 端（Avalonia）；WPF 专属文件（窗口、DPAPI、`publish.ps1`）只读不维护；共享 `Models/`、`Services/` 物理在 `src/DeepSeekBalanceWidget/` 下、由 Mac csproj `<Compile Include>` 链接，改动只保证本仓编译通过，不跨仓 cherry-pick

**构建与测试**

- 只跑 `dotnet build src/DeepSeekBalanceWidget.Mac/DeepSeekBalanceWidget.Mac.csproj`；**禁** `dotnet build DeepSeekBalanceWidget.sln`（macOS 缺 WindowsDesktop SDK 必失败）
- 测试工程在 macOS 不可运行（引用 WPF csproj）；共享代码验证以 Mac csproj build 为准
- 4 个既有 warning（AVLN3001 等）不阻塞、默认不修

**发版（易漏）**

- 三处版本必须一致：csproj `<Version>` / `Info.plist` 两键 / README 下载表；打 tag 前核对 CHANGELOG 顶部
- push `v*` tag 自动触发 `.github/workflows/release-macos.yml` 构建 arm64+x64 并 `--clobber` 覆盖资产；本地 `release/*.zip` 不是发布物
- 排查闪退先读崩溃报告 `app_version`/`procPath`（Launchpad 与 `~/Applications/DeepSeekBalanceWidget.backup-*` 可能指旧包，误判当前代码）

**红线与规矩**

- commit/push 无用户明确授权一律不做（本次大交接 2 已授权，仅限本批交接文件与阶段成果）；**本仓分支是 master，不是 main**
- 汇报 ≤10 行＝三行心跳＋≤3 要点；只报目标/用户安排的工作/大影响三类，小事自决不报
- 临时文件一律进仓内 `temp/`（已 gitignore、不入库、push 前清空）；派 opencode 通道角色写本仓外目录会被 `external_directory` 拒
- 旧版备份命名 `*.旧版-*`（gitignore 命中、备份不覆盖）；大残留删除等用户明确说大阶段结束再详细清
- 治理规则改动后：`node scripts/model/check-ledger.mjs docs/model` 须 `LEDGER-OK` + `bash scripts/sync-old-projects.sh` 对齐 + HANDOFF 记一行
- Read 工具读图可能串图/旧缓存：用全新唯一文件名＋图片尺寸吻合＋像素统计交叉验证，不裸信目检
- macOS 自带 grep 不支持 `-P`，用 `awk`/`grep -E`

## 恢复读盘（全体系唯一顺序，别乱）

1. AGENTS；2. 角色卡；3. 根 `USER_MODEL_OVERRIDE.md`；4. 本 HANDOFF；5. 根 `经验一句话.md`；6. 任务目标放最后。
只有出现冲突才扩大读取范围。
