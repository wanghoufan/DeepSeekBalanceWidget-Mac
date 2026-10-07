# Skill 候选审查清单（大交接 2 · 2026-10-07）

> 本阶段开发中「用户反复提醒 / 重复要求 / 重复流程」的沉淀候选，**全部待用户审查拍板**，未拍板不创建。
> 拍板方式：逐条回复「建 / 不建 / 改了再建」；批准的按对应创建器规范落地（user 级走 `central-skill-creator-mac-local-zh`，项目级走 `project-skill-creator-mac-local-zh`）。
> 目录名说明：按大交接指令原样使用 `docs/skill-reveiw`（review 的原始拼写，不擅自更名）。

## 候选清单

| # | 候选 Skill | 类型建议 | 解决什么（用户反复提醒的点） | 本阶段证据 | 建议 |
|---|---|---|---|---|---|
| 1 | 阶段大交接（stage-handoff-local-zh） | user 级 | 阶段收尾固定五步：neat-freak 文档对齐 → 写/更新 HANDOFF → 梳理 skill 候选供审查 → commit+push → 输出「只读 handoff 恢复」的下一 agent 提示词。用户用【大交接】【大交接 2】两次口令触发，流程完全可复用 | 本会话即第 2 次执行 | 【建议】建 |
| 2 | AI 读图可信度校验（ai-image-read-verify-local-zh） | user 级 | 读图工具返回错位/旧缓存附件时的验证法：全新唯一文件名、图片尺寸与预期吻合、「本会话从未出现过的画面」判定、像素统计代替目检（如打码区高频占比 0.0 vs 对照 0.125） | 本阶段踩中 4+ 次（封面探图串图、hero 目检），每次靠这四招脱险 | 【建议】建 |
| 3 | macOS 窗口截图取证与脱敏（mac-window-capture-redact-local-zh） | user 级 | 起窗（`open -a`）→ winlist 探窗口 id → `screencapture -l` 取窗 → 隐私字段 PIL 像素化 → 统计验证打码生效 → 合成 16:9 上站素材的完整链 | 本阶段做 hero 图、菜单栏条、两次邮箱打码，全部同一套动作 | 【建议】建 |
| 4 | 封面与 README hero 跨处同步 | — | 「GitHub hero 换了、站点封面要跟着换」 | 本轮质询后**已沉淀**进中央 `github-homepage-showcase-local-zh` v2.1.0（SKILL/cover-rules/sync-flows 三处） | 已沉淀，不重复建 |
| 5 | 发版三处版本一致性门禁（release-version-gate-local-zh） | 项目级 | 打 tag 前自动核对 csproj `<Version>`＋`Info.plist` 两键＋README 下载表＋CHANGELOG 顶部四者一致，不一致即报错 | AGENTS「易漏」节长期存在，每次发版靠人脑核对，历史上已发生过版本不符 | 【建议】建（0.7.0 发版前落地最划算） |
| 6 | 汇报口径 / commit 红线 / 洁癖自决边界 | — | 「只报三类、小事自决不问」「无授权不 push」「小残留自决、大清理等口令」 | 高频提醒，但**已全部写入 AGENTS**（2026-09-20/29、10-03 立规） | 已规则化，不需 skill |

## 判断说明

- #1–#3 是**跨项目可复用流程**（不绑定本仓），故建议 user 级进中央仓库；#5 绑定本仓工程结构，项目级。
- #4、#6 已有归宿（skill／AGENTS），列此仅为痕迹完整。
- 未列入的观察项（太碎不成 skill）：macOS `grep -P` 不可用（已进本 HANDOFF 注意事项）、菜单栏浅色像素阈值定位法（属 #3 内一环）、`echo ===` 触发 zsh `=` 展开（一次性知识点）。

## 待用户操作

逐条拍板 #1、#2、#3、#5（回复序号＋建/不建即可）。拍板后由对应创建器规范执行创建、验证与登记。
