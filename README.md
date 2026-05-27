# EquipmentMonitor — 工业设备数据采集监控系统

基于 **WinForms + .NET Framework 4.8** 的纯上位机监控系统。

## 功能

- 串口通信（9600-8-N-1）
- Modbus RTU 主站 + 从站
- 实时曲线 + SQLite 存储
- 报警 + Excel 导出
- 生产者-消费者架构
- 看门狗自启

## 项目结构

```
EquipmentMonitor/
├── MainForm.cs           # UI 层
├── DatabaseHelper.cs     # 数据层
├── AppLogger.cs          # 日志
├── DeviceData.cs         # 模型
├── DeviceCard.cs         # 自定义控件
├── DeviceEditForm.cs     # 编辑弹窗
└── watchdog.bat          # 看门狗
```
