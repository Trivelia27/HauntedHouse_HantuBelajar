using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HantuBelajar
{
    /// <summary>
    /// Mode uji coba pemain (penelitian, within-subject): panduan -> latihan (panduan awal) -> Sesi A -> kuesioner A -> Sesi B -> kuesioner B -> perbandingan.
    /// Satu sesi memakai DDA, sesi lain kesulitan tetap (d = 0,40). Urutan diseimbangkan: peserta bernomor ganjil DDA dulu, genap Tetap dulu.
    /// Peserta hanya melihat "Sesi A / Sesi B" (info kesulitan disembunyikan). Seed rumah tiap sesi sama untuk semua peserta,
    /// memori hantu dikosongkan tiap sesi, dan memori/simpanan pemilik game dikembalikan setelah selesai.
    /// </summary>
    public class Playtest : MonoBehaviour
    {
        public static Playtest I;
        public static bool Active { get { return I != null && I.phase != Phase.None; } }

        public enum Phase { None, Latihan, SesiA, SesiB }
        public Phase phase = Phase.None;
        public int participant = 1;
        public bool ddaFirst = true;
        public float sessionMinutes = 8f, practiceMinutes = 3f;
        public float timeLeft;
        public int housesInPhase;
        bool phaseEnding;

        public const int SeedLatihan = 31000, SeedA = 41000, SeedB = 52000;
        const string PrefsBackupKey = "HB_PlaytestBackup";
        static readonly string[] BackupKeys = { "HB_GhostMemory2", "HB_SaveHouse", "HB_SaveD", "HB_SaveSeed", "HB_HintsShown", "HB_TutDone", "HB_GuideSeen" };

        public string Code { get { return "P" + participant.ToString("00"); } }
        public string Folder { get { return Path.Combine(DataRoot, Code); } }
        public string SessionLabel { get { return phase == Phase.Latihan ? "LATIHAN" : phase == Phase.SesiA ? "SESI A" : phase == Phase.SesiB ? "SESI B" : ""; } }
        public string ConditionOf(Phase p) { return p == Phase.Latihan ? "Latihan" : (p == Phase.SesiA) == ddaFirst ? "DDA" : "Tetap"; }
        public string OrderLabel { get { return ddaFirst ? "A=DDA,B=Tetap" : "A=Tetap,B=DDA"; } }

        /// <summary>Folder data: DataUjiCoba di samping exe (build) atau di folder proyek (editor).</summary>
        public static string DataRoot { get { return !string.IsNullOrEmpty(RootOverride) ? RootOverride : Path.Combine(Directory.GetParent(Application.dataPath).FullName, "DataUjiCoba"); } }
        public static string RootOverride;

        public static Playtest Ensure()
        {
            if (I == null) I = GameManager.I.gameObject.AddComponent<Playtest>();
            return I;
        }

        /// <summary>Nomor peserta berikutnya berdasarkan folder Pxx yang sudah ada.</summary>
        public static int NextParticipant()
        {
            int max = 0;
            try
            {
                if (Directory.Exists(DataRoot))
                    foreach (string d in Directory.GetDirectories(DataRoot))
                    {
                        string n = Path.GetFileName(d);
                        if (n.Length > 1 && n[0] == 'P' && int.TryParse(n.Substring(1), out int k)) max = Mathf.Max(max, k);
                    }
            }
            catch { }
            return max + 1;
        }

        // ------------------------------------------------------------------ alur
        public void Begin(int number, float minutes, float practice)
        {
            participant = Mathf.Max(1, number); sessionMinutes = Mathf.Clamp(minutes, 2f, 30f); practiceMinutes = Mathf.Clamp(practice, 1f, 10f);
            ddaFirst = participant % 2 == 1;
            Backup();
            Telemetry.Begin(Folder, Code);
            WriteInfo("berjalan");
            LearningProbe.Reset(participant);
            GameManager.I.ui.ShowGuide(true, () => StartPhase(Phase.Latihan));
        }

        void StartPhase(Phase p)
        {
            phase = p; housesInPhase = 0; phaseEnding = false;
            timeLeft = (p == Phase.Latihan ? practiceMinutes : sessionMinutes) * 60f;
            string cond = ConditionOf(p);
            Telemetry.SetSession(SessionLabel, cond);
            Telemetry.Event("mulai_sesi", SessionLabel + " " + cond);
            GhostMemory.Reset(); GhostMemory.feed.Clear();
            GameManager gm = GameManager.I;
            gm.dda.adaptive = cond == "DDA";
            gm.dda.d = 0.4f; gm.dda.P = DDAManager.Target; gm.dda.Ps = DDAManager.Target; gm.dda.Apply();
            gm.StartStudyHouse(p == Phase.Latihan ? SeedLatihan : p == Phase.SesiA ? SeedA : SeedB);
        }

        /// <summary>Dipanggil GameManager tiap rumah dimulai.</summary>
        public void OnHouseStarted() { housesInPhase++; }

        void Update()
        {
            if (phase == Phase.None || phaseEnding) return;
            GameManager gm = GameManager.I;
            if (gm == null || !gm.Playing) return;
            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f) EndPhaseByTime();
        }

        void EndPhaseByTime()
        {
            phaseEnding = true;
            GameManager gm = GameManager.I;
            gm.EndHouseForStudy("waktu_habis");
            EndPhase();
        }

        /// <summary>Tombol lanjut di layar akhir rumah.</summary>
        public void Continue()
        {
            if (phase == Phase.Latihan || timeLeft <= 0f) { phaseEnding = true; EndPhase(); return; }
            GameManager.I.NextHouse();
        }

        void EndPhase()
        {
            GameManager gm = GameManager.I;
            gm.EnterStudyScreen();
            PlaytestUI ui = gm.ui.playtest;
            switch (phase)
            {
                case Phase.Latihan:
                    ui.ShowBreak("LATIHAN SELESAI", "Bagus! Sekarang kamu akan memainkan dua sesi, masing-masing " + Minutes(sessionMinutes) + ".\n" +
                                 "Setelah tiap sesi ada beberapa pertanyaan singkat. Mainkan seperti biasa - tidak ada jawaban benar atau salah.",
                                 "MULAI SESI A", () => StartPhase(Phase.SesiA));
                    break;
                case Phase.SesiA:
                    ui.ShowQuestionnaire("SESI A", a =>
                    {
                        Telemetry.Questionnaire("SESI A", ConditionOf(Phase.SesiA), a);
                        ui.ShowBreak("SESI A SELESAI", "Istirahat sebentar bila perlu.\nSesi B berlangsung " + Minutes(sessionMinutes) + ", lalu ada pertanyaan yang sama.", "MULAI SESI B", () => StartPhase(Phase.SesiB));
                    });
                    break;
                case Phase.SesiB:
                    ui.ShowQuestionnaire("SESI B", a =>
                    {
                        Telemetry.Questionnaire("SESI B", ConditionOf(Phase.SesiB), a);
                        ui.ShowCompare(c =>
                        {
                            Telemetry.Compare(OrderLabel, ConditionOf(Phase.SesiA), ConditionOf(Phase.SesiB), c);
                            Finish(true);
                        });
                    });
                    break;
            }
        }

        void Finish(bool completed)
        {
            Telemetry.Event(completed ? "uji_coba_selesai" : "uji_coba_dibatalkan", "");
            WriteInfo(completed ? "selesai" : "dibatalkan");
            Telemetry.End();
            phase = Phase.None;
            Restore();
            GameManager.I.dda.adaptive = true;
            if (completed) GameManager.I.ui.playtest.ShowDone(Code, Folder);
        }

        /// <summary>Keluar ke menu saat uji coba berjalan: data yang sudah ada tetap tersimpan.</summary>
        public void Abort()
        {
            if (phase == Phase.None) return;
            GameManager.I.EndHouseForStudy("dibatalkan");
            Finish(false);
        }

        // ------------------------------------------------------------------ info & simpanan pemilik game
        void WriteInfo(string status)
        {
            StringBuilder s = new StringBuilder();
            s.AppendLine("Peserta: " + Code);
            s.AppendLine("Status: " + status);
            s.AppendLine("Urutan kondisi: " + OrderLabel + "   (ganjil = DDA dulu, genap = Tetap dulu)");
            s.AppendLine("Durasi sesi: " + sessionMinutes.ToString("0.#", CultureInfo.InvariantCulture) + " menit, latihan " + practiceMinutes.ToString("0.#", CultureInfo.InvariantCulture) + " menit");
            s.AppendLine("Seed: latihan " + SeedLatihan + ", sesi A " + SeedA + ", sesi B " + SeedB + " (bertambah 1 tiap rumah)");
            s.AppendLine("Kesulitan awal tiap sesi: d = 0.40; kondisi Tetap = d tidak berubah");
            s.AppendLine("Versi game: " + Application.version + "   Unity " + Application.unityVersion);
            s.AppendLine("Layar: " + Screen.width + "x" + Screen.height + "   FOV " + Settings.Fov.ToString("0", CultureInfo.InvariantCulture) + "   kecerahan " + Settings.Brightness.ToString("0.00", CultureInfo.InvariantCulture));
            s.AppendLine("Waktu: " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            Telemetry.WriteInfo(s.ToString());
        }

        static bool IsString(string k) { return k == "HB_GhostMemory2" || k == "HB_HintsShown"; }

        /// <summary>Simpan memori hantu, simpanan rumah, dan status panduan/petunjuk pemilik game; peserta selalu mulai dari panduan & petunjuk baru.</summary>
        void Backup()
        {
            StringBuilder s = new StringBuilder();
            foreach (string k in BackupKeys)
            {
                if (!PlayerPrefs.HasKey(k)) { s.Append(k).Append('\u0001').Append('\u0002').Append('\n'); continue; }
                string v = IsString(k) ? PlayerPrefs.GetString(k) : k == "HB_SaveD" ? PlayerPrefs.GetFloat(k).ToString("R", CultureInfo.InvariantCulture) : PlayerPrefs.GetInt(k).ToString(CultureInfo.InvariantCulture);
                s.Append(k).Append('\u0001').Append(v.Replace("\n", " ")).Append('\n');
            }
            PlayerPrefs.SetString(PrefsBackupKey, s.ToString());
            PlayerPrefs.Save();
            HUD.ResetHints();
        }

        void Restore()
        {
            string b = PlayerPrefs.GetString(PrefsBackupKey, "");
            foreach (string line in b.Split('\n'))
            {
                int i = line.IndexOf('\u0001'); if (i <= 0) continue;
                string k = line.Substring(0, i), v = line.Substring(i + 1);
                if (v == "\u0002") { PlayerPrefs.DeleteKey(k); continue; }
                if (IsString(k)) PlayerPrefs.SetString(k, v);
                else if (k == "HB_SaveD") PlayerPrefs.SetFloat(k, float.Parse(v, CultureInfo.InvariantCulture));
                else PlayerPrefs.SetInt(k, int.Parse(v, CultureInfo.InvariantCulture));
            }
            PlayerPrefs.DeleteKey(PrefsBackupKey);
            PlayerPrefs.Save();
            if (PlayerPrefs.HasKey("HB_GhostMemory2")) GhostMemory.Load(); else GhostMemory.Reset();
            GhostMemory.feed.Clear();
            GameManager.I.ui.hud.ReloadShownHints();
        }

        static string Minutes(float m) { return m.ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',') + " menit"; }

        public string TimerText()
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(timeLeft));
            return SessionLabel + "  ·  " + (s / 60) + ":" + (s % 60).ToString("00");
        }
    }
}
