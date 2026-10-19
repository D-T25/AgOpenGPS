using AgOpenGPS.Classes.RowGuidance;
using AgOpenGPS.Properties;
using System;
using System.Globalization;
using System.IO.Ports;
using System.Windows.Forms;

namespace AgOpenGPS
{
    /// <summary>
    /// Displays the shared Nano reader's latest data so diagnostics and the
    /// steering calculation never try to open the same COM port separately.
    /// </summary>
    public sealed class FormRowSensorDiagnostics : Form
    {
        private readonly RowGuidanceRuntime _runtime;
        private readonly ComboBox _ports = new ComboBox();
        private readonly Button _refresh = new Button();
        private readonly Button _connect = new Button();
        private readonly Label _status = new Label();
        private readonly Label _reading = new Label();
        private readonly Label _age = new Label();
        private readonly Timer _timer = new Timer();

        public FormRowSensorDiagnostics(RowGuidanceRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            Text = "Row Sensor USB Diagnostics";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new System.Drawing.Size(560, 250);

            var title = new Label
            {
                Text = "Nano row-feeler input (steering output disabled)",
                AutoSize = true,
                Font = new System.Drawing.Font("Microsoft Sans Serif", 13F, System.Drawing.FontStyle.Bold),
                Location = new System.Drawing.Point(22, 20)
            };
            _ports.DropDownStyle = ComboBoxStyle.DropDownList;
            _ports.Location = new System.Drawing.Point(25, 67);
            _ports.Size = new System.Drawing.Size(160, 32);

            _refresh.Text = "Refresh";
            _refresh.Location = new System.Drawing.Point(197, 65);
            _refresh.Size = new System.Drawing.Size(90, 36);
            _refresh.Click += (sender, args) => RefreshPorts();

            _connect.Text = "Connect";
            _connect.Location = new System.Drawing.Point(300, 65);
            _connect.Size = new System.Drawing.Size(100, 36);
            _connect.Click += (sender, args) => ToggleConnection();

            _status.AutoSize = true;
            _status.Location = new System.Drawing.Point(25, 122);
            _reading.AutoSize = true;
            _reading.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold);
            _reading.Location = new System.Drawing.Point(25, 155);
            _age.AutoSize = true;
            _age.Location = new System.Drawing.Point(25, 200);
            Controls.AddRange(new Control[] { title, _ports, _refresh, _connect, _status, _reading, _age });

            _timer.Interval = 250;
            _timer.Tick += (sender, args) => UpdateDisplay();
            _timer.Start();
            FormClosing += (sender, args) => _timer.Stop();
            RefreshPorts();
            UpdateDisplay();
        }

        private void RefreshPorts()
        {
            string selected = _ports.SelectedItem as string ?? VehicleSettings.Default.setRowGuidance_comPort;
            _ports.Items.Clear();
            foreach (string name in SerialPort.GetPortNames()) _ports.Items.Add(name);
            if (!string.IsNullOrEmpty(selected) && _ports.Items.Contains(selected)) _ports.SelectedItem = selected;
            else if (_ports.Items.Count > 0) _ports.SelectedIndex = 0;
        }

        private void ToggleConnection()
        {
            if (_runtime.IsConnected)
            {
                _runtime.Disconnect();
                UpdateDisplay();
                return;
            }

            string port = _ports.SelectedItem as string;
            if (string.IsNullOrEmpty(port))
            {
                _status.Text = "No COM port selected. Connect the Nano and refresh.";
                return;
            }

            if (!_runtime.Connect(port, out string error))
                _status.Text = "Could not open port: " + error;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            _connect.Text = _runtime.IsConnected ? "Disconnect" : "Connect";
            if (!_runtime.TryGetLatest(out RowSensorPacket packet, out double ageSeconds))
            {
                _status.Text = packet == null
                    ? _runtime.ConnectionStatus
                    : "Sensor packets are stale; row correction falls back safely.";
                _reading.Text = "Waiting for valid ROW1 packets";
                _age.Text = packet == null ? "No data received" : string.Format(CultureInfo.InvariantCulture,
                    "Last packet {0:F1} s ago (stale)", ageSeconds);
                return;
            }

            _reading.Text = packet.ChannelCount == 2
                ? string.Format(CultureInfo.InvariantCulture,
                    "L: {0} / 1023, {1:F3} V    R: {2} / 1023, {3:F3} V",
                    packet.LeftAdcCounts, packet.LeftMillivolts / 1000.0,
                    packet.RightAdcCounts, packet.RightMillivolts / 1000.0)
                : string.Format(CultureInfo.InvariantCulture,
                    "1 sensor: {0} / 1023     {1:F3} V",
                    packet.LeftAdcCounts, packet.LeftMillivolts / 1000.0);
            _age.Text = string.Format(CultureInfo.InvariantCulture,
                "{0}; {1} channel(s); candidate {2:F2}° (not sent to steering)",
                ageSeconds > 1.0 ? string.Format(CultureInfo.InvariantCulture, "Last packet {0:F1} s ago", ageSeconds) : "Live data",
                packet.ChannelCount, _runtime.CandidateSteerAngleDegrees);
        }
    }
}
