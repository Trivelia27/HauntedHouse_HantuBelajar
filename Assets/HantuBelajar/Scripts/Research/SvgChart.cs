using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HantuBelajar
{
    /// <summary>
    /// Grafik SVG statis untuk laporan markdown (tema terang, palet kategorikal tervalidasi: biru, oranye, aqua).
    /// Batang tipis (maks 24 px, ujung data membulat 4 px, celah 2 px), garis 2 px, penanda 8 px bercincin warna permukaan,
    /// garis bantu tipis, legenda + label langsung; teks memakai warna tinta, bukan warna seri. Nilai lengkap ada di tabel laporan.
    /// </summary>
    public static class SvgChart
    {
        public static readonly string[] Palette = { "#2a78d6", "#eb6834", "#1baf7a" };
        const string Surface = "#fcfcfb", Ink = "#0b0b0b", Ink2 = "#52514e", GridC = "#e6e5e1", RefC = "#8a8984", Font = "Segoe UI, Helvetica, Arial, sans-serif";
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static string N(double v) { return v.ToString("0.##", Inv); }
        static string Esc(string s) { return (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;"); }
        static string Tick(double v, string fmt, bool pct) { return pct ? Stats.F(v * 100, "0") + "%" : Stats.F(v, fmt); }

        static List<double> NiceTicks(double lo, double hi, int count)
        {
            List<double> t = new List<double>();
            double range = hi - lo; if (range <= 0) { t.Add(lo); return t; }
            double raw = range / count, mag = Math.Pow(10, Math.Floor(Math.Log10(raw))), norm = raw / mag;
            double step = (norm <= 1 ? 1 : norm <= 2 ? 2 : norm <= 2.5 ? 2.5 : norm <= 5 ? 5 : 10) * mag;
            for (double v = Math.Ceiling(lo / step - 1e-9) * step; v <= hi + step * 1e-6; v += step) t.Add(Math.Round(v, 10));
            return t;
        }

        static void Header(StringBuilder s, int w, int h, string title, string subtitle)
        {
            s.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"").Append(w).Append("\" height=\"").Append(h).Append("\" viewBox=\"0 0 ").Append(w).Append(' ').Append(h)
             .Append("\" font-family=\"").Append(Font).Append("\" role=\"img\" aria-label=\"").Append(Esc(title)).Append("\">\n");
            s.Append("<rect x=\"0.5\" y=\"0.5\" width=\"").Append(w - 1).Append("\" height=\"").Append(h - 1).Append("\" rx=\"10\" fill=\"").Append(Surface).Append("\" stroke=\"#e2e1dc\"/>\n");
            s.Append("<text x=\"24\" y=\"32\" font-size=\"16\" font-weight=\"600\" fill=\"").Append(Ink).Append("\">").Append(Esc(title)).Append("</text>\n");
            if (!string.IsNullOrEmpty(subtitle)) s.Append("<text x=\"24\" y=\"52\" font-size=\"12\" fill=\"").Append(Ink2).Append("\">").Append(Esc(subtitle)).Append("</text>\n");
        }

        static void Legend(StringBuilder s, int right, int y, string[] series, bool lines)
        {
            if (series.Length < 2) return;
            // ukur kasar dari kanan ke kiri
            float x = right;
            for (int i = series.Length - 1; i >= 0; i--)
            {
                float tw = series[i].Length * 6.6f;
                x -= tw;
                s.Append("<text x=\"").Append(N(x)).Append("\" y=\"").Append(y + 4).Append("\" font-size=\"12\" fill=\"").Append(Ink2).Append("\">").Append(Esc(series[i])).Append("</text>\n");
                x -= lines ? 22 : 16;
                if (lines) s.Append("<line x1=\"").Append(N(x)).Append("\" y1=\"").Append(y).Append("\" x2=\"").Append(N(x + 16)).Append("\" y2=\"").Append(y).Append("\" stroke=\"").Append(Palette[i % Palette.Length]).Append("\" stroke-width=\"2\" stroke-linecap=\"round\"/>\n");
                else s.Append("<rect x=\"").Append(N(x)).Append("\" y=\"").Append(y - 5).Append("\" width=\"10\" height=\"10\" rx=\"2\" fill=\"").Append(Palette[i % Palette.Length]).Append("\"/>\n");
                x -= 18;
            }
        }

        static void YAxis(StringBuilder s, int L, int R, int top, float ph, double yMin, double yMax, string fmt, bool pct, Func<double, double> Y)
        {
            foreach (double v in NiceTicks(yMin, yMax, 5))
            {
                double y = Y(v);
                s.Append("<line x1=\"").Append(L).Append("\" x2=\"").Append(R).Append("\" y1=\"").Append(N(y)).Append("\" y2=\"").Append(N(y)).Append("\" stroke=\"").Append(GridC).Append("\" stroke-width=\"1\"/>\n");
                s.Append("<text x=\"").Append(L - 8).Append("\" y=\"").Append(N(y + 4)).Append("\" font-size=\"12\" text-anchor=\"end\" fill=\"").Append(Ink2).Append("\">").Append(Tick(v, fmt, pct)).Append("</text>\n");
            }
        }

        static void RefLine(StringBuilder s, int L, int R, double y, string label)
        {
            s.Append("<line x1=\"").Append(L).Append("\" x2=\"").Append(R).Append("\" y1=\"").Append(N(y)).Append("\" y2=\"").Append(N(y)).Append("\" stroke=\"").Append(RefC).Append("\" stroke-width=\"1.5\" stroke-dasharray=\"5 4\"/>\n");
            if (!string.IsNullOrEmpty(label)) s.Append("<text x=\"").Append(R - 4).Append("\" y=\"").Append(N(y - 6)).Append("\" font-size=\"12\" text-anchor=\"end\" fill=\"").Append(Ink2).Append("\">").Append(Esc(label)).Append("</text>\n");
        }

        static string BarPath(double x, double w, double yTop, double yBase)
        {
            double h = yBase - yTop;
            if (h < 0) { double t = yTop; yTop = yBase; yBase = t; h = -h; }
            double r = Math.Min(4, Math.Min(w / 2, h));
            if (r < 0.5) return "M" + N(x) + "," + N(yBase) + " h" + N(w) + " V" + N(yTop) + " h" + N(-w) + " Z";
            return "M" + N(x) + "," + N(yBase) + " V" + N(yTop + r) + " Q" + N(x) + "," + N(yTop) + " " + N(x + r) + "," + N(yTop) + " H" + N(x + w - r) +
                   " Q" + N(x + w) + "," + N(yTop) + " " + N(x + w) + "," + N(yTop + r) + " V" + N(yBase) + " Z";
        }

        /// <summary>Batang berkelompok per kategori dengan galat 95% (lo/hi boleh null). val[seri][kategori].</summary>
        public static string Bars(string title, string subtitle, string[] cats, string[] series, double[][] val, double[][] lo, double[][] hi,
                                  double yMin, double yMax, string yFmt, bool yPct, double? refY = null, string refLabel = null)
        {
            const int W = 760, H = 400, L = 64, R = 736, T = 80, B = 344;
            float ph = B - T;
            StringBuilder s = new StringBuilder();
            Header(s, W, H, title, subtitle);
            Legend(s, R, 64, series, false);
            Func<double, double> Y = v => B - (Math.Max(yMin, Math.Min(yMax, v)) - yMin) / (yMax - yMin) * ph;
            YAxis(s, L, R, T, ph, yMin, yMax, yFmt, yPct, Y);
            int nc = cats.Length, ns = series.Length;
            double band = (double)(R - L) / nc;
            double barW = Math.Min(24, (band * 0.62 - (ns - 1) * 2) / ns);
            double groupW = ns * barW + (ns - 1) * 2;
            double baseV = Math.Max(yMin, Math.Min(yMax, 0));
            for (int c = 0; c < nc; c++)
            {
                double gx = L + band * (c + 0.5) - groupW / 2;
                for (int k = 0; k < ns; k++)
                {
                    double v = val[k][c];
                    if (double.IsNaN(v)) continue;
                    double x = gx + k * (barW + 2);
                    s.Append("<path d=\"").Append(BarPath(x, barW, Y(v), Y(baseV))).Append("\" fill=\"").Append(Palette[k % Palette.Length]).Append("\"><title>")
                     .Append(Esc(series[k] + " - " + cats[c] + ": " + Tick(v, yFmt, yPct))).Append("</title></path>\n");
                    if (lo != null && hi != null && !double.IsNaN(lo[k][c]) && !double.IsNaN(hi[k][c]))
                    {
                        double cx = x + barW / 2, y1 = Y(lo[k][c]), y2 = Y(hi[k][c]);
                        s.Append("<path d=\"M").Append(N(cx)).Append(',').Append(N(y1)).Append(" V").Append(N(y2)).Append(" M").Append(N(cx - 4)).Append(',').Append(N(y1)).Append(" h8 M")
                         .Append(N(cx - 4)).Append(',').Append(N(y2)).Append(" h8\" stroke=\"").Append(Ink2).Append("\" stroke-width=\"1.5\" fill=\"none\"/>\n");
                    }
                }
                s.Append("<text x=\"").Append(N(L + band * (c + 0.5))).Append("\" y=\"").Append(B + 22).Append("\" font-size=\"13\" text-anchor=\"middle\" fill=\"").Append(Ink).Append("\">").Append(Esc(cats[c])).Append("</text>\n");
            }
            s.Append("<line x1=\"").Append(L).Append("\" x2=\"").Append(R).Append("\" y1=\"").Append(N(Y(baseV))).Append("\" y2=\"").Append(N(Y(baseV))).Append("\" stroke=\"").Append(Ink2).Append("\" stroke-width=\"1\"/>\n");
            if (refY.HasValue) RefLine(s, L, R, Y(refY.Value), refLabel);
            s.Append("<text x=\"24\" y=\"").Append(H - 14).Append("\" font-size=\"11\" fill=\"").Append(Ink2).Append("\">Garis galat = selang kepercayaan 95%. Nilai lengkap di tabel laporan.</text>\n");
            s.Append("</svg>\n");
            return s.ToString();
        }

        /// <summary>Garis per seri dengan pita selang 95% (lo/hi boleh null) dan label langsung di ujung garis.</summary>
        public static string Lines(string title, string subtitle, string xLabel, double[] xs, string[] series, double[][] ys, double[][] lo, double[][] hi,
                                   double yMin, double yMax, string yFmt, bool yPct, double? refY = null, string refLabel = null)
        {
            const int W = 760, H = 420, L = 64, R = 660, T = 80, B = 352;
            float ph = B - T;
            StringBuilder s = new StringBuilder();
            Header(s, W, H, title, subtitle);
            Legend(s, 736, 64, series, true);
            double xMin = xs[0], xMax = xs[xs.Length - 1]; if (xMax <= xMin) xMax = xMin + 1;
            Func<double, double> X = v => L + (v - xMin) / (xMax - xMin) * (R - L);
            Func<double, double> Y = v => B - (Math.Max(yMin, Math.Min(yMax, v)) - yMin) / (yMax - yMin) * ph;
            YAxis(s, L, R, T, ph, yMin, yMax, yFmt, yPct, Y);
            int stepX = Math.Max(1, (int)Math.Ceiling(xs.Length / 12.0));
            for (int i = 0; i < xs.Length; i += stepX)
                s.Append("<text x=\"").Append(N(X(xs[i]))).Append("\" y=\"").Append(B + 20).Append("\" font-size=\"12\" text-anchor=\"middle\" fill=\"").Append(Ink2).Append("\">").Append(N(xs[i])).Append("</text>\n");
            s.Append("<text x=\"").Append(N((L + R) / 2.0)).Append("\" y=\"").Append(B + 42).Append("\" font-size=\"12\" text-anchor=\"middle\" fill=\"").Append(Ink2).Append("\">").Append(Esc(xLabel)).Append("</text>\n");
            s.Append("<line x1=\"").Append(L).Append("\" x2=\"").Append(R).Append("\" y1=\"").Append(B).Append("\" y2=\"").Append(B).Append("\" stroke=\"").Append(Ink2).Append("\" stroke-width=\"1\"/>\n");
            List<double> labelY = new List<double>();
            if (refY.HasValue)
            {
                // garis acuan; labelnya di kolom label kanan (bukan di atas garis data) dan label seri menghindarinya
                RefLine(s, L, R, Y(refY.Value), null);
                if (!string.IsNullOrEmpty(refLabel))
                {
                    double ry = Y(refY.Value) + 4;
                    labelY.Add(ry);
                    s.Append("<text x=\"").Append(R + 10).Append("\" y=\"").Append(N(ry)).Append("\" font-size=\"11\" font-style=\"italic\" fill=\"").Append(Ink2).Append("\">").Append(Esc(refLabel)).Append("</text>\n");
                }
            }
            for (int k = 0; k < series.Length; k++)
            {
                string col = Palette[k % Palette.Length];
                if (lo != null && hi != null && lo[k] != null && hi[k] != null)
                {
                    StringBuilder poly = new StringBuilder();
                    for (int i = 0; i < xs.Length; i++) if (!double.IsNaN(hi[k][i])) poly.Append(N(X(xs[i]))).Append(',').Append(N(Y(hi[k][i]))).Append(' ');
                    for (int i = xs.Length - 1; i >= 0; i--) if (!double.IsNaN(lo[k][i])) poly.Append(N(X(xs[i]))).Append(',').Append(N(Y(lo[k][i]))).Append(' ');
                    s.Append("<polygon points=\"").Append(poly.ToString().Trim()).Append("\" fill=\"").Append(col).Append("\" fill-opacity=\"0.07\"/>\n");
                }
                StringBuilder pts = new StringBuilder();
                for (int i = 0; i < xs.Length; i++) if (!double.IsNaN(ys[k][i])) pts.Append(N(X(xs[i]))).Append(',').Append(N(Y(ys[k][i]))).Append(' ');
                s.Append("<polyline points=\"").Append(pts.ToString().Trim()).Append("\" fill=\"none\" stroke=\"").Append(col).Append("\" stroke-width=\"2\" stroke-linejoin=\"round\" stroke-linecap=\"round\"/>\n");
                int last = -1;
                for (int i = 0; i < xs.Length; i++)
                {
                    if (double.IsNaN(ys[k][i])) continue;
                    last = i;
                    s.Append("<circle cx=\"").Append(N(X(xs[i]))).Append("\" cy=\"").Append(N(Y(ys[k][i]))).Append("\" r=\"4\" fill=\"").Append(col).Append("\" stroke=\"").Append(Surface).Append("\" stroke-width=\"2\"><title>")
                     .Append(Esc(series[k] + " - " + xLabel + " " + N(xs[i]) + ": " + Tick(ys[k][i], yFmt, yPct))).Append("</title></circle>\n");
                }
                if (last >= 0 && series.Length <= 4)
                {
                    // label langsung di ujung garis; geser bila bertumpuk dengan label seri lain
                    double ly = Y(ys[k][last]) + 4;
                    foreach (double o in labelY) if (Math.Abs(o - ly) < 14) ly = o + (ly >= o ? 14 : -14);
                    labelY.Add(ly);
                    s.Append("<text x=\"").Append(N(X(xs[last]) + 10)).Append("\" y=\"").Append(N(ly)).Append("\" font-size=\"12\" fill=\"").Append(Ink2).Append("\">").Append(Esc(series[k])).Append("</text>\n");
                }
            }
            if (lo != null) s.Append("<text x=\"24\" y=\"").Append(H - 12).Append("\" font-size=\"11\" fill=\"").Append(Ink2).Append("\">Pita = selang kepercayaan 95%. Nilai lengkap di tabel laporan.</text>\n");
            s.Append("</svg>\n");
            return s.ToString();
        }
    }
}
