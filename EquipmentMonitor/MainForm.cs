using Modbus.Data;
using Modbus.Device;
using NPOI.XSSF.UserModel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using Timer = System.Windows.Forms.Timer;

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

        // ====== 生产者-消费者 ======
        private ConcurrentQueue<DeviceData> _dataQueue; // 线程安全队列
        private CancellationTokenSource _cts; // 取消令牌
        private Task _producerTask; // 后台采集任务

        // ====== 串口通信 ======
        private SerialPort _serialPort;

        private IModbusMaster _modbusMaster;

        // ====== 自动发送 ======
        private Timer _autoSendTimer;

        // ====== Modbus 从站 ======
        private ModbusSlave _modbusSlave;
        private Task _slaveTask;
        private CancellationTokenSource _slaveCts;

        // ====== TCP 通信 ======
        private TcpClient _tcpClient;
        private NetworkStream _tcpStream;
        private CancellationTokenSource _tcpCts;

        // ====== SQLite 数据库 ======
        private DatabaseHelper _dbHelper;

        // ====== 报警阈值 ======
        private double _alarmHigh = 100;
        private double _alarmLow = 10;

        private Dictionary<string, DateTime> _alarmShown;

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

            // ====== 加载外部配置 ======
            LoadConfigFromFile();
            // ==========================

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
            checkColumn.DataPropertyName = "IsSelected"; // 绑定到数据模型的 IsSelected 属性
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

            ToolStripSeparator separator4 = new ToolStripSeparator();
            ToolStripButton btnSend = new ToolStripButton("📤 发送");

            ToolStripSeparator separator5 = new ToolStripSeparator();
            ToolStripButton btnAutoSend = new ToolStripButton("🔄 自动发送");
            btnAutoSend.CheckOnClick = true;
            ToolStripButton btnShowPacket = new ToolStripButton("📋 报文");
            ToolStripButton btnRead = new ToolStripButton("📖 读 Modbus");

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
            btnSend.Click += (sender, args) => BtnSend_Click(null, EventArgs.Empty);
            btnAutoSend.Click += (sender, args) =>
            {
                if (btnAutoSend.Checked)
                {
                    _autoSendTimer.Start();
                }
                else
                {
                    _autoSendTimer.Stop();
                }
            };
            btnShowPacket.Click += (sender, args) =>
            {
                byte[] packet = BuildReadHoldingRegisters(0x01, 0, 1);
                AppendLog($"Modbus报文：{BitConverter.ToString(packet)}");
                AppendLog($"地址：{packet[0]} 功能码：{packet[1]}");
                AppendLog(
                    $"起始地址：{packet[2] * 256 + packet[3]} 数量：{packet[4] * 256 + packet[5]}" //(packet[2] << 8) | packet[3]  // 意思完全相同
                );
                byte[] crc = CalculateCRC16(packet, 6);
                AppendLog($"  CRC 校验：{crc[0]:X2} {crc[1]:X2}（√ 和报文一致）");
            };
            btnRead.Click += (sender, args) => ReadModbusRegisters();

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

            toolStrip.Items.Add(separator3);

            toolStrip.Items.Add(btnSend);

            toolStrip.Items.Add(separator5);

            toolStrip.Items.Add(btnAutoSend);
            toolStrip.Items.Add(btnShowPacket);
            toolStrip.Items.Add(btnRead);

            ToolStripButton btnStartSlave = new ToolStripButton("🖥 启动从站");
            btnStartSlave.Click += (sender, args) => StartModbusSlave();
            toolStrip.Items.Add(btnStartSlave);

            ToolStripButton btnTcpTest = new ToolStripButton("🌐 TCP 测试");
            btnTcpTest.Click += async (sender, args) => await TestModbusTcp();
            toolStrip.Items.Add(btnTcpTest);

            ToolStripButton btnExportExcel = new ToolStripButton("📊 导出 Excel");
            btnExportExcel.Click += (sender, args) => ExportToExcel();
            toolStrip.Items.Add(btnExportExcel);

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
            dataGridView1.ColumnHeadersDefaultCellStyle.Font = new Font(
                "微软雅黑",
                9F,
                FontStyle.Bold
            );
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

            // ====== 生产者-消费者初始化 ======
            _dataQueue = new ConcurrentQueue<DeviceData>();
            _cts = new CancellationTokenSource();

            // 消费定时器：UI 线程每 500ms 从队列取数据显示
            Timer consumeTimer = new Timer();
            consumeTimer.Interval = 500;
            consumeTimer.Tick += ConsumeTimer_Tick;
            consumeTimer.Start();
            // ===============================

            //byte[] testMessage = { 0x00, 0x85, 0x01, 0x0F, 0x01, 0x84, 0x0A };
            //ParseIndustryMessage(testMessage);

            // ====== 自动发送定时器初始化 ======
            _autoSendTimer = new Timer();
            _autoSendTimer.Interval = 3000;
            _autoSendTimer.Tick += AutoSendTimer_Tick;

            // ====== Chart 实时曲线 ======
            _chartTemperature.Series.Clear();
            _chartTemperature.ChartAreas.Clear();

            // 创建一个绘图区域
            ChartArea chartArea = new ChartArea();
            chartArea.AxisY.Title = "温度 (℃)";
            chartArea.AxisX.Title = "时间";
            chartArea.AxisX.LabelStyle.Format = "HH:mm:ss";
            chartArea.AxisX.IntervalType = DateTimeIntervalType.Seconds;
            _chartTemperature.ChartAreas.Add(chartArea);

            // 创建温度曲线
            Series seriesTemp = new Series();
            seriesTemp.Name = "温度";
            seriesTemp.ChartType = SeriesChartType.Line; // 折线图
            seriesTemp.BorderWidth = 2; // 线宽
            seriesTemp.Color = Color.Red; // 红色
            _chartTemperature.Series.Add(seriesTemp);

            // 记录 60 个点（1 分钟）
            seriesTemp.Points.Clear();

            // ====== 数据库初始化 ======
            _dbHelper = new DatabaseHelper();
            _dbHelper.CleanupOldData(); // 启动时清理旧数据

            _numAlarmHigh.Value = (decimal)_alarmHigh;
            _numAlarmLow.Value = (decimal)_alarmLow;

            // ---------- 写一条启动日志 ----------
            AppendLog("系统启动完成");
            AppLogger.Info("系统启动完成");
        }

        private void ExportToExcel()
        {
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Filter = "Excel文件(*.xlsx)|*.xlsx";
                dlg.FileName = $"设备数据_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var workbook = new XSSFWorkbook();

                        // Sheet 1：历史数据（最近 24 小时）
                        var sheet1 = workbook.CreateSheet("历史数据");
                        var header1 = sheet1.CreateRow(0);
                        header1.CreateCell(0).SetCellValue("设备名称");
                        header1.CreateCell(1).SetCellValue("当前值");
                        header1.CreateCell(2).SetCellValue("单位");
                        header1.CreateCell(3).SetCellValue("状态");
                        header1.CreateCell(4).SetCellValue("更新时间");

                        // 从数据库查最近 24 小时的数据
                        var historyData = _dbHelper.QueryHistory(
                            "",   // 空字符串表示查所有设备
                            DateTime.Now.AddDays(-1),
                            DateTime.Now
                        );

                        int rowIdx = 1;
                        foreach (var item in historyData)
                        {
                            var row = sheet1.CreateRow(rowIdx++);
                            row.CreateCell(0).SetCellValue(item.DeviceName);
                            row.CreateCell(1).SetCellValue(item.Value);
                            row.CreateCell(2).SetCellValue(item.Unit);
                            row.CreateCell(3).SetCellValue(item.Status);
                            row.CreateCell(4).SetCellValue(item.RecordTime.ToString("yyyy-MM-dd HH:mm:ss"));
                        }

                        for (int i = 0; i <= 4; i++)
                            sheet1.AutoSizeColumn(i);

                        // 写入文件
                        using (var fs = new FileStream(dlg.FileName, FileMode.Create, FileAccess.Write))
                        {
                            workbook.Write(fs);
                        }

                        AppendLog($"已导出 {historyData.Count} 条历史数据：{dlg.FileName}");
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"导出失败：{ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 开始采集
        /// </summary>
        private void BtnStart_Click(object sender, EventArgs e)
        {
            button1.Enabled = false;
            button2.Enabled = true;

            try
            {
                // 创建串口对象
                _serialPort = new SerialPort();

                // ====== 配置串口参数（从界面读取） ======
                _serialPort.PortName = comboBox1.Text; // 串口号
                if (!int.TryParse(comboBox2.Text, out int baudRate))
                {
                    MessageBox.Show("波特率格式不正确");
                    button1.Enabled = true;
                    button2.Enabled = false;
                    return;
                }
                _serialPort.BaudRate = baudRate;
                _serialPort.DataBits = 8; // 数据位
                _serialPort.StopBits = StopBits.One; // 停止位
                _serialPort.Parity = Parity.None; // 校验位

                // 串口超时：默认是无限等待，必须显式设置才生效
                _serialPort.ReadTimeout = 2000; // 读超时 2 秒
                _serialPort.WriteTimeout = 2000; // 写超时 2 秒
                // ===================================

                // 串口收到数据时触发
                _serialPort.DataReceived += SerialPort_DataReceived;

                _serialPort.Open();

                // 创建 Modbus RTU 主站
                _modbusMaster = ModbusSerialMaster.CreateRtu(_serialPort);

                label1.Text = "PLC-1: 已连接";
                _lblPLC.Text = "PLC-1: 已连接";

                _cts = new CancellationTokenSource();
                _producerTask = ProduceDataAsync(_cts.Token);

                AppendLog($"已连接串口 {comboBox1.Text}，波特率 {comboBox2.Text}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开串口失败：{ex.Message}");
                button1.Enabled = true;
                button2.Enabled = false;
            }
        }

        /// <summary>
        /// 停止采集
        /// </summary>
        private void BtnStop_Click(object sender, EventArgs e)
        {
            button1.Enabled = true;
            button2.Enabled = false;

            _cts?.Cancel();

            // 关闭串口：丢到后台线程 fire-and-forget，不阻塞 UI
            var port = _serialPort;
            var master = _modbusMaster;
            _serialPort = null;
            _modbusMaster = null;

            if (port != null)
            {
                Task.Run(() =>
                {
                    try
                    {
                        // 先 Dispose modbus master，让它不要再访问串口
                        master?.Dispose();

                        if (port.IsOpen)
                            port.Close();
                        port.Dispose();
                    }
                    catch { }
                });
            }

            label1.Text = "PLC-1: 已停止";
            _lblPLC.Text = "PLC-1: 已停止";

            AppendLog("串口已关闭");
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
            AppLogger.Info($"数据已保存:{port},{baud},{interval}ms");
            AppendLog($"声音报警：{(checkBox1.Checked ? "开启" : "关闭")}：");
            AppLogger.Info($"声音报警：{(checkBox1.Checked ? "开启" : "关闭")}：");
            AppendLog($"自动保存：{(checkBox2.Checked ? "开启" : "关闭")}：");
            AppLogger.Info($"自动保存：{(checkBox2.Checked ? "开启" : "关闭")}：");
            AppendLog($"工作模式：{mode}");
            AppLogger.Info($"工作模式：{mode}");
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
                AppLogger.Info($"已排序：{colName}（{(_sortAscending ? "升序" : "降序")}）");
            }
            catch (Exception ex)
            {
                AppendLog($"排序失败：{ex.Message}");
                AppLogger.Error($"排序失败：{ex.Message}", ex);
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
                        AppLogger.Info($"配置已加载：{dlg.FileName}");
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
                    AppLogger.Info($"数据已导出到：{dlg.FileName}");
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
                    AppLogger.Info($"保存设置目录已设置：{selectedPath}");
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
            AppLogger.Info("已筛选：仅显示报警设备");
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
            AppLogger.Info("已清除筛选：显示全部设备");
        }

        /// <summary>
        /// 批量导出选中的设备数据到 CSV 文件
        /// </summary>
        private void 批量导出选中ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            dataGridView1.EndEdit();

            var selectedDevices = new List<DeviceData>();
            foreach (DeviceData device in _dataList)
            {
                if (device.IsSelected)
                    selectedDevices.Add(device);
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
            AppLogger.Info($"已选中 {selectedDevices.Count} 台设备，准备导出");
            // 后续可以接 SaveFileDialog + 写 CSV 文件
        }

        /// <summary>
        /// 🗑 删除设备：勾选的设备 → 确认 → 删除
        /// </summary>
        private void BtnDelete_Click(object value, EventArgs empty)
        {
            // 提交当前正在编辑的勾选框值
            dataGridView1.EndEdit();

            List<DeviceData> toDelete = new List<DeviceData>();
            foreach (DeviceData device in _dataList)
            {
                if (device.IsSelected)
                    toDelete.Add(device);
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
                AppLogger.Info($"已删除{toDelete.Count} 台设备");
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
                        AppLogger.Info($"已编辑设备：{device.DeviceName}");
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
                    AppLogger.Info($"已添加设备：{newDevice.DeviceName}");
                }
            }
        }

        /// <summary>
        /// 生产者：后台线程循环采集数据 → 放入队列
        /// </summary>
        private async Task ProduceDataAsync(CancellationToken token)
        {
            // 在后台线程运行，不阻塞 UI
            await Task.Run(async () =>
            {
                Random random = new Random();
                while (!token.IsCancellationRequested)
                {
                    // 模拟采集数据
                    foreach (var device in _dataList)
                    {
                        //数值波动
                        double delta = (random.NextDouble() - 0.5) * 10;
                        double newValue = Math.Round(device.Value + delta, 1);
                        if (newValue < 0)
                        {
                            newValue = 0;
                        }

                        // 随机状态变化（5% 概率）
                        string newStatus = device.Status;
                        if (random.Next(100) < 5)
                        {
                            string[] statuses = { "正常", "报警", "离线" };
                            newStatus = statuses[random.Next(3)];
                        }

                        // 创建新数据对象 → 放入队列
                        DeviceData newData = new DeviceData
                        {
                            DeviceName = device.DeviceName,
                            Value = newValue,
                            Unit = device.Unit,
                            Status = newStatus,
                            UpdateTime = DateTime.Now,
                        };
                        _dataQueue.Enqueue(newData);
                    }
                    // 每秒采集一次
                    Thread.Sleep(1000);

                    // ====== 串口断开重连检查 ======
                    // 如果串口被意外断开（线松了、PLC重启了），尝试重连
                    if (_serialPort != null && !_serialPort.IsOpen)
                    {
                        this.BeginInvoke(
                            new Action(() =>
                            {
                                label1.Text = "PLC-1：离线";
                                _lblPLC.Text = "PLC-1：离线";
                                AppendLog("串口断开，尝试重连。");
                            })
                        );

                        try
                        {
                            //关闭旧的资源串口
                            _serialPort.Dispose();
                            _serialPort = null;

                            //重新创建并打开
                            if (!int.TryParse(comboBox2.Text, out int baudRateReconnect))
                            {
                                this.BeginInvoke(
                                    new Action(() => AppendLog("重连失败：波特率无效"))
                                );
                                continue;
                            }
                            _serialPort = new SerialPort(
                                comboBox1.Text,
                                baudRateReconnect,
                                Parity.None,
                                8,
                                StopBits.One
                            );
                            _serialPort.Open();

                            this.BeginInvoke(
                                new Action(() =>
                                {
                                    label1.Text = "PLC-1：已连接";
                                    _lblPLC.Text = "PLC-1：已连接";
                                    AppendLog("串口重连成功。");
                                })
                            );
                        }
                        catch
                        {
                            this.BeginInvoke(
                                new Action(() =>
                                {
                                    AppendLog("重连失败，5秒后重试");
                                })
                            );
                        }
                    }
                    // 工业场景：TCP 连接可能意外断开，需要定时检查

                    // 心跳包 = 定时发一段小数据给 PLC，PLC 回复说明还活着
                    // 收不到回复 → 判定断开 → 自动重连

                    // 在你的生产者循环里可以这样：
                    if (_tcpClient != null && !_tcpClient.Connected)
                    {
                        AppendLog("TCP 断开，尝试重连...");
                        try
                        {
                            _tcpClient.Close();
                            _tcpClient = new TcpClient();
                            await _tcpClient.ConnectAsync("192.168.1.100", 502);
                            AppendLog("TCP 重连成功");
                        }
                        catch
                        {
                            // 重试
                        }
                    }
                }
            });
        }

        /// <summary>
        /// 消费者：UI 线程每 500ms 从队列取数据 → 更新 DataGridView
        /// </summary>
        private void ConsumeTimer_Tick(object sender, EventArgs e)
        {
            // 一次最多处理 10 条，避免 UI 卡太久
            int processed = 0;

            while (_dataQueue.TryDequeue(out DeviceData newData) && processed < 10)
            {
                foreach (var device in _dataList)
                {
                    if (device.DeviceName == newData.DeviceName)
                    {
                        device.Value = newData.Value;
                        device.Unit = newData.Unit;
                        device.Status = newData.Status;
                        break;
                    }
                }
                processed++;
            }

            if (processed > 0)
            {
                // 每 5 秒存一次数据库
                if (_dbHelper != null && DateTime.Now.Second % 5 == 0)
                {
                    foreach (var device in _dataList)
                    {
                        _dbHelper.InsertRecord(
                            device.DeviceName,
                            device.Value,
                            device.Unit,
                            device.Status
                        );
                    }
                    AppendLog($"数据已存入 SQLite：{_dataList.Count} 条");
                }

                //报警判断
                foreach (var device in _dataList)
                {
                    // 只对温度设备做报警判断
                    if (device.DeviceName.Contains("温度"))
                    {
                        if (device.Value > _alarmHigh || device.Value < _alarmLow)
                        {
                            device.Status = "报警";

                            //保存当前时间，避免每500ms弹一次
                            if (_alarmShown == null)
                                _alarmShown = new Dictionary<string, DateTime>();
                            _alarmShown[device.DeviceName] = DateTime.Now;

                            //弹窗
                            if (
                                !_alarmShown.ContainsKey(device.DeviceName)
                                || (DateTime.Now - _alarmShown[device.DeviceName]).TotalSeconds > 30
                            )
                            {
                                //弹窗
                                MessageBox.Show(
                                    $"⚠ {device.DeviceName} 超限！当前值：{device.DisplayValue}",
                                    "设备报警",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning
                                );

                                //存报警到数据库
                                _dbHelper.InsertAlarmRecord(
                                    device.DeviceName,
                                    device.Value,
                                    $"超限：{device.DisplayValue}"
                                );
                            }
                        }
                    }
                }

                dataGridView1.Refresh();

                foreach (var device in _dataList)
                {
                    if (device.DeviceName == "反应釜A-温度")
                    {
                        //添加新数据点（X=时间，Y=数值）
                        _chartTemperature.Series["温度"].Points.AddXY(DateTime.Now, device.Value);

                        // 保持最多 60 个点，多了删最早的
                        while (_chartTemperature.Series["温度"].Points.Count > 60)
                            _chartTemperature.Series["温度"].Points.RemoveAt(0);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 窗体关闭时：停止后台线程
        /// </summary>\
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _cts?.Cancel();

            // 关闭串口：fire-and-forget，不阻塞窗口关闭
            var port = _serialPort;
            var master = _modbusMaster;
            _serialPort = null;
            _modbusMaster = null;

            if (port != null)
            {
                Task.Run(() =>
                {
                    try
                    {
                        master?.Dispose();
                        if (port.IsOpen)
                            port.Close();
                        port.Dispose();
                    }
                    catch { }
                });
            }

            base.OnFormClosing(e);
        }

        /// <summary>
        /// 从 exe 同级的 appsettings.json 读取配置
        /// 如果文件不存在，用默认值
        /// </summary>
        private void LoadConfigFromFile()
        {
            string configPath = Path.Combine(Application.StartupPath, "appsettings.json");

            if (!File.Exists(configPath))
            {
                AppendLog("配置文件不存在，使用默认值");
                return;
            }

            try
            {
                string json = File.ReadAllText(configPath);
                var config = new JavaScriptSerializer().Deserialize<AppConfig>(json);

                comboBox1.Text = config.PortName;
                comboBox2.Text = config.BaudRate.ToString();
                numericUpDown1.Value = config.Interval;
                checkBox1.Checked = config.EnableAlarm;
                checkBox2.Checked = config.EnableAutoSave;

                radioButton1.Checked = config.WorkMode != "自动";
                radioButton2.Checked = config.WorkMode == "自动";

                AppendLog($"已加载配置：{configPath}");
            }
            catch (Exception ex)
            {
                AppendLog($"加载配置失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 设置开机自启
        /// </summary>
        private static void SetAutoStart()
        {
            try
            {
                string appName = "EquipmentMonitorDay1";
                string appPath = Application.ExecutablePath;

                using (
                    var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                        true
                    )
                )
                {
                    if (key != null)
                    {
                        key.SetValue(appName, appPath);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"设置开机启动失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 取消开机自启
        /// </summary>
        private static void RemoveAutoStart()
        {
            try
            {
                string appName = "EquipmentMonitorDay1";

                using (
                    var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                        true
                    )
                )
                {
                    if (key != null && key.GetValue(appName) != null)
                    {
                        key.DeleteValue(appName);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"取消开机启动失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 串口收到数据时触发（在后台线程！不能直接更新 UI）
        /// </summary>
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                SerialPort port = (SerialPort)sender;
                int bytesToRead = port.BytesToRead;
                byte[] buffer = new byte[bytesToRead];
                port.Read(buffer, 0, bytesToRead);

                // 收到的数据放到队列，让消费者处理
                // 这里在后台线程，不能直接改 UI
                string hexString = BitConverter.ToString(buffer);

                // 通过 Invoke 写日志
                this.BeginInvoke(
                    new Action(() => AppendLog($"收到{bytesToRead} 字节：{hexString}"))
                );
            }
            catch (Exception ex)
            {
                this.BeginInvoke(new Action(() => AppendLog($"处理串口数据失败：{ex.Message}")));
            }
        }

        /// <summary>
        /// 发送数据到串口
        /// 可以在工具栏加个"发送"按钮调用
        /// </summary>
        private void BtnSend_Click(object sender, EventArgs e)
        {
            if (_serialPort == null || !_serialPort.IsOpen)
            {
                AppendLog("串口未打开，无法发送");
                return;
            }
            try
            {
                // 发送一段测试数据：01 03 00 00 00 01 84 0A
                byte[] testData = { 0x01, 0x03, 0x00, 0x00, 0x00, 0x01, 0x84, 0x0A };
                _serialPort.Write(testData, 0, testData.Length);
                AppendLog($"发送 {testData.Length} 字节数据");
            }
            catch (Exception ex)
            {
                AppendLog($"发送失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 演示：字节数组怎么转成数值
        /// </summary>
        private void TestDataParsing()
        {
            byte[] rawData = { 0x00, 0x85, 0x01, 0x0F, 0x33, 0x33, 0xAB, 0x42 };

            // 第 1~2 字节：00 85 → 温度值（大端）
            /*rawData[0]	0x00	高字节（高位）
              rawData[1]	0x85 = 133	低字节（低位）
              rawData[0] << 8	0x00 << 8 = 0x0000	将高字节左移 8 位，放到高 8 位位置*/
            int tempRaw = (rawData[0] << 8) + rawData[1]; //	0x0000 + 0x85 = 0x0085 = 133
            double temperature = tempRaw / 10;

            // 第 3~4 字节：01 0F → 压力值（大端）
            int pressRaw = (rawData[2] << 8) + rawData[3]; // 0x010F = 271
            double pressure = pressRaw / 100.0; // 2.71 MPa

            AppendLog($"========== 数据解析演示 ==========");
            AppendLog($"原始字节：{BitConverter.ToString(rawData)}");
            AppendLog($"温度：{temperature}℃  （00 85 → 133 → ÷10 → 13.3℃）");
            AppendLog($"压力：{pressure}MPa  （01 0F → 271 → ÷100 → 2.71MPa）");

            // 假设收到 4 个字节：01 02 03 04

            // 大端（Big-Endian）：高位在前（PLC 默认方式）
            //  值 = 0x01020304 = 16909060

            // 小端（Little-Endian）：低位在前（Windows 默认方式）
            //  值 = 0x04030201 = 67305985
        }

        /// <summary>
        /// 实战：解析一段模拟的工业设备报文
        /// PLC 传来 9 个字节，包含温度、压力、状态
        /// </summary>
        private void ParseIndustryMessage(byte[] message)
        {
            // 报文格式（假设的工业协议）：
            // ┌────   ┬───   ─┬────   ┬───  ─┬───  ─┬────   ┬────   ┬────┬────┐
            // │ 温度高 │ 温度低 │ 压力高 │ 压力低 │ 状态  │ CRC低 │ CRC高 │
            // │  0x00 │  0x85 │  0x01 │  0x0F│  0x01│  0x__ │  0x__ │
            // └───   ─┴──   ──┴───   ─┴───  ─┴───  ─┴────   ┴───   ─┴────┴────┘
            if (message.Length < 7)
                return;

            // ====== 1. 解析温度（大端，精度 0.1） ======
            // 第 0~1 字节：00 85 → 0x0085 = 133 → 13.3℃
            int tempRaw = (message[0] << 8) + message[1];
            double temperature = tempRaw / 10.0;

            // ====== 2. 解析压力（大端，精度 0.01） ======
            // 第 2~3 字节：01 0F → 0x010F = 271 → 2.71 MPa
            int pressRaw = (message[2] << 8) + message[3];
            double pressure = pressRaw / 100.0;

            // ====== 3. 解析状态 ======
            // 第 4 字节：0x01 = 正常，0x02 = 报警，0x03 = 离线
            string status;
            switch (message[4])
            {
                case 0x01:
                    status = "正常";
                    break;
                case 0x02:
                    status = "报警";
                    break;
                case 0x03:
                    status = "离线";
                    break;
                default:
                    status = "未知";
                    break;
            }

            // 输出到日志
            AppendLog("========== 工业报文解析 ==========");
            AppendLog($"原始报文：{BitConverter.ToString(message)}");
            AppendLog($"温度：{temperature}℃");
            AppendLog($"压力：{pressure}MPa");
            AppendLog($"状态：{status}");
            AppendLog($"=================================");
        }

        /// <summary>
        /// 自动发送定时器：每 3 秒发一段测试数据
        /// </summary>
        private void AutoSendTimer_Tick(object sender, EventArgs e)
        {
            if (_serialPort == null || !_serialPort.IsOpen)
            {
                AppendLog("串口未打开，无法自动发送");
                _autoSendTimer.Stop();
            }
            try
            {
                // 发送 Modbus 读寄存器测试报文
                byte[] data = { 0x01, 0x03, 0x00, 0x00, 0x00, 0x01, 0x84, 0x0A };
                _serialPort.Write(data, 0, data.Length);
                AppendLog($"自动发送：{BitConverter.ToString(data)}");
            }
            catch (Exception ex)
            {
                AppendLog($"自动发送异常：{ex.Message}");
            }
        }

        /// <summary>
        /// 构建 Modbus RTU 读保持寄存器报文（功能码 0x03）
        /// </summary>
        private byte[] BuildReadHoldingRegisters(byte address, ushort startReg, ushort regCount)
        {
            byte[] packet = new byte[8];

            packet[0] = address; // 设备地址
            packet[1] = 0x03; // 功能码
            packet[2] = (byte)(startReg >> 8); // 起始地址高位
            packet[3] = (byte)(startReg & 0xFF); // 起始地址低位
            packet[4] = (byte)(regCount >> 8); // 数量高位
            packet[5] = (byte)(regCount & 0xFF); // 数量低位

            byte[] crc = CalculateCRC16(packet, 6); // 前 6 字节算 CRC
            packet[6] = crc[0]; // CRC 低字节
            packet[7] = crc[1]; // CRC 高字节

            return packet;
        }

        /// <summary>
        /// CRC16-Modbus 校验（带长度参数）（面试手写频率最高）
        /// </summary>
        private byte[] CalculateCRC16(byte[] data, int length)
        {
            ushort crc = 0xFFFF;

            for (int i = 0; i < length; i++)
            {
                crc ^= data[i];
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x0001) != 0) //运算符按位与:两个整数逐位做与运算，对应位都为 1 结果才为 1
                        crc = (ushort)((crc >> 1) ^ 0xA001);
                    else
                        crc >>= 1;
                }
            }
            return new byte[] { (byte)(crc & 0xFF), (byte)(crc >> 8) };
        }

        /// <summary>
        /// 用 NModbus4 读取保持寄存器（异步，不阻塞 UI）
        /// </summary>
        private async void ReadModbusRegisters()
        {
            // 先捕获本地引用，避免读写期间 _modbusMaster 被其他线程置 null
            var master = _modbusMaster;
            if (master == null)
            {
                AppendLog("Modbus 未连接");
                return;
            }

            // 设置串口超时：等 2 秒没回复就放弃
            master.Transport.ReadTimeout = 2000;

            AppendLog("正在读取寄存器...");

            try
            {
                // Task.Run 放到后台线程，避免阻塞 UI
                ushort[] value = await Task.Run(() => master.ReadHoldingRegisters(1, 0, 3));
                AppendLog($"寄存器0~2：{value[0]}, {value[1]}, {value[2]}");
            }
            catch (TimeoutException)
            {
                AppendLog("读取超时：没有从站响应");
            }
            catch (Exception ex)
            {
                AppendLog($"读取寄存器失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 在 COM6 启动 Modbus RTU 从站（模拟 PLC）
        /// </summary>
        private void StartModbusSlave()
        {
            if (_modbusSlave != null)
                return;

            try
            {
                SerialPort slavePort = new SerialPort("COM6", 9600, Parity.None, 8, StopBits.One);
                slavePort.Open();
                AppendLog($"从站端口已打开：COM6");

                _modbusSlave = ModbusSerialSlave.CreateRtu(1, slavePort);
                _modbusSlave.DataStore = DataStoreFactory.CreateDefaultDataStore(10, 10, 10, 10);
                _modbusSlave.DataStore.HoldingRegisters[1] = 85;
                _modbusSlave.DataStore.HoldingRegisters[2] = 271;
                _modbusSlave.DataStore.HoldingRegisters[3] = 100;

                _slaveCts = new CancellationTokenSource();
                _slaveTask = Task.Run(() =>
                {
                    AppendLog("从站开始监听...");
                    _modbusSlave.Listen();
                });

                AppendLog("Modbus 从站已启动（COM6，地址 1）");
            }
            catch (Exception ex)
            {
                AppendLog($"启动从站失败：{ex.Message}");
            }
        }

        /// <summary>
        /// TCP 测试：连接一个公共测试服务器，收发数据
        /// </summary>
        private async Task TestTcpConnection()
        {
            try
            {
                AppendLog("正在连接 TCP 服务器...");

                _tcpCts = new CancellationTokenSource();
                _tcpClient = new TcpClient();

                // 异步连接，超时 3 秒
                var connectTask = _tcpClient.ConnectAsync("www.baidu.com", 80);
                if (await Task.WhenAny(connectTask, Task.Delay(3000)) == connectTask)
                {
                    AppendLog("TCP 连接成功");
                    _tcpStream = _tcpClient.GetStream();

                    // 发一个 HTTP GET 请求
                    string httpRequest =
                        "GET / HTTP/1.1\r\nHost: www.baidu.com\r\nConnection: close\r\n\r\n";
                    byte[] sendData = Encoding.UTF8.GetBytes(httpRequest);
                    await _tcpStream.WriteAsync(sendData, 0, sendData.Length, _tcpCts.Token);
                    AppendLog($"已发送 {sendData.Length} 字节");

                    // 收响应
                    byte[] buffer = new byte[1024];
                    int received = await _tcpStream.ReadAsync(
                        buffer,
                        0,
                        buffer.Length,
                        _tcpCts.Token
                    );
                    string response = Encoding.UTF8.GetString(buffer, 0, received);
                    AppendLog(
                        $"收到 {received} 字节，前 100 字：{response.Substring(0, Math.Min(100, response.Length))}"
                    );

                    _tcpClient.Close();
                }
                else
                {
                    AppendLog("TCP 连接超时");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"TCP 通信失败：{ex.Message}");
            }
        }

        /// <summary>
        /// Modbus TCP 测试
        /// </summary>
        private async Task TestModbusTcp()
        {
            try
            {
                //连接Modbus TCP 从站（本地测试，自己连自己）
                var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync("127.0.0.1", 502);
                var master = ModbusIpMaster.CreateIp(tcpClient);

                ushort[] values = await master.ReadHoldingRegistersAsync(1, 0, 3);
                AppendLog($"Modbus TCP 读取：{values[0]}, {values[1]}, {values[2]}");

                tcpClient.Close();
            }
            catch (Exception ex)
            {
                AppendLog($"Modbus TCP 测试失败：{ex.Message}");
            }
        }
    }
}
