using System;
using System.IO;
using System.Windows.Forms;

namespace EquipmentMonitorDay1
{
    /// <summary>
    /// 简易日志类（替代 NLog）
    /// 按天切割、自动保留 30 天
    /// </summary>
    public static class AppLogger
    {
        private static readonly string _logDir = Path.Combine(Application.StartupPath, "Logs");

        /// <summary>
        /// 写一条信息日志
        /// </summary>
        public static void Info(string message)
        {
            WriteLog("INFO", message);
        }

        /// <summary>
        /// 写一条错误日志
        /// </summary>
        public static void Error(string message, Exception ex = null)
        {
            string msg = message;
            if (ex != null)
                msg += $"\n异常：{ex.Message}\n 堆栈：{ex.StackTrace}";

            WriteLog("ERROR", msg);
        }

        /// <summary>
        /// 写一条警告日志
        /// </summary>
        public static void Warning(string message)
        {
            WriteLog("WARNING", message);
        }

        /// <summary>
        /// 核心写日志方法
        /// 每天一个文件，自动切割
        /// </summary>
        private static void WriteLog(string level, string message)
        {
            try
            {
                // 确保日志目录存在
                Directory.CreateDirectory(_logDir);

                // 日志文件名：2026-5-23.log
                string fileName = $"{DateTime.Now:yyyy-MM-dd}.log";
                string filePath = Path.Combine(_logDir, fileName);

                // 写日志内容：时间 + 级别 + 消息
                string line = $"{DateTime.Now:HH:mm:ss}[{level}]{message}";

                // 追加写入日志文件
                File.AppendAllText(filePath, line + Environment.NewLine);

                // 删除 30 天前的日志文件
                CleanupOldLogs();
            }
            catch (Exception ex)
            {
                // 如果写日志失败了，直接忽略，避免二次异常
            }
        }

        /// <summary>
        /// 删除 30 天前的日志文件
        /// </summary>
        private static void CleanupOldLogs()
        {
            try
            {
                var dir = new DirectoryInfo(_logDir);
                if (!dir.Exists)
                    return;

                foreach (var file in dir.GetFiles("*.log"))
                {
                    if (file.CreationTime < DateTime.Now.AddDays(-30))
                        file.Delete();
                }
            }
            catch
            {
                // 清理失败不影响程序
            }
        }
    }
}
