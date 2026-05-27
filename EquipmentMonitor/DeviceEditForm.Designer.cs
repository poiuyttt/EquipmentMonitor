using System.Windows.Forms;
using System.Drawing;

namespace EquipmentMonitorDay1
{
    partial class DeviceEditForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this._txtDeviceName = new System.Windows.Forms.TextBox();
            this._numValue = new System.Windows.Forms.NumericUpDown();
            this._cmbUnit = new System.Windows.Forms.ComboBox();
            this._cmbStatus = new System.Windows.Forms.ComboBox();
            this._btnOK = new System.Windows.Forms.Button();
            this._btnCancel = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this._numValue)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(105, 49);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(65, 12);
            this.label1.TabIndex = 0;
            this.label1.Text = "设备名称：";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(105, 89);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(41, 12);
            this.label2.TabIndex = 1;
            this.label2.Text = "数值：";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(105, 124);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(41, 12);
            this.label3.TabIndex = 2;
            this.label3.Text = "单位：";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(105, 163);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(29, 12);
            this.label4.TabIndex = 3;
            this.label4.Text = "状态";
            // 
            // _txtDeviceName
            // 
            this._txtDeviceName.Location = new System.Drawing.Point(230, 49);
            this._txtDeviceName.Name = "_txtDeviceName";
            this._txtDeviceName.Size = new System.Drawing.Size(100, 21);
            this._txtDeviceName.TabIndex = 4;
            // 
            // _numValue
            // 
            this._numValue.Location = new System.Drawing.Point(230, 87);
            this._numValue.Name = "_numValue";
            this._numValue.Size = new System.Drawing.Size(120, 21);
            this._numValue.TabIndex = 5;
            // 
            // _cmbUnit
            // 
            this._cmbUnit.FormattingEnabled = true;
            this._cmbUnit.Items.AddRange(new object[] {
            "℃",
            "MPa",
            "%",
            "L/min"});
            this._cmbUnit.Location = new System.Drawing.Point(229, 121);
            this._cmbUnit.Name = "_cmbUnit";
            this._cmbUnit.Size = new System.Drawing.Size(121, 20);
            this._cmbUnit.TabIndex = 6;
            // 
            // _cmbStatus
            // 
            this._cmbStatus.FormattingEnabled = true;
            this._cmbStatus.Items.AddRange(new object[] {
            "正常",
            "报警",
            "离线"});
            this._cmbStatus.Location = new System.Drawing.Point(229, 160);
            this._cmbStatus.Name = "_cmbStatus";
            this._cmbStatus.Size = new System.Drawing.Size(121, 20);
            this._cmbStatus.TabIndex = 7;
            // 
            // _btnOK
            // 
            this._btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this._btnOK.Location = new System.Drawing.Point(127, 220);
            this._btnOK.Name = "_btnOK";
            this._btnOK.Size = new System.Drawing.Size(75, 23);
            this._btnOK.TabIndex = 8;
            this._btnOK.Text = "确定";
            this._btnOK.UseVisualStyleBackColor = true;
            // 
            // _btnCancel
            // 
            this._btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._btnCancel.Location = new System.Drawing.Point(255, 220);
            this._btnCancel.Name = "_btnCancel";
            this._btnCancel.Size = new System.Drawing.Size(75, 23);
            this._btnCancel.TabIndex = 9;
            this._btnCancel.Text = "取消";
            this._btnCancel.UseVisualStyleBackColor = true;
            // 
            // DeviceEditForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(465, 308);
            this.Controls.Add(this._btnCancel);
            this.Controls.Add(this._btnOK);
            this.Controls.Add(this._cmbStatus);
            this.Controls.Add(this._cmbUnit);
            this.Controls.Add(this._numValue);
            this.Controls.Add(this._txtDeviceName);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Name = "DeviceEditForm";
            this.Text = "DeviceEditForm";
            ((System.ComponentModel.ISupportInitialize)(this._numValue)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox _txtDeviceName;
        private System.Windows.Forms.NumericUpDown _numValue;
        private System.Windows.Forms.ComboBox _cmbUnit;
        private System.Windows.Forms.ComboBox _cmbStatus;
        private System.Windows.Forms.Button _btnOK;
        private System.Windows.Forms.Button _btnCancel;
    }
}