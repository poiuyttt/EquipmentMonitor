using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace EquipmentMonitorDay1
{
    public partial class MainForm : Form
    {
        // 数据源 — 绑定到 DataGridView 后，数据变了UI自动刷新
        private BindingList<DeviceData> _dataList;

        // 运行计时器
        private Timer _statusTimer;
        private DateTime _startTime;

        public MainForm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 窗体加载时执行初始化（OnLoad 只在运行时触发，设计器不会调用）
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            comboBox1.SelectedIndex = 0;  // 默认 COM1
            comboBox2.SelectedIndex = 0;  // 默认 9600

            // ---------- 窗口标题 ----------
            this.Text = "工业设备监控系统 v1.0";

            // ---------- 初始化数据 ----------
            _dataList = new BindingList<DeviceData>
            {
                new DeviceData { DeviceName = "反应釜A-温度", Value = 85.3, Unit = "℃", Status = "正常", UpdateTime = DateTime.Now },
                new DeviceData { DeviceName = "反应釜A-压力", Value = 0.6, Unit = "MPa", Status = "正常", UpdateTime = DateTime.Now },
                new DeviceData { DeviceName = "蒸馏塔-液位", Value = 75.0, Unit = "%", Status = "正常", UpdateTime = DateTime.Now },
                new DeviceData { DeviceName = "烘干炉-温度", Value = 210.5, Unit = "℃", Status = "报警", UpdateTime = DateTime.Now },
            };

            // ---------- DataGridView 绑定 ----------
            dataGridView1.DataSource = _dataList;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.ReadOnly = true;
            dataGridView1.RowHeadersVisible = false;

            // 设置列标题
            dataGridView1.Columns["DeviceName"].HeaderText = "设备名称";
            dataGridView1.Columns["Value"].Visible = false;
            dataGridView1.Columns["Unit"].Visible = false;
            dataGridView1.Columns["Status"].HeaderText = "状态";
            dataGridView1.Columns["Status"].Width = 80;
            dataGridView1.Columns["UpdateTime"].HeaderText = "更新时间";

            dataGridView1.Columns["UpdateTime"].DefaultCellStyle.Format = "HH:mm:ss";

            if (dataGridView1.Columns["DisplayValue"] != null)
                dataGridView1.Columns["DisplayValue"].HeaderText = "当前值";

            // 按状态设行颜色
            dataGridView1.RowPrePaint += DataGridView1_RowPrePaint;

            // ---------- 状态栏计时 ----------
            _startTime = DateTime.Now;
            _statusTimer = new Timer();
            _statusTimer.Interval = 1000;
            _statusTimer.Tick += StatusTimer_Tick;
            _statusTimer.Start();

            // ---------- 绑定按钮事件 ----------
            button1.Click += BtnStart_Click;
            button2.Click += BtnStop_Click;
            button3.Click += BtnExit_Click;
            button4.Click += BtnSave_Click;

            // ---------- 写一条启动日志 ----------
            AppendLog("系统启动完成");
        }

        /// <summary>
        /// 开始采集
        /// </summary>
        private void BtnStart_Click(object sender, EventArgs e)
        {
            button1.Enabled = false;
            button2.Enabled = true;
            label1.Text = "PLC-1: 已连接";
            AppendLog("开始数据采集...");
        }

        /// <summary>
        /// 停止采集
        /// </summary>
        private void BtnStop_Click(object sender, EventArgs e)
        {
            button1.Enabled = true;
            button2.Enabled = false;
            label1.Text = "PLC-1: 已停止";
            AppendLog("数据采集已停止");
        }

        /// <summary>
        /// 退出系统
        /// </summary>
        private void BtnExit_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "确认退出系统？",
                "确认",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );
            if (result == DialogResult.Yes)
            {
                Application.Exit();
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            string port = comboBox1.Text;
            string baud = comboBox2.Text;
            int interval = (int)numericUpDown1.Value;

            string message = $"串口: {port}\r\n波特率: {baud}\r\n采集间隔: {interval}ms";
            string mode = radioButton1.Checked ? "手动" : "自动";

            MessageBox.Show(message, "已保存", MessageBoxButtons.OK, MessageBoxIcon.Information);
            AppendLog($"数据已保存:{port},{baud},{interval}ms");
            AppendLog($"声音报警：{(checkBox1.Checked ? "开启" : "关闭")}：");
            AppendLog($"自动保存：{(checkBox2.Checked ? "开启" : "关闭")}：");
            AppendLog($"工作模式：{mode}");
        }

        /// <summary>
        /// 状态栏计时器
        /// </summary>
        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            var elapsed = DateTime.Now - _startTime;
            this.Text =
                $"工业设备监控系统 v1.0 — 已运行 {elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
        }

        /// <summary>
        /// DataGridView 行颜色：正常=绿，报警=粉，离线=灰
        /// </summary>
        private void DataGridView1_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            var row = dataGridView1.Rows[e.RowIndex];
            if (row.DataBoundItem is DeviceData data)
            {
                switch (data.Status)
                {
                    case "报警":
                        row.DefaultCellStyle.BackColor = System.Drawing.Color.LightPink;
                        break;
                    case "离线":
                        row.DefaultCellStyle.BackColor = System.Drawing.Color.LightGray;
                        break;
                    case "正常":
                        row.DefaultCellStyle.BackColor = System.Drawing.Color.LightGreen;
                        break;
                }
            }
        }

        /// <summary>
        /// 写日志到文本框
        /// </summary>
        private void AppendLog(string message)
        {
            textBox1.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
        }

        private void 导入配置ToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "选择配置文件";
                dlg.Filter = "配置文件(*.json)|*.json|所有文件(*.*)|*.*";
                dlg.InitialDirectory = Application.StartupPath;

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    AppendLog($"已选择配置文件：{dlg.FileName}");
                    //以后这里写读取配置的代码
                }
            }
        }

        private void 导出数据ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Title = "导出数据";
                dlg.Filter = "CSV文件(*.csv)|*.csv|Excel文件(*.xlsx)|*.xlsx";
                dlg.FileName = $"设备数据_{DateTime.Now:yyyyMMdd_HHmmss}";

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    AppendLog($"数据已导出到：{dlg.FileName}");
                }
            }
        }

        private void 关于ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("工业设备监控系统 v1.0\n适用于WinForms上位机开发教学", "关于");
        }
    }

    /// <summary>
    /// 设备数据模型
    /// </summary>
    public class DeviceData
    {
        public string DeviceName { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; }
        public string Status { get; set; }
        public DateTime UpdateTime { get; set; }

        // 显示在 DataGridView 里的格式化字符串
        public string DisplayValue => $"{Value} {Unit}";
    }
}
