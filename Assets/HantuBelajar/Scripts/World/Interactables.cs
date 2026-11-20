using UnityEngine;

namespace HantuBelajar
{
    /// <summary>Objek yang bisa dipakai pemain dengan tombol E.</summary>
    public abstract class Interactable : MonoBehaviour
    {
        public abstract string Prompt(PlayerController p);
        public abstract void Use(PlayerController p);
        /// <summary>Titik yang didekati bot/autopilot untuk memakai objek ini.</summary>
        public virtual Vector3 UsePoint { get { return transform.position; } }
    }

    // ------------------------------------------------------------------ tempat sembunyi
    public class HidingSpot : Interactable
    {
        public int id, floor, dir;
        public SpotKind kind;
        public RoomType roomType;
        public Vector2Int cell;
        public Vector3Int frontNode;
        public bool occupied;
        public int usesThisHouse, checksThisHouse;
        public Vector3 insidePos, exitPos;
        public Quaternion insideRot;
        public Transform shakeTarget;
        Vector3 shakeBase; float shake;

        public void Init(Placed p, RoomType rt)
        {
            id = p.id; kind = (SpotKind)p.kind; roomType = rt; floor = p.f; cell = p.cell; dir = p.dir; frontNode = p.FrontNode;
            exitPos = G.W(frontNode);
        }

        public void SetShakeTarget(Transform t) { shakeTarget = t; shakeBase = t.localPosition; }

        public string KindName { get { return kind == SpotKind.Lemari ? "lemari" : "kolong ranjang"; } }
        public override string Prompt(PlayerController p) { return occupied ? "" : (kind == SpotKind.Lemari ? "[E] Sembunyi di dalam lemari" : "[E] Merangkak ke kolong ranjang"); }
        public override void Use(PlayerController p) { if (!occupied) p.EnterHide(this); }
        public override Vector3 UsePoint { get { return exitPos; } }

        /// <summary>Dipanggil hantu saat memeriksa tempat ini.</summary>
        public void Inspect(float dur)
        {
            shake = dur; checksThisHouse++;
            if (occupied && GameManager.I != null)
            {
                GameManager.I.dda.OnNearMiss();   // momen tegang untuk DDA; tidak memengaruhi keputusan hantu
                GhostMemory.Feed("Hantu memeriksa " + KindName + " dari luar... tidak tahu kamu di dalam");
            }
        }

        void Update()
        {
            if (shakeTarget == null) return;
            if (shake > 0f)
            {
                shake -= Time.deltaTime;
                float a = Mathf.Clamp01(shake) * 0.025f;
                shakeTarget.localPosition = shakeBase + new Vector3(Mathf.Sin(Time.time * 47f) * a, 0f, Mathf.Sin(Time.time * 39f) * a);
                if (shake <= 0f) shakeTarget.localPosition = shakeBase;
            }
        }
    }

    // ------------------------------------------------------------------ relik
    public class RelicPickup : Interactable
    {
        public int index; public bool taken;
        public static readonly string[] Names = { "Foto Lama", "Toples Abu", "Lilin Kuno", "Jam Kakek", "Botol Ramuan" };
        public static readonly string[] Lore =
        {
            "Foto keluarga pemilik rumah. Wajah sang ibu tergores habis.",
            "Toples berisi abu. Ada nama yang tertulis samar di tutupnya.",
            "Lilin yang tidak pernah habis terbakar.",
            "Jam saku yang berhenti tepat pukul 03.07.",
            "Botol ramuan berbau melati. Masih hangat.",
        };
        Transform item; Light glow; ParticleSystem sparkle; float baseY;

        public void Build(System.Random rng, int idx)
        {
            index = idx;
            // meja kecil sebagai alas
            GameObject table = AssetLibrary.Model("smalltable", rng);
            float top = 0.75f;
            if (table != null)
            {
                Bounds tb = Kit.ScaledBounds(table, G.S, 0f);
                Kit.Centered(table, transform, Vector3.zero, 0f, G.S, false, true, true, "Pedestal");
                top = tb.size.y;
            }
            else Kit.Box(transform, "Pedestal", transform.position + Vector3.up * 0.375f, new Vector3(0.6f, 0.75f, 0.6f), Look.DarkWood);
            string[] keys = { "pictures", "jar", "candlestick", "clock", "bottle" };
            GameObject pf = AssetLibrary.Model(keys[idx % keys.Length], rng);
            GameObject holder = new GameObject("Relic"); holder.transform.SetParent(transform, false);
            holder.transform.localPosition = new Vector3(0f, top + 0.05f, 0f);
            item = holder.transform; baseY = holder.transform.localPosition.y;
            if (pf != null)
            {
                float s = Kit.FitScale(pf, 0f, new Vector3(0.38f, 0.42f, 0.38f));
                Kit.Centered(pf, item, Vector3.zero, 0f, s, true, false, true, "Item");
                // bingkai foto paket hanya bermuka satu sisi -> pasang dua saling membelakangi agar tetap utuh saat berputar
                if (keys[idx % keys.Length] == "pictures") Kit.Centered(pf, item, Vector3.zero, 180f, s, true, false, true, "ItemBack");
            }
            else Kit.Prim(PrimitiveType.Sphere, item, new Vector3(0f, 0.15f, 0f), new Vector3(0.22f, 0.3f, 0.22f), Look.Emissive("relicFb", new Color(1f, 0.75f, 0.3f), 2f));
            GameObject lg = new GameObject("Glow"); lg.transform.SetParent(transform, false); lg.transform.localPosition = new Vector3(0f, top + 0.6f, 0f);
            glow = lg.AddComponent<Light>(); glow.type = LightType.Point; glow.color = new Color(1f, 0.72f, 0.35f); glow.range = 3.6f; glow.intensity = 3f; glow.shadows = LightShadows.None;
            sparkle = Fx.Sparkles(item, new Color(1f, 0.85f, 0.5f, 0.8f), 0.35f);
            BoxCollider bc = gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, (top + 0.5f) * 0.5f, 0f); bc.size = new Vector3(0.8f, top + 0.5f, 0.8f); bc.isTrigger = true;
        }

        void Update()
        {
            if (taken || item == null) return;
            item.Rotate(0f, 35f * Time.deltaTime, 0f, Space.World);
            item.localPosition = new Vector3(0f, baseY + Mathf.Sin(Time.time * 1.7f + index) * 0.03f, 0f);
            glow.intensity = 2.8f + Mathf.Sin(Time.time * 2.6f + index) * 0.6f;
        }
        public override string Prompt(PlayerController p) { return taken ? "" : "[E] Ambil relik: " + Names[index % Names.Length]; }
        public override void Use(PlayerController p)
        {
            if (taken) return;
            taken = true;
            if (item) item.gameObject.SetActive(false);
            if (glow) glow.enabled = false;
            if (sparkle) sparkle.Stop();
            GameManager.I.OnRelic(this);
        }
    }

    // ------------------------------------------------------------------ baterai
    public class BatteryPickup : Interactable
    {
        public bool taken;
        public void Build()
        {
            GameObject b = new GameObject("Battery"); b.transform.SetParent(transform, false);
            b.transform.localPosition = new Vector3(0f, 0.09f, 0f);
            b.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Kit.Prim(PrimitiveType.Cylinder, b.transform, Vector3.zero, new Vector3(0.09f, 0.11f, 0.09f), Look.Mat("battBody", new Color(0.08f, 0.08f, 0.09f), 0.6f));
            Kit.Prim(PrimitiveType.Cylinder, b.transform, new Vector3(0f, 0.035f, 0f), new Vector3(0.092f, 0.025f, 0.092f), Look.Emissive("battBand", new Color(0.35f, 1f, 0.45f), 1.4f));
            Kit.Prim(PrimitiveType.Cylinder, b.transform, new Vector3(0f, 0.12f, 0f), new Vector3(0.04f, 0.012f, 0.04f), Look.Brass);
            GameObject lg = new GameObject("Glow"); lg.transform.SetParent(transform, false); lg.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            Light l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(0.4f, 1f, 0.5f); l.range = 2f; l.intensity = 1.8f; l.shadows = LightShadows.None;
            BoxCollider bc = gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, 0.25f, 0f); bc.size = new Vector3(0.7f, 0.5f, 0.7f); bc.isTrigger = true;
        }
        public override string Prompt(PlayerController p) { return taken ? "" : "[E] Ambil baterai senter"; }
        public override void Use(PlayerController p)
        {
            if (taken) return;
            taken = true;
            p.battery = Mathf.Min(100f, p.battery + 45f);
            GameManager.I.Toast("Baterai senter +45%", null, "battery", UIKit.Good);
            AudioDirector.I.Pickup();
            gameObject.SetActive(false);
        }
    }

    // ------------------------------------------------------------------ kunci
    public class KeyPickup : Interactable
    {
        public bool taken;
        Transform item; Light glow;
        public void Build(System.Random rng)
        {
            GameObject table = AssetLibrary.Model("smalltable", rng);
            float top = 0.7f;
            if (table != null) { Kit.Centered(table, transform, Vector3.zero, 0f, G.S, false, true, true, "Table"); top = Kit.ScaledBounds(table, G.S, 0f).size.y; }
            GameObject holder = new GameObject("Key"); holder.transform.SetParent(transform, false);
            holder.transform.localPosition = new Vector3(0f, top + 0.22f, 0f);
            item = holder.transform;
            GameObject pf = AssetLibrary.Model("key");
            if (pf != null) { float s = Kit.FitScale(pf, 0f, new Vector3(0.3f, 0.36f, 0.3f)); Kit.Centered(pf, item, Vector3.zero, 0f, s, false, false, false, "KeyModel"); }
            else
            {
                Kit.Prim(PrimitiveType.Cylinder, item, Vector3.zero, new Vector3(0.03f, 0.14f, 0.03f), Look.Brass);
                Kit.Prim(PrimitiveType.Sphere, item, new Vector3(0f, 0.16f, 0f), new Vector3(0.1f, 0.1f, 0.03f), Look.Brass);
            }
            GameObject lg = new GameObject("Glow"); lg.transform.SetParent(transform, false); lg.transform.localPosition = new Vector3(0f, top + 0.6f, 0f);
            glow = lg.AddComponent<Light>(); glow.type = LightType.Point; glow.color = new Color(1f, 0.25f, 0.2f); glow.range = 3.2f; glow.intensity = 3f; glow.shadows = LightShadows.None;
            Fx.Sparkles(item, new Color(1f, 0.4f, 0.35f, 0.7f), 0.25f);
            BoxCollider bc = gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, (top + 0.5f) * 0.5f, 0f); bc.size = new Vector3(0.8f, top + 0.5f, 0.8f); bc.isTrigger = true;
        }
        void Update()
        {
            if (taken || item == null) return;
            item.Rotate(0f, 70f * Time.deltaTime, 0f, Space.World);
            glow.intensity = 2.6f + Mathf.Sin(Time.time * 3f) * 0.7f;
        }
        public override string Prompt(PlayerController p) { return taken ? "" : "[E] Ambil kunci tua"; }
        public override void Use(PlayerController p)
        {
            if (taken) return;
            taken = true; p.hasKey = true;
            gameObject.SetActive(false);
            AudioDirector.I.Pickup();
            GameManager.I.OnKey();
        }
    }

    // ------------------------------------------------------------------ pintu terkunci (daun ganda)
    public class LockedDoor : Interactable
    {
        public bool locked = true;
        Transform leafL, leafR, chain; BoxCollider block; float open;

        public void Build(float halfClear, float height)
        {
            Material wood = Look.DarkWood, panel = Look.Wood;
            for (int s = -1; s <= 1; s += 2)
            {
                GameObject hinge = new GameObject(s < 0 ? "HingeL" : "HingeR");
                hinge.transform.SetParent(transform, false);
                hinge.transform.localPosition = new Vector3(halfClear * s, 0f, 0f);
                float w = halfClear;
                Vector3 c = new Vector3(-s * w * 0.5f, height * 0.5f, 0f);
                Kit.Prim(PrimitiveType.Cube, hinge.transform, c, new Vector3(w - 0.02f, height, 0.07f), wood, "Leaf");
                for (int py = 0; py < 3; py++)
                {
                    float yy = height * (0.2f + 0.3f * py);
                    for (int side = -1; side <= 1; side += 2)
                        Kit.Prim(PrimitiveType.Cube, hinge.transform, new Vector3(c.x, yy, 0.04f * side), new Vector3(w * 0.7f, height * 0.22f, 0.02f), panel, "Panel");
                }
                Kit.Prim(PrimitiveType.Sphere, hinge.transform, new Vector3(-s * (w - 0.12f), 1.0f, 0.07f), new Vector3(0.07f, 0.07f, 0.07f), Look.Brass, "Knob");
                Kit.Prim(PrimitiveType.Sphere, hinge.transform, new Vector3(-s * (w - 0.12f), 1.0f, -0.07f), new Vector3(0.07f, 0.07f, 0.07f), Look.Brass, "Knob");
                if (s < 0) leafL = hinge.transform; else leafR = hinge.transform;
            }
            // rantai + gembok
            GameObject ch = new GameObject("Chain"); ch.transform.SetParent(transform, false);
            chain = ch.transform;
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 2; i++)
                    Kit.Prim(PrimitiveType.Cube, chain, new Vector3(0f, 1.0f + (i == 0 ? 0.1f : -0.1f), 0.06f * side), new Vector3(0.75f, 0.035f, 0.035f), Look.Iron, "Chain", Quaternion.Euler(0f, 0f, i == 0 ? 12f : -12f));
            Kit.Prim(PrimitiveType.Cube, chain, new Vector3(0f, 0.92f, 0.1f), new Vector3(0.16f, 0.2f, 0.07f), Look.Brass, "Padlock");
            Kit.Prim(PrimitiveType.Cube, chain, new Vector3(0f, 0.92f, -0.1f), new Vector3(0.16f, 0.2f, 0.07f), Look.Brass, "Padlock");
            block = gameObject.AddComponent<BoxCollider>();
            block.center = new Vector3(0f, height * 0.5f, 0f);
            block.size = new Vector3(halfClear * 2f + 0.1f, height, 0.25f);
        }

        public override string Prompt(PlayerController p)
        {
            if (!locked) return "";
            return p.hasKey ? "[E] Buka gembok dengan kunci tua" : "Pintu digembok. Cari kuncinya.";
        }
        public override void Use(PlayerController p)
        {
            if (!locked) return;
            if (!p.hasKey) { AudioDirector.I.Rattle(); GameManager.I.Toast("Pintu terkunci", "Kuncinya ada di suatu tempat di rumah ini - lihat peta (M)", "lock", UIKit.Danger); return; }
            Unlock();
            p.hasKey = false;
            GameManager.I.OnUnlock();
        }
        public void Unlock()
        {
            locked = false;
            if (chain) chain.gameObject.SetActive(false);
            if (block) block.enabled = false;
            AudioDirector.I.DoorOpen();
        }
        void Update()
        {
            if (locked || open >= 1f) return;
            open = Mathf.MoveTowards(open, 1f, Time.deltaTime * 0.8f);
            float a = Mathf.SmoothStep(0f, 100f, open);
            if (leafL) leafL.localRotation = Quaternion.Euler(0f, a, 0f);
            if (leafR) leafR.localRotation = Quaternion.Euler(0f, -a, 0f);
        }
    }

    // ------------------------------------------------------------------ pintu keluar
    public class ExitDoor : Interactable
    {
        Light lamp; bool opened;

        public void Build(float doorTop)
        {
            GameObject door = AssetLibrary.Model("door");
            if (door != null)
            {
                Bounds b = Kit.RawBounds(door);
                float sx = G.M / (b.size.x * G.S), sy = doorTop / (b.size.y * G.S);
                Vector3 pos = transform.position - transform.right * (b.center.x * G.S * sx) - transform.forward * (b.min.z * G.S) + Vector3.up * (-b.min.y * G.S * sy);
                Kit.Spawn(door, transform, pos, transform.eulerAngles.y, G.S, new Vector3(sx, sy, 1f), false, true, "Door");
            }
            else
            {
                Kit.Prim(PrimitiveType.Cube, transform, new Vector3(0f, doorTop * 0.5f, 0.04f), new Vector3(G.M - 0.1f, doorTop, 0.08f), Look.DarkWood, "Door");
                Kit.Prim(PrimitiveType.Sphere, transform, new Vector3(0.4f, 1.0f, 0.1f), Vector3.one * 0.07f, Look.Brass, "Knob");
            }
            // lampu di atas pintu: merah = terkunci, hangat = bisa keluar
            GameObject sconce = AssetLibrary.Model("sconce");
            GameObject lg;
            if (sconce != null)
            {
                lg = Kit.Spawn(sconce, transform, transform.position + Vector3.up * (doorTop + 0.25f) + transform.forward * 0.02f, transform.eulerAngles.y, G.S * 0.85f, null, true, true, "ExitLamp");
                lamp = lg.GetComponentInChildren<Light>(true);
            }
            if (lamp == null)
            {
                lg = new GameObject("ExitLamp"); lg.transform.SetParent(transform, false); lg.transform.localPosition = new Vector3(0f, doorTop + 0.35f, 0.3f);
                lamp = lg.AddComponent<Light>(); lamp.type = LightType.Point;
            }
            lamp.enabled = true;
            lamp.type = LightType.Point; lamp.range = 6f; lamp.intensity = 3f; lamp.shadows = LightShadows.None;
            lamp.color = new Color(1f, 0.15f, 0.1f);
            BoxCollider bc = gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, doorTop * 0.5f, 0.1f); bc.size = new Vector3(G.M, doorTop, 0.25f);
        }

        public void SetUnlocked(bool on)
        {
            if (lamp) { lamp.color = on ? new Color(1f, 0.85f, 0.6f) : new Color(1f, 0.15f, 0.1f); lamp.intensity = on ? 5f : 3f; }
        }

        public override Vector3 UsePoint { get { return transform.position + transform.forward * 1.0f; } }
        public override string Prompt(PlayerController p)
        {
            int need = GameManager.I.RelicsNeeded - GameManager.I.relicsFound;
            return need > 0 ? "Pintu keluar terkunci - kumpulkan " + need + " relik lagi" : "[E] Buka pintu dan kabur!";
        }
        public override void Use(PlayerController p)
        {
            if (GameManager.I.RelicsNeeded - GameManager.I.relicsFound > 0) { AudioDirector.I.Rattle(); return; }
            if (opened) return;
            opened = true;
            AudioDirector.I.DoorOpen();
            GameManager.I.OnWin();
        }
    }
}
