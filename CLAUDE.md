# Web Traffic Pro — 项目开发规范

## 项目概述
桌面工具软件：通过 Selenium Headless 浏览器 + 代理池，
模拟真实用户经由搜索引擎关键词访问目标网站，
实现 Web 流量模拟与统计分析，数据本地 SQLite 存储。

---

## 技术栈

| 类别 | 技术 | 版本 |
|------|------|------|
| 框架 | .NET Framework | 4.8 |
| 语言 | C# | 7.3 |
| UI | DevExpress WinForms | v25.1 |
| 浏览器自动化 | Selenium WebDriver | 最新稳定版 |
| 数据库 | SQLite | Microsoft.Data.Sqlite |
| 序列化 | Newtonsoft.Json | 最新稳定版 |
| 日志 | NLog | 最新稳定版 |
| 代理协议 | HTTP / HTTPS / SOCKS5 | — |

---

## 解决方案结构
```
WebTrafficPro.sln
├── WebTraffic.UI          # WinForms 启动项目
├── WebTraffic.Core        # 接口 + 实体 Model（纯 C#，无第三方依赖）
├── WebTraffic.Engine      # 业务引擎
│   ├── Selenium/          # 浏览器自动化、关键词访问
│   ├── Proxy/             # 代理导入、验证、轮换
│   ├── Task/              # 并发调度、定时任务
│   └── Fingerprint/       # UA 库管理、浏览器指纹
├── WebTraffic.Data        # SQLite Repository（所有数据库访问）
├── WebTraffic.Logging     # NLog 封装
└── WebTraffic.Common      # 常量 / 枚举 / 扩展方法 / 工具类
```

**依赖方向（单向，禁止反向引用）：**
```
UI → Core ← Engine → Data    → Common
                   → Logging → Common
UI → Common
```

---

## 当前实现状态（2026-03-16）

### ✅ 已完整实现
- WebTraffic.Common：全部枚举 + 扩展方法
- WebTraffic.Core：全部 Model（含 CookieStore）+ 接口（ITaskService / IProxyService /
  IFingerprintService / IScheduleService / IStatsService / IAppLogger）
- WebTraffic.Data：DatabaseInitializer（8张表，含 cookie_store）+ 全部 Repository（含 CookieStoreRepository）
- WebTraffic.Logging：NLog 封装 + 内存环形缓冲区
- WebTraffic.Engine/Task：TaskRunner + ScheduleRunner + CronExpression
- WebTraffic.Engine/Proxy：ProxyService（解析 / 验证 / 轮换）
- WebTraffic.Engine/StatsService：IStatsService 实现
- WebTraffic.Engine/Fingerprint：FingerprintOptionsBuilder（含移动端/平板分辨率）+
  UAProfileCache + FingerprintService（RandomUA / RotatePerSession 已修复）
- WebTraffic.Engine/Selenium：DriverManager + KeywordVisitor（完整行为）
  - Cookie 持久化读写（profile_id 关联，过期跳过）
  - DOM 注入（搜索结果未找到时，IsInjected = true）
  - 正态分布停留时长（Box-Muller）
  - 随机滚动（分段平滑 + 随机回滚）
  - 鼠标随机移动（Actions.MoveToElement）
  - 页面交互深度（悬停图片、划选文字、hover 菜单）
  - 退出行为模拟（后退 / 导航其他地址）
  - Organic / Social / Email / Direct 来源模拟 + Referer 记录
- WebTraffic.UI：MainForm + 全部 7 个页面（TaskPage / SchedulePage /
  ProxyPage / FingerprintPage / StatsPage / LogPage / SettingsPage）
  - TaskPage：开始/暂停/停止、实时看板（请求量/成功/失败/速率）、日志表格追加、来源权重配置
  - ProxyPage：批量导入、批量验证、删除、统计看板绑定
  - FingerprintPage：配置保存/加载、UA 卡片库筛选渲染、应用配置
  - SchedulePage：定时任务增删改、启用/禁用、立即触发、执行日志面板
  - StatsPage：ChartControl 趋势折线图 + 来源饼图、自动刷新 Timer
  - LogPage：订阅 MemoryRingBuffer.NewLogEntry、级别筛选、导出
  - SettingsPage：读写 AppSettingsRepository、模板保存/加载、License 激活



---

## 功能模块

### 1. 关键词流量任务（TaskPage）
- 配置：目标 URL、关键词列表（每行一个）、访问次数、
  并发线程数、最小/最大延迟（秒）、来源分配权重
- 操作：开始 / 暂停 / 停止 / 导出日志 / 加载模板
- 实时看板：总请求 / 成功 / 失败 / 速率（req/s）/ 已用时
- 日志表格列：时间 / 目标URL / Referer / 代理IP / 耗时 / 状态

### 2. 定时任务（SchedulePage）
- 列表列：任务名称 / 执行时间 / 重复规则 / 上次执行 / 状态 / 操作
- 底部执行日志面板

### 3. 代理管理（ProxyPage）
- 批量导入支持格式：
  - `ip:port`
  - `ip:port:user:pass`
  - `socks5://ip:port`
- 代理列表列：代理地址 / 类型 / 国家 / 延迟 / 匿名度 / 状态
- 统计看板：总代理 / 可用 / 失效 / 平均延迟

### 4. 指纹 & UA 管理（FingerprintPage）
左侧反检测配置面板 + 右侧 UA 卡片库。

**左侧配置面板（开关控件，3 个分组）：**

浏览器标识：
- 隐藏 navigator.webdriver（✅ 已实现）
- 注入真实 Chrome 插件列表（✅ 已实现）
- 禁用自动化控制提示（✅ 已实现）
- 禁用 WebRTC 泄露（✅ 已实现）

图形 & 环境：
- Canvas 指纹噪声注入（✅ 已实现，含 toDataURL / toBlob / getImageData 拦截）
- WebGL 指纹混淆（✅ 已实现）
- 随机屏幕分辨率（✅ 已实现，桌面 / 移动端 / 平板均已适配）
- 随机时区（✅ 已实现）
- 随机 Accept-Language（✅ 已实现）

UA 轮换：
- 随机轮换 UA（✅ 已实现，正确读取 RandomUA 标志）
- 每次会话更换（✅ 已实现，RotatePerSession 会话内固定，跨会话更换）
- 仅使用已选中 UA（✅ 已实现）

**右侧 UA 卡片库：**
- 筛选 Chip（多选）：桌面 / 移动端 / 平板 / Chrome / Firefox / Edge / Safari
- 操作按钮：全选 / 清空选中 / 导入自定义 UA / 应用配置
- 统计：已选 N / 总数
- UA 卡片字段：名称 / UA字符串 / 设备类型标签 / 使用频率进度条

### 5. 统计报告（StatsPage）
- 大数字卡片：总请求量 / 成功率 / 代理消耗 / 运行时长
- 图表：请求量趋势折线图 / 来源分布

### 6. 详细日志（LogPage）
- 列：时间 / 级别（INFO / WARN / ERROR）/ 消息

### 7. 设置 & 账户（SettingsPage）
- 基础配置：全局超时、最大重试次数、最大并发线程数、
  DNS超时、Keep-Alive、忽略SSL错误、gzip压缩
- Selenium 配置：Chrome路径、ChromeDriver路径（留空自动下载）、
  Headless 模式开关（默认开启）、禁用图片加载、
  禁用GPU加速、禁用扩展程序、每实例最大内存（MB）
- 任务模板：保存 / 加载配置模板（Newtonsoft.Json 序列化）
- License：本地激活码验证
- 更新：检查更新提示、更新日志展示

---

## WebTraffic.Core 规范

只定义接口和实体，**不引用任何第三方库**。

**核心实体：**
```csharp
public class TaskConfig
{
    public int Id { get; set; }
    public string TargetUrl { get; set; }
    public string Keywords { get; set; }       // 换行分隔
    public int VisitCount { get; set; }
    public int ThreadCount { get; set; }
    public int MinDelay { get; set; }          // 秒
    public int MaxDelay { get; set; }          // 秒
    public string SourceWeights { get; set; }  // JSON 序列化
}

public class ExecutionLog
{
    public int Id { get; set; }
    public DateTime ExecutedAt { get; set; }
    public string ProxyIp { get; set; }
    public string TargetUrl { get; set; }
    public string Referer { get; set; }
    public int ElapsedMs { get; set; }
    public int HttpStatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public bool IsInjected { get; set; }       // DOM 注入标记
}

public class UAProfile
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string UserAgent { get; set; }
    public DeviceType DeviceType { get; set; }
    public string BrowserType { get; set; }    // Chrome / Firefox / Edge / Safari
    public bool IsSelected { get; set; }
}

public class FingerprintConfig
{
    // 浏览器标识
    public bool HideWebDriver { get; set; }
    public bool InjectPluginList { get; set; }
    public bool DisableAutomationBar { get; set; }
    public bool DisableWebRTC { get; set; }
    // 图形 & 环境
    public bool CanvasNoise { get; set; }
    public bool WebGLSpoof { get; set; }
    public bool RandomResolution { get; set; }
    public bool RandomTimezone { get; set; }
    public bool RandomLanguage { get; set; }
    // UA 轮换
    public bool RandomUA { get; set; }
    public bool RotatePerSession { get; set; }
    public bool UseSelectedOnly { get; set; }
}
```

**枚举（WebTraffic.Common）：**
```csharp
// 避免与系统命名空间冲突
public enum ProxyType    { Http, Https, Socks5 }
public enum ProxyStatus  { Active, Inactive, Unchecked }
public enum JobStatus    { Idle, Running, Paused, Stopped }  // 非 TaskStatus
public enum DeviceType   { Desktop, Mobile, Tablet }
public enum AppLogLevel  { Info, Warn, Error }               // 非 LogLevel
```

---

## WebTraffic.Engine 规范

### Selenium 模块

**Headless 模式（读取 app_settings selenium.headless）：**
```csharp
if (useHeadless)
    options.AddArgument("--headless");
else
    options.AddArgument("--window-position=-32000,-32000");
// 窗口尺寸由 FingerprintOptionsBuilder 统一设置
// DriverManager 不得硬编码任何 --window-size 参数
options.AddArgument("--disable-gpu");
options.AddArgument("--no-sandbox");
options.AddArgument("--disable-dev-shm-usage");
```

**KeywordVisitor 已实现行为：**
- 按 SourceWeights 加权随机选取搜索引擎（Google / Bing）
- 随机选取关键词，打开搜索引擎首页
- 等待搜索框出现，输入关键词，等待结果区域
- 查找含目标域名的链接并点击（含 JS fallback）
- 等待目标页加载完成（document.readyState）
- Direct 模式直接导航
- Organic / Social / Email / Direct 来源模拟，Referer 记录到 ExecutionLog.Referer
- DOM 注入：第一页未找到目标 URL 时，通过 JS 插入伪造条目，IsInjected = true
- Cookie 持久化：任务结束写入 cookie_store，下次同 profile_id 自动加载，过期跳过
- 停留时长：Box-Muller 正态分布（均值 = (min+max)/2，标准差 = (max-min)/6）
- 随机滚动（分段平滑，JS executeScript，随机回滚）
- 鼠标随机移动（Actions.MoveToElement）
- 页面交互深度：悬停图片、划选文字、hover 菜单项
- 视口检测：操作前通过 getBoundingClientRect 确认元素在可视区
- 退出行为：按权重随机选择后退 / 导航到其他地址
- 构造 ExecutionLog（成功/失败），异常时必须 QuitDriver

**KeywordVisitor 待实现行为：**
- Referer 通过 CDP Network.setExtraHTTPHeaders 实际注入请求头（目前仅记录字段）
- 多标签页：以可配置概率新标签打开，停留后关闭切回
- 等待 GA 事件：JS 监听 dataLayer.push，确认 pageview 触发后继续

**全程约束：禁止 Thread.Sleep，全部用 WebDriverWait 或 Task.Delay**
（注：Actions 链式调用内部允许极短 Thread.Sleep(5ms)）

### Proxy 模块（✅ 已完整实现）

ProxyService 实现 IProxyService，包含：
- ProxyParser：解析三种格式（ip:port / ip:port:user:pass / socks5://ip:port）转 ProxyInfo
- ProxyChecker：请求 https://api.ipify.org，超时 10s，最大并发 20，
  失败标记 ProxyStatus.Inactive
- ProxyRotator：顺序轮询 / 随机两种策略（proxy.rotate_mode 配置）

### Task 模块（✅ 已完整实现）
- SemaphoreSlim 控制并发上限
- CancellationTokenSource 实现停止
- ManualResetEventSlim 实现暂停/恢复
- System.Threading.Timer 定时触发
- event 回调实时通知 UI

### Fingerprint 模块

**UA 与分辨率联动（FingerprintOptionsBuilder）：**

BuildOptions 必须接收 UAProfile 参数，
窗口尺寸与 DeviceType 强制匹配：

- Desktop：
  固定 1920×1080 或从以下随机：
  { "1920,1080", "1366,768", "1440,900", "1280,800", "1536,864" }

- Mobile：
  固定 390×844 或从以下随机：
  { "390,844", "375,812", "414,896", "360,800", "393,851" }
  必须同时设置 mobileEmulation 参数

- Tablet：
  固定 768×1024 或从以下随机：
  { "768,1024", "820,1180", "800,1280", "601,962" }

**已修复：**
- CanvasNoise 已补齐 toBlob / getImageData 拦截路径
- RandomUA = false 时正确固定 UA，不再始终随机
- RotatePerSession：会话内固定同一 UA，跨会话更换（已实现）

**app_settings key 格式：**
```
fingerprint.hide_webdriver
fingerprint.inject_plugin_list
fingerprint.disable_automation_bar
fingerprint.disable_webrtc
fingerprint.canvas_noise
fingerprint.webgl_spoof
fingerprint.random_resolution
fingerprint.random_timezone
fingerprint.random_language
fingerprint.random_ua
fingerprint.rotate_per_session
fingerprint.use_selected_only
selenium.headless
proxy.rotate_mode
```

---

## WebTraffic.Data 规范

所有 SQL 封装在 Repository 层，**禁止** Engine 或 UI 层直接写 SQL。

**Repository 列表：**
```
AppSettingsRepository    ✅
TaskConfigRepository     ✅
ProxyRepository          ✅
ExecutionLogRepository   ✅
ScheduleJobRepository    ✅
UAProfileRepository      ✅
CookieStoreRepository    ✅
```

**数据表（DatabaseInitializer）：**
```
task_config      ✅
task_template    ✅
schedule_job     ✅
proxy_list       ✅
execution_log    ✅（含 is_injected 列）
ua_profile       ✅
app_settings     ✅
cookie_store     ✅（字段：id / profile_id / domain / cookie_json / updated_at）
```

**规则：**
- using 管理 SqliteConnection，确保及时释放
- 写操作使用事务
- 批量操作使用事务批处理，禁止逐条提交
- 数据库路径从 AppSettingsRepository 读取，禁止硬编码
- execution_log 分页查询，禁止全量加载

**数据库默认路径：**
```
C:\Users\{User}\AppData\Roaming\WebTrafficPro\data.db
```

---

## WebTraffic.Logging 规范（✅ 已完整实现）

对外接口：
```csharp
void Info(string message);
void Warn(string message);
void Error(string message, Exception ex = null);
```

- NLog 输出到本地文件 + 内存环形缓冲区
- UI 订阅 NewLogEntry 事件，不直接依赖 NLog
- 路径：`AppData\Roaming\WebTrafficPro\logs\`
- 命名：`webtraffic-{yyyy-MM-dd}.log`
- 单文件最大 10MB，保留 30 天

---

## DevExpress v25.1 UI 规范

- 所有 DevExpress 问题必须使用 **dxdocs MCP server** 查询 v25.1 文档
- 查询时注明控件全名和版本：
  `"DevExpress v25.1 WinForms GridControl 分组排序"`
- 禁止原生 WinForms 控件替代 DevExpress 控件
- 弹窗统一 `XtraMessageBox`，禁止 `MessageBox.Show`
- 长时间操作（>300ms）必须在后台线程执行

**常用控件对照：**
```
数据表格    → GridControl + GridView
选项卡      → XtraTabControl
工具栏      → BarManager / RibbonControl
进度条      → ProgressBarControl / MarqueeProgressBarControl
弹窗        → XtraMessageBox
侧边导航    → NavBarControl / AccordionControl
图表        → ChartControl（DevExpress.XtraCharts）
开关控件    → ToggleSwitch
```

---

## C# 7.3 编码规范

**禁止使用 C# 8.0+ 特性：**
- `string?`（nullable reference types）
- Records
- Switch expression（`x switch { ... }`）
- Default interface methods
- `using` declarations（无大括号版本）
- Range / Index（`^1`、`1..3`）

**命名规范：**
```csharp
// 枚举（注意避开系统命名冲突）
JobStatus / AppLogLevel

// 接口
ITaskService / IProxyService

// 异步方法
Task<List<ProxyInfo>> GetActiveProxiesAsync()

// 私有字段
private readonly IProxyService _proxyService;

// app_settings key
"selenium.headless"
"fingerprint.hide_webdriver"
```

---

## 错误处理规范

- Selenium 异常捕获后写 ExecutionLog + NLog，不中断任务
- 代理失败标记 ProxyStatus.Inactive，跳过继续
- 用户可见错误用 XtraMessageBox 提示
- 系统错误写日志，界面显示"操作失败，请查看日志"
- **禁止空 catch 块**
```csharp
try
{
    await ExecuteTaskAsync(config, token);
}
catch (OperationCanceledException)
{
    _logger.Info("任务已停止");
}
catch (Exception ex)
{
    _logger.Error("任务执行异常", ex);
    OnTaskError?.Invoke(this, ex.Message);
}
```

---

## 文件目录规范
```
C:\Users\{User}\AppData\Roaming\WebTrafficPro\
├── data.db
├── config.json
├── logs\
│   └── webtraffic-yyyy-MM-dd.log
└── templates\
```

---
