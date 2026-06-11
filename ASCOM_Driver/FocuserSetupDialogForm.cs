using System;
using System.Drawing;
using System.Windows.Forms;

namespace ASCOM.Autofocus
{
    public class FocuserSetupDialogForm : Form
    {
        private Focuser _driver;

        private CheckBox chkAutoDetect;
        private ComboBox cbComPort;
        private NumericUpDown numMaxPosition;
        private CheckBox chkReverseRotation;
        private NumericUpDown numTempCoeff;
        private CheckBox chkTrace;

        private Button btnOk;
        private Button btnCancel;
        private Button btnScan;

        public FocuserSetupDialogForm(Focuser driver)
        {
            _driver = driver;
            Text = "VestalFocuser beta 0.6.10 — Settings";
            Size = new Size(560, 520);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BuildUI();
            LoadSettings();
        }

        private void BuildUI()
        {
            // COM Port section
            var gbCom = new GroupBox
            {
                Text = "Serial Port",
                Location = new Point(16, 12),
                Size = new Size(520, 110)
            };

            chkAutoDetect = new CheckBox
            {
                Text = "Auto-detect COM port",
                Location = new Point(14, 22),
                Size = new Size(480, 24),
                Checked = true
            };
            chkAutoDetect.CheckedChanged += ChkAutoDetect_CheckedChanged;
            gbCom.Controls.Add(chkAutoDetect);

            var lblPort = new Label { Text = "Port:", Location = new Point(14, 55), Size = new Size(40, 24) };
            gbCom.Controls.Add(lblPort);

            cbComPort = new ComboBox
            {
                Location = new Point(60, 52),
                Size = new Size(140, 24),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            gbCom.Controls.Add(cbComPort);

            btnScan = new Button
            {
                Text = "Scan",
                Location = new Point(210, 51),
                Size = new Size(70, 26)
            };
            btnScan.Click += BtnScan_Click;
            gbCom.Controls.Add(btnScan);

            Controls.Add(gbCom);

            // Hardware section
            var gbHW = new GroupBox
            {
                Text = "Hardware",
                Location = new Point(16, 132),
                Size = new Size(520, 100)
            };

            var lblMax = new Label { Text = "Max Steps:", Location = new Point(14, 25), Size = new Size(90, 24) };
            gbHW.Controls.Add(lblMax);

            numMaxPosition = new NumericUpDown
            {
                Location = new Point(110, 23),
                Size = new Size(120, 24),
                Minimum = 100,
                Maximum = 100000,
                Value = 16384
            };
            gbHW.Controls.Add(numMaxPosition);

            chkReverseRotation = new CheckBox
            {
                Text = "Reverse rotation direction",
                Location = new Point(14, 55),
                Size = new Size(480, 24)
            };
            gbHW.Controls.Add(chkReverseRotation);

            Controls.Add(gbHW);

            // Temperature section
            var gbTemp = new GroupBox
            {
                Text = "Temperature Compensation",
                Location = new Point(16, 242),
                Size = new Size(520, 60)
            };

            var lblCoeff = new Label { Text = "Steps per °C:", Location = new Point(14, 25), Size = new Size(100, 24) };
            gbTemp.Controls.Add(lblCoeff);

            numTempCoeff = new NumericUpDown
            {
                Location = new Point(120, 23),
                Size = new Size(120, 24),
                Minimum = 0,
                Maximum = 1000,
                DecimalPlaces = 1,
                Increment = 1,
                Value = 0
            };
            gbTemp.Controls.Add(numTempCoeff);

            Controls.Add(gbTemp);

            // Diagnostics section
            var gbTrace = new GroupBox
            {
                Text = "Diagnostics",
                Location = new Point(16, 312),
                Size = new Size(520, 80)
            };

            chkTrace = new CheckBox
            {
                Text = "Enable trace logging",
                Location = new Point(14, 20),
                Size = new Size(200, 24)
            };
            gbTrace.Controls.Add(chkTrace);

            var lblTracePath = new Label
            {
                Text = "saved to Documents\\ASCOM\\Logs",
                Location = new Point(32, 44),
                Size = new Size(470, 20),
                ForeColor = System.Drawing.Color.Gray
            };
            gbTrace.Controls.Add(lblTracePath);

            Controls.Add(gbTrace);

            // Buttons
            btnOk = new Button
            {
                Text = "OK",
                Location = new Point(16, 415),
                Size = new Size(80, 28),
                DialogResult = DialogResult.OK
            };
            btnOk.Click += BtnOk_Click;
            Controls.Add(btnOk);

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(106, 415),
                Size = new Size(80, 28),
                DialogResult = DialogResult.Cancel
            };
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private void LoadSettings()
        {
            chkAutoDetect.Checked = Focuser.autoDetectComPort;
            numMaxPosition.Value = Focuser.maxPosition;
            chkReverseRotation.Checked = Focuser.reverseRotation;
            numTempCoeff.Value = (decimal)Focuser.tempCoefficient;
            chkTrace.Checked = _driver.tl.Enabled;

            // Populate COM ports
            ScanPorts();
            if (!string.IsNullOrEmpty(Focuser.comPortOverride))
            {
                for (int i = 0; i < cbComPort.Items.Count; i++)
                {
                    if (cbComPort.Items[i].ToString() == Focuser.comPortOverride)
                    {
                        cbComPort.SelectedIndex = i;
                        break;
                    }
                }
            }
            if (cbComPort.SelectedIndex < 0 && cbComPort.Items.Count > 0)
                cbComPort.SelectedIndex = 0;

            cbComPort.Enabled = !chkAutoDetect.Checked;
        }

        private void ScanPorts()
        {
            cbComPort.Items.Clear();
            foreach (var port in System.IO.Ports.SerialPort.GetPortNames())
            {
                cbComPort.Items.Add(port);
            }
        }

        private void BtnScan_Click(object sender, EventArgs e)
        {
            ScanPorts();
            if (cbComPort.Items.Count > 0) cbComPort.SelectedIndex = 0;
        }

        private void ChkAutoDetect_CheckedChanged(object sender, EventArgs e)
        {
            cbComPort.Enabled = !chkAutoDetect.Checked;
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            Focuser.autoDetectComPort = chkAutoDetect.Checked;
            if (cbComPort.SelectedItem != null)
                Focuser.comPortOverride = cbComPort.SelectedItem.ToString();
            Focuser.maxPosition = (int)numMaxPosition.Value;
            Focuser.reverseRotation = chkReverseRotation.Checked;
            Focuser.tempCoefficient = (double)numTempCoeff.Value;
            _driver.tl.Enabled = chkTrace.Checked;
        }
    }
}
