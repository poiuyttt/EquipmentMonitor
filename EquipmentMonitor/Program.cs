using System;
using System.Windows.Forms;

namespace EquipmentMonitorDay1
{
    internal static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // ====== 全局异常捕获 ======
            // 1. UI 线程异常（WinForms 控件事件里抛出的）
            Application.ThreadException += (sender, e) =>
            {
                //写日志到文件
                string logPath = Application.StartupPath + "\\error.log";
                System.IO.File.AppendAllText(
                    logPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] UI线程异常: {e.Exception}{Environment.NewLine}"
                );

                MessageBox.Show(
                    $"程序发生未处理的异常: {e.Exception.Message}\n\n错误已记录到 {logPath}",
                    "程序错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                AppLogger.Error("UI 线程未处理异常", e.Exception);
            };

            // 2. 非 UI 线程异常（后台线程里抛出的）
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                string logPath = Application.StartupPath + "\\error.log";
                System.IO.File.AppendAllText(
                    logPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 后台线程异常: {e.ExceptionObject}{Environment.NewLine}"
                );
                AppLogger.Error("后台线程未处理异常", e.ExceptionObject as Exception);
            };

            // 设置应用程序处理异常模式为：捕获所有 Windows 窗体异常
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Application.Run(new MainForm());
        }
    }
}
