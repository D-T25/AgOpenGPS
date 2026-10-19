using AgOpenGPS.Properties;
using System;
using System.Globalization;
using System.IO.Ports;
using System.Text;

namespace AgOpenGPS.Classes.RowGuidance
{
    /// <summary>
    /// Owns the Nano USB connection and produces a bounded candidate steer target.
    /// SteeringOutputEnabled is a hard off switch: this branch can receive and
    /// calculate row corrections, but it cannot apply them to the steer PGN.
    /// </summary>
    public sealed class RowGuidanceRuntime : IDisposable
    {
        public const bool SteeringOutputEnabled = false;
        private const double PacketTimeoutSeconds = 0.5;

        private readonly object _sync = new object();
        private readonly StringBuilder _buffer = new StringBuilder();
        private SerialPort _serial;
        private RowSensorPacket _latestPacket;
        private DateTime _latestPacketUtc = DateTime.MinValue;
        private DateTime _engagedSinceUtc = DateTime.MinValue;
        private double _filteredMillivolts;
        private bool _hasFilteredValue;
        private double _candidateSteerAngleDegrees;
        private string _connectionStatus = "Disconnected";

        public string ConnectionStatus
        {
            get { lock (_sync) return _connectionStatus; }
        }

        public double CandidateSteerAngleDegrees
        {
            get { lock (_sync) return _candidateSteerAngleDegrees; }
        }

        public bool IsConnected
        {
            get { lock (_sync) return _serial != null && _serial.IsOpen; }
        }

        public bool Connect(string portName, out string error)
        {
            error = null;
            Disconnect();
            if (string.IsNullOrWhiteSpace(portName))
            {
                lock (_sync) _connectionStatus = "No COM port selected";
                error = "Select the Nano COM port first.";
                return false;
            }

            try
            {
                var serial = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One)
                {
                    NewLine = "\n",
                    ReadTimeout = 100
                };
                serial.DataReceived += SerialDataReceived;
                serial.Open();
                lock (_sync)
                {
                    _serial = serial;
                    _buffer.Clear();
                    _latestPacket = null;
                    _latestPacketUtc = DateTime.MinValue;
                    _hasFilteredValue = false;
                    _connectionStatus = "Connected to " + portName + "; waiting for ROW1 data";
                }
                return true;
            }
            catch (Exception ex)
            {
                lock (_sync) _connectionStatus = "COM connection failed: " + ex.Message;
                error = ex.Message;
                return false;
            }
        }

        public void Disconnect()
        {
            SerialPort serial;
            lock (_sync)
            {
                serial = _serial;
                _serial = null;
                _buffer.Clear();
                _latestPacket = null;
                _latestPacketUtc = DateTime.MinValue;
                _hasFilteredValue = false;
                _engagedSinceUtc = DateTime.MinValue;
                _connectionStatus = "Disconnected";
            }

            if (serial != null)
            {
                try { serial.DataReceived -= SerialDataReceived; } catch { }
                try { if (serial.IsOpen) serial.Close(); } catch { }
                serial.Dispose();
            }
        }

        public bool TryGetLatest(out RowSensorPacket packet, out double ageSeconds)
        {
            lock (_sync)
            {
                packet = _latestPacket;
                ageSeconds = packet == null ? double.PositiveInfinity : (DateTime.UtcNow - _latestPacketUtc).TotalSeconds;
                return packet != null && ageSeconds <= PacketTimeoutSeconds;
            }
        }

        /// <summary>
        /// Returns AOG's GPS target unless row assist is engaged and the selected
        /// mode has a fresh valid packet. The result is always bounded by both the
        /// configured correction cap and the vehicle steering angle limit.
        /// </summary>
        public double CalculateCandidateTarget(double gpsTargetDegrees, VehicleSettings settings,
            bool engaged, bool inTurn, bool reversing, double vehicleMaxSteerAngle,
            out bool rowDataValid, out bool rowAssistReady, out string status)
        {
            rowDataValid = TryGetLatest(out RowSensorPacket packet, out double ageSeconds);
            rowAssistReady = false;
            int mode = Math.Max(0, Math.Min(2, settings.setRowGuidance_mode));
            if (mode == 0 || !engaged || inTurn)
            {
                status = inTurn ? "GPS target; row assist bypassed during turn" : "GPS target";
                return StoreCandidate(gpsTargetDegrees);
            }

            if (reversing)
            {
                status = "Row assist unavailable in reverse";
                return StoreCandidate(mode == 1 ? gpsTargetDegrees : 0.0);
            }

            if (!rowDataValid)
            {
                lock (_sync)
                {
                    _engagedSinceUtc = DateTime.MinValue;
                    _hasFilteredValue = false;
                }
                status = "No fresh row-sensor packet";
                return StoreCandidate(mode == 1 ? gpsTargetDegrees : 0.0);
            }

            DateTime now = DateTime.UtcNow;
            lock (_sync)
            {
                if (_engagedSinceUtc == DateTime.MinValue) _engagedSinceUtc = now;
                if ((now - _engagedSinceUtc).TotalSeconds < settings.setRowGuidance_engageDelaySeconds)
                {
                    status = "Waiting for row-assist engage delay";
                    _candidateSteerAngleDegrees = mode == 1 ? gpsTargetDegrees : 0.0;
                    return _candidateSteerAngleDegrees;
                }

                double filter = Math.Max(0.0, Math.Min(1.0, settings.setRowGuidance_filterFactor));
                double alpha = Math.Max(0.05, 1.0 - filter);
                if (!_hasFilteredValue)
                {
                    _filteredMillivolts = packet.Millivolts;
                    _hasFilteredValue = true;
                }
                else
                {
                    _filteredMillivolts += alpha * (packet.Millivolts - _filteredMillivolts);
                }

                double errorMillivolts = _filteredMillivolts - settings.setRowGuidance_centerMillivolts;
                double deadband = Math.Max(0, settings.setRowGuidance_deadbandMillivolts);
                if (Math.Abs(errorMillivolts) <= deadband) errorMillivolts = 0;
                else errorMillivolts -= Math.Sign(errorMillivolts) * deadband;
                if (settings.setRowGuidance_invert) errorMillivolts *= -1;

                double correction = (errorMillivolts / 1000.0) * settings.setRowGuidance_gainDegreesPerVolt;
                double correctionLimit = Math.Max(0.0, Math.Min(10.0, settings.setRowGuidance_maxCorrectionDegrees));
                correction = Math.Max(-correctionLimit, Math.Min(correctionLimit, correction));
                double candidate = mode == 1 ? gpsTargetDegrees + correction : correction;
                double vehicleLimit = Math.Max(0.0, Math.Abs(vehicleMaxSteerAngle));
                _candidateSteerAngleDegrees = Math.Max(-vehicleLimit, Math.Min(vehicleLimit, candidate));
                rowAssistReady = true;
                status = string.Format(CultureInfo.InvariantCulture,
                    "Fresh row data ({0:F0} ms); candidate {1:F2}°; steering output {2}",
                    ageSeconds * 1000.0, _candidateSteerAngleDegrees,
                    SteeringOutputEnabled ? "enabled" : "hard-disabled");
                return _candidateSteerAngleDegrees;
            }
        }

        public void SetEngaged(bool engaged)
        {
            lock (_sync)
            {
                // Start the delay only after fresh packets begin arriving.
                _engagedSinceUtc = DateTime.MinValue;
                _hasFilteredValue = false;
                if (!engaged) _candidateSteerAngleDegrees = 0.0;
            }
        }

        private double StoreCandidate(double target)
        {
            lock (_sync)
            {
                _candidateSteerAngleDegrees = target;
                return target;
            }
        }

        private void SerialDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                var serial = sender as SerialPort;
                if (serial == null) return;
                string incoming = serial.ReadExisting();
                lock (_sync)
                {
                    if (!ReferenceEquals(serial, _serial)) return;
                    _buffer.Append(incoming);
                    while (true)
                    {
                        int newline = -1;
                        for (int i = 0; i < _buffer.Length; i++)
                            if (_buffer[i] == '\n') { newline = i; break; }
                        if (newline < 0) break;

                        string line = _buffer.ToString(0, newline).Trim();
                        _buffer.Remove(0, newline + 1);
                        if (RowSensorPacket.TryParse(line, out RowSensorPacket packet))
                        {
                            _latestPacket = packet;
                            _latestPacketUtc = DateTime.UtcNow;
                            _connectionStatus = "Receiving ROW1 data";
                        }
                    }
                    if (_buffer.Length > 256) _buffer.Clear();
                }
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        public void Dispose() => Disconnect();
    }
}
