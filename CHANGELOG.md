# Changelog（macOS 版仓库）

> ⚠️ 本仓库 **只维护 macOS 端**（`DeepSeekBalanceWidget-Mac`）。Windows（WPF）端已拆分为独立仓库，其变更不再记录在此。

本项目遵循语义化版本号。

## 未发布

### 功能

- 新增 **OpenCode Go 双账号监测**：设置页可配置第二把 API Key（独立钥匙串条目 `opencode-api-key-2`），胶囊 OC 区块并排显示 OC1/OC2 两张迷你卡（间距加大、只配一把 Key 时仅显示 OC1），详情卡双账号分组显示（账号1/账号2 前缀 + 空行分组），菜单栏新增独立 OC2 状态项（排在主项右侧，未配置第二把 Key 时自动隐藏；启动后中途才启用账号2 时，新显示的 OC2 会插在主项左侧，重启后恢复预期顺序）；账号2 预警独立评估
- 新增 **胶囊月额度恢复天数**：OC 卡月行恢复时间显示纯天数数字（如 `24`），菜单栏主项 OC 标签改为 `OC1`
- 新增 **菜单栏 GPT 5 小时恢复倒计时**：菜单栏 GPT 段由 `GPT 65/79%` 变为 `GPT 65/79% 4h24m`（倒计时紧跟在百分比右侧）；窗口排序与胶囊 / tooltip 统一为按 `DurationMinutes` 升序，倒计时复用 `CodexUsageFormatter.FormatCountdownShort`，已到期显示 `0m`、不放汉字
- 新增 **GPT 预警事件日志**（`AlertEventLogger`）：低量预警与恢复提醒在评估触发时即落盘记录（写在弹窗开关判断之前），用户关闭弹窗后仍可回溯「提醒到底触发过没有」
- 新增 **Codex 原生凭据刷新**（`CcSwitchCodexUsageProvider`）：除 CC Switch 存储外支持读取 Codex 原生 `auth.json` 账号（JWT 解析邮箱），OAuth token 过期自动刷新并原子回写两处存储；配套更新 `CcSwitchCodexUsageProviderTests`

### 修复

- 修复 **测试连接闪退**：设置页 `ThemeBrush` 用 `Application.FindResource` 解析 ThemeDictionaries 中的主题画刷失败即抛未处理异常，现改为控件作用域解析并回退灰色
- 修复 **保存第二把 Key 后原生崩溃**（SIGSEGV）：`MacMenuBarBalance` 未 retain `statusItemWithLength:` 返回的 NSStatusItem（AppKit 不持有该对象），第二个状态项创建后 autorelease pool 清空即释放，主线程再访问即段错误；现创建时 retain、Dispose 时 release
- 修复 **菜单栏整条不显示（重启 app 才回来）**：macOS 给运行期新建的 NSStatusItem 分配的是屏幕外槽位（实测 frame 原点 −1585 / −3633、`screen=nil`，`setLength:` 强制重排、先建新再销旧都拉不回来，也不会自愈），只有进程启动时创建的那次能拿到可见位置。状态项因此改为只在窗口打开时创建一次，之后「是否占用菜单栏」一律用 `setVisible:` 显隐；此前账号2 状态项按配置 Dispose + 重建，正是这条路径会把主项挤掉
- 修复 **OpenCode 额度不显示**：本机 OpenCode CLI 写入的 auth.json 条目名为 `opencode-go`，Provider 此前只识别 `opencode` 导致 fallback 读取永远失败、区块一直显示「未配置 API Key」；现两种条目名均兼容
- 修复 **设置保存可能卡死/闪退**：钥匙串写入（`security` 命令）此前在 UI 线程同步执行，遇到 macOS 钥匙串授权确认框会把整个 app 挂死（观感即崩溃）；现改为 `security` 加 15 秒超时、保存流程移入后台线程，失败时设置页内显示明确错误
- 修复 **钥匙串写入失败会连带丢掉已存 Key**：`security add-generic-password -U` 是「先删后建」，删除不随后续写入失败回滚，钥匙串授权被拒时旧 Key 已没了；现写入前先读出旧值、失败时回写，并把 `security` 的真实报错与退出码带进错误提示（此前只有一句「请在钥匙串访问中检查权限」）。注：授权被整体拒绝时回写同样会被拒，彻底堵住该窗口需改用 `SecItemUpdate` 原地更新
- 设置页保存时控件属性先在 UI 线程取快照再进后台线程，避免跨线程读取 Avalonia 控件
- 修复 **欠费时余额一直「刷新失败：币种 CNY 总余额非法」**：DeepSeek 在账户欠费（`is_available=false`）时返回负余额（如 `total_balance: "-0.71"`），`BalanceParser.TryAmount` 此前用 `value >= 0` 把负数判为脏数据，导致整次刷新失败、界面长期停留在上一次的缓存值；现允许负数，界面按 `¥-0.71` + 「账户不可用」正常展示
- 修复 **已删除的 OpenCode 账号仍占着菜单栏**：账号2 的 Key 被服务端撤销（401/403）时，此前只把卡片显示为 `--`、菜单栏状态项继续存在；现视为账号已删除，收回 Provider、隐藏（不再销毁）OC2 状态项并移除详情卡

### 工程化

- 新增 **全局未处理异常日志**：`AppDomain.UnhandledException` / `UnobservedTaskException` 落盘 `~/Library/Application Support/DeepSeekBalanceWidget/crash-YYYYMMDD.log`，进程崩溃不再无声消失，可回溯完整堆栈

## 0.5.0 — 2026-09-01

### 功能

- 新增 **Mac 胶囊置顶面板**：始终置顶桌面显示，支持 MiniMode 切换，DS/GPT/OC 区块水平排列
- 新增 **Mac 设置页标签式导航**：左侧 RadioButton 导航（监测项/预警/界面/通用），右侧 ScrollViewer 面板切换
- 新增 **Mac 报警功能**：`MacAlarmSound`（11种声音程序化合成）+ `ToastWindow`（弹窗+堆叠+淡入淡出）+ `MacToastService`
- 新增 **Mac 置顶功能**：NSWindowSetLevel P/Invoke 实现 macOS 原生窗口层级
- 新增 **Mac 单实例检测**：防止多开，Dock 图标点击还原已有窗口
- 新增 **Mac 监测项独立开关**：设置面板新增 DeepSeek / ChatGPT / WorkBuddy / OpenCode 四项独立开关，ApplyMonitoringVisibility 按配置显隐
- 新增 **Mac OpenRouter 额度监测**（胶囊 OR 单行 + 展开卡片）：读取官方 `GET https://openrouter.ai/api/v1/credits`（需 Management Key），返回 `data.total_credits/total_usage`，展示剩余/总额（`$剩余 / $总额` + 剩余% + 进度条三色 <20%红/≤70%橙/else绿）；胶囊 OR = 剩余% + $剩余 + 34px 进度条，菜单栏同步 `OR xx%`；接口/Provider/Parser/Formatter/Model 五文件已接入真实网络请求（401/403/非2xx/网络异常/JSON 异常分支全覆盖，Timeout 15s），设置卡片默认关闭、Key 存入钥匙串（`com.deepseekbalancewidget.openrouter-api-key`）
- 新增 **进度条 Track+Fill 模式**：替换 ProgressBar 控件，使用 Border 背景轨道 + 填充条，三色区间（<20%红, ≤70%橙, else绿）
- 新增 **设置页 Apply 按钮**：应用设置但不关闭窗口，支持实时预览
- 新增 **清除 Key 二次确认**：红色样式 + 确认对话框，防止误触
- 新增 **GPT/OpenCode 预警分离**：独立配置 5h/周/月额度档位
- 新增 **高峰时段移至 DS 余额旁**：PeakText 在 BalanceRow 区域
- 新增 **resolve-model 脚本**：模型路由解析，读取 MODEL_ROUTING_REGISTRY.yaml
- 新增 **模型分工治理文件**：8 角色模型配置写入 MODEL_ROUTING_REGISTRY.yaml区块

- 新增 **OpenCode Go 额度监测**（胶囊右侧区块，替代原 WB 占位）：读取官方用量接口 `zen/go/v1/usage`，5 小时 / 周 / 月三窗口全量展示；胶囊内每行 = 剩余百分比 + 距恢复倒计时 + 最右进度条（剩余 ≥60% 绿 / 30-59% 黄 / <30% 红），「OC」标签跨三行垂直居中；展开卡片新增「OpenCode Go 额度」表格（剩余% / 美元估算 / 恢复时间点 / 倒计时），美元金额按 Go 套餐固定限额（$12 / $30 / $60）换算估算
- 新增 **监测项独立开关**：设置「监测项」分组内 DeepSeek / ChatGPT / WorkBuddy / OpenCode 四项均可独立启停，关闭的区块从胶囊与详细面板隐藏且不占宽度；WorkBuddy 默认关闭
- OpenCode 额度预警：与 ChatGPT 额度预警共用同一套档位（默认 20% / 10%）与恢复判定配置，5h / 周 / 月三窗口独立跟踪
- 警报系统重构：预警类通知改为**常驻弹窗 + 循环警报声**，需点击「知道了」关闭（持续模式），或限时 ≥10 秒后自动关闭（限时模式）；警报窗位置可配置（右上角 / 右侧中间 / 右下角，默认右上角），多警报堆叠、点掉最后一张才停声；恢复类通知保持自动消失不响声
- 设置新增「预警行为」分组：警报声开关、持续 / 限时模式、警报窗位置、试听警报
- 设置窗口每个监测项统一「开关 + API Key（如需）+ 测试连接」行式布局，测试结果行内显示（绿 ✓ / 红 ✗）；OpenCode Key 支持手动粘贴（macOS 登录钥匙串加密）或自动读本机 `~/.local/share/opencode/auth.json`
- 新增 ChatGPT 额度预警：5 小时额度与周额度各自独立跟踪剩余百分比，降到设定档位（默认 20% / 10%）时弹窗预警；一次刷新若跨过多个档位只播报最低档，同一档位每个周期仅提醒一次
- **GPT 额度恢复提醒改为「只要恢复即提醒」**：5 小时额度与周额度只要回到恢复线（默认 100%，即满额）就弹窗，不再要求本周期先触发过低量预警；恢复判定阈值默认 95 → 100，正文写明具体账号与窗口
- 恢复播报与低量预警在同一刷新内互斥（恢复优先），避免「仅剩 X%」与「已恢复」自相矛盾地同时弹出
- **Mac 端接入 ChatGPT / OpenCode 额度预警弹窗**（此前仅 Windows 接线，Mac 评估器已编译但主窗口未调用）：低量预警走橙色常驻弹窗 + 警报声，恢复走绿色标题通知、自动消失；恢复通知新增**绿色环形恢复箭头图标 + 绿色边框**（用户确认的视觉方案），与橙色预警一眼可辨
- **OpenCode 不再播报额度恢复**（消耗量小，无需打扰），仅保留低量预警
- **胶囊 GPT 区块边框状态色**：任一启用窗口剩余 ≤ 最高预警档位 → 橙色边框（≤ 最低档位 → 红色）；恢复提醒触发后绿色高亮 2 分钟，到期自动恢复默认边框

### 优化

- 提醒可见性增强：预警用橙色、恢复用绿色，淡入淡出动画，多通知堆叠不相互覆盖
- 修复 `ShowToastNotifications` 配置项此前从未被读取的死代码问题；余额告警与额度预警现在都遵守该开关

### 工程化

- 新增平台无关的 `CodexQuotaAlertEvaluator`，按「账号 + 窗口」独立维护跟踪状态，不绑定具体 UI 框架，判定逻辑只此一份
- 新增 `CodexQuotaAlertEvaluatorTests`（15 个用例），覆盖首次基线不打扰、分档去重、跨档只播最低档、恢复后重新启用、周额度独立跟踪、冷却吸收抖动、恢复与预警互斥等场景
- 新增共享层 `OpenCodeUsageProvider` / `OpenCodeUsageParser` / `OpenCodeUsageFormatter` / `OpenCodeQuotaAlertEvaluator`（macOS 已接入编译）
- 新增 `OpenCodeUsageParserTests` 与 `OpenCodeQuotaAlertEvaluatorTests`（覆盖官方响应解析、多形态 resetsAt、auth.json Key 解析、基线 / 分档去重 / 恢复判定 / 三窗口独立跟踪）
- OpenCode 内嵌警报音由运行时合成的双音 WAV 循环播放，不依赖外部音频文件
- 胶囊 GPT / OC 区块垂直居中对齐；迷你胶囊改为**宽度自适应内容**，贴边 / 最小化 / 关闭按钮自然贴最右，不再出现右侧空白
- OpenCode 失败状态可见化：未配置 Key / Key 无效 / 网络失败直接显示在区块标题与胶囊标签上（橙色），不再只藏在 tooltip
- 设置窗口重构为**左侧标签导航 + 右侧内容**（监测项 / 预警 / 界面 / 通用 四分区），固定尺寸不再超出屏幕；底部「清除 Key / 保存」跨标签常驻；修复监测项行内 Key 输入框遮挡文字的问题
- 警报声风格扩展为 11 种：短鸣、递升、递降、清脆、铃声、叮咚、快速、慢脉冲、柔和、标准、急促；设置 → 预警行为 → 警报声风格，默认标准
- 胶囊 OC 进度条缩短为固定 28px 宽，贴合内容不再显得过长
- 设置页全面改版为 **Ant Design 风格**：左侧导航竖栏（选中项蓝色竖条 + 浅蓝高亮）+ 右侧卡片内容区，主色 #1677FF，统一圆角输入框 / 主次按钮 / 卡片分组；监测项改为 2×2 卡片布局，测试按钮独立成行不再被 Key 输入框遮挡
- 迷你胶囊进一步精简：删除胶囊内刷新时间（展开卡片「上次刷新」保留），操作区收为单行、四键垂直居中，胶囊右侧留白收紧（右内边距 12→4、右外边距 12→8），OC 与按钮组间距固定 8px

### 修复

- 修复胶囊 OC 区块标签「OC」被 Border 左右内边距（各 6px）吃掉 12px、第 0 列实际可见宽度不足而不可见的问题：第 0 列宽 20→26，留出 14px 显示空间
- 修复胶囊 GPT 区块倒计时列用 `Width=Auto` 在 StackPanel 父容器下被拉伸、短文本（如「6d」）右侧出现大空白的问题：改为按内容精准固定列宽（账号 12 / 5h 额度 32 / 5h 倒计时 42 / 周额度 32 / 周倒计时 46），Border 内边距由 4 收紧到 2
- 修复额度预警因用量数据短暂回弹到恢复线（≥95%）即误触发 `ResetCycle` 清空 `NotifiedThresholds`，导致同一档位（如 10%）反复弹窗的问题；恢复播报不再挂靠 `ResetCycle`，改为「上次低于恢复线 → 本次回到恢复线及以上」的跃迁判定，满额时反复刷新不会重复播报（OpenCode / ChatGPT 共用 evaluator，已补对应测试）

## 0.4.0 — 2026-08-27

### 功能

- ChatGPT 恢复 5 小时滚动额度窗口后，展开卡片 GPT 区块改为**对齐表格**：两个账号上下两行，账号名称 / 5h 额度 / 5h 恢复 / 周额度 / 周恢复五列逐列对齐，每格含剩余百分比与重置倒计时
- 迷你胶囊改为**单行宽布局**：DeepSeek 余额（最左）｜GPT 区块（两个账号上下两行、四列对齐：5h 额度 / 5h 倒计时 / 周额度 / 周倒计时）｜WorkBuddy 占位（最右），DS 与 WB 单行区块垂直居中，贴边按钮固定最右
- 新增设置项「胶囊区块顺序」：上移 / 下移调整 DeepSeek / ChatGPT / WorkBuddy / tE 的胶囊渲染顺序（持久化到 `AgentOrder`），保存后立即生效
- WorkBuddy 未接入前胶囊显示灰显占位「WB --」，为后续多 Agent 面板接入预留区块
- macOS 窗口同步显示每窗口剩余、重置时间与倒计时
- 新增紧凑恢复列与单窗口行格式化方法，解析层保持双窗口兼容

## 0.3.0 — 2026-08-15

### 功能

- 新增 macOS 双平台支持：基于 Avalonia 的原生 `.app` 菜单栏应用，支持 Apple Silicon（M 系列）与 Intel Mac
- macOS 版菜单栏实时显示 DeepSeek 余额、ChatGPT Plus 用量百分比和北京时间峰值时段
- macOS 版 API Key 保存到登录钥匙串，支持登录时自动启动
- 新增 macOS 一键发布与安装脚本（`scripts/publish-macos.sh`、`scripts/install-macos.sh`）
- 新增本机 Codex 用量消耗速率追踪（`CodexConsumptionRateTracker`）
- GitHub Releases 发布 macOS（arm64 / x64）安装包（Windows 安装包现由 Windows 独立仓库发布）

### 工程化

- 新增 macOS 构建与发布脚本（`publish-macos.sh` / `install-macos.sh`）；Release 工作流构建 macOS 安装包（Windows 端构建现归 Windows 独立仓库）

## 0.2.0 — 2026-08-06

### 功能

- 恢复 ChatGPT Plus 额度监测，每分钟显示用量窗口剩余百分比和重置时间
- 设置中提供额度监测开关、字号和字重选项
- 新增贴边自动隐藏，可从桌面四边悬停唤回
- 清理旧运行产物，统一新版发布入口

## 0.1.0 — 2026-07-31

首个可用版本。

### 功能

- DeepSeek API CNY/USD 余额查询与严格响应解析
- 总余额、充值余额、有效赠送余额和余额变化显示
- 低余额及异常下降告警
- 完整卡片和迷你胶囊模式
- 迷你模式拖动保持迷你状态，双击展开
- 系统托盘、置顶、隐藏、退出和开机自启
- 北京时间峰值时段参考
- 多显示器边界恢复和窗口位置记忆
- 加密保存 API Key（当时为 Windows DPAPI CurrentUser；macOS 端现为登录钥匙串）
- Mock 场景及 31 项自动化测试

### 工程化

- 标准化 `src`、`tests`、`docs`、`artifacts` 和 `scripts` 目录
- 一键启动和单文件自包含发布脚本
- GitHub Actions 构建、测试和标签发布
