using UnityEngine;

namespace HantuBelajar
{
    /// <summary>Pemetaan tingkat kesulitan d (0..1) ke parameter PCG dan parameter AI hantu (fungsi murni, bisa diuji).</summary>
    public static class DdaMap
    {
        public static PcgParams Pcg(float d, int seed)
        {
            d = Mathf.Clamp01(d);
            PcgParams p = new PcgParams { seed = seed };
            p.floors = d < 0.22f ? 1 : 2;
            if (p.floors == 1) { p.width = Mathf.RoundToInt(Mathf.Lerp(7f, 8f, d / 0.22f)); p.height = 6; }
            else { p.width = Mathf.RoundToInt(Mathf.Lerp(6f, 8f, d)); p.height = Mathf.RoundToInt(Mathf.Lerp(5f, 6f, d)); }
            p.minRoom = 2; p.maxRoom = 3;
            p.hidingSpots = Mathf.RoundToInt(Mathf.Lerp(10f, 5f, d));
            p.batteries = Mathf.RoundToInt(Mathf.Lerp(5f, 1f, d));
            p.relicCount = d > 0.68f ? 4 : 3;
            p.loopChance = Mathf.Lerp(0.45f, 0.12f, d);
            p.propDensity = Mathf.Lerp(0.3f, 0.45f, d);
            p.lockedRooms = d >= 0.3f ? 1 : 0;
            p.relicSpotDist = Mathf.RoundToInt(Mathf.Lerp(2f, 7f, d));
            p.litRatio = Mathf.Lerp(0.9f, 0.45f, d);
            return p;
        }

        public struct GhostParams
        {
            public float patrolSpeed, chaseSpeed, visionRange, fov, hearingMult, searchTime, learnWeight, checkMul, loseTime, explore, batteryDrain, pressureTime, awakeDelay, catchHold, chaseWindup;
        }

        public static GhostParams Ghost(float d)
        {
            d = Mathf.Clamp01(d);
            return new GhostParams
            {
                patrolSpeed = Mathf.Lerp(1.4f, 2.3f, d),
                chaseSpeed = Mathf.Lerp(3.1f, 4.6f, d),      // jalan pemain 3,0 m/s, lari 5,3 m/s
                visionRange = Mathf.Lerp(6f, 12f, d),
                fov = Mathf.Lerp(70f, 100f, d),
                hearingMult = Mathf.Lerp(0.55f, 1.3f, d),
                searchTime = Mathf.Lerp(8f, 18f, d),
                learnWeight = Mathf.Lerp(0.3f, 0.95f, d),
                checkMul = Mathf.Lerp(0.5f, 1.1f, d),
                loseTime = Mathf.Lerp(2f, 3.8f, d),
                explore = Mathf.Lerp(0.35f, 0.08f, d),
                batteryDrain = Mathf.Lerp(0.55f, 1.3f, d),
                pressureTime = Mathf.Lerp(90f, 40f, d),
                awakeDelay = Mathf.Lerp(22f, 10f, d),        // hantu baru aktif setelah beberapa detik
                catchHold = Mathf.Lerp(0.4f, 0.15f, d),      // harus menempel sesaat sebelum menangkap (ada kesempatan lolos)
                chaseWindup = Mathf.Lerp(0.9f, 0.35f, d),    // hantu menjerit sesaat sebelum mengejar (waktu bereaksi)
            };
        }
    }
}
