using System.Text;

namespace HarmonicEngine.Diagnostics
{
    public static class HarmonicLogLineFormatter
    {
        public static string Format(in HarmonicDiagnosticEvent e)
        {
            var sb = new StringBuilder(192);
            sb.Append('[').Append(e.TimeSeconds.ToString("F3")).Append("s] ");
            sb.Append('[').Append(e.Type).Append("] ");
            sb.Append('[').Append(e.Category).Append("] ");
            if (!string.IsNullOrEmpty(e.Message))
            {
                sb.Append(e.Message).Append(' ');
            }

            sb.Append("frame=").Append(e.FrameIndex);
            sb.Append(" active=").Append(e.ActiveParticleCount);
            if (e.PeakParticleCount > 0)
            {
                sb.Append(" peak=").Append(e.PeakParticleCount);
            }

            if (e.CanvasHitCount > 0)
            {
                sb.Append(" canvasHits=").Append(e.CanvasHitCount);
            }

            if (e.IntArg0 != 0 || e.IntArg1 != 0)
            {
                sb.Append(" i0=").Append(e.IntArg0).Append(" i1=").Append(e.IntArg1);
            }

            return sb.ToString().TrimEnd();
        }
    }
}
