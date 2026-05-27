using System;

namespace EquipmentMonitorDay1
{
    /// <summary>
    /// 设备数据模型
    /// </summary>
    public class DeviceData
    {
        public bool IsSelected { get; set; }
        public string DeviceName { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; }
        public string Status { get; set; }
        public DateTime UpdateTime { get; set; }

        // 显示在 DataGridView 里的格式化字符串
        public string DisplayValue => $"{Value} {Unit}";
    }
}
