using AgOpenGPS.Properties;
using System;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;

namespace AgOpenGPS
{
    /// <summary>
    /// Combine-only setup screen for the corn row-feeler prototype.
    /// The engage button is intentionally a UI/state check only: this prototype
    /// does not alter the steering target or command the steering valve.
    /// </summary>
    public sealed class FormRowGuidanceSettings : Form
    {
        // Keep false until the USB reader, stale-data handling, and steering
        // output path have been built and verified on the target Windows setup.
        private const bool SteeringOutputEnabled = false;

        private readonly ComboBox _mode = new ComboBox();
        private readonly ComboBox _port = new ComboBox();
        private readonly NumericUpDown _center = Number(500, 1000, 4500, 2500, 0);
        private readonly CheckBox _invert = new CheckBox();
        private readonly NumericUpDown _deadband = Number(100, 0, 500, 40, 0);
        private readonly NumericUpDown _gain = Number(100, 0, 10, 1, 2);
        private readonly NumericUpDown _maxCorrection = Number(100, 0, 10, 3, 1);
        private readonly NumericUpDown _delay = Number(100, 0, 10, 1, 1);
        private readonly NumericUpDown _filter = Number(100, 0, 1, 0.25M, 2);
        private readonly Button _engage = new Button();
        private readonly Label _status = new Label();
        private bool _armed;

        public FormRowGuidanceSettings()
        {
            Text = "Corn Row Guidance Setup";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(570, 530);
            BackColor = Color.FromArgb(238, 240, 242);

            AddLabel("Row guidance mode", 24, 25);
            _mode.DropDownStyle = ComboBoxStyle.DropDownList;
            _mode.Items.AddRange(new object[] { "GPS only", "GPS + feeler assist", "Feeler only" });
            _mode.Location = new Point(270, 22);
            _mode.Width = 260;
            Controls.Add(_mode);

            AddLabel("Nano USB COM port", 24, 75);
            _port.DropDownStyle = ComboBoxStyle.DropDownList;
            _port.Location = new Point(270, 72);
            _port.Width = 175;
            Controls.Add(_port);
            var refresh = new Button { Text = "Refresh", Location = new Point(455, 70), Size = new Size(75, 28) };
            refresh.Click += (s, e) => RefreshPorts();
            Controls.Add(refresh);

            AddLabel("Centered sensor voltage (mV)", 24, 125);
            Place(_center, 270, 122);
            _invert.Text = "Invert left/right correction direction";
            _invert.AutoSize = true;
            _invert.Location = new Point(270, 160);
            Controls.Add(_invert);

            AddLabel("Deadband (mV)", 24, 200);
            Place(_deadband, 270, 197);
            AddLabel("Correction gain (degrees/volt)", 24, 240);
            Place(_gain, 270, 237);
            AddLabel("Maximum correction (degrees)", 24, 280);
            Place(_maxCorrection, 270, 277);
            AddLabel("Engage delay (seconds)", 24, 320);
            Place(_delay, 270, 317);
            AddLabel("Input filter (0 = off, 1 = heavy)", 24, 360);
            Place(_filter, 270, 357);

            _engage.Text = "Engage row assist";
            _engage.Location = new Point(24, 410);
            _engage.Size = new Size(190, 48);
            _engage.BackColor = Color.FromArgb(50, 70, 90);
            _engage.ForeColor = Color.White;
            _engage.FlatStyle = FlatStyle.Flat;
            _engage.Click += (s, e) => ToggleArm();
            Controls.Add(_engage);

            _status.Location = new Point(230, 406);
            _status.Size = new Size(300, 62);
            _status.Text = "Disarmed. Steering output is disabled in this prototype.";
            Controls.Add(_status);

            var save = new Button { Text = "Save settings", Location = new Point(350, 480), Size = new Size(115, 32) };
            save.Click += (s, e) => SaveSettings();
            Controls.Add(save);
            var close = new Button { Text = "Close", Location = new Point(465, 480), Size = new Size(75, 32) };
            close.Click += (s, e) => Close();
            Controls.Add(close);

            LoadSettings();
            RefreshPorts();
            FormClosing += (s, e) => { _armed = false; };
        }

        private static NumericUpDown Number(int width, decimal min, decimal max, decimal value, int decimals)
        {
            return new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                Value = Math.Max(min, Math.Min(max, value)),
                DecimalPlaces = decimals,
                Increment = decimals == 0 ? 1 : 0.05M,
                Width = width
            };
        }

        private void AddLabel(string text, int x, int y)
        {
            Controls.Add(new Label { Text = text, Location = new Point(x, y), Size = new Size(235, 26), TextAlign = ContentAlignment.MiddleLeft });
        }

        private void Place(Control control, int x, int y)
        {
            control.Location = new Point(x, y);
            Controls.Add(control);
        }

        private void LoadSettings()
        {
            var settings = VehicleSettings.Default;
            _mode.SelectedIndex = Math.Max(0, Math.Min(2, settings.setRowGuidance_mode));
            _center.Value = Math.Max(_center.Minimum, Math.Min(_center.Maximum, settings.setRowGuidance_centerMillivolts));
            _invert.Checked = settings.setRowGuidance_invert;
            _deadband.Value = Math.Max(_deadband.Minimum, Math.Min(_deadband.Maximum, settings.setRowGuidance_deadbandMillivolts));
            _gain.Value = Math.Max(_gain.Minimum, Math.Min(_gain.Maximum, (decimal)settings.setRowGuidance_gainDegreesPerVolt));
            _maxCorrection.Value = Math.Max(_maxCorrection.Minimum, Math.Min(_maxCorrection.Maximum, (decimal)settings.setRowGuidance_maxCorrectionDegrees));
            _delay.Value = Math.Max(_delay.Minimum, Math.Min(_delay.Maximum, (decimal)settings.setRowGuidance_engageDelaySeconds));
            _filter.Value = Math.Max(_filter.Minimum, Math.Min(_filter.Maximum, (decimal)settings.setRowGuidance_filterFactor));
        }

        private void RefreshPorts()
        {
            string selected = _port.SelectedItem as string ?? VehicleSettings.Default.setRowGuidance_comPort;
            _port.Items.Clear();
            foreach (string name in SerialPort.GetPortNames()) _port.Items.Add(name);
            if (!string.IsNullOrEmpty(selected) && _port.Items.Contains(selected)) _port.SelectedItem = selected;
            else if (_port.Items.Count > 0) _port.SelectedIndex = 0;
        }

        private void ToggleArm()
        {
            if (!SteeringOutputEnabled)
            {
                _armed = !_armed;
                _engage.Text = _armed ? "Disengage row assist" : "Engage row assist";
                _status.Text = _armed
                    ? "Armed for interface testing only. No steering command is sent."
                    : "Disarmed. Steering output is disabled in this prototype.";
                return;
            }
        }

        private void SaveSettings()
        {
            var settings = VehicleSettings.Default;
            settings.setRowGuidance_mode = Math.Max(0, _mode.SelectedIndex);
            settings.setRowGuidance_comPort = _port.SelectedItem as string ?? string.Empty;
            settings.setRowGuidance_centerMillivolts = (int)_center.Value;
            settings.setRowGuidance_invert = _invert.Checked;
            settings.setRowGuidance_deadbandMillivolts = (int)_deadband.Value;
            settings.setRowGuidance_gainDegreesPerVolt = (double)_gain.Value;
            settings.setRowGuidance_maxCorrectionDegrees = (double)_maxCorrection.Value;
            settings.setRowGuidance_engageDelaySeconds = (double)_delay.Value;
            settings.setRowGuidance_filterFactor = (double)_filter.Value;
            settings.Save();
            _status.Text = "Settings saved to the current vehicle profile. Steering output remains disabled.";
        }
    }
}
