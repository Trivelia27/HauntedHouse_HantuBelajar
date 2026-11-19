using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HantuBelajar.EditorTools
{
    /// <summary>
    /// Eksperimen PCG (tanpa play mode): validitas, waktu, determinisme, dan dampak tingkat kesulitan.
    /// Batch: Unity.exe -batchmode -nographics -projectPath P -executeMethod HantuBelajar.EditorTools.Experiments.RunPcgBatch -quit
    /// </summary>
    public static class Experiments
    {
        static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        [MenuItem("Tools/Hantu Belajar/Eksperimen PCG")]
        public static void RunPcg()
        {
            float[] levels = { 0.1f, 0.45f, 0.8f };
            string[] names = { "Mudah (d=0,10)", "Sedang (d=0,45)", "Sulit (d=0,80)" };
            const int N = 200;
            StringBuilder md = new StringBuilder();
            md.AppendLine("# Hasil Eksperimen PCG - Haunted House: Hantu yang Mempelajari Ketakutanmu");
            md.AppendLine();
            md.AppendLine("Dibuat otomatis oleh `Tools > Hantu Belajar > Eksperimen PCG` (" + N + " seed per tingkat kesulitan, seed 1.." + N + "). Parameter PCG berasal dari `DdaMap.Pcg(d, seed)`.");
            md.AppendLine();
            md.AppendLine("| Tingkat | Valid | Percobaan rata2 | Waktu rata2 (ms) | Lantai | Ruangan | Pintu (loop) | Ruang terkunci | Tempat sembunyi | Jarak relik-sembunyi maks (target) | Dilonggarkan | Deterministik |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
            StringBuilder ascii = new StringBuilder();
            for (int li = 0; li < levels.Length; li++)
            {
                int valid = 0, det = 0, lockedN = 0, relaxed = 0;
                float att = 0, ms = 0, rooms = 0, doors = 0, loops = 0, spots = 0, rsd = 0, floors = 0;
                Dictionary<string, int> fails = new Dictionary<string, int>();
                PcgParams p0 = DdaMap.Pcg(levels[li], 1);
                for (int s = 1; s <= N; s++)
                {
                    PcgParams p = DdaMap.Pcg(levels[li], s);
                    HouseLayout L = HouseGenerator.Generate(p);
                    HouseLayout L2 = HouseGenerator.Generate(p);
                    if (L.hash == L2.hash) det++;
                    if (L.valid) valid++;
                    else { string k = L.failReason; fails.TryGetValue(k, out int c); fails[k] = c + 1; }
                    att += L.attempts; ms += L.ms; rooms += L.rooms.Count; doors += L.doors.Count; loops += L.loops; spots += L.spots.Count; floors += L.floors;
                    if (L.lockedRoom >= 0) lockedN++;
                    if (L.relicSpotRelax > 0) relaxed++;
                    rsd += L.relicSpotMax;
                    if (s <= 2) ascii.AppendLine(Ascii(L, names[li] + " seed " + s));
                }
                md.AppendLine("| " + names[li] + " | " + valid + "/" + N + " | " + F(att / N) + " | " + F(ms / N) + " | " + F(floors / N) + " | " + F(rooms / N) + " | " + F(doors / N) + " (" + F(loops / N) + ") | " + lockedN + "/" + N + " | " + F(spots / N) + " | " + F(rsd / N) + " (" + p0.relicSpotDist + ") | " + relaxed + " | " + det + "/" + N + " |");
                foreach (var kv in fails) md.AppendLine("  - gagal: " + kv.Key + " x" + kv.Value);
            }
            md.AppendLine();
            md.AppendLine("Keterangan: *Valid* = semua sel kosong terhubung, pintu keluar terjangkau tanpa kunci, kunci terjangkau tanpa kunci, semua relik & tempat sembunyi terjangkau, tiap lantai punya relik. *Dilonggarkan* = jumlah rumah yang batas jarak relik-ke-tempat-sembunyi perlu dinaikkan agar relik cukup.");
            File.WriteAllText("Hasil_Eksperimen_PCG.md", md.ToString());
            Directory.CreateDirectory("TestOutput");
            File.WriteAllText("TestOutput/pcg_ascii.txt", ascii.ToString());
            Debug.Log(md.ToString());
        }

        public static void RunPcgBatch()
        {
            try { RunPcg(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static string F(float v) { return v.ToString("0.0", CI); }

        /// <summary>Gambar denah ASCII untuk pemeriksaan cepat.</summary>
        public static string Ascii(HouseLayout L, string title)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("== " + title + "  valid=" + L.valid + " " + L.failReason + " hash=" + L.hash + " lantai=" + L.floors + " ruang=" + L.rooms.Count);
            for (int f = 0; f < L.floors; f++)
            {
                sb.AppendLine("-- " + L.FloorName(f));
                for (int y = L.h - 1; y >= -1; y--)
                {
                    StringBuilder row = new StringBuilder(), mid = new StringBuilder();
                    for (int x = 0; x < L.w; x++)
                    {
                        if (y >= 0)
                        {
                            Vector2Int c = new Vector2Int(x, y);
                            mid.Append(x == 0 ? "|" : "");
                            mid.Append(CellChar(L, f, c));
                            mid.Append(L.Wall(f, c, 1) ? (L.IsDoor(f, c, 1) ? (L.IsLocked(f, c, 1) ? "L" : "D") : "|") : (L.IsDoor(f, c, 1) ? (L.IsLocked(f, c, 1) ? "L" : "D") : " "));
                            if (x == L.w - 1 && L.exitNode.z == f && L.exitNode.x == x && L.exitNode.y == y && L.exitDir == 1) { mid.Length--; mid.Append("X"); }
                        }
                        Vector2Int cc = new Vector2Int(x, Mathf.Max(y, 0));
                        bool horiz = y < 0 ? true : (y == 0 ? false : false);
                        if (y >= 0)
                        {
                            Vector2Int below = new Vector2Int(x, y);
                            bool wall = L.Wall(f, below, 2);
                            bool door = L.IsDoor(f, below, 2);
                            row.Append(x == 0 ? "+" : "");
                            row.Append(door ? (L.IsLocked(f, below, 2) ? " L " : " D ") : (wall ? (L.exitNode.z == f && L.exitNode.x == x && L.exitNode.y == y && L.exitDir == 2 ? "-X-" : "---") : "   "));
                            row.Append("+");
                        }
                        _ = cc; _ = horiz;
                    }
                    if (y == L.h - 1)
                    {
                        StringBuilder top = new StringBuilder("+");
                        for (int x = 0; x < L.w; x++) top.Append(L.exitNode.z == f && L.exitNode.x == x && L.exitNode.y == y && L.exitDir == 0 ? "-X-+" : "---+");
                        sb.AppendLine(top.ToString());
                    }
                    if (y >= 0) { sb.AppendLine(mid.ToString()); sb.AppendLine(row.ToString()); }
                }
            }
            return sb.ToString();
        }

        static string CellChar(HouseLayout L, int f, Vector2Int c)
        {
            Vector3Int n = G.N(f, c);
            char ch = '.';
            if (L.Blocked(n)) ch = '#';
            foreach (Stair s in L.stairs)
            {
                if ((f == s.f || f == s.f + 1) && c == s.Ramp1) ch = '^';
                if (n == s.BottomNode) ch = 'b';
                if (n == s.LandingNode) ch = 'l';
            }
            foreach (Placed p in L.spots) if (p.Node == n) ch = p.kind == 0 ? 'W' : 'B';
            foreach (Placed p in L.props) if (p.Node == n) ch = '#';
            foreach (Placed p in L.relics) if (p.Node == n) ch = 'R';
            foreach (Placed p in L.batteries) if (p.Node == n) ch = 'e';
            if (L.key != null && L.key.Node == n) ch = 'K';
            if (L.start == n) ch = 'S';
            if (L.exitNode == n && ch == '.') ch = 'x';
            if (L.ghostStart == n && ch == '.') ch = 'G';
            char rc = (char)('a' + (L.room[f, c.x, c.y] % 26));
            return " " + ch + (L.rooms[L.room[f, c.x, c.y]].locked ? "!" : rc.ToString());
        }
    }
}
