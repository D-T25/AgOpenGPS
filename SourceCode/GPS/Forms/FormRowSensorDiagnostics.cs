using AgOpenGPS.Classes.RowGuidance;
using System;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Windows.Forms;

namespace AgOpenGPS
{
    /// <summary>
    /// Read-only USB serial diagnostics for the Nano row-feeler prototype.
    /// This window observes sensor data only and never commands steering.
    /// </summary>
    public sealed class FormRowSensorDiagnostics : Form
    {
        private readonly ComboBox _ports = new ComboBox();
        private readonly Button _refresh = new Button();
        private readonly Button _connect = new Button();
        private readonly Label _status = new Label();
        private readonly Label _reading = new Label();
        private readonly Label _age = new Label();
        private readonly SerialPort _serial = new SerialPort();
        private readonly object _bufferLock = new object();
        private readonly StringBuilder _buffer = new StringBuilder();
        private readonly Timer _timer = new Timer();
        private DateTime _lastPacketUtc = DateTime.MinValue;

        public FormRowSensorDiagnostics()
        {
            Text = "Row Sensor USB Diagnostics";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new System.Drawing.Size(560, 250);

            var title = new Label
            {
                Text = "Nano row-feeler input (read only)",
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

            _status.Text = "Disconnected";
            _status.AutoSize = true;
            _status.Location = new System.Drawing.Point(25, 122);

            _reading.Text = "Waiting for ROW1 packets";
            _reading.AutoSize = true;
            _reading.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold);
            _reading.Location = new System.Drawing.Point(25, 155);

            _age.Text = "No data received";
            _age.AutoSize = true;
            _age.Location = new System.Drawing.Point(25, 200);

            Controls.AddRange(new Control[] { title, _ports, _refresh, _connect, _status, _reading, _age });

            _serial.BaudRate = 115200;
            _serial.DataBits = 8;
            _serial.Parity = Parity.None;
            _serial.StopBits = StopBits.One;
            _serial.NewLine = "\n";
            _serial.DataReceived += SerialDataReceived;

            _timer.Interval = 250;
            _timer.Tick += (sender, args) => UpdateAge();
            _timer.Start();

            FormClosing += (sender, args) => ClosePort();
            RefreshPorts();
        }

        private void RefreshPorts()
        {
            string selected = _ports.SelectedItem as string;
            _ports.Items.Clear();
            foreach (string portName in SerialPort.GetPortNames())
                _ports.Items.Add(portName);

            if (selected != null && _ports.Items.Contains(selected))
                _ports.SelectedItem = selected;
            else if (_ports.Items.Count > 0)
                _ports.SelectedIndex = 0;

            _status.Text = _ports.Items.Count == 0
                ? "No COM ports found. Connect the Nano and refresh."
                : "Select the Nano's COM port, then connect.";
        }

        private void ToggleConnection()
        {
            if (_serial.IsOpen)
            {
                ClosePort();
                _connect.Text = "Connect";
                _status.Text = "Disconnected";
                return;
            }

            if (_ports.SelectedItem == null)
            {
                _status.Text = "Select a COM port first.";
                return;
            }

            try
            {
                _serial.PortName = _ports.SelectedItem.ToString();
                lock (_bufferLock) _buffer.Clear();
                _lastPacketUtc = DateTime.MinValue;
                _serial.Open();
                _connect.Text = "Disconnect";
                _status.Text = "Connected at 115200 baud; waiting for valid ROW1 packets.";
            }
            catch (Exception ex)
            {
                _status.Text = "Could not open port: " + ex.Message;
                ClosePort();
            }
        }

        private void SerialDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string incoming = _serial.ReadExisting();
                lock (_bufferLock)
                {
                    _buffer.Append(incoming);
                    while (true)
                    {
                        int newline = IndexOfNewline(_buffer);
                        if (newline < 0) break;

                        string line = _buffer.ToString(0, newline).Trim();
                        _buffer.Remove(0, newline + 1);
                        if (RowSensorPacket.TryParse(line, out RowSensorPacket packet))
                            BeginInvoke(new Action(() => DisplayPacket(packet)));
                    }

                    if (_buffer.Length > 256)
                        _buffer.Clear(); // discard an unterminated/corrupt line
                }
            }
            catch (InvalidOperationException) { }
        }

        private static int IndexOfNewline(StringBuilder builder)
        {
            for (int i = 0; i < builder.Length; i++)
                if (builder[i] == '\n') return i;
            return -1;
        }

        private void DisplayPacket(RowSensorPacket packet)
        {
            if (IsDisposed) return;
            _lastPacketUtc = DateTime.UtcNow;
            _reading.Text = string.Format(
                CultureInfo.InvariantCulture,
                "ADC: {0} / 1023     Sensor: {1:F3} V     Sequence: {2}",
                packet.AdcCounts,
                packet.Millivolts / 1000.0,
                packet.Sequence);
            _status.Text = "Receiving valid row-sensor data.";
            UpdateAge();
        }

        private void UpdateAge()
        {
            if (_lastPacketUtc == DateTime.MinValue)
            {
                _age.Text = "No data received";
                return;
            }

            double seconds = (DateTime.UtcNow - _lastPacketUtc).TotalSeconds;
            _age.Text = seconds > 1.0
                ? string.Format(CultureInfo.InvariantCulture, "Last packet {0:F1} s ago", seconds)
                : "Live data";
            if (seconds > 2.0)
                _status.Text = "No recent packets; check the Nano and serial connection.";
        }

        private void ClosePort()
        {
            try
            {
                if (_serial.IsOpen) _serial.Close();
            }
            catch { }
            _connect.Text = "Connect";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Stop();
                _timer.Dispose();
                _serial.DataReceived -= SerialDataReceived;
                _serial.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
