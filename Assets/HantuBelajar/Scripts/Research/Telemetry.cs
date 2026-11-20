using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HantuBelajar
{
    /// <summary>
    /// Pencatat data uji coba pemain (CSV, langsung ditulis ke disk per kejadian / per rumah agar aman bila game ditutup paksa).
    /// Folder: DataUjiCoba/Pxx/ di samping exe (atau folder proyek di editor):
    ///   rumah.csv        satu baris per rumah (hasil, waktu, relik, dikejar, sembunyi, kesulitan d & indikator DDA P, senter, jarak)
    ///   kejadian.csv     kejadian bertanda waktu (relik, kunci, dikejar, sembunyi, tertangkap, lolos, ...)
    ///   belajar.csv      ukuran "hantu belajar" (LearningProbe): prediksi tempat sembunyi & keputusan hadang/jaga vs acak
    ///   kuesioner.csv    jawaban skala 1-5 per sesi;  perbandingan.csv  sesi mana yang lebih seru / pas / pintar
    /// Angka memakai titik desimal (format CSV standar).
    /// </summary>
    public static class Telemetry
    {
        public static bool On { get; private set; }
        public static string Dir { get; private set; } = "";
        public static string Participant { get; private set; } = "";
        public static string Session { get; private set; } = "";
        public static string Condition { get; private set; } = "";
        public static int HouseInSession { get; private set; }

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static float flashT, playT, dist; static Vector3 lastPos; static bool hasLast;
        static float dStart; static int seed, floors, spots, batteries; static bool houseOpen;

        public const string HouseHeader = "peserta,sesi,kondisi,rumah_ke,seed,hasil,durasi_dtk,relik,relik_target,kunci,dikejar,waktu_dikejar_dtk,sembunyi,waktu_sembunyi_dtk,nyaris,d_awal,d_akhir,P_rata,P_jarak_target,evaluasi_dda,senter_persen,jarak_m,lantai,tempat_sembunyi,baterai_rumah,waktu";
        public const string EventHeader = "peserta,sesi,kondisi,rumah_ke,waktu_rumah_dtk,jenis,detail,waktu";
        public const string LearnHeader = "peserta,sesi,kondisi,rumah_ke,jenis,p_model,p_acak,peringkat,kandidat,hit_model,hit_acak,info,waktu";
        public const string QuestionHeader = "peserta,sesi,kondisi,q1_seru,q2_tegang,q3_pas,q4_kesulitan,q5_frustrasi,q6_mampu,q7_adaptif,q8_main_lagi,waktu";
        public const string CompareHeader = "peserta,urutan,lebih_seru,lebih_pas,hantu_lebih_pintar,waktu";

        public static void Begin(string dir, string participant)
        {
            Dir = dir; Participant = participant; Session = ""; Condition = ""; HouseInSession = 0; houseOpen = false;
            Directory.CreateDirectory(dir);
            On = true;
        }

        public static void End() { On = false; houseOpen = false; }

        public static void SetSession(string session, string condition) { Session = session; Condition = condition; HouseInSession = 0; houseOpen = false; }

        public static void HouseStart(GameManager gm)
        {
            if (!On) return;
            HouseInSession++; houseOpen = true;
            flashT = playT = dist = 0f; hasLast = false;
            dStart = gm.dda.d; seed = gm.seed;
            floors = gm.layout.floors; spots = gm.layout.spots.Count; batteries = gm.layout.batteries.Count;
            Event("mulai_rumah", "seed " + seed + " d " + dStart.ToString("0.00", Inv));
        }

        public static void Tick(GameManager gm, float dt)
        {
            if (!On || !houseOpen || !gm.Playing) return;
            PlayerController p = gm.player;
            playT += dt;
            if (p.flashOn && p.battery > 0f && !p.hidden) flashT += dt;
            Vector3 pos = p.transform.position;
            if (hasLast && !p.hidden) { Vector3 d = pos - lastPos; d.y = 0f; if (d.magnitude < 3f) dist += d.magnitude; }
            lastPos = pos; hasLast = true;
        }

        /// <summary>hasil: lolos / tertangkap / waktu_habis / dibatalkan.</summary>
        public static void HouseEnd(GameManager gm, string result)
        {
            if (!On || !houseOpen) return;
            houseOpen = false;
            DDAManager d = gm.dda;
            float meanP = d.nP > 0 ? d.sumP / d.nP : float.NaN, meanDev = d.nP > 0 ? Mathf.Abs(meanP - DDAManager.Target) : float.NaN;
            bool key = gm.player.hasKey || (gm.house != null && gm.house.lockedDoor != null && !gm.house.lockedDoor.locked);
            string line = Csv(Participant, Session, Condition, HouseInSession.ToString(), seed.ToString(), result, F(gm.houseTime, "0.0"), gm.relicsFound.ToString(), gm.RelicsNeeded.ToString(),
                              key ? "1" : "0", d.chasesHouse.ToString(), F(d.chaseTimeHouse, "0.0"), d.hidesHouse.ToString(), F(d.hideTimeHouse, "0.0"), d.nearMisses.ToString(),
                              F(dStart, "0.000"), F(d.d, "0.000"), F(meanP, "0.000"), F(meanDev, "0.000"), d.nP.ToString(), F(playT > 0f ? flashT / playT * 100f : 0f, "0"),
                              F(dist, "0"), floors.ToString(), spots.ToString(), batteries.ToString(), Stamp());
            Append("rumah.csv", HouseHeader, line);
            Event("akhir_rumah", result);
        }

        public static void Event(string type, string detail)
        {
            if (!On) return;
            float t = GameManager.I != null ? GameManager.I.houseTime : 0f;
            Append("kejadian.csv", EventHeader, Csv(Participant, Session, Condition, HouseInSession.ToString(), F(t, "0.0"), type, detail ?? "", Stamp()));
        }

        public static void Learn(string kind, float pModel, float pRandom, int rank, int cand, int hitModel, int hitRandom, string info)
        {
            if (!On) return;
            Append("belajar.csv", LearnHeader, Csv(Participant, Session, Condition, HouseInSession.ToString(), kind,
                   pModel >= 0f ? F(pModel, "0.0000") : "", pRandom >= 0f ? F(pRandom, "0.0000") : "", rank >= 0 ? rank.ToString() : "", cand >= 0 ? cand.ToString() : "",
                   hitModel >= 0 ? hitModel.ToString() : "", hitRandom >= 0 ? hitRandom.ToString() : "", info ?? "", Stamp()));
        }

        public static void Questionnaire(string session, string condition, int[] answers)
        {
            if (!On) return;
            string[] f = new string[3 + answers.Length + 1];
            f[0] = Participant; f[1] = session; f[2] = condition;
            for (int i = 0; i < answers.Length; i++) f[3 + i] = answers[i].ToString();
            f[f.Length - 1] = Stamp();
            Append("kuesioner.csv", QuestionHeader, Csv(f));
        }

        /// <summary>answers: 0 = sesi A, 1 = sama, 2 = sesi B; dicatat sebagai kondisi (DDA / Tetap / sama).</summary>
        public static void Compare(string order, string condA, string condB, int[] answers)
        {
            if (!On) return;
            string Map(int a) { return a == 0 ? condA : a == 2 ? condB : "sama"; }
            Append("perbandingan.csv", CompareHeader, Csv(Participant, order, Map(answers[0]), Map(answers[1]), Map(answers[2]), Stamp()));
        }

        public static void WriteInfo(string text)
        {
            if (!On) return;
            File.WriteAllText(Path.Combine(Dir, "info.txt"), text, new UTF8Encoding(false));
        }

        // ------------------------------------------------------------------ util
        static string F(float v, string fmt) { return float.IsNaN(v) ? "" : v.ToString(fmt, Inv); }
        static string Stamp() { return System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Inv); }

        public static string Csv(params string[] fields)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) sb.Append(',');
                string s = fields[i] ?? "";
                if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0) sb.Append('"').Append(s.Replace("\"", "\"\"")).Append('"');
                else sb.Append(s);
            }
            return sb.ToString();
        }

        static void Append(string file, string header, string line)
        {
            try
            {
                string path = Path.Combine(Dir, file);
                bool exists = File.Exists(path);
                using (StreamWriter w = new StreamWriter(path, true, new UTF8Encoding(false)))
                {
                    if (!exists) w.WriteLine(header);
                    w.WriteLine(line);
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[Telemetry] gagal menulis " + file + ": " + e.Message); }
        }
    }
}
