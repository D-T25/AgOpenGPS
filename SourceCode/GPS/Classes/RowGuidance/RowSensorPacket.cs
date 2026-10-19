using System;
using System.Globalization;

namespace AgOpenGPS.Classes.RowGuidance
{
    /// <summary>
    /// One or two crop-sensor samples sent by RowGuidanceNano.
    /// ROW1: ROW1,sequence,adc,millivolts
    /// ROW2: ROW2,sequence,leftAdc,leftMillivolts,rightAdc,rightMillivolts
    /// </summary>
    public sealed class RowSensorPacket
    {
        public uint Sequence { get; }
        public int ChannelCount { get; }
        public ushort LeftAdcCounts { get; }
        public ushort LeftMillivolts { get; }
        public ushort RightAdcCounts { get; }
        public ushort RightMillivolts { get; }

        // Compatibility aliases for the original one-channel diagnostics.
        public ushort AdcCounts => LeftAdcCounts;
        public ushort Millivolts => LeftMillivolts;

        private RowSensorPacket(uint sequence, int channelCount, ushort leftAdc, ushort leftMillivolts,
            ushort rightAdc, ushort rightMillivolts)
        {
            Sequence = sequence;
            ChannelCount = channelCount;
            LeftAdcCounts = leftAdc;
            LeftMillivolts = leftMillivolts;
            RightAdcCounts = rightAdc;
            RightMillivolts = rightMillivolts;
        }

        public static bool TryParse(string line, out RowSensorPacket packet)
        {
            packet = null;
            if (string.IsNullOrWhiteSpace(line)) return false;

            string[] fields = line.Trim().Split(',');
            bool dual = fields.Length == 6 && string.Equals(fields[0], "ROW2", StringComparison.Ordinal);
            bool single = fields.Length == 4 && string.Equals(fields[0], "ROW1", StringComparison.Ordinal);
            if (!single && !dual) return false;

            if (!uint.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint sequence) ||
                !ushort.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out ushort leftAdc) ||
                !ushort.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out ushort leftMillivolts))
                return false;

            ushort rightAdc = 0;
            ushort rightMillivolts = 0;
            if (dual &&
                (!ushort.TryParse(fields[4], NumberStyles.None, CultureInfo.InvariantCulture, out rightAdc) ||
                 !ushort.TryParse(fields[5], NumberStyles.None, CultureInfo.InvariantCulture, out rightMillivolts)))
                return false;

            if (leftAdc > 1023 || leftMillivolts > 5000 ||
                (dual && (rightAdc > 1023 || rightMillivolts > 5000)))
                return false;

            packet = new RowSensorPacket(sequence, dual ? 2 : 1, leftAdc, leftMillivolts,
                rightAdc, rightMillivolts);
            return true;
        }
    }
}
