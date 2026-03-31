using System;
using System.Collections.Generic;
using WebTraffic.Common.Enums;

namespace WebTraffic.Logging
{
    /// <summary>
    /// 线程安全的内存环形缓冲区。
    /// 容量满时自动覆盖最旧条目，UI 面板通过 <see cref="NewLogEntry"/> 事件实时接收新日志。
    /// </summary>
    public sealed class MemoryRingBuffer
    {
        // ── 单例 ────────────────────────────────────────────────────────

        private static readonly MemoryRingBuffer _instance = new MemoryRingBuffer(1000);

        /// <summary>全局共享缓冲区实例，UI 和 Logger 均访问此实例。</summary>
        public static MemoryRingBuffer Instance { get { return _instance; } }

        // ── 事件 ────────────────────────────────────────────────────────

        /// <summary>
        /// 每当有新日志写入时触发。
        /// UI 层订阅此事件时，必须通过 <c>Invoke / BeginInvoke</c> 切换到 UI 线程。
        /// </summary>
        public event EventHandler<LogEntry> NewLogEntry;

        // ── 状态 ────────────────────────────────────────────────────────

        private readonly int _capacity;
        private readonly LogEntry[] _buffer;
        private int _head;   // 下一次写入的位置
        private int _count;
        private readonly object _lock = new object();

        // ── 构造 ────────────────────────────────────────────────────────

        public MemoryRingBuffer(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException("capacity", "容量必须大于 0");

            _capacity = capacity;
            _buffer   = new LogEntry[capacity];
        }

        // ── 公开方法 ────────────────────────────────────────────────────

        /// <summary>
        /// 写入一条日志，满时覆盖最旧条目，并触发 <see cref="NewLogEntry"/> 事件。
        /// </summary>
        public void Write(AppLogLevel level, string message, Exception ex = null)
        {
            var entry = new LogEntry
            {
                Timestamp     = DateTime.Now,
                Level         = level,
                Message       = message ?? string.Empty,
                ExceptionText = ex != null ? FormatException(ex) : string.Empty,
            };

            lock (_lock)
            {
                _buffer[_head] = entry;
                _head          = (_head + 1) % _capacity;
                if (_count < _capacity) _count++;
            }

            // 事件触发在锁外，避免死锁
            var handler = NewLogEntry;
            if (handler != null)
                handler(this, entry);
        }

        /// <summary>
        /// 返回缓冲区内所有条目（按时间从旧到新）。
        /// </summary>
        public List<LogEntry> GetAll()
        {
            var result = new List<LogEntry>();
            lock (_lock)
            {
                if (_count == 0)
                    return result;

                // 环形缓冲区的起始读取位置：
                // 当缓冲区已满时，最旧条目在 _head；未满时从 0 开始
                int start = _count < _capacity ? 0 : _head;
                for (int i = 0; i < _count; i++)
                {
                    result.Add(_buffer[(start + i) % _capacity]);
                }
            }
            return result;
        }

        /// <summary>当前已存储的条目数。</summary>
        public int Count
        {
            get { lock (_lock) { return _count; } }
        }

        /// <summary>清空缓冲区。</summary>
        public void Clear()
        {
            lock (_lock)
            {
                Array.Clear(_buffer, 0, _capacity);
                _head  = 0;
                _count = 0;
            }
        }

        // ── 私有 ────────────────────────────────────────────────────────

        private static string FormatException(Exception ex)
        {
            if (ex == null) return string.Empty;

            var inner = ex.InnerException != null
                ? "\r\nInnerException: " + ex.InnerException.Message
                : string.Empty;

            return string.Format("{0}: {1}{2}\r\n{3}",
                ex.GetType().Name, ex.Message, inner, ex.StackTrace);
        }
    }
}
