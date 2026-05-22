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
            get => _lblStatus.Text;
            set
            {
                _lblStatus.Text = value;
                UpdateStatusColor(value);
            }
        }

        private void UpdateStatusColor(string value)
        {
            switch (value)
            {
                case "正常":
                    _panelBackground.BackColor = Color.LightGreen;
                    _lblStatus.ForeColor = Color.Green;
                    break;
                case "报警":
                    _panelBackground.BackColor = Color.LightPink;
                    _lblStatus.ForeColor = Color.Red;
                    break;
                case "离线":
                    _panelBackground.BackColor = Color.LightGray;
                    _lblStatus.ForeColor = Color.Gray;
                    break;
                default:
                    _panelBackground.BackColor = Color.White;
                    _lblStatus.ForeColor = Color.Black;
                    break;
            }
        }


    }
}
