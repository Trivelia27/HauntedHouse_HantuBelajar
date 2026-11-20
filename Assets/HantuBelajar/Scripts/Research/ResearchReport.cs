using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace HantuBelajar
{
    public class DdaRow { public string profile, cond, result; public int rep, house, seed, nP; public double time, relFrac, chases, hides, near, dStart, dEnd, meanP; }
    public class LearnHide { public string cond; public int rep, house, index, rank, cand; public double pModel, pUniform; }
    public class LearnDec { public string cond, kind; public int rep, house; public bool hitM, hitR; }

    /// <summary>
    /// Menyusun laporan markdown + grafik SVG + CSV untuk: eksperimen DDA (bot), eksperimen "hantu belajar" (bot), dan uji coba pemain (CSV peserta).
    /// Kesimpulan ditulis mengikuti hasil uji statistik (tidak pernah mengklaim signifikan bila tidak).
    /// </summary>
    public static class ResearchReport
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        public const double Target = 0.62;

        public const string DdaBotNote =
            "Profil bot (`AutoPilot`), dibedakan oleh persepsi, reaksi, dan keputusan (navigasi & penghindar perabot sama untuk semua): " +
            "**Pro** sadar dikejar dari 18 m / terlihat dari 11 m, langsung bereaksi, berlari saat hantu jauh sambil menyisakan stamina, mengendap bila hantu dekat tetapi membelakangi, " +
            "memilih tempat sembunyi aman yang jarang dicurigai hantu; **Sedang** sadar dari 13 m / 8 m, bereaksi 0,45 dtk, sesekali ragu atau salah jalan, tempat sembunyi aman terdekat; " +
            "**Lemah** sadar dari 10 m / 6 m, bereaksi 0,8 dtk, sering ragu dan salah jalan, panik berlari (berisik, stamina cepat habis) saat hantu dekat, " +
            "kembali ke tempat sembunyi favorit walau berisiko dan keluar terlalu cepat. Kalibrasi tanpa DDA: `TestOutput/kalibrasi_bot.txt`.";
        public static readonly string[] LearnConds = { "Berkebiasaan", "Acak" };
        public static readonly string[] LearnNotes =
        {
            "bot memilih **lemari** pada 85% pelarian bila ada lemari dalam jangkauan - kebiasaan yang bisa dipelajari",
            "bot memilih tempat sembunyi **acak** di sekitarnya - tanpa kebiasaan (kontrol)",
        };
        public const float LearnD = 0.6f;

        static string F(double v, string f = "0.00") { return Stats.F(v, f); }
        static string I(double v, string f = "0.####") { return double.IsNaN(v) ? "" : v.ToString(f, Inv); }

        static void Save(string root, string rel, string content)
        {
            string p = Path.Combine(root, rel);
            string dir = Path.GetDirectoryName(p);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(p, content, new UTF8Encoding(false));
        }

        static List<double> Bin(IEnumerable<bool> b) { return b.Select(x => x ? 1.0 : 0.0).ToList(); }
        static double Rate(List<double> l) { return l.Count == 0 ? double.NaN : l.Average(); }

        static string WinCell(List<double> l)
        {
            if (l.Count == 0) return "-";
            int k = (int)Math.Round(l.Sum());
            Stats.Wilson(k, l.Count, out double lo, out double hi);
            return k + "/" + l.Count + " (" + Stats.Pct((double)k / l.Count) + "; " + Stats.Pct(lo) + "-" + Stats.Pct(hi) + ")";
        }

        /// <summary>Rata-rata [selang 95%]; selang dipotong ke rentang nilai yang mungkin (mis. skala 1-5, proporsi 0-1) agar tidak menampilkan nilai mustahil saat n kecil.</summary>
        static string MeanCell(List<double> l, string f = "0.00", double min = double.NegativeInfinity, double max = double.PositiveInfinity)
        {
            if (l.Count == 0) return "-";
            Stats.MeanCI(l, out double lo, out double hi);
            if (!double.IsNaN(lo)) lo = Math.Max(min, lo);
            if (!double.IsNaN(hi)) hi = Math.Min(max, hi);
            return F(l.Average(), f) + " [" + F(lo, f) + "; " + F(hi, f) + "]";
        }

        // ================================================================== 1. EKSPERIMEN DDA
        // ------------------------------------------------------------------ data mentah (eksperimen bisa dijalankan per potongan lalu digabung)
        public static string DdaCsv(List<DdaRow> rows)
        {
            StringBuilder csv = new StringBuilder("profil,kondisi,replikasi,rumah,seed,hasil,waktu_dtk,relik_frac,dikejar,sembunyi,nyaris,d_awal,d_akhir,P_rata,evaluasi\n");
            foreach (DdaRow r in rows.OrderBy(x => x.profile == "Pro" ? 0 : x.profile == "Sedang" ? 1 : 2).ThenBy(x => x.rep).ThenBy(x => x.cond).ThenBy(x => x.house))
                csv.AppendLine(string.Join(",", r.profile, r.cond, r.rep.ToString(), r.house.ToString(), r.seed.ToString(), r.result, I(r.time, "0.0"), I(r.relFrac, "0.###"),
                               I(r.chases, "0"), I(r.hides, "0"), I(r.near, "0"), I(r.dStart, "0.000"), I(r.dEnd, "0.000"), I(r.meanP, "0.000"), r.nP.ToString()));
            return csv.ToString();
        }

        public static List<DdaRow> LoadDdaRows(string dir)
        {
            List<DdaRow> rows = new List<DdaRow>();
            if (!Directory.Exists(dir)) return rows;
            foreach (string f in Directory.GetFiles(dir, "*.csv").OrderBy(x => x))
                foreach (Dictionary<string, string> r in ReadCsv(f))
                    rows.Add(new DdaRow
                    {
                        profile = S(r, "profil"), cond = S(r, "kondisi"), rep = (int)D(r, "replikasi"), house = (int)D(r, "rumah"), seed = (int)D(r, "seed"), result = S(r, "hasil"),
                        time = D(r, "waktu_dtk"), relFrac = D(r, "relik_frac"), chases = D(r, "dikejar"), hides = D(r, "sembunyi"), near = D(r, "nyaris"),
                        dStart = D(r, "d_awal"), dEnd = D(r, "d_akhir"), meanP = D(r, "P_rata"), nP = double.IsNaN(D(r, "evaluasi")) ? 0 : (int)D(r, "evaluasi"),
                    });
            return rows;
        }

        public static string LearnCsv(List<LearnHide> hides, List<LearnDec> decs)
        {
            StringBuilder csv = new StringBuilder("jenis,kondisi,replikasi,rumah,urutan,p_model,p_acak,peringkat,kandidat,hit_model,hit_acak\n");
            foreach (LearnHide h in hides) csv.AppendLine(string.Join(",", "sembunyi", h.cond, h.rep.ToString(), h.house.ToString(), h.index.ToString(), I(h.pModel), I(h.pUniform), h.rank.ToString(), h.cand.ToString(), "", ""));
            foreach (LearnDec e in decs) csv.AppendLine(string.Join(",", e.kind, e.cond, e.rep.ToString(), e.house.ToString(), "", "", "", "", "", e.hitM ? "1" : "0", e.hitR ? "1" : "0"));
            return csv.ToString();
        }

        public static void LoadLearnRows(string dir, List<LearnHide> hides, List<LearnDec> decs)
        {
            if (!Directory.Exists(dir)) return;
            foreach (string f in Directory.GetFiles(dir, "*.csv").OrderBy(x => x))
                foreach (Dictionary<string, string> r in ReadCsv(f))
                {
                    string kind = S(r, "jenis");
                    if (kind == "sembunyi")
                        hides.Add(new LearnHide { cond = S(r, "kondisi"), rep = (int)D(r, "replikasi"), house = (int)D(r, "rumah"), index = (int)D(r, "urutan"), pModel = D(r, "p_model"), pUniform = D(r, "p_acak"), rank = (int)D(r, "peringkat"), cand = (int)D(r, "kandidat") });
                    else
                        decs.Add(new LearnDec { kind = kind, cond = S(r, "kondisi"), rep = (int)D(r, "replikasi"), house = (int)D(r, "rumah"), hitM = S(r, "hit_model") == "1", hitR = S(r, "hit_acak") == "1" });
                }
        }

        public static string WriteDda(string root, List<DdaRow> rows, int reps, int houses, float scale, string profileNote)
        {
            string[] profs = { "Pro", "Sedang", "Lemah" }, conds = { "DDA", "Tetap" };
            Save(root, "Hasil_Eksperimen_DDA.csv", DdaCsv(rows));

            List<DdaRow> Cell(string p, string c) { return rows.Where(r => r.profile == p && r.cond == c).ToList(); }
            List<double> Wins(string p, string c) { return Bin(Cell(p, c).Select(r => r.result == "lolos")); }
            List<double> Dev(string p, string c) { return Cell(p, c).Where(r => r.nP > 0).Select(r => Math.Abs(r.meanP - Target)).ToList(); }

            StringBuilder md = new StringBuilder();
            md.AppendLine("# Hasil Eksperimen DDA - Haunted House: Hantu yang Mempelajari Ketakutanmu");
            md.AppendLine();
            md.AppendLine("Dibuat otomatis oleh `Tools > Hantu Belajar > Uji > Eksperimen DDA` (" + DateTime.Now.ToString("yyyy-MM-dd", Inv) + ").");
            md.AppendLine();
            md.AppendLine("## Desain");
            md.AppendLine("- **3 profil bot x 2 kondisi x " + reps + " replikasi x " + houses + " rumah = " + rows.Count + " rumah.** Kondisi **DDA** (kesulitan d menyesuaikan) dibandingkan dengan **Tetap** (d = 0,40 sepanjang permainan).");
            md.AppendLine("- Tiap replikasi mulai dari d = 0,40 dengan memori hantu kosong. Seed rumah sama untuk DDA dan Tetap (rumah pertama identik; rumah berikutnya bisa berbeda karena DDA mengubah parameter PCG).");
            md.AppendLine("- Bot memainkan game sungguhan (fisika, AI hantu, PCG) dengan skala waktu x" + I(scale, "0.#") + " tanpa grafis. Batas 420 detik (7 menit) permainan per rumah; lewat batas dihitung gagal.");
            md.AppendLine("- " + profileNote);
            md.AppendLine("- Ukuran: tingkat lolos (selang Wilson 95%), indikator performa DDA **P** (0-1, dihitung tiap 8 detik di kedua kondisi) dirata-rata per rumah dan jaraknya ke target **0,62**, serta kesulitan d.");
            md.AppendLine();

            // --- tingkat lolos
            md.AppendLine("## 1. Tingkat lolos per profil");
            md.AppendLine();
            md.AppendLine("| Profil | DDA: lolos (95% CI) | Tetap: lolos (95% CI) | Uji Fisher eksak |");
            md.AppendLine("|---|---|---|---|");
            double[][] wv = { new double[3], new double[3] }, wl = { new double[3], new double[3] }, wh = { new double[3], new double[3] };
            for (int i = 0; i < 3; i++)
            {
                List<double> a = Wins(profs[i], "DDA"), b = Wins(profs[i], "Tetap");
                int ka = (int)a.Sum(), kb = (int)b.Sum();
                double p = Stats.FisherExact(ka, a.Count - ka, kb, b.Count - kb);
                md.AppendLine("| " + profs[i] + " | " + WinCell(a) + " | " + WinCell(b) + " | " + Stats.P(p) + " |");
                for (int c = 0; c < 2; c++)
                {
                    List<double> l = c == 0 ? a : b; int k = (int)l.Sum();
                    wv[c][i] = l.Count > 0 ? (double)k / l.Count : double.NaN;
                    Stats.Wilson(k, l.Count, out wl[c][i], out wh[c][i]);
                }
            }
            md.AppendLine();
            Save(root, "Analisis/dda_lolos.svg", SvgChart.Bars("Tingkat lolos per profil bot", "DDA vs kesulitan tetap (d = 0,40); garis putus = target performa 62%",
                 profs, conds, wv, wl, wh, 0, 1, "0.00", true, Target, "target 62%"));
            md.AppendLine("![Tingkat lolos per profil](Analisis/dda_lolos.svg)");
            md.AppendLine();

            // --- keadilan antarprofil
            List<double> proT = Wins("Pro", "Tetap"), lemT = Wins("Lemah", "Tetap"), proD = Wins("Pro", "DDA"), lemD = Wins("Lemah", "DDA");
            double gapT = Rate(proT) - Rate(lemT), gapD = Rate(proD) - Rate(lemD);
            Stats.Bootstrap(rng => (Rate(Stats.Resample(proT, rng)) - Rate(Stats.Resample(lemT, rng))) - (Rate(Stats.Resample(proD, rng)) - Rate(Stats.Resample(lemD, rng))), out double gLo, out double gHi);
            md.AppendLine("**Selisih Pro - Lemah** (makin kecil = kesulitan makin menyesuaikan kemampuan): Tetap **" + Stats.Pct(gapT) + "**, DDA **" + Stats.Pct(gapD) +
                          "**; pengecilan selisih " + Stats.Pct(gapT - gapD) + " (bootstrap 95%: " + Stats.Pct(gLo) + " s.d. " + Stats.Pct(gHi) + ").");
            md.AppendLine();

            // --- kedekatan ke target
            md.AppendLine("## 2. Kedekatan performa ke target (|P rata-rata - 0,62| per rumah, makin kecil makin baik)");
            md.AppendLine();
            md.AppendLine("| Profil | DDA | Tetap | Mann-Whitney U |");
            md.AppendLine("|---|---|---|---|");
            double[][] dv = { new double[3], new double[3] }, dl = { new double[3], new double[3] }, dh = { new double[3], new double[3] };
            List<double> allD = new List<double>(), allT = new List<double>();
            for (int i = 0; i < 3; i++)
            {
                List<double> a = Dev(profs[i], "DDA"), b = Dev(profs[i], "Tetap");
                allD.AddRange(a); allT.AddRange(b);
                double p = Stats.MannWhitney(a, b, out double z, out double r);
                md.AppendLine("| " + profs[i] + " | " + MeanCell(a, "0.000") + " | " + MeanCell(b, "0.000") + " | " + Stats.P(p) + ", r = " + F(r) + " |");
                for (int c = 0; c < 2; c++)
                {
                    List<double> l = c == 0 ? a : b;
                    dv[c][i] = l.Count > 0 ? l.Average() : double.NaN;
                    if (l.Count > 1) Stats.MeanCI(l, out dl[c][i], out dh[c][i]); else { dl[c][i] = dh[c][i] = double.NaN; }
                }
            }
            double pAll = Stats.MannWhitney(allD, allT, out double zAll, out double rAll);
            md.AppendLine("| **Semua** | " + MeanCell(allD, "0.000") + " | " + MeanCell(allT, "0.000") + " | " + Stats.P(pAll) + ", r = " + F(rAll) + " |");
            md.AppendLine();
            double dMax = Math.Max(0.3, Math.Ceiling((dh.SelectMany(x => x).Where(x => !double.IsNaN(x)).DefaultIfEmpty(0.3).Max() + 0.02) * 20) / 20);
            Save(root, "Analisis/dda_jarak_target.svg", SvgChart.Bars("Jarak performa ke target", "|P rata-rata per rumah - 0,62|; makin pendek makin dekat ke target", profs, conds, dv, dl, dh, 0, dMax, "0.00", false));
            md.AppendLine("![Jarak performa ke target](Analisis/dda_jarak_target.svg)");
            md.AppendLine();

            // --- perjalanan d
            md.AppendLine("## 3. Perjalanan kesulitan d pada kondisi DDA");
            md.AppendLine();
            double[] xs = Enumerable.Range(0, houses + 1).Select(x => (double)x).ToArray();
            double[][] dm = new double[3][], dlo = new double[3][], dhi = new double[3][];
            StringBuilder dt = new StringBuilder("| Setelah rumah ke- | " + string.Join(" | ", profs) + " |\n|---|---|---|---|\n");
            for (int i = 0; i < 3; i++) { dm[i] = new double[houses + 1]; dlo[i] = new double[houses + 1]; dhi[i] = new double[houses + 1]; }
            for (int h = 0; h <= houses; h++)
            {
                dt.Append("| " + h + " |");
                for (int i = 0; i < 3; i++)
                {
                    List<double> v = h == 0 ? Cell(profs[i], "DDA").Where(r => r.house == 1).Select(r => r.dStart).ToList()
                                            : Cell(profs[i], "DDA").Where(r => r.house == h).Select(r => r.dEnd).ToList();
                    dm[i][h] = v.Count > 0 ? v.Average() : double.NaN;
                    if (v.Count > 1) Stats.MeanCI(v, out dlo[i][h], out dhi[i][h]); else { dlo[i][h] = dhi[i][h] = dm[i][h]; }
                    dt.Append(" " + (v.Count > 0 ? F(dm[i][h]) : "-") + " |");
                }
                dt.Append('\n');
            }
            Save(root, "Analisis/dda_kesulitan.svg", SvgChart.Lines("Kesulitan d selama bermain (kondisi DDA)", "rata-rata " + reps + " replikasi; mulai 0,40; tanpa DDA tetap 0,40", "setelah rumah ke-",
                 xs, profs, dm, dlo, dhi, 0, 1, "0.0", false, 0.4, "Tetap = 0,40"));
            md.AppendLine("![Perjalanan kesulitan](Analisis/dda_kesulitan.svg)");
            md.AppendLine();
            md.Append(dt);
            md.AppendLine();

            // --- performa P per rumah: apakah mendekati target?
            md.AppendLine("## 4. Performa P per rumah dibanding target 0,62");
            md.AppendLine();
            md.AppendLine("P = indikator performa DDA (0 = sangat tertekan, 1 = sangat unggul), rata-rata per rumah. Pada kondisi DDA, P pemain kuat seharusnya turun dan P pemain lemah naik mendekati 0,62; pada kondisi Tetap tidak ada yang menariknya.");
            md.AppendLine();
            double[] hx = Enumerable.Range(1, houses).Select(x => (double)x).ToArray();
            StringBuilder pt = new StringBuilder("| Rumah ke- | " + string.Join(" | ", conds.SelectMany(c => profs.Select(p => p + " " + c))) + " |\n|---|" + string.Concat(Enumerable.Repeat("---|", conds.Length * profs.Length)) + "\n");
            double[][][] pmAll = new double[conds.Length][][];
            for (int c = 0; c < conds.Length; c++)
            {
                double[][] pm = new double[3][], plo = new double[3][], phi = new double[3][];
                for (int i = 0; i < 3; i++)
                {
                    pm[i] = new double[houses]; plo[i] = new double[houses]; phi[i] = new double[houses];
                    for (int h = 1; h <= houses; h++)
                    {
                        List<double> v = Cell(profs[i], conds[c]).Where(r => r.house == h && r.nP > 0).Select(r => r.meanP).ToList();
                        pm[i][h - 1] = v.Count > 0 ? v.Average() : double.NaN;
                        if (v.Count > 1) Stats.MeanCI(v, out plo[i][h - 1], out phi[i][h - 1]); else plo[i][h - 1] = phi[i][h - 1] = pm[i][h - 1];
                    }
                }
                pmAll[c] = pm;
                Save(root, "Analisis/dda_P_" + conds[c] + ".svg", SvgChart.Lines("Performa P per rumah - " + (conds[c] == "DDA" ? "dengan DDA" : "kesulitan tetap"),
                     "rata-rata " + reps + " replikasi dengan selang 95%; garis putus = target 0,62", "rumah ke-", hx, profs, pm, plo, phi, 0, 1, "0.0", false, Target, "target 0,62"));
            }
            for (int h = 1; h <= houses; h++)
            {
                pt.Append("| " + h + " |");
                for (int c = 0; c < conds.Length; c++) for (int i = 0; i < 3; i++) pt.Append(" " + (double.IsNaN(pmAll[c][i][h - 1]) ? "-" : F(pmAll[c][i][h - 1])) + " |");
                pt.Append('\n');
            }
            md.AppendLine("![Performa P dengan DDA](Analisis/dda_P_DDA.svg)");
            md.AppendLine();
            md.AppendLine("![Performa P kesulitan tetap](Analisis/dda_P_Tetap.svg)");
            md.AppendLine();
            md.Append(pt);
            md.AppendLine();

            // --- kesimpulan
            md.AppendLine("## Kesimpulan");
            double[] rT = profs.Select(p => Rate(Wins(p, "Tetap"))).ToArray(), rD = profs.Select(p => Rate(Wins(p, "DDA"))).ToArray();
            double[] relT = profs.Select(p => Cell(p, "Tetap").Select(r => r.relFrac).DefaultIfEmpty(double.NaN).Average()).ToArray();
            double[] timeCaughtT = profs.Select(p => Cell(p, "Tetap").Where(r => r.result == "tertangkap").Select(r => r.time).DefaultIfEmpty(double.NaN).Average()).ToArray();
            double PairP(int a, int b, string c)
            {
                List<double> x = Wins(profs[a], c), y = Wins(profs[b], c); int kx = (int)x.Sum(), ky = (int)y.Sum();
                return Stats.FisherExact(kx, x.Count - kx, ky, y.Count - ky);
            }
            // 1) keterampilan bot
            string skill = "- **Keterampilan bot (tanpa DDA).** Tingkat lolos Pro / Sedang / Lemah = " + Stats.Pct(rT[0]) + " / " + Stats.Pct(rT[1]) + " / " + Stats.Pct(rT[2]) +
                           "; relik terkumpul " + Stats.Pct(relT[0]) + " / " + Stats.Pct(relT[1]) + " / " + Stats.Pct(relT[2]) +
                           "; lama bertahan sebelum tertangkap " + F(timeCaughtT[0], "0") + " / " + F(timeCaughtT[1], "0") + " / " + F(timeCaughtT[2], "0") + " dtk.";
            double p01 = PairP(0, 1, "Tetap"), p12 = PairP(1, 2, "Tetap");
            skill += rT[0] >= rT[1] && rT[1] >= rT[2] ? " Urutan keterampilan sesuai harapan." : "";
            if (rT[0] < rT[1])
                skill += " Pro dan Sedang tidak terpisah pada tingkat lolos (Fisher " + Stats.P(p01) + ")" +
                         (timeCaughtT[0] > timeCaughtT[1] + 10 ? ", tetapi Pro bertahan lebih lama." : relT[0] > relT[1] + 0.05 ? ", tetapi Pro mengumpulkan lebih banyak relik." : " maupun pada relik dan lama bertahan - keduanya setara sebagai pemain cakap.");
            if (rT[1] > rT[2]) skill += " Lemah jelas paling lemah" + (p12 < 0.05 ? " (Sedang vs Lemah " + Stats.P(p12) + ")." : ".");
            md.AppendLine(skill);
            // 2) arah penyesuaian kesulitan
            string Dir(double v) { return v > 0.42 ? "naik" : v < 0.38 ? "turun" : "hampir tetap"; }
            double[] dEnd = { dm[0][houses], dm[1][houses], dm[2][houses] };
            md.AppendLine("- **Kesulitan menyesuaikan pemain.** Dari awal 0,40, kesulitan akhir dengan DDA: Pro " + F(dEnd[0]) + " (" + Dir(dEnd[0]) + "), Sedang " + F(dEnd[1]) + " (" + Dir(dEnd[1]) + "), Lemah " + F(dEnd[2]) + " (" + Dir(dEnd[2]) + ")" +
                          (dEnd[0] > dEnd[1] && dEnd[1] > dEnd[2] ? " - urutannya mengikuti keterampilan: makin lemah pemain, makin besar penurunan kesulitan." : dEnd[0] > dEnd[2] ? " - pemain terkuat mendapat kesulitan lebih tinggi daripada pemain terlemah." : " - belum memisahkan pemain kuat dan lemah."));
            // 3) tingkat lolos
            int closer = Enumerable.Range(0, 3).Count(i => Math.Abs(rD[i] - Target) < Math.Abs(rT[i] - Target));
            md.AppendLine("- **Tingkat lolos.** DDA menaikkan tingkat lolos " + string.Join(", ", Enumerable.Range(0, 3).Select(i => profs[i] + " " + Stats.Pct(rT[i]) + " -> " + Stats.Pct(rD[i]) + " (" + Stats.P(PairPc(i)) + ")")) +
                          ". Sebagai pembanding kasar, tingkat lolos " + closer + " dari 3 profil lebih dekat ke 62% dengan DDA.");
            double PairPc(int i)
            {
                List<double> x = Wins(profs[i], "DDA"), y = Wins(profs[i], "Tetap"); int kx = (int)x.Sum(), ky = (int)y.Sum();
                return Stats.FisherExact(kx, x.Count - kx, ky, y.Count - ky);
            }
            // 4) kesenjangan antarprofil
            string gapTxt = "- **Kesenjangan Pro - Lemah** " + (gapD < gapT ? "mengecil" : "membesar") + " dengan DDA (" + Stats.Pct(gapT) + " -> " + Stats.Pct(gapD) + "; bootstrap 95% untuk pengecilan: " + Stats.Pct(gLo) + " s.d. " + Stats.Pct(gHi) + ")";
            if (rT.All(v => v < Target) && gapD >= gapT)
                gapTxt += ". Kesulitan tetap d = 0,40 ternyata terlalu sulit untuk semua profil (semua di bawah 62%), sehingga DDA menurunkan kesulitan untuk semua; profil yang paling mampu memanfaatkan hantu yang lebih mudah (Pro) paling diuntungkan. Lemah tetap sering tertangkap walau kesulitan sudah rendah - penurunan per rumah dibatasi 0,30 dan kelemahannya (reaksi lambat, panik berlari) tidak bergantung pada kecepatan hantu.";
            else gapTxt += ".";
            md.AppendLine(gapTxt);
            // 5) indikator P
            List<double> pCaught = rows.Where(r => r.result == "tertangkap" && r.nP > 0).Select(r => r.meanP).ToList(), pWon = rows.Where(r => r.result == "lolos" && r.nP > 0).Select(r => r.meanP).ToList();
            double pPw = Stats.MannWhitney(pCaught, pWon, out double zPw, out double rPw);
            string pTxt = "- **Indikator performa P.** Jarak P ke target 0,62: DDA " + F(allD.DefaultIfEmpty(double.NaN).Average(), "0.000") + " vs Tetap " + F(allT.DefaultIfEmpty(double.NaN).Average(), "0.000") + " (" + Stats.P(pAll) + ", " + Stats.Sig(pAll) + ").";
            if (pCaught.Count > 0 && pWon.Count > 0 && pCaught.Average() >= pWon.Average() - 0.02)
                pTxt += " Penyebabnya terlihat di data: rata-rata P rumah yang berakhir **tertangkap** (" + F(pCaught.Average()) + ") tidak lebih rendah daripada rumah yang **lolos** (" + F(pWon.Average()) + "; Mann-Whitney " + Stats.P(pPw) + "). " +
                        "Penangkapan biasanya terjadi tiba-tiba setelah jendela evaluasi 8 detik yang tenang, sehingga P per rumah tidak mencerminkan kegagalan - terutama untuk pemain lemah yang cepat tertangkap. " +
                        "DDA tetap menyesuaikan lewat kejadian tertangkap (-0,15) dan lolos (+0,08), itulah yang menggerakkan kesulitan di atas. " +
                        "Perbaikan yang disarankan: masukkan kejadian tertangkap dan tingkat bahaya (jarak hantu, waktu sejak terakhir dikejar) ke dalam P.";
            md.AppendLine(pTxt);
            md.AppendLine();
            md.AppendLine("Keterbatasan: bot bukan manusia (tidak merasa takut, tidak belajar dari pengalaman), jadi eksperimen ini menguji **mekanisme** DDA. Klaim tentang pengalaman pemain memerlukan uji coba pemain (`Tools > Hantu Belajar > Analisis Data Uji Coba`, lihat `PROTOKOL_UJI_COBA.md`).");
            md.AppendLine();
            md.AppendLine("Data per rumah: `Hasil_Eksperimen_DDA.csv`.");
            Save(root, "Hasil_Eksperimen_DDA.md", md.ToString());
            return md.ToString();
        }

        // ================================================================== 2. EKSPERIMEN HANTU BELAJAR
        public static string WriteLearning(string root, List<LearnHide> hides, List<LearnDec> decs, int reps, int houses, float scale, float d, string[] conds, string[] condNotes)
        {
            Save(root, "Hasil_Eksperimen_Belajar.csv", LearnCsv(hides, decs));

            StringBuilder md = new StringBuilder();
            md.AppendLine("# Hasil Eksperimen \"Hantu Belajar\" - Haunted House: Hantu yang Mempelajari Ketakutanmu");
            md.AppendLine();
            md.AppendLine("Dibuat otomatis oleh `Tools > Hantu Belajar > Uji > Eksperimen hantu belajar` (" + DateTime.Now.ToString("yyyy-MM-dd", Inv) + ").");
            md.AppendLine();
            md.AppendLine("## Desain");
            md.AppendLine("- **" + conds.Length + " kondisi pemain bot x " + reps + " replikasi x " + houses + " rumah**, kesulitan tetap d = " + F(d) + " (bobot belajar hantu " + F(DdaMap.Ghost(d).learnWeight) + "). Memori hantu dikosongkan di awal tiap replikasi lalu dibawa dari rumah ke rumah.");
            for (int i = 0; i < conds.Length; i++) md.AppendLine("  - **" + conds[i] + "**: " + condNotes[i]);
            md.AppendLine("- Evaluasi **kontrafaktual**: setiap keputusan hantu yang memakai memori dibandingkan dengan pilihan **acak** pada lintasan pemain yang sama (perilaku game tidak diubah).");
            md.AppendLine("  - *Prediksi tempat sembunyi*: saat pemain masuk tempat sembunyi, peluang model untuk tempat itu (rata-rata posterior Beta dinormalkan atas tempat sembunyi <= 6 langkah) dibanding peluang acak 1/kandidat. Dihitung sebelum kebiasaan saat itu dicatat.");
            md.AppendLine("  - *Hadang*: titik hasil heatmap vs node acak; tepat bila pemain lewat <= 1 sel dari titik itu dalam 25 detik.");
            md.AppendLine("  - *Jaga*: tempat hasil Thompson sampling vs tempat acak dari kandidat yang sama; tepat bila pemain sedang/akan (20 detik) bersembunyi di sana.");
            md.AppendLine();

            // --- prediksi tempat sembunyi
            md.AppendLine("## 1. Prediksi tempat sembunyi");
            md.AppendLine();
            md.AppendLine("| Kondisi | Kejadian | Peluang model | Peluang acak | Keunggulan (model - acak) | Uji Wilcoxon (vs 0) | Tebakan pertama tepat (model / peluang) |");
            md.AppendLine("|---|---|---|---|---|---|---|");
            Dictionary<string, double> liftMean = new Dictionary<string, double>(), liftP = new Dictionary<string, double>();
            foreach (string c in conds)
            {
                List<LearnHide> l = hides.Where(h => h.cond == c).ToList();
                List<double> lift = l.Select(h => h.pModel - h.pUniform).ToList();
                double p = Stats.Wilcoxon(lift, out double w, out double z, out double r, out int n);
                liftMean[c] = lift.Count > 0 ? lift.Average() : double.NaN; liftP[c] = p;
                double top1 = l.Count > 0 ? l.Count(h => h.rank == 1) / (double)l.Count : double.NaN, chance = l.Count > 0 ? l.Average(h => 1.0 / h.cand) : double.NaN;
                md.AppendLine("| " + c + " | " + l.Count + " | " + F(l.Select(h => h.pModel).DefaultIfEmpty(double.NaN).Average(), "0.000") + " | " + F(l.Select(h => h.pUniform).DefaultIfEmpty(double.NaN).Average(), "0.000") +
                              " | " + MeanCell(lift, "0.000") + " | " + Stats.P(p) + ", r = " + F(r) + " | " + Stats.Pct(top1) + " / " + Stats.Pct(chance) + " |");
            }
            md.AppendLine();

            // kurva belajar per rumah
            double[] xs = Enumerable.Range(1, houses).Select(x => (double)x).ToArray();
            double[][] lm = new double[conds.Length][], llo = new double[conds.Length][], lhi = new double[conds.Length][];
            StringBuilder lt = new StringBuilder("| Rumah ke- | " + string.Join(" | ", conds.Select(c => c + " (keunggulan, n)")) + " |\n|---|" + string.Concat(conds.Select(c => "---|")) + "\n");
            for (int i = 0; i < conds.Length; i++) { lm[i] = new double[houses]; llo[i] = new double[houses]; lhi[i] = new double[houses]; }
            for (int h = 1; h <= houses; h++)
            {
                lt.Append("| " + h + " |");
                for (int i = 0; i < conds.Length; i++)
                {
                    List<double> v = hides.Where(x => x.cond == conds[i] && x.house == h).Select(x => x.pModel - x.pUniform).ToList();
                    lm[i][h - 1] = v.Count > 0 ? v.Average() : double.NaN;
                    if (v.Count > 1) Stats.MeanCI(v, out llo[i][h - 1], out lhi[i][h - 1]); else { llo[i][h - 1] = lhi[i][h - 1] = lm[i][h - 1]; }
                    lt.Append(" " + (v.Count > 0 ? F(lm[i][h - 1], "0.000") + " (" + v.Count + ")" : "-") + " |");
                }
                lt.Append('\n');
            }
            double yLo = Math.Min(-0.1, Math.Floor((llo.SelectMany(x => x).Where(x => !double.IsNaN(x)).DefaultIfEmpty(-0.1).Min() - 0.02) * 20) / 20);
            double yHi = Math.Max(0.3, Math.Ceiling((lhi.SelectMany(x => x).Where(x => !double.IsNaN(x)).DefaultIfEmpty(0.3).Max() + 0.02) * 20) / 20);
            Save(root, "Analisis/belajar_kurva.svg", SvgChart.Lines("Keunggulan prediksi hantu atas tebakan acak", "peluang model - peluang acak untuk tempat sembunyi yang dipilih; 0 = tidak lebih baik dari acak",
                 "rumah ke-", xs, conds, lm, llo, lhi, yLo, yHi, "0.00", false, 0, "setara acak"));
            md.AppendLine("![Kurva belajar](Analisis/belajar_kurva.svg)");
            md.AppendLine();
            md.Append(lt);
            md.AppendLine();

            // --- hadang & jaga
            md.AppendLine("## 2. Keputusan hadang dan jaga vs pilihan acak");
            md.AppendLine();
            md.AppendLine("| Keputusan | Kondisi | Jumlah | Tepat (model) | Tepat (acak) | Uji McNemar eksak |");
            md.AppendLine("|---|---|---|---|---|---|");
            string[] kinds = { "hadang", "jaga" };
            List<string> cats = new List<string>(); List<double> mv = new List<double>(), ml = new List<double>(), mh = new List<double>(), rv = new List<double>(), rl = new List<double>(), rh = new List<double>();
            Dictionary<string, double> decP = new Dictionary<string, double>(), decDiff = new Dictionary<string, double>();
            foreach (string k in kinds)
                foreach (string c in conds.Concat(new[] { "Semua" }))
                {
                    List<LearnDec> l = decs.Where(e => e.kind == k && (c == "Semua" || e.cond == c)).ToList();
                    int hm = l.Count(e => e.hitM), hr = l.Count(e => e.hitR), b = l.Count(e => e.hitM && !e.hitR), cc = l.Count(e => !e.hitM && e.hitR);
                    double p = Stats.McNemarExact(b, cc);
                    decP[k + c] = p; decDiff[k + c] = l.Count > 0 ? (hm - hr) / (double)l.Count : double.NaN;
                    md.AppendLine("| " + (k == "hadang" ? "Hadang" : "Jaga") + " | " + (c == "Semua" ? "*gabungan*" : c) + " | " + l.Count + " | " + WinCell(Bin(l.Select(e => e.hitM))) + " | " + WinCell(Bin(l.Select(e => e.hitR))) + " | " + Stats.P(p) + " |");
                    if (c == "Semua") continue;                        // grafik hanya per kondisi
                    cats.Add((k == "hadang" ? "Hadang" : "Jaga") + " - " + c);
                    Stats.Wilson(hm, l.Count, out double a1, out double a2); Stats.Wilson(hr, l.Count, out double b1, out double b2);
                    mv.Add(l.Count > 0 ? hm / (double)l.Count : double.NaN); ml.Add(a1); mh.Add(a2);
                    rv.Add(l.Count > 0 ? hr / (double)l.Count : double.NaN); rl.Add(b1); rh.Add(b2);
                }
            md.AppendLine();
            Save(root, "Analisis/belajar_keputusan.svg", SvgChart.Bars("Keputusan hantu yang tepat", "berdasarkan memori (model) vs pilihan acak pada lintasan pemain yang sama", cats.ToArray(), new[] { "Model (memori hantu)", "Acak" },
                 new[] { mv.ToArray(), rv.ToArray() }, new[] { ml.ToArray(), rl.ToArray() }, new[] { mh.ToArray(), rh.ToArray() }, 0, 1, "0.00", true));
            md.AppendLine("![Keputusan hantu](Analisis/belajar_keputusan.svg)");
            md.AppendLine();

            // --- kesimpulan
            md.AppendLine("## Kesimpulan");
            string habit = conds[0], rnd = conds.Length > 1 ? conds[1] : conds[0];
            md.AppendLine("- **Prediksi tempat sembunyi.** Pada pemain **" + habit + "**, prediksi hantu " + (liftMean[habit] > 0 && liftP[habit] < 0.05 ? "**lebih baik dari acak** (keunggulan rata-rata " + F(liftMean[habit], "0.000") + ", " + Stats.P(liftP[habit]) + ")." : "belum terbukti lebih baik dari acak (" + Stats.P(liftP[habit]) + ")."));
            if (conds.Length > 1)
            {
                List<double> lh1 = hides.Where(h => h.cond == habit).Select(h => h.pModel - h.pUniform).ToList(), lh2 = hides.Where(h => h.cond == rnd).Select(h => h.pModel - h.pUniform).ToList();
                double pc = Stats.MannWhitney(lh1, lh2, out double zc, out double rc);
                string t = "- Pada pemain **" + rnd + "** (kontrol), keunggulannya " + F(liftMean[rnd], "0.000") + " (" + Stats.P(liftP[rnd]) + ")";
                if (liftMean[rnd] < liftMean[habit])
                    t += ", " + F(liftMean[habit] / Math.Max(1e-6, liftMean[rnd]), "0.0") + "x lebih kecil daripada pemain berkebiasaan (Mann-Whitney " + Stats.P(pc) + ", " + Stats.Sig(pc) + ")." +
                         (liftP[rnd] < 0.05 ? " Keunggulan kecil yang tersisa wajar: pilihan \"acak\" bot tetap dibatasi pada tempat yang aman dan terjangkau, dan tempat yang sama bisa terpakai lagi, sehingga ada sedikit pola yang bisa dipelajari." : "") +
                         " Jadi keunggulan hantu terutama datang dari **kebiasaan pemain**.";
                else t += " - tidak lebih kecil dari kondisi berkebiasaan, sehingga perlu ditelaah.";
                md.AppendLine(t);
            }
            string jh = "jaga" + habit, jr = "jaga" + rnd, hh = "hadang" + habit, hr2 = "hadang" + rnd;
            if (decP.ContainsKey(jh))
                md.AppendLine("- **Jaga** (berjaga di tempat sembunyi yang dicurigai, Thompson sampling): pada pemain " + habit + " model lebih tepat " + Stats.Pct(decDiff[jh]) + " daripada acak (" + Stats.P(decP[jh]) + ", " + Stats.Sig(decP[jh]) + ")" +
                              (decP.ContainsKey(jr) ? "; pada pemain " + rnd + " selisihnya " + Stats.Pct(decDiff[jr]) + " (" + Stats.P(decP[jr]) + ")" : "") + ". Penjagaan memang hanya menguntungkan hantu bila pemain punya tempat sembunyi kebiasaan.");
            if (decP.ContainsKey("hadangSemua"))
                md.AppendLine("- **Hadang** (menunggu di jalur favorit, heatmap jalur): selisih model - acak " + Stats.Pct(decDiff[hh]) + " pada pemain " + habit + " (" + Stats.P(decP[hh]) + ") dan " + Stats.Pct(decDiff[hr2]) + " pada pemain " + rnd + " (" + Stats.P(decP[hr2]) + "); gabungan " +
                              Stats.Pct(decDiff["hadangSemua"]) + " (" + Stats.P(decP["hadangSemua"]) + ", " + Stats.Sig(decP["hadangSemua"]) + "). Hadang belajar dari **jalur**, bukan tempat sembunyi; kedua bot sama-sama mengejar relik lewat jalur terpendek sehingga sama-sama punya kebiasaan jalur - kondisi " + rnd + " bukan kontrol untuk hadang.");
            md.AppendLine();
            md.AppendLine("Catatan: hantu tidak pernah menemukan pemain yang sedang bersembunyi (desain game); \"tepat\" berarti tebakan hantu menunjuk tempat/jalur yang memang dipakai pemain, yang membuat hadangan dan penjagaan terasa \"cerdas\".");
            md.AppendLine();
            md.AppendLine("Data per kejadian: `Hasil_Eksperimen_Belajar.csv`.");
            Save(root, "Hasil_Eksperimen_Belajar.md", md.ToString());
            return md.ToString();
        }

        // ================================================================== 3. ANALISIS UJI COBA PEMAIN
        static List<Dictionary<string, string>> ReadCsv(string path)
        {
            List<Dictionary<string, string>> rows = new List<Dictionary<string, string>>();
            if (!File.Exists(path)) return rows;
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            if (lines.Length == 0) return rows;
            List<string> head = SplitCsv(lines[0]);
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                List<string> f = SplitCsv(lines[i]);
                Dictionary<string, string> d = new Dictionary<string, string>();
                for (int k = 0; k < head.Count; k++) d[head[k]] = k < f.Count ? f[k] : "";
                rows.Add(d);
            }
            return rows;
        }

        static List<string> SplitCsv(string line)
        {
            List<string> r = new List<string>(); StringBuilder cur = new StringBuilder(); bool q = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (q) { if (ch == '"') { if (i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; } else q = false; } else cur.Append(ch); }
                else if (ch == '"') q = true;
                else if (ch == ',') { r.Add(cur.ToString()); cur.Clear(); }
                else cur.Append(ch);
            }
            r.Add(cur.ToString());
            return r;
        }

        static double D(Dictionary<string, string> r, string k) { return r.TryGetValue(k, out string s) && double.TryParse(s, NumberStyles.Float, Inv, out double v) ? v : double.NaN; }
        static string S(Dictionary<string, string> r, string k) { return r.TryGetValue(k, out string s) ? s : ""; }

        static readonly string[] QKeys = { "q1_seru", "q2_tegang", "q3_pas", "q4_kesulitan", "q5_frustrasi", "q6_mampu", "q7_adaptif", "q8_main_lagi" };

        /// <summary>Gabungkan data semua peserta di folder dataRoot (subfolder Pxx) dan tulis laporan. outName = nama file markdown.</summary>
        public static string AnalyzePlaytest(IList<string> dataRoots, string root, string outName, string chartPrefix, bool simulated)
        {
            List<string> dirs = new List<string>();
            foreach (string dr in dataRoots)
                if (Directory.Exists(dr)) dirs.AddRange(Directory.GetDirectories(dr).Where(x => File.Exists(Path.Combine(x, "rumah.csv"))));
            dirs = dirs.OrderBy(x => Path.GetFileName(x)).ToList();
            string dataRoot = string.Join("`, `", dataRoots.Select(x => x.Replace('\\', '/')));
            List<Dictionary<string, string>> houses = new List<Dictionary<string, string>>(), quest = new List<Dictionary<string, string>>(), comp = new List<Dictionary<string, string>>(), learn = new List<Dictionary<string, string>>();
            Dictionary<string, int> seen = new Dictionary<string, int>(); List<string> dup = new List<string>();
            foreach (string d in dirs)
            {
                // kunci peserta = nama folder; nomor ganda (mis. dari dua komputer) dibedakan agar dua orang tidak tergabung
                string key = Path.GetFileName(d);
                if (seen.TryGetValue(key, out int cnt)) { seen[key] = cnt + 1; dup.Add(key); key = key + "#" + (cnt + 1); } else seen[key] = 1;
                void Add(List<Dictionary<string, string>> into, string file) { foreach (Dictionary<string, string> r in ReadCsv(Path.Combine(d, file))) { r["peserta"] = key; into.Add(r); } }
                Add(houses, "rumah.csv"); Add(quest, "kuesioner.csv"); Add(comp, "perbandingan.csv"); Add(learn, "belajar.csv");
            }
            // peserta lengkap = punya kuesioner DDA dan Tetap
            List<string> parts = quest.Select(q => S(q, "peserta")).Distinct().Where(p => quest.Any(q => S(q, "peserta") == p && S(q, "kondisi") == "DDA") && quest.Any(q => S(q, "peserta") == p && S(q, "kondisi") == "Tetap")).OrderBy(p => p).ToList();
            int n = parts.Count;

            StringBuilder md = new StringBuilder();
            md.AppendLine("# Hasil Uji Coba Pemain - Haunted House: Hantu yang Mempelajari Ketakutanmu");
            md.AppendLine();
            if (simulated) { md.AppendLine("> **DATA SIMULASI** - laporan ini dibuat dari peserta bot untuk menguji alur pencatatan & analisis, BUKAN hasil penelitian."); md.AppendLine(); }
            md.AppendLine("Dibuat otomatis oleh `Tools > Hantu Belajar > Analisis Data Uji Coba` (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm", Inv) + ") dari folder `" + dataRoot + "`.");
            md.AppendLine();
            md.AppendLine("## Peserta");
            int ddaFirst = comp.Count(c => S(c, "urutan").StartsWith("A=DDA") && parts.Contains(S(c, "peserta"))), tetapFirst = comp.Count(c => S(c, "urutan").StartsWith("A=Tetap") && parts.Contains(S(c, "peserta")));
            md.AppendLine("- Peserta lengkap: **" + n + "** (" + ddaFirst + " mulai dengan DDA, " + tetapFirst + " mulai dengan kesulitan tetap). Folder data ditemukan: " + dirs.Count + ".");
            md.AppendLine("- Desain within-subject: tiap peserta memainkan kedua kondisi; urutan diseimbangkan; perbandingan memakai uji peringkat bertanda Wilcoxon (berpasangan, dua sisi), efek r = z / sqrt(n).");
            if (n < 8) md.AppendLine("- **Catatan:** peserta masih " + n + " orang; disarankan minimal 8-12 agar uji statistik bermakna.");
            if (dup.Count > 0) md.AppendLine("- **Peringatan:** nomor peserta ganda ditemukan (" + string.Join(", ", dup.Distinct()) + "); dianalisis sebagai peserta berbeda (ditandai #2, #3). Periksa kembali catatan peneliti.");
            int dropped = dirs.Count - n;
            if (dropped > 0) md.AppendLine("- " + dropped + " folder peserta tidak lengkap (belum mengisi kuesioner kedua sesi) dan tidak dianalisis.");
            md.AppendLine();
            if (n == 0) { md.AppendLine("Belum ada peserta yang menyelesaikan kedua sesi."); Save(root, outName, md.ToString()); return md.ToString(); }

            // --- kuesioner
            md.AppendLine("## 1. Kuesioner (skala 1-5)");
            md.AppendLine();
            md.AppendLine("| Butir | DDA | Tetap | Selisih (DDA - Tetap) | Wilcoxon | r |");
            md.AppendLine("|---|---|---|---|---|---|");
            double[][] qv = { new double[8], new double[8] }, ql = { new double[8], new double[8] }, qh = { new double[8], new double[8] };
            List<string> qFindings = new List<string>();
            for (int i = 0; i < 8; i++)
            {
                List<double> a = new List<double>(), b = new List<double>(), diff = new List<double>();
                foreach (string p in parts)
                {
                    double va = quest.Where(q => S(q, "peserta") == p && S(q, "kondisi") == "DDA").Select(q => D(q, QKeys[i])).FirstOrDefault();
                    double vb = quest.Where(q => S(q, "peserta") == p && S(q, "kondisi") == "Tetap").Select(q => D(q, QKeys[i])).FirstOrDefault();
                    if (double.IsNaN(va) || double.IsNaN(vb)) continue;
                    a.Add(va); b.Add(vb); diff.Add(va - vb);
                }
                double pw = Stats.Wilcoxon(diff, out double w, out double z, out double r, out int nz);
                md.AppendLine("| " + PlaytestUI.ItemShort[i] + (i == 3 ? " (3 = pas)" : "") + " | " + MeanCell(a, "0.00", 1, 5) + " | " + MeanCell(b, "0.00", 1, 5) + " | " + F(diff.DefaultIfEmpty(double.NaN).Average()) + " | " + Stats.P(pw) + " | " + F(r) + " |");
                if (pw < 0.05) qFindings.Add(PlaytestUI.ItemShort[i] + " (" + (diff.Average() > 0 ? "lebih tinggi" : "lebih rendah") + " dengan DDA, " + Stats.P(pw) + ")");
                qv[0][i] = a.Count > 0 ? a.Average() : double.NaN; qv[1][i] = b.Count > 0 ? b.Average() : double.NaN;
                if (a.Count > 1) Stats.MeanCI(a, out ql[0][i], out qh[0][i]); else ql[0][i] = qh[0][i] = qv[0][i];
                if (b.Count > 1) Stats.MeanCI(b, out ql[1][i], out qh[1][i]); else ql[1][i] = qh[1][i] = qv[1][i];
            }
            md.AppendLine();
            Save(root, "Analisis/" + chartPrefix + "kuesioner.svg", SvgChart.Bars("Kuesioner pemain", "rata-rata skala 1-5 per butir (n = " + n + " peserta)", PlaytestUI.ItemShort, new[] { "DDA", "Tetap" }, qv, ql, qh, 1, 5, "0", false));
            md.AppendLine("![Kuesioner](Analisis/" + chartPrefix + "kuesioner.svg)");
            md.AppendLine();
            md.AppendLine("Butir 4 (kesulitan) diukur 1 = terlalu mudah, 3 = pas, 5 = terlalu sulit. Jarak ke 3 per peserta: DDA " +
                          F(parts.Select(p => Math.Abs(quest.Where(q => S(q, "peserta") == p && S(q, "kondisi") == "DDA").Select(q => D(q, "q4_kesulitan")).FirstOrDefault() - 3)).DefaultIfEmpty(double.NaN).Average()) +
                          ", Tetap " + F(parts.Select(p => Math.Abs(quest.Where(q => S(q, "peserta") == p && S(q, "kondisi") == "Tetap").Select(q => D(q, "q4_kesulitan")).FirstOrDefault() - 3)).DefaultIfEmpty(double.NaN).Average()) + " (makin kecil makin pas).");
            md.AppendLine();

            // --- performa
            md.AppendLine("## 2. Performa per sesi (rata-rata per peserta)");
            md.AppendLine();
            md.AppendLine("| Ukuran | DDA | Tetap | Wilcoxon |");
            md.AppendLine("|---|---|---|---|");
            List<Dictionary<string, string>> real = houses.Where(h => S(h, "hasil") != "dibatalkan").ToList();
            (string name, Func<List<Dictionary<string, string>>, double> f, string fmt, double min, double max)[] metrics =
            {
                ("Rumah dimainkan", l => l.Count, "0.0", 0, double.PositiveInfinity),
                ("Tingkat lolos", l => l.Count == 0 ? double.NaN : l.Count(h => S(h, "hasil") == "lolos") / (double)l.Count, "0.00", 0, 1),
                ("Tertangkap per rumah", l => l.Count == 0 ? double.NaN : l.Count(h => S(h, "hasil") == "tertangkap") / (double)l.Count, "0.00", 0, 1),
                ("Dikejar per rumah", l => l.Count == 0 ? double.NaN : l.Average(h => D(h, "dikejar")), "0.00", 0, double.PositiveInfinity),
                ("Relik per rumah (proporsi)", l => l.Count == 0 ? double.NaN : l.Average(h => D(h, "relik") / Math.Max(1, D(h, "relik_target"))), "0.00", 0, 1),
                ("Jarak P ke target 0,62 per rumah", l => { List<double> v = l.Select(h => D(h, "P_jarak_target")).Where(x => !double.IsNaN(x)).ToList(); return v.Count == 0 ? double.NaN : v.Average(); }, "0.000", 0, 1),
                ("Kesulitan d akhir sesi", l => l.Count == 0 ? double.NaN : D(l[l.Count - 1], "d_akhir"), "0.00", 0, 1),
            };
            foreach (var m in metrics)
            {
                List<double> a = new List<double>(), b = new List<double>(), diff = new List<double>();
                foreach (string p in parts)
                {
                    double va = m.f(real.Where(h => S(h, "peserta") == p && S(h, "kondisi") == "DDA").ToList()), vb = m.f(real.Where(h => S(h, "peserta") == p && S(h, "kondisi") == "Tetap").ToList());
                    if (double.IsNaN(va) || double.IsNaN(vb)) continue;
                    a.Add(va); b.Add(vb); diff.Add(va - vb);
                }
                double pw = Stats.Wilcoxon(diff, out double w, out double z, out double r, out int nz);
                md.AppendLine("| " + m.name + " | " + MeanCell(a, m.fmt, m.min, m.max) + " | " + MeanCell(b, m.fmt, m.min, m.max) + " | " + Stats.P(pw) + " |");
            }
            md.AppendLine();

            // --- preferensi
            md.AppendLine("## 3. Preferensi langsung");
            md.AppendLine();
            md.AppendLine("| Pertanyaan | Pilih DDA | Pilih Tetap | Sama | Uji binomial (DDA vs Tetap) |");
            md.AppendLine("|---|---|---|---|---|");
            string[] ck = { "lebih_seru", "lebih_pas", "hantu_lebih_pintar" };
            for (int i = 0; i < 3; i++)
            {
                List<string> v = comp.Where(c => parts.Contains(S(c, "peserta"))).Select(c => S(c, ck[i])).ToList();
                int a = v.Count(x => x == "DDA"), b = v.Count(x => x == "Tetap"), s = v.Count(x => x == "sama");
                md.AppendLine("| " + PlaytestUI.CompareItems[i] + " | " + a + " | " + b + " | " + s + " | " + Stats.P(Stats.BinomTwoSided(a, a + b, 0.5)) + " |");
            }
            md.AppendLine();

            // --- hantu belajar pada pemain sungguhan
            md.AppendLine("## 4. Hantu belajar dari pemain sungguhan");
            md.AppendLine();
            List<Dictionary<string, string>> lh = learn.Where(r => S(r, "jenis") == "sembunyi" && S(r, "kondisi") != "Latihan").ToList();
            List<double> lift = lh.Select(r => D(r, "p_model") - D(r, "p_acak")).Where(x => !double.IsNaN(x)).ToList();
            double pl = Stats.Wilcoxon(lift, out double lw, out double lz, out double lr, out int ln);
            md.AppendLine("- Prediksi tempat sembunyi: " + lh.Count + " kejadian, peluang model rata-rata " + F(lh.Select(r => D(r, "p_model")).DefaultIfEmpty(double.NaN).Average(), "0.000") +
                          " vs acak " + F(lh.Select(r => D(r, "p_acak")).DefaultIfEmpty(double.NaN).Average(), "0.000") + "; keunggulan " + MeanCell(lift, "0.000") + " (" + Stats.P(pl) + ").");
            foreach (string k in new[] { "hadang", "jaga" })
            {
                List<Dictionary<string, string>> l = learn.Where(r => S(r, "jenis") == k && S(r, "kondisi") != "Latihan").ToList();
                int hm = l.Count(r => S(r, "hit_model") == "1"), hr = l.Count(r => S(r, "hit_acak") == "1");
                int b = l.Count(r => S(r, "hit_model") == "1" && S(r, "hit_acak") != "1"), c = l.Count(r => S(r, "hit_model") != "1" && S(r, "hit_acak") == "1");
                md.AppendLine("- Keputusan " + k + ": " + l.Count + " kejadian, tepat " + WinCell(Bin(l.Select(r => S(r, "hit_model") == "1"))) + " vs acak " + WinCell(Bin(l.Select(r => S(r, "hit_acak") == "1"))) + " (McNemar " + Stats.P(Stats.McNemarExact(b, c)) + ").");
            }
            md.AppendLine();

            // --- efek urutan
            md.AppendLine("## 5. Efek urutan (Sesi A vs Sesi B, apa pun kondisinya)");
            List<double> ab = new List<double>();
            foreach (string p in parts)
            {
                List<Dictionary<string, string>> A = real.Where(h => S(h, "peserta") == p && S(h, "sesi") == "SESI A").ToList(), B = real.Where(h => S(h, "peserta") == p && S(h, "sesi") == "SESI B").ToList();
                if (A.Count == 0 || B.Count == 0) continue;
                ab.Add(B.Count(h => S(h, "hasil") == "lolos") / (double)B.Count - A.Count(h => S(h, "hasil") == "lolos") / (double)A.Count);
            }
            double pab = Stats.Wilcoxon(ab, out double aw, out double az, out double ar, out int an);
            md.AppendLine("Perubahan tingkat lolos dari Sesi A ke Sesi B: " + F(ab.DefaultIfEmpty(double.NaN).Average()) + " (" + Stats.P(pab) + "). " +
                          (pab < 0.05 ? "Ada efek latihan/urutan; penyeimbangan urutan membuat efek ini terbagi rata pada kedua kondisi." : "Tidak terlihat efek urutan yang berarti."));
            md.AppendLine();
            md.AppendLine("## Ringkasan");
            md.AppendLine(qFindings.Count > 0 ? "- Perbedaan signifikan pada kuesioner: " + string.Join("; ", qFindings) + "." : "- Tidak ada butir kuesioner yang berbeda signifikan antara DDA dan Tetap (" + n + " peserta).");
            md.AppendLine("- Uji beberapa butir sekaligus meningkatkan peluang temuan kebetulan; laporkan butir utama (seru, kesulitan pas, frustrasi) sebagai hipotesis utama.");
            md.AppendLine();
            md.AppendLine("Data mentah: folder `DataUjiCoba/Pxx/` (rumah.csv, kejadian.csv, belajar.csv, kuesioner.csv, perbandingan.csv, info.txt).");
            Save(root, outName, md.ToString());
            return md.ToString();
        }
    }
}
