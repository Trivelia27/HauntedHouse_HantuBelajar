using System;
using System.Collections.Generic;
using System.Globalization;

namespace HantuBelajar
{
    /// <summary>
    /// Statistik untuk laporan eksperimen & uji coba pemain, tanpa pustaka luar.
    /// Selang kepercayaan 95%, uji dua sisi: Fisher eksak, Mann-Whitney U, Wilcoxon bertanda (eksak untuk n &lt;= 25),
    /// t berpasangan, McNemar eksak, binomial, serta bootstrap persentil.
    /// </summary>
    public static class Stats
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------------ deskriptif
        public static double Mean(IList<double> x)
        {
            if (x == null || x.Count == 0) return double.NaN;
            double s = 0; foreach (double v in x) s += v; return s / x.Count;
        }

        public static double SD(IList<double> x)
        {
            int n = x.Count; if (n < 2) return 0;
            double m = Mean(x), s = 0; foreach (double v in x) s += (v - m) * (v - m);
            return Math.Sqrt(s / (n - 1));
        }

        public static double Median(IList<double> x)
        {
            List<double> a = new List<double>(x); a.Sort(); int n = a.Count;
            if (n == 0) return double.NaN;
            return n % 2 == 1 ? a[n / 2] : 0.5 * (a[n / 2 - 1] + a[n / 2]);
        }

        /// <summary>Selang kepercayaan 95% rata-rata (distribusi t).</summary>
        public static void MeanCI(IList<double> x, out double lo, out double hi)
        {
            int n = x.Count; double m = Mean(x);
            if (n < 2) { lo = hi = m; return; }
            double h = TInv(0.975, n - 1) * SD(x) / Math.Sqrt(n);
            lo = m - h; hi = m + h;
        }

        /// <summary>Selang skor Wilson 95% untuk proporsi k/n.</summary>
        public static void Wilson(int k, int n, out double lo, out double hi)
        {
            if (n <= 0) { lo = 0; hi = 1; return; }
            const double z = 1.959964;
            double p = (double)k / n, den = 1 + z * z / n, c = p + z * z / (2.0 * n);
            double w = z * Math.Sqrt(p * (1 - p) / n + z * z / (4.0 * n * n));
            lo = Math.Max(0, (c - w) / den); hi = Math.Min(1, (c + w) / den);
        }

        // ------------------------------------------------------------------ distribusi
        public static double NormalCdf(double z) { return 0.5 * Erfc(-z / Math.Sqrt(2.0)); }

        static double Erfc(double x)
        {
            double z = Math.Abs(x), t = 1.0 / (1.0 + 0.5 * z);
            double r = t * Math.Exp(-z * z - 1.26551223 + t * (1.00002368 + t * (0.37409196 + t * (0.09678418 + t * (-0.18628806 + t * (0.27886807 +
                       t * (-1.13520398 + t * (1.48851587 + t * (-0.82215223 + t * 0.17087277)))))))));
            return x >= 0 ? r : 2.0 - r;
        }

        public static double LogGamma(double x)
        {
            double[] c = { 76.18009172947146, -86.50532032941677, 24.01409824083091, -1.231739572450155, 0.1208650973866179e-2, -0.5395239384953e-5 };
            double y = x, tmp = x + 5.5;
            tmp -= (x + 0.5) * Math.Log(tmp);
            double ser = 1.000000000190015;
            for (int j = 0; j < 6; j++) ser += c[j] / ++y;
            return -tmp + Math.Log(2.5066282746310005 * ser / x);
        }

        static double LogFact(int n) { return n < 2 ? 0 : LogGamma(n + 1.0); }

        static double BetaCF(double a, double b, double x)
        {
            const int MaxIt = 300; const double Eps = 3e-14, Tiny = 1e-300;
            double qab = a + b, qap = a + 1, qam = a - 1, c = 1, d = 1 - qab * x / qap;
            if (Math.Abs(d) < Tiny) d = Tiny;
            d = 1 / d; double h = d;
            for (int m = 1; m <= MaxIt; m++)
            {
                int m2 = 2 * m;
                double aa = m * (b - m) * x / ((qam + m2) * (a + m2));
                d = 1 + aa * d; if (Math.Abs(d) < Tiny) d = Tiny;
                c = 1 + aa / c; if (Math.Abs(c) < Tiny) c = Tiny;
                d = 1 / d; h *= d * c;
                aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));
                d = 1 + aa * d; if (Math.Abs(d) < Tiny) d = Tiny;
                c = 1 + aa / c; if (Math.Abs(c) < Tiny) c = Tiny;
                d = 1 / d;
                double del = d * c; h *= del;
                if (Math.Abs(del - 1) < Eps) break;
            }
            return h;
        }

        /// <summary>Fungsi beta tak lengkap teregulasi I_x(a, b).</summary>
        public static double BetaInc(double a, double b, double x)
        {
            if (x <= 0) return 0;
            if (x >= 1) return 1;
            double bt = Math.Exp(LogGamma(a + b) - LogGamma(a) - LogGamma(b) + a * Math.Log(x) + b * Math.Log(1 - x));
            return x < (a + 1) / (a + b + 2) ? bt * BetaCF(a, b, x) / a : 1 - bt * BetaCF(b, a, 1 - x) / b;
        }

        public static double TCdf(double t, double df)
        {
            double p = 0.5 * BetaInc(df / 2.0, 0.5, df / (df + t * t));
            return t >= 0 ? 1 - p : p;
        }

        public static double TInv(double q, double df)
        {
            double lo = -200, hi = 200;
            for (int i = 0; i < 200; i++) { double m = 0.5 * (lo + hi); if (TCdf(m, df) < q) lo = m; else hi = m; }
            return 0.5 * (lo + hi);
        }

        // ------------------------------------------------------------------ uji
        /// <summary>Uji eksak Fisher dua sisi untuk tabel 2x2 [[a, b], [c, d]].</summary>
        public static double FisherExact(int a, int b, int c, int d)
        {
            int r1 = a + b, r2 = c + d, c1 = a + c, n = r1 + r2;
            if (n == 0) return 1;
            double LogP(int x) { return LogFact(r1) + LogFact(r2) + LogFact(c1) + LogFact(n - c1) - LogFact(n) - LogFact(x) - LogFact(r1 - x) - LogFact(c1 - x) - LogFact(r2 - c1 + x); }
            double p0 = LogP(a), sum = 0;
            int lo = Math.Max(0, c1 - r2), hi = Math.Min(r1, c1);
            for (int x = lo; x <= hi; x++) { double lp = LogP(x); if (lp <= p0 + 1e-7) sum += Math.Exp(lp); }
            return Math.Min(1, sum);
        }

        /// <summary>Uji Mann-Whitney U dua sisi (pendekatan normal, koreksi ties & kontinuitas). r = efek z / sqrt(N).</summary>
        public static double MannWhitney(IList<double> x, IList<double> y, out double z, out double r)
        {
            z = 0; r = 0;
            int n1 = x.Count, n2 = y.Count;
            if (n1 == 0 || n2 == 0) return 1;
            List<KeyValuePair<double, int>> all = new List<KeyValuePair<double, int>>();
            foreach (double v in x) all.Add(new KeyValuePair<double, int>(v, 0));
            foreach (double v in y) all.Add(new KeyValuePair<double, int>(v, 1));
            all.Sort((p, q) => p.Key.CompareTo(q.Key));
            int N = all.Count; double[] rank = new double[N]; double tie = 0;
            for (int i = 0; i < N;)
            {
                int j = i; while (j + 1 < N && all[j + 1].Key == all[i].Key) j++;
                double rr = (i + j) / 2.0 + 1; for (int k = i; k <= j; k++) rank[k] = rr;
                double t = j - i + 1; tie += t * t * t - t; i = j + 1;
            }
            double R1 = 0; for (int i = 0; i < N; i++) if (all[i].Value == 0) R1 += rank[i];
            double U = R1 - n1 * (n1 + 1) / 2.0, mu = n1 * n2 / 2.0;
            double sigma = Math.Sqrt(n1 * n2 / 12.0 * ((N + 1) - tie / (N * (N - 1.0))));
            if (sigma <= 0 || double.IsNaN(sigma)) return 1;
            double diff = U - mu, cc = diff > 0 ? -0.5 : diff < 0 ? 0.5 : 0;
            z = (diff + cc) / sigma; r = z / Math.Sqrt(N);
            return Math.Min(1, 2 * (1 - NormalCdf(Math.Abs(z))));
        }

        /// <summary>Uji peringkat bertanda Wilcoxon dua sisi untuk selisih berpasangan (selisih nol dibuang). Eksak bila n &lt;= 25.</summary>
        public static double Wilcoxon(IList<double> diffs, out double wPlus, out double z, out double r, out int n)
        {
            List<double> d = new List<double>();
            foreach (double v in diffs) if (Math.Abs(v) > 1e-12) d.Add(v);
            n = d.Count; wPlus = 0; z = 0; r = 0;
            if (n == 0) return 1;
            List<int> idx = new List<int>(); for (int i = 0; i < n; i++) idx.Add(i);
            idx.Sort((p, q) => Math.Abs(d[p]).CompareTo(Math.Abs(d[q])));
            double[] rank = new double[n]; double tie = 0;
            for (int i = 0; i < n;)
            {
                int j = i; while (j + 1 < n && Math.Abs(d[idx[j + 1]]) == Math.Abs(d[idx[i]])) j++;
                double rr = (i + j) / 2.0 + 1; for (int k = i; k <= j; k++) rank[idx[k]] = rr;
                double t = j - i + 1; tie += t * t * t - t; i = j + 1;
            }
            for (int i = 0; i < n; i++) if (d[i] > 0) wPlus += rank[i];
            double mu = n * (n + 1) / 4.0, sigma = Math.Sqrt(n * (n + 1) * (2 * n + 1) / 24.0 - tie / 48.0);
            z = sigma > 0 ? (wPlus - mu) / sigma : 0; r = z / Math.Sqrt(n);
            if (n <= 25)
            {
                int[] rk = new int[n]; int S = 0;
                for (int i = 0; i < n; i++) { rk[i] = (int)Math.Round(rank[i] * 2); S += rk[i]; }
                double[] dist = new double[S + 1]; dist[0] = 1;
                foreach (int v in rk) for (int s = S; s >= v; s--) dist[s] += dist[s - v];
                int obs = (int)Math.Round(wPlus * 2), low = Math.Min(obs, S - obs);
                double cum = 0; for (int s = 0; s <= low; s++) cum += dist[s];
                return Math.Min(1, 2 * cum / Math.Pow(2, n));
            }
            return Math.Min(1, 2 * (1 - NormalCdf(Math.Abs(z))));
        }

        /// <summary>Uji t berpasangan dua sisi. dz = efek Cohen untuk data berpasangan.</summary>
        public static double PairedT(IList<double> diffs, out double t, out double df, out double dz)
        {
            int n = diffs.Count; t = 0; df = n - 1; dz = 0;
            if (n < 2) return 1;
            double m = Mean(diffs), s = SD(diffs);
            if (s <= 0) return m == 0 ? 1 : 0;
            t = m / (s / Math.Sqrt(n)); dz = m / s;
            return Math.Min(1, 2 * (1 - TCdf(Math.Abs(t), df)));
        }

        /// <summary>Uji McNemar eksak (pasangan berbeda b dan c).</summary>
        public static double McNemarExact(int b, int c) { return BinomTwoSided(Math.Min(b, c), b + c, 0.5); }

        /// <summary>Uji binomial dua sisi: k sukses dari n dengan peluang p0.</summary>
        public static double BinomTwoSided(int k, int n, double p0)
        {
            if (n == 0) return 1;
            double LogPmf(int i) { return LogFact(n) - LogFact(i) - LogFact(n - i) + i * Math.Log(p0) + (n - i) * Math.Log(1 - p0); }
            double pk = LogPmf(k), sum = 0;
            for (int i = 0; i <= n; i++) { double lp = LogPmf(i); if (lp <= pk + 1e-7) sum += Math.Exp(lp); }
            return Math.Min(1, sum);
        }

        /// <summary>Bootstrap persentil 95%: statistic dipanggil dengan generator acak untuk menarik sampel ulang.</summary>
        public static void Bootstrap(Func<Random, double> statistic, out double lo, out double hi, int reps = 2000, int seed = 12345)
        {
            Random rng = new Random(seed);
            List<double> v = new List<double>(reps);
            for (int i = 0; i < reps; i++) { double s = statistic(rng); if (!double.IsNaN(s)) v.Add(s); }
            if (v.Count == 0) { lo = hi = double.NaN; return; }
            v.Sort();
            lo = v[(int)Math.Floor(0.025 * (v.Count - 1))]; hi = v[(int)Math.Ceiling(0.975 * (v.Count - 1))];
        }

        /// <summary>Ambil sampel ulang (dengan pengembalian) dari daftar.</summary>
        public static List<double> Resample(IList<double> x, Random rng)
        {
            List<double> r = new List<double>(x.Count);
            for (int i = 0; i < x.Count; i++) r.Add(x[rng.Next(x.Count)]);
            return r;
        }

        // ------------------------------------------------------------------ format (desimal koma untuk laporan berbahasa Indonesia)
        public static string F(double v, string fmt = "0.00")
        {
            if (double.IsNaN(v)) return "-";
            return v.ToString(fmt, Inv).Replace('.', ',');
        }

        public static string Pct(double v, int dec = 0) { return double.IsNaN(v) ? "-" : F(v * 100.0, dec == 0 ? "0" : "0." + new string('0', dec)) + "%"; }

        public static string P(double p)
        {
            if (double.IsNaN(p)) return "p = -";
            return p < 0.001 ? "p < 0,001" : "p = " + F(p, "0.000");
        }

        /// <summary>Tanda signifikansi untuk teks laporan.</summary>
        public static string Sig(double p) { return p < 0.05 ? "signifikan (α = 0,05)" : "tidak signifikan"; }

        public static string Inv4(double v) { return v.ToString("0.####", Inv); }
    }
}
