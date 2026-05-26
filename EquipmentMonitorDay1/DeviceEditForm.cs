using System.Windows.Forms;

namespace EquipmentMonitorDay1
{
    /// <summary>
    /// 添加/编辑设备数据的弹出窗口
    /// </summary>
    public partial class DeviceEditForm : Form
    {
        // ====== 公开属性：主窗体通过这两个属性读写数据 ======
        public string DeviceName
        {
            get => _txtDeviceName.Text.Trim();
            set => _txtDeviceName.Text = value;
        }

        public double DeviceValue
        {
            get => (double)_numValue.Value;
            set => _numValue.Value = (decimal)value;
        }

        public string Unit
        {
            get => _cmbUnit.Text;
            set => _cmbUnit.Text = value;
        }

        public string Status
        {
            get => _cmbStatus.Text;
            set => _cmbStatus.Text = value;
        }


        public DeviceEditForm()
        {
            InitializeComponent();
            this.Text = "添加设备";
            _numValue.Maximum = 10000;
            _numValue.DecimalPlaces = 1;
        }
    }
}
