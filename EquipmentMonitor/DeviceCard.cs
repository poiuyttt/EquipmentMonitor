using System.Drawing;
using System.Windows.Forms;

namespace EquipmentMonitorDay1
{
    public partial class DeviceCard : UserControl
    {
        public DeviceCard()
        {
            InitializeComponent();

            this.BackColor = Color.White;
            this.BorderStyle = BorderStyle.FixedSingle;

            // 把方形 Panel 切成圆形
            // GraphicsPath = 画路径，AddEllipse = 画椭圆
            // 宽高一样就是正圆
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path.AddEllipse(0, 0, _indicator.Width - 1, _indicator.Height - 1);
                _indicator.Region = new Region(path);
            }
        }

        public string DeviceName
        {
            get => _lblDeviceName.Text;
            set => _lblDeviceName.Text = value;
        }

        public string DisplayValue
        {
            get => _lblValue.Text;
            set => _lblValue.Text = value;
        }

        public string Status
        {
            get => _lblStatusText.Text;
            set
            {
                _lblStatusText.Text = value;
                UpdateIndicator(value);
            }
        }

        private void UpdateIndicator(string value)
        {
            switch (value)
            {
                case "正常":
                    _indicator.BackColor = Color.LimeGreen;
                    break;
                case "报警":
                    _indicator.BackColor = Color.Red;
                    break;
                case "离线":
                    _indicator.BackColor = Color.Gray;
                    break;
                default:
                    _indicator.BackColor = Color.White;
                    break;
            }

            // 背景色也随状态变
            switch (value)
            {
                case "正常":
                    _panelBackground.BackColor = Color.LightGreen;
                    break;
                case "报警":
                    _panelBackground.BackColor = Color.LightPink;
                    break;
                case "离线":
                    _panelBackground.BackColor = Color.LightGray;
                    break;
                default:
                    _panelBackground.BackColor = Color.White;
                    break;
            }
        }
    }
}
