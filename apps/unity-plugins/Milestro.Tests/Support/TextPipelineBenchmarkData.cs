using System;
using System.Globalization;

namespace Milestro.Tests
{
    internal sealed class TextPipelineBenchmarkData
    {
        internal const string Alphanumeric = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        internal const string Digits = "0123456789";
        internal const int TextLength = 8;
        internal readonly string[,] Frames;
        internal readonly string[] Prewarm;
        internal readonly string Checksum;

        internal TextPipelineBenchmarkData(int frames, int labels, uint seed, string alphabet)
        {
            if (frames <= 0 || labels <= 0 || seed == 0 || string.IsNullOrEmpty(alphabet))
            {
                throw new ArgumentOutOfRangeException(nameof(frames));
            }
            Frames = new string[frames, labels];
            var state = seed;
            var checksum = 14695981039346656037UL;
            var buffer = new char[TextLength];
            for (var frame = 0; frame < frames; ++frame)
            {
                for (var label = 0; label < labels; ++label)
                {
                    for (var character = 0; character < buffer.Length; ++character)
                    {
                        state ^= state << 13;
                        state ^= state >> 17;
                        state ^= state << 5;
                        buffer[character] = alphabet[(int)(state % (uint)alphabet.Length)];
                        checksum = unchecked((checksum ^ buffer[character]) * 1099511628211UL);
                    }
                    Frames[frame, label] = new string(buffer);
                }
            }
            Checksum = checksum.ToString("x16", CultureInfo.InvariantCulture);
            Prewarm = new string[(alphabet.Length + TextLength - 1) / TextLength];
            for (var chunk = 0; chunk < Prewarm.Length; ++chunk)
            {
                for (var character = 0; character < buffer.Length; ++character)
                {
                    buffer[character] = alphabet[(chunk * TextLength + character) % alphabet.Length];
                }
                Prewarm[chunk] = new string(buffer);
            }
        }

        internal static double Percentile(double[] samples, double fraction)
        {
            if (samples == null || samples.Length == 0 || fraction < 0 || fraction > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(samples));
            }
            var sorted = (double[])samples.Clone();
            Array.Sort(sorted);
            var position = (sorted.Length - 1) * fraction;
            var lower = (int)Math.Floor(position);
            var upper = (int)Math.Ceiling(position);
            return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
        }
    }
}
