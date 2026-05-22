using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
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

        // 完整数据备份（过滤时保留原始数据用）
        private List<DeviceData> _allData;
        private bool _isFiltered = false;

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

            // 备份完整数据（筛选时需要恢复）
            _allData = _dataList.ToList();

            // 第二步：DataGridView 绑定到 BindingSource（不再直接绑 _dataList）
            dataGridView1.DataSource = _bindingSource;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.RowHeadersVisible = false;
            // 禁止用户新增/删除行
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;

            // DataGridViewCheckBoxColumn = 带勾选框的列
            DataGridViewCheckBoxColumn checkColumn = new DataGridViewCheckBoxColumn();
            checkColumn.HeaderText = "选择";
            checkColumn.Name = "checkColumn";
            checkColumn.Width = 50;
            checkColumn.FalseValue = false;
            checkColumn.TrueValue = true;

            // Insert = 插到第 0 列（最左边），不然后面 Columns["DeviceName"] 这些的索引会乱
            dataGridView1.Columns.Insert(0, checkColumn);

            // 逐列锁定：只有复选框列可编辑，其余数据列全部只读
            foreach (DataGridViewColumn col in dataGridView1.Columns)
            {
                if (col.Name != "checkColumn")
                    col.ReadOnly = true;
            }

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

            ToolStripSeparator separator3 = new ToolStripSeparator();

            ToolStripButton btnAdd = new ToolStripButton("➕ 添加");
            ToolStripButton btnEdit = new ToolStripButton("✏ 编辑");
            ToolStripButton btnDelete = new ToolStripButton("🗑 删除");

            // 事件绑定：工具栏按钮 → 复用你已有的方法
            btnStart.Click += (sender, args) => BtnStart_Click(null, EventArgs.Empty);
            btnStop.Click += (sender, args) => BtnStop_Click(null, EventArgs.Empty);
            btnSave.Click += (sender, args) => BtnSave_Click(null, EventArgs.Empty);
            btnExport.Click += (sender, args) =>
                批量导出选中ToolStripMenuItem_Click(null, EventArgs.Empty);
            btnAlarmFilter.Click += (sender, args) =>
            {
                if (btnAlarmFilter.Checked)
                    显示报警设备ToolStripMenuItem_Click(null, EventArgs.Empty);
                else
                    显示全部设备ToolStripMenuItem_Click(null, EventArgs.Empty);
            };
            btnAdd.Click += (sender, args) => BtnAdd_Click(null, EventArgs.Empty);
            btnEdit.Click += (sender, args) => BtnEdit_Click(null, EventArgs.Empty);
            btnDelete.Click += (sender, args) => BtnDelete_Click(null, EventArgs.Empty);

            // Items.Add = 按顺序加到工具栏上
            toolStrip.Items.Add(btnStart);
            toolStrip.Items.Add(btnStop);

            toolStrip.Items.Add(separator); // 一条竖线隔开两组按钮

            toolStrip.Items.Add(btnSave);
            toolStrip.Items.Add(btnExport);

            toolStrip.Items.Add(separator2);

            toolStrip.Items.Add(btnAlarmFilter);

            toolStrip.Items.Add(separator3);

            toolStrip.Items.Add(btnAdd);
            toolStrip.Items.Add(btnEdit);
            toolStrip.Items.Add(btnDelete);

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

            // 创建设备卡片
            DeviceCard card = new DeviceCard();
            card.DeviceName = "反应釜A-温度";
            card.DisplayValue = "85.3 ℃";
            card.Status = "正常";

            flowLayoutDeviceCards.Controls.Add(card);



            // ====== 界面美化 ======
            // 主窗体背景色
            this.BackColor = Color.FromArgb(240, 240, 240);

            // 逐个 GroupBox 改标题颜色
            groupBox1.ForeColor = Color.FromArgb(0, 70, 130);
            groupBox2.ForeColor = Color.FromArgb(0, 70, 130);
            groupBox3.ForeColor = Color.FromArgb(0, 70, 130);
            groupBox4.ForeColor = Color.FromArgb(0, 70, 130);
            groupBox5.ForeColor = Color.FromArgb(0, 70, 130);

            // DataGridView 表头样式
            dataGridView1.EnableHeadersVisualStyles = false;
            dataGridView1.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(0, 70, 130);
            dataGridView1.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dataGridView1.ColumnHeadersDefaultCellStyle.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
            dataGridView1.ColumnHeadersHeight = 30;

            // 隔行变色
            dataGridView1.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248);
            dataGridView1.RowsDefaultCellStyle.BackColor = Color.White;

            // 逐个按钮改扁平化
            button1.FlatStyle = FlatStyle.Flat;
            button1.FlatAppearance.BorderColor = Color.FromArgb(0, 70, 130);
            button1.BackColor = Color.White;
            button1.ForeColor = Color.FromArgb(0, 70, 130);

            button2.FlatStyle = FlatStyle.Flat;
            button2.FlatAppearance.BorderColor = Color.FromArgb(0, 70, 130);
            button2.BackColor = Color.White;
            button2.ForeColor = Color.FromArgb(0, 70, 130);

            button3.FlatStyle = FlatStyle.Flat;
            button3.FlatAppearance.BorderColor = Color.FromArgb(0, 70, 130);
            button3.BackColor = Color.White;
            button3.ForeColor = Color.FromArgb(0, 70, 130);

            button4.FlatStyle = FlatStyle.Flat;
            button4.FlatAppearance.BorderColor = Color.FromArgb(0, 70, 130);
            button4.BackColor = Color.White;
            button4.ForeColor = Color.FromArgb(0, 70, 130);
            // ======================
            // ======================

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
        /// </summary>
        private void 显示报警设备ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_isFiltered)
                return; // 已经筛选过了，不用重复

            _isFiltered = true;

            // 同步更新 _allData 备份（避免数据变更后丢失）
            _allData = _dataList.ToList();

            _bindingSource.SuspendBinding();
            _dataList.Clear();
            foreach (var item in _allData)
            {
                if (item.Status == "报警")
                    _dataList.Add(item);
            }
            _bindingSource.ResumeBinding();

            AppendLog("已筛选：仅显示报警设备");
        }

        /// <summary>
        /// 清除筛选，显示全部设备
        /// </summary>
        private void 显示全部设备ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!_isFiltered)
                return;

            _isFiltered = false;

            _bindingSource.SuspendBinding();
            _dataList.Clear();
            foreach (var item in _allData)
                _dataList.Add(item);
            _bindingSource.ResumeBinding();

            AppendLog("已清除筛选：显示全部设备");
        }

        /// <summary>
        /// 批量导出选中的设备数据到 CSV 文件
        /// </summary>
        private void 批量导出选中ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // 遍历 DataGridView 的所有行，找出勾选了的
            var selectedDevices = new List<DeviceData>();
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                // Cells["checkColumn"].Value = 这一行的勾选框的值
                // 如果没勾选，Value 可能是 null 或 false
                if (row.Cells["checkColumn"].Value is bool isChecked && isChecked)
                {
                    // row.DataBoundItem = 这一行绑定的数据对象
                    if (row.DataBoundItem is DeviceData device)
                    {
                        selectedDevices.Add(device);
                    }
                }
            }
            if (selectedDevices.Count == 0)
            {
                MessageBox.Show(
                    "请先勾选要导出的设备",
                    "提示",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                return;
            }

            AppendLog($"已选中 {selectedDevices.Count} 台设备，准备导出");
            // 后续可以接 SaveFileDialog + 写 CSV 文件
        }


        /// <summary>
        /// 🗑 删除设备：勾选的设备 → 确认 → 删除
        /// </summary>
        private void BtnDelete_Click(object value, EventArgs empty)
        {
            List<DeviceData> toDelete = new List<DeviceData>();
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                if (row.Cells["checkColumn"].Value is bool isCheck && isCheck)
                {
                    if (row.DataBoundItem is DeviceData device)
                    {
                        toDelete.Add(device);
                    }
                }
            }
            if (toDelete.Count == 0)
            {
                MessageBox.Show(
                    "请先勾选要删除的设备",
                    "提示",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                return;
            }

            var result = MessageBox.Show(
                $"确认删除选中的 {toDelete.Count} 台设备吗？",
                "确认删除",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (result == DialogResult.Yes)
            {
                foreach (var device in toDelete)
                {
                    _dataList.Remove(device);
                }

                AppendLog($"已删除{toDelete.Count} 台设备");
            }
        }

        /// <summary>
        /// ✏ 编辑设备：选中一行 → 弹出窗口 → 修改 → 确定 → 更新
        /// </summary>
        private void BtnEdit_Click(object value, EventArgs empty)
        {
            if (dataGridView1.CurrentRow == null)
            {
                MessageBox.Show(
                    "请先选中一行要编辑的设备",
                    "提示",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                return;
            }

            if (dataGridView1.CurrentRow.DataBoundItem is DeviceData device)
            {
                using (DeviceEditForm dialog = new DeviceEditForm())
                {
                    dialog.Text = "编辑设备";

                    // 把当前值回填到窗口
                    dialog.DeviceName = device.DeviceName;
                    dialog.DeviceValue = device.Value;
                    dialog.Unit = device.Unit;
                    dialog.Status = device.Status;

                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        // 用户点了确定 → 更新设备数据
                        device.DeviceName = dialog.DeviceName;
                        device.Value = dialog.DeviceValue;
                        device.Unit = dialog.Unit;
                        device.Status = dialog.Status;
                        device.UpdateTime = DateTime.Now;

                        // 通知 DataGridView 刷新
                        _bindingSource.ResetBindings(true);

                        AppendLog($"已编辑设备：{device.DeviceName}");
                    }
                }
            }
        }

        /// <summary>
        /// ➕ 添加设备：弹出窗口 → 填数据 → 确定 → 加入列表
        /// </summary>
        private void BtnAdd_Click(object value, EventArgs empty)
        {
            using (DeviceEditForm dialog = new DeviceEditForm())
            {
                dialog.Text = "添加设备";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    // 用户点了确定 → 创建新设备数据
                    DeviceData newDevice = new DeviceData
                    {
                        DeviceName = dialog.DeviceName,
                        Value = dialog.DeviceValue,
                        Unit = dialog.Unit,
                        Status = dialog.Status,
                        UpdateTime = DateTime.Now,
                    };

                    // 添加到 BindingList → DataGridView 自动刷新
                    _dataList.Add(newDevice);

                    AppendLog($"已添加设备：{newDevice.DeviceName}");
                }
            }
        }

    }
}
