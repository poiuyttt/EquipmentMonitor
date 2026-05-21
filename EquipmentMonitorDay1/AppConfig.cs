namespace EquipmentMonitorDay1
{
    public class AppConfig
    {
        public string PortName { get; set; } = "COM1";

        public int BaudRate { get; set; } = 9600;

        public int Interval { get; set; } = 1000;

        public bool EnableAlarm { get; set; } = true;

        public bool EnableAutoSave { get; set; } = false;

        public string WorkMode { get; set; } = "手动";
    }
}
