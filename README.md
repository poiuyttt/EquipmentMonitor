# 工业设备监控系统 (EquipmentMonitor)

基于 .NET Framework 4.8 和 WinForms 开发的工业级上位机数据采集监控系统，支持串口/Modbus RTU/TCP 多种通信协议，实现数据实时采集、展示、存储、报警和导出功能。

## 项目亮点

- **生产者-消费者异步架构**：采用 ConcurrentQueue 实现后台数据采集与 UI 更新的解耦，确保界面流畅不卡顿
- **多协议通信支持**：内置串口、Modbus RTU 主站/从站、TCP 通信模块，适应多样化工业现场场景
- **工业级稳定性**：全局异常捕获 + 断线自动重连 + 看门狗自启动 + NLog 日志系统，支持 7×24 小时稳定运行
- **完整的数据链路**：实时采集 → 内存展示 → SQLite 持久化 → Excel 导出，形成闭环数据管理
- **用户友好界面**：扁平化设计 + 设备卡片 + 实时曲线 + 状态颜色标识，提升操作体验

## 技术栈

| 类别 | 技术选型 |
|------|----------|
| **界面框架** | WinForms + DataGridView + Chart Controls |
| **通信协议** | SerialPort (串口) + NModbus4 (Modbus RTU) + TcpClient (TCP) |
| **数据存储** | SQLite (System.Data.SQLite) |
| **日志系统** | NLog (按天切割 + 自动清理) |
| **数据导出** | NPOI (Excel .xlsx) |
| **异步模型** | Task.Run + ConcurrentQueue + CancellationToken |
| **部署方案** | 自包含发布 + 看门狗脚本 |

## 核心功能

### 通信层
- 串口通信：支持波特率、数据位、停止位、校验位配置，超时设置，断线自动重连
- Modbus RTU 主站：读写保持寄存器（功能码 0x03/0x06/0x10），支持多设备地址
- Modbus RTU 从站：内置模拟器，无需真实 PLC 即可完成开发调试
- TCP 通信：异步连接 + 心跳检测 + 断线重连机制

### 数据处理
- 生产者-消费者模式：后台线程采集 → 线程安全队列 → UI 定时器消费
- 实时曲线展示：Chart 控件绘制 60 秒数据趋势图
- SQLite 持久化：每秒采样，支持按设备/时间范围查询，自动清理 30 天前历史数据

### 报警系统
- 阈值配置：支持上下限报警阈值设置
- 自动报警：数据超限自动弹窗 + 报警记录写入数据库
- 防抖动机制：30 秒内同一设备不重复弹窗
- 报警筛选：一键筛选仅显示报警设备

### 数据管理
- Excel 导出：NPOI 导出历史数据为 .xlsx 格式
- 配置管理：appsettings.json 外部配置，免重新编译
- 日志记录：NLog 按天切割，自动删除 30 天前日志
- 看门狗：batch 脚本监控程序状态，崩溃后 5 秒自动重启

## 项目结构

```
EquipmentMonitor/
├── MainForm.cs              # 主窗口：UI 交互 + 事件处理 + 曲线展示 + 报警逻辑
├── DatabaseHelper.cs        # SQLite 数据访问层：CRUD 操作 + 历史数据查询
├── AppLogger.cs             # NLog 日志封装：统一日志接口
├── AppConfig.cs             # 配置模型：appsettings.json 映射
├── DeviceData.cs            # 设备数据模型：数据绑定 + 状态管理
├── DeviceCard.cs            # 自定义设备卡片控件
├── DeviceEditForm.cs        # 设备添加/编辑弹窗
├── Program.cs               # 程序入口 + 全局异常捕获
├── NLog.config              # NLog 配置文件
├── appsettings.json         # 外部配置文件
└── watchdog.bat             # 看门狗脚本
```

## 快速开始

### 环境要求
- Windows 10/11
- Visual Studio 2019+
- .NET Framework 4.8
- （可选）com0com 虚拟串口工具

### 运行步骤

1. 克隆项目并打开解决方案
   ```bash
   git clone <repo-url>
   cd EquipmentMonitor
   ```

2. 使用 Visual Studio 打开 `EquipmentMonitor.slnx`

3. 还原 NuGet 包
   ```bash
   # Visual Studio 会自动还原，或使用 NuGet 控制台
   Update-Package -reinstall
   ```

4. （可选）使用 com0com 创建虚拟串口对（如 COM5 ↔ COM6）

5. 编译并运行程序

6. 配置串口参数，点击"启动"开始采集

7. （可选）点击"启动从站"在另一端模拟 Modbus 设备

## 配置说明

### appsettings.json
```json
{
  "PortName": "COM5",
  "BaudRate": 9600,
  "Interval": 1000,
  "EnableAlarm": true,
  "EnableAutoSave": true
}
```

### NLog.config
- 日志按天切割，存储在 `Logs/` 目录
- 自动保留 30 天日志

## 发布部署

### 自包含发布
```bash
# 发布为单文件可执行程序
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

### 看门狗使用
1. 将 `watchdog.bat` 与发布的程序放在同一目录
2. 运行 `watchdog.bat` 即可实现程序崩溃自动重启

## NuGet 依赖

| 包名 | 版本 | 用途 |
|------|------|------|
| NModbus4 | 2.1.0 | Modbus RTU 主站/从站通信 |
| System.Data.SQLite | 1.0.119 | 本地关系型数据库 |
| NPOI | 2.8.0 | Excel 文件读写 |
| NLog | 6.1.3 | 日志记录框架 |
| SkiaSharp | 3.119.2 | 图形渲染（Chart 依赖） |

## 设计模式与架构

- **分层架构**：UI 层（MainForm）→ 业务层 → 数据访问层（DatabaseHelper）→ 通信层
- **生产者-消费者模式**：后台采集线程（生产者）与 UI 消费线程分离
- **数据绑定**：使用 BindingList + BindingSource 实现 UI 自动刷新
- **事件驱动**：基于 Windows Forms 事件模型处理用户交互

## 应用场景

- 工业生产现场设备监控
- 实验室数据采集系统
- 教学演示（上位机开发、Modbus 通信）
- 小型自动化项目集成

## 开发心得

通过这个项目，深入理解了：
- WinForms 桌面应用开发与数据绑定
- 工业通信协议（Modbus RTU/TCP）的实现
- 多线程编程与线程安全数据结构
- 生产者-消费者异步架构设计
- SQLite 本地数据库的集成与使用
- 工业级软件的稳定性保障措施
