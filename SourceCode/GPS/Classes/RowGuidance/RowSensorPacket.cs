using System;
using System.Globalization;

namespace AgOpenGPS.Classes.RowGuidance
{
    /// <summary>
    /// A single read-only row-sensor sample sent by the RowGuidanceNano sketch.
    /// Packet format: ROW1,&lt;sequence&gt;,&lt;adcCounts&gt;,&lt;millivolts&gt;
    /// </summary>
    public sealed class RowSensorPacket
    {
        public uint Sequence { get; }
        public ushort AdcCounts { get; }
        public ushort Millivolts { get; }

        private RowSensorPacket(uint sequence, ushort adcCounts, ushort millivolts)
        {
            Sequence = sequence;
            AdcCounts = adcCounts;
            Millivolts = millivolts;
        }

        public static bool TryParse(string line, out RowSensorPacket packet)
        {
            packet = null;
            if (string.IsNullOrWhiteSpace(line))
                return false;

            string[] fields = line.Trim().Split(',');
            if (fields.Length != 4 || !string.Equals(fields[0], "ROW1", StringComparison.Ordinal))
                return false;

            if (!uint.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint sequence) ||
                !ushort.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out ushort adc) ||
                !ushort.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out ushort millivolts))
                return false;

            if (adc > 1023 || millivolts > 5000)
                return false;

            packet = new RowSensorPacket(sequence, adc, millivolts);
            return true;
        }
    }
}
