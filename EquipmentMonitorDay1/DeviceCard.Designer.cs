namespace EquipmentMonitorDay1
{
    partial class DeviceCard
    {
        /// <summary> 
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region 组件设计器生成的代码

        /// <summary> 
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this._panelBackground = new System.Windows.Forms.Panel();
            this._lblStatus = new System.Windows.Forms.Label();
            this._lblValue = new System.Windows.Forms.Label();
            this._lblDeviceName = new System.Windows.Forms.Label();
            this._panelBackground.SuspendLayout();
            this.SuspendLayout();
            // 
            // _panelBackground
            // 
            this._panelBackground.Controls.Add(this._lblStatus);
            this._panelBackground.Controls.Add(this._lblValue);
            this._panelBackground.Controls.Add(this._lblDeviceName);
            this._panelBackground.Dock = System.Windows.Forms.DockStyle.Fill;
            this._panelBackground.Location = new System.Drawing.Point(0, 0);
            this._panelBackground.Name = "_panelBackground";
            this._panelBackground.Size = new System.Drawing.Size(200, 56);
            this._panelBackground.TabIndex = 0;
            // 
            // _lblStatus
            // 
            this._lblStatus.AutoSize = true;
            this._lblStatus.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Bold);
            this._lblStatus.Location = new System.Drawing.Point(3, 40);
            this._lblStatus.Name = "_lblStatus";
            this._lblStatus.Size = new System.Drawing.Size(41, 12);
            this._lblStatus.TabIndex = 3;
            this._lblStatus.Text = "label3";
            // 
            // _lblValue
            // 
            this._lblValue.AutoSize = true;
            this._lblValue.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Bold);
            this._lblValue.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(122)))), ((int)(((byte)(204)))));
            this._lblValue.Location = new System.Drawing.Point(3, 18);
            this._lblValue.Name = "_lblValue";
            this._lblValue.Size = new System.Drawing.Size(41, 12);
            this._lblValue.TabIndex = 2;
            this._lblValue.Text = "label2";
            // 
            // _lblDeviceName
            // 
            this._lblDeviceName.AutoSize = true;
            this._lblDeviceName.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Bold);
            this._lblDeviceName.Location = new System.Drawing.Point(3, 3);
            this._lblDeviceName.Name = "_lblDeviceName";
            this._lblDeviceName.Size = new System.Drawing.Size(41, 12);
            this._lblDeviceName.TabIndex = 1;
            this._lblDeviceName.Text = "label1";
            // 
            // DeviceCard
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.Controls.Add(this._panelBackground);
            this.Name = "DeviceCard";
            this.Size = new System.Drawing.Size(200, 56);
            this._panelBackground.ResumeLayout(false);
            this._panelBackground.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel _panelBackground;
        private System.Windows.Forms.Label _lblValue;
        private System.Windows.Forms.Label _lblDeviceName;
        private System.Windows.Forms.Label _lblStatus;
    }
}
