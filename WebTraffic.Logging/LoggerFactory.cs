using System;
using System.IO;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using WebTraffic.Core.Interfaces;

namespace WebTraffic.Logging
{
    /// <summary>
    /// 负责在代码中构建 NLog 配置并提供 <see cref="IAppLogger"/> 工厂方法。
    /// 应用启动时调用一次 <see cref="Initialize"/>，之后用 <see cref="Create"/> 获取 logger。
    /// </summary>
    public static class LoggerFactory
    {
        private static volatile bool _initialized;
        private static readonly object _initLock = new object();

        // ── 公开方法 ────────────────────────────────────────────────────

        /// <summary>
        /// 初始化 NLog 配置：
        /// <list type="bullet">
        ///   <item>文件 Target — 按日期滚动，单文件最大 10 MB，保留 30 天</item>
        ///   <item>MemoryRingBuffer Target — 同步写入内存环形缓冲区</item>
        /// </list>
        /// 可安全多次调用（幂等）。
        /// </summary>
        /// <param name="logDirectory">
        /// 日志目录，null 时使用默认路径
        /// <c>%APPDATA%\WebTrafficPro\logs\</c>。
        /// </param>
        public static void Initialize(string logDirectory = null)
        {
            if (_initialized) return;

            lock (_initLock)
            {
                if (_initialized) return;

                var logDir = string.IsNullOrEmpty(logDirectory)
                    ? Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "WebTrafficPro",
                        "logs")
                    : logDirectory;

                if (!Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);

                // ── 注册自定义 Target ────────────────────────────────
                LogManager.Setup().SetupExtensions(ext =>
                    {
                        ext.RegisterTarget<MemoryRingBufferTarget>("MemoryRingBuffer");
                    });

                // ── 文件 Target ──────────────────────────────────────
                var fileTarget = new FileTarget("file")
                {
                    FileName = Path.Combine(logDir, "webtraffic-${date:format=yyyy-MM-dd}.log"),
                    Layout = "${longdate} [${level:uppercase=true:padding=-5}] ${logger:shortName=true} ${message}${onexception:${newline}${exception:format=toString}}",
                    ArchiveEvery = FileArchivePeriod.Day,
                    ArchiveFileName = Path.Combine(logDir, "webtraffic-{#}.log"),
                    ArchiveNumbering = ArchiveNumberingMode.Date,
                    ArchiveDateFormat = "yyyy-MM-dd",
                    MaxArchiveFiles = 30,
                    ArchiveAboveSize = 10 * 1024 * 1024,   // 10 MB
                    Encoding = System.Text.Encoding.UTF8,
                    KeepFileOpen = true,
                    ConcurrentWrites = false,
                    AutoFlush = true,
                };

                // 使用异步包装，防止文件 I/O 阻塞调用线程
                var asyncFileTarget = new AsyncTargetWrapper("asyncFile", fileTarget)
                {
                    OverflowAction = AsyncTargetWrapperOverflowAction.Grow,
                    QueueLimit = 10000,
                };

                // ── 内存环形缓冲区 Target ────────────────────────────
                var memTarget = new MemoryRingBufferTarget
                {
                    Name = "memRingBuffer",
                    Layout = "${message}",   // 原始消息，ExceptionText 由 Write() 内部格式化
                };

                // ── 组合配置 ─────────────────────────────────────────
                var config = new LoggingConfiguration();
                config.AddTarget(asyncFileTarget);
                config.AddTarget(memTarget);

                // 文件：Info 及以上
                config.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, asyncFileTarget, "*");
                // 内存：Info 及以上
                config.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, memTarget, "*");

                LogManager.Configuration = config;
                LogManager.ReconfigExistingLoggers();

                _initialized = true;
            }
        }

        /// <summary>
        /// 获取指定名称的 <see cref="IAppLogger"/>。
        /// 同一名称多次调用返回不同包装实例，但共享底层 NLog Logger（NLog 内部缓存）。
        /// </summary>
        public static IAppLogger Create(string name = "WebTrafficPro")
        {
            EnsureInitialized();
            return new AppLogger(LogManager.GetLogger(name));
        }

        /// <summary>
        /// 按调用方类型名称创建 Logger，方便区分来源模块。
        /// </summary>
        public static IAppLogger Create(Type callerType)
        {
            EnsureInitialized();
            return new AppLogger(LogManager.GetLogger(callerType.FullName));
        }

        /// <summary>
        /// 刷新所有 Target 缓冲区并关闭 NLog（应用退出时调用）。
        /// </summary>
        public static void Shutdown()
        {
            LogManager.Flush();
            LogManager.Shutdown();
        }

        // ── 私有 ────────────────────────────────────────────────────────

        private static void EnsureInitialized()
        {
            if (!_initialized)
                Initialize();
        }
    }
}
