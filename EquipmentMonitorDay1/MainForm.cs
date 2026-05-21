using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace EquipmentMonitorDay1
{
    public partial class MainForm : Form
    {
        // 数据源 — 绑定到 DataGridView 后，数据变了UI自动刷新
        private BindingList<DeviceData> _dataList;

        // 数据绑定桥梁
        private BindingSource _bindingSource;

        // 运行计时器
        private Timer _statusTimer;
        private DateTime _startTime;

        // 记录上次点的是哪一列
        private string _lastSortColumn;

        // 排序方向：非 null 时表示当前排序列名
        private bool _sortAscending; // 去掉 = true，在事件里决定

        // 状态栏里的时间标签，Timer 要更新它，所以定义为字段
        private ToolStripStatusLabel _lblTime;
        private ToolStripStatusLabel _lblPLC;

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

            comboBox1.SelectedIndex = 0; // 默认 COM1
            comboBox2.SelectedIndex = 0; // 默认 9600

            // ---------- 窗口标题 ----------
            this.Text = "工业设备监控系统 v1.0";

            // ---------- 初始化数据 ----------
            _dataList = new BindingList<DeviceData>
            {
                new DeviceData
                {
                    DeviceName = "反应釜A-温度",
                    Value = 85.3,
                    Unit = "℃",
                    Status = "正常",
                    UpdateTime = DateTime.Now,
                },
                new DeviceData
                {
                    DeviceName = "反应釜A-压力",
                    Value = 0.6,
                    Unit = "MPa",
                    Status = "正常",
                    UpdateTime = DateTime.Now,
                },
                new DeviceData
                {
                    DeviceName = "蒸馏塔-液位",
                    Value = 75.0,
                    Unit = "%",
                    Status = "正常",
                    UpdateTime = DateTime.Now,
                },
                new DeviceData
                {
                    DeviceName = "烘干炉-温度",
                    Value = 210.5,
                    Unit = "℃",
                    Status = "报警",
                    UpdateTime = DateTime.Now,
                },
            };

            // ---------- DataGridView 绑定 ----------
            // 第一步：BindingSource 绑定到数据源
            _bindingSource = new BindingSource();
            _bindingSource.DataSource = _dataList;

            // 第二步：DataGridView 绑定到 BindingSource（不再直接绑 _dataList）
            dataGridView1.DataSource = _bindingSource;
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

            // 让"当前值"列和"状态"列可以点击排序
            // SortMode 有三个值：
            //   Automatic  = 点列头自动排序（默认是 None）
            //   Programmatic = 只能代码排序，用户点不了
            //   NotSortable  = 禁止排序
            dataGridView1.Columns["DisplayValue"].SortMode = DataGridViewColumnSortMode.Automatic;
            dataGridView1.Columns["Status"].SortMode = DataGridViewColumnSortMode.Automatic;

            // 手动处理排序：BindingList<DeviceData> 不支持自动排序，需要自己写
            dataGridView1.ColumnHeaderMouseClick += DataGridView1_ColumnHeaderMouseClick;

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

            // ============================================================
            // 创建工具栏（菜单栏下方）
            // ============================================================
            // ToolStrip = 一个水平的工具条，可以放按钮、分隔符、下拉菜单等
            ToolStrip toolStrip = new ToolStrip();

            ToolStripButton btnStart = new ToolStripButton("▶ 启动");
            ToolStripButton btnStop = new ToolStripButton("⏸ 停止");

            // ToolStripSeparator = 竖线分隔符，把功能分组
            ToolStripSeparator separator = new ToolStripSeparator();

            ToolStripButton btnSave = new ToolStripButton("💾 保存");
            ToolStripButton btnExport = new ToolStripButton("📤 导出");

            ToolStripSeparator separator2 = new ToolStripSeparator();

            ToolStripButton btnAlarmFilter = new ToolStripButton("🔔 仅显示报警");
            btnAlarmFilter.CheckOnClick = true; // 开关模式

            // 事件绑定：工具栏按钮 → 复用你已有的方法
            btnStart.Click += (sender, args) => BtnStart_Click(null, EventArgs.Empty);
            btnStop.Click += (sender, args) => BtnStop_Click(null, EventArgs.Empty);
            btnSave.Click += (sender, args) => BtnSave_Click(null, EventArgs.Empty);
            btnAlarmFilter.Click += (sender, args) =>
            {
                if (btnAlarmFilter.Checked)
                    显示报警设备ToolStripMenuItem_Click(null, EventArgs.Empty);
                else
                    显示全部设备ToolStripMenuItem_Click(null, EventArgs.Empty);
            };


            // btnExport 你先不用绑，后面再实现导出功能

            // Items.Add = 按顺序加到工具栏上
            toolStrip.Items.Add(btnStart);
            toolStrip.Items.Add(btnStop);
            toolStrip.Items.Add(separator); // 一条竖线隔开两组按钮
            toolStrip.Items.Add(btnSave);
            toolStrip.Items.Add(btnExport);
            toolStrip.Items.Add(separator2);
            toolStrip.Items.Add(btnAlarmFilter);

            this.Controls.Add(toolStrip);

            // ============================================================
            // 创建状态栏（窗体底部）
            // ============================================================
            StatusStrip statusStrip = new StatusStrip();

            // ToolStripStatusLabel = 状态栏上的一个文本块
            _lblPLC = new ToolStripStatusLabel("PLC-1: 已停止");
            ToolStripStatusLabel lblSpring = new ToolStripStatusLabel(""); // 空的，用来占位

            // Spring = true → 这个标签会"弹开"，把后面的内容挤到右边
            lblSpring.Spring = true;

            // _lblTime 声明为字段，因为 Timer 事件里要更新它
            _lblTime = new ToolStripStatusLabel("00:00:00");

            // 按顺序加入状态栏
            statusStrip.Items.Add(_lblPLC);
            statusStrip.Items.Add(lblSpring);
            statusStrip.Items.Add(_lblTime);

            this.Controls.Add(statusStrip);

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
            _lblPLC.Text = "PLC-1: 已连接";
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
            _lblPLC.Text = "PLC-1: 已停止";

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
            // TimeSpan = 两个时间点之间的差值
            // DateTime.Now - _startTime = 当前时刻 - 启动时刻 = 已经运行了多久
            TimeSpan elapsed = DateTime.Now - _startTime;

            // 更新状态栏上的时间标签
            // D2 = 两位数格式，比如 3 显示为 "03"
            _lblTime.Text = $"{elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";

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
        /// 列头点击排序：BindingList 不支持自动排序，手动对列表排序后重建数据源
        /// </summary>
        private void DataGridView1_ColumnHeaderMouseClick(
            object sender,
            DataGridViewCellMouseEventArgs e
        )
        {
            // 只有"当前值"和"状态"列允许排序
            string colName = dataGridView1.Columns[e.ColumnIndex].Name;
            if (colName != "DisplayValue" && colName != "Status")
                return;

            try
            {
                // 如果点的是新列，重置为升序；否则切换方向
                if (colName != _lastSortColumn)
                {
                    _lastSortColumn = colName;
                    _sortAscending = true; // 新列默认升序
                }
                else
                {
                    _sortAscending = !_sortAscending; // 同一列切换方向
                }

                List<DeviceData> sorted;
                if (colName == "DisplayValue")
                {
                    sorted = _sortAscending
                        ? _dataList.OrderBy(d => d.Value).ToList()
                        : _dataList.OrderByDescending(d => d.Value).ToList();
                }
                else // Status
                {
                    sorted = _sortAscending
                        ? _dataList.OrderBy(d => d.Status).ToList()
                        : _dataList.OrderByDescending(d => d.Status).ToList();
                }

                dataGridView1.SuspendLayout(); // 冻结布局，避免闪烁
                // 赋值前先解除绑定，防止重置期间触发 UI 刷新
                _bindingSource.SuspendBinding();
                _dataList.Clear();
                foreach (var item in sorted)
                    _dataList.Add(item);
                _bindingSource.ResumeBinding();
                dataGridView1.ResumeLayout(); // 恢复布局

                AppendLog($"已排序：{colName}（{(_sortAscending ? "升序" : "降序")}）");
            }
            catch (Exception ex)
            {
                AppendLog($"排序失败：{ex.Message}");
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
                // 对话框打开时的初始目录：当前程序 exe 所在目录
                dlg.InitialDirectory = Application.StartupPath;

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // File.ReadAllText = 把整个文件读成字符串
                        string fileContent = File.ReadAllText(dlg.FileName);

                        // JavaScriptSerializer.Deserialize<AppConfig> = 把 JSON 字串转成 AppConfig 对象
                        var serializer = new JavaScriptSerializer();
                        AppConfig config = serializer.Deserialize<AppConfig>(fileContent);

                        comboBox1.Text = config.PortName; // 串口号
                        comboBox2.Text = config.BaudRate.ToString(); // 波特率（int→string）
                        numericUpDown1.Value = config.Interval; // 采集间隔
                        checkBox1.Checked = config.EnableAlarm; // 声音报警
                        checkBox2.Checked = config.EnableAutoSave; // 自动保存

                        AppendLog($"配置已加载：{dlg.FileName}");
                    }
                    catch (Exception ex)
                    {
                        // 任何错误（文件不存在、JSON 格式错误、权限不足）都不会让程序崩溃
                        MessageBox.Show(
                            $"加载配置失败：{ex.Message}",
                            "错误",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error
                        );
                    }
                }
                // 如果用户点了"取消"，ShowDialog() 返回 DialogResult.Cancel
                // 直接走到 using 结束，什么都不做
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

        private void 保存设置ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "选择保存设置的文件夹";

                dlg.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments); // 默认选中"文档"文件夹

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    string selectedPath = dlg.SelectedPath;

                    AppendLog($"保存设置目录已设置：{selectedPath}");
                }
            }
        }

        /// <summary>
        /// 切换筛选：只显示状态为"报警"的设备
        /// 可以绑定到工具栏按钮或 CheckBox
        /// </summary>
        private void 显示报警设备ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // BindingSource.Filter = 筛选条件表达式
            // 语法：列名 = '值'    注意字符串值要用单引号括起来
            _bindingSource.Filter = "Status = '报警'";

            AppendLog("已筛选：仅显示报警设备");
        }

        /// <summary>
        /// 清除筛选，显示全部设备
        /// </summary>
        private void 显示全部设备ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // RemoveFilter() = 去掉筛选条件，恢复全部数据
            _bindingSource.RemoveFilter();

            AppendLog("已清除筛选：显示全部设备");
        }
    }
}
