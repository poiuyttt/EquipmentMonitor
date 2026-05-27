# EquipmentMonitor — 工业设备数据采集监控系统

基于 **WinForms (.NET Framework 4.8)** 的上位机监控系统，涵盖串口/Modbus/TCP 通信、实时曲线、SQLite 持久化与报警管理。

## 技术栈

| 层级 | 技术 |
|---|---|
| 界面 | WinForms + DataGridView + Chart |
| 通信 | SerialPort / NModbus4 (Modbus RTU) / TcpClient |
| 数据 | SQLite / NPOI (Excel) / NLog |
| 架构 | 生产者-消费者 (Task.Run + ConcurrentQueue) |
| 部署 | 自包含发布 / Inno Setup / 看门狗 |

## 功能

- 串口通信（9600-8-N-1，超时设定，断开自动重连）
- Modbus RTU 主站（读写保持寄存器）+ 内置从站模拟器
- TCP 异步通信（心跳包 + 断线重连）
- 实时曲线（Chart，60 秒滚动）
- SQLite 历史数据存储（自动清理 30 天前数据）
- 报警阈值配置 + 弹窗 + 记录入库
- Excel 导出（NPOI）
- NLog 日志（按天切割，自动删除）
- 全局异常捕获 + 看门狗 (watchdog.bat) 崩溃自启
- appsettings.json 参数外置，免重新编译

## 项目结构

```
EquipmentMonitor/
├── MainForm.cs              # 主窗口（UI / 事件 / Chart / 报警）
├── DatabaseHelper.cs        # SQLite 数据访问
├── AppLogger.cs             # NLog 日志封装
├── AppConfig.cs             # 配置模型
├── DeviceData.cs            # 设备数据模型
├── DeviceCard.cs            # 自定义卡片控件
├── DeviceEditForm.cs        # 编辑弹窗
├── Program.cs               # 入口 + 全局异常捕获
├── NLog.config              # NLog 配置
├── appsettings.json         # 外部配置
└── watchdog.bat             # 看门狗
```

## 快速开始

```bash
# 1. Visual Studio 打开 EquipmentMonitor.slnx
# 2. 安装 com0com 虚拟串口 (COM5 ↔ COM6)
# 3. F5 运行，选 COM5 / 9600
# 4. 点"开始采集"→ 点"启动从站"→ 点"读寄存器"验证通信
```

## NuGet 依赖

| 包 | 用途 |
|---|---|
| NModbus4 | Modbus RTU 主站/从站 |
| System.Data.SQLite | 数据持久化 |
| NPOI | Excel 导出 |
| NLog | 日志记录 |

## 发布

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## 面试亮点

- 生产者-消费者异步架构，UI 不卡顿
- 串口 + Modbus RTU + TCP 全协议覆盖
- 全局异常捕获 + 串口重连 + 看门狗，7×24h 运行
- com0com 虚拟串口完成全链路测试
