using UnityEngine;

namespace HantuBelajar
{
    public class HidingSpot : Interactable
    {
        public int id; public SpotKind kind; public RoomType roomType; public Vector2Int cell, front; public int dir;
        public bool occupied; public int usesThisHouse;
        Transform door; float shake;

        public void Init(Placed p, RoomType rt, System.Random rng)
        {
            id = p.id; kind = (SpotKind)p.kind; roomType = rt; cell = p.cell; dir = p.dir; front = p.cell + G.Dirs[p.dir];
            float back = -G.T * 0.5f + 0.1f;
            GameObject model = kind == SpotKind.Lemari ? AssetLibrary.Model(l => l.wardrobe, rng) : AssetLibrary.Model(l => l.bed, rng);
            Vector3 size = kind == SpotKind.Lemari ? new Vector3(1.4f, 2.2f, 0.75f) : new Vector3(1.6f, 0.9f, 2.2f);
            if (model != null)
            {
                GameObject w = AssetLibrary.Fit(model, transform, size);
                w.transform.localPosition = new Vector3(0, 0, back + size.z * 0.5f);
                door = w.transform;
            }
            else if (kind == SpotKind.Lemari)
            {
                HouseBuilder.LBox(transform, new Vector3(0, 1.1f, back + 0.37f), new Vector3(1.4f, 2.2f, 0.7f), Props.DarkMat, "Body");
                GameObject piv = new GameObject("DoorPivot"); piv.transform.SetParent(transform, false);
                piv.transform.localPosition = new Vector3(-0.68f, 0, back + 0.74f);
                HouseBuilder.LBox(piv.transform, new Vector3(0.68f, 1.1f, 0f), new Vector3(1.34f, 2.1f, 0.05f), Props.WoodMat, "Door");
                HouseBuilder.LBox(piv.transform, new Vector3(1.2f, 1.1f, 0.05f), new Vector3(0.05f, 0.2f, 0.05f), Look.Mat("brass", new Color(0.6f, 0.45f, 0.2f), 0.7f), "Knob");
                door = piv.transform;
            }
            else
            {
                HouseBuilder.LBox(transform, new Vector3(0, 0.55f, back + 1.1f), new Vector3(1.6f, 0.25f, 2.1f), Props.ClothMat(new Color(0.55f, 0.5f, 0.45f)), "Mattress");
                HouseBuilder.LBox(transform, new Vector3(0, 0.62f, back + 1.3f), new Vector3(1.65f, 0.22f, 1.6f), Props.ClothMat(new Color(0.35f, 0.12f, 0.12f)), "Blanket");
                HouseBuilder.LBox(transform, new Vector3(0, 0.75f, back + 0.3f), new Vector3(0.9f, 0.15f, 0.4f), Props.ClothMat(new Color(0.8f, 0.78f, 0.7f)), "Pillow");
                HouseBuilder.LBox(transform, new Vector3(0, 0.8f, back + 0.04f), new Vector3(1.7f, 1.6f, 0.08f), Props.DarkMat, "Headboard");
                for (int i = 0; i < 4; i++) HouseBuilder.LBox(transform, new Vector3(i % 2 == 0 ? -0.75f : 0.75f, 0.22f, back + (i < 2 ? 0.1f : 2.1f)), new Vector3(0.1f, 0.44f, 0.1f), Props.DarkMat, "Leg");
                door = transform.Find("Blanket");
            }
            BoxCollider bc = gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, size.y * 0.5f, back + size.z * 0.5f);
            bc.size = new Vector3(size.x, size.y, size.z);
        }

        public Vector3 InsidePos { get { return transform.position + transform.forward * (kind == SpotKind.Lemari ? -1.05f : -0.6f) + Vector3.up * (kind == SpotKind.Lemari ? 1.55f : 0.3f); } }
        public Vector3 ExitPos { get { return G.W(front); } }

        public override string Prompt(PlayerController p) { return occupied ? "" : "[E] Sembunyi di " + HouseGenerator.SpotName((int)kind); }
        public override void Use(PlayerController p) { if (!occupied) p.EnterHide(this); }

        /// <summary>Hantu memeriksa tempat ini (pintu dibuka / ranjang diguncang).</summary>
        public void Inspect(float t) { shake = t; }
        void Update()
        {
            if (door == null) return;
            if (shake > 0f)
            {
                shake -= Time.deltaTime;
                if (kind == SpotKind.Lemari && door.name == "DoorPivot") door.localRotation = Quaternion.Euler(0, -Mathf.Sin(Mathf.Clamp01(shake) * Mathf.PI) * 70f, 0);
                else door.localPosition += Random.insideUnitSphere * 0.004f;
            }
            else if (door.name == "DoorPivot") door.localRotation = Quaternion.Slerp(door.localRotation, Quaternion.identity, Time.deltaTime * 4f);
        }
    }

    public class RelicPickup : Interactable
    {
        public int index; public bool taken;
        Transform gem; Light glow;
        public static readonly string[] Names = { "Liontin Retak", "Boneka Kain", "Kunci Perak", "Foto Keluarga", "Kotak Musik" };

        public void Build(System.Random rng)
        {
            GameObject pedestal = HouseBuilder.LBox(transform, new Vector3(0, 0.45f, 0), new Vector3(0.6f, 0.9f, 0.6f), Props.DarkMat, "Pedestal");
            GameObject model = AssetLibrary.Model(l => l.relic, rng);
            if (model != null) { GameObject w = AssetLibrary.Fit(model, transform, new Vector3(0.4f, 0.4f, 0.4f)); w.transform.localPosition = new Vector3(0, 0.92f, 0); gem = w.transform; }
            else
            {
                GameObject g = HouseBuilder.LPrim(PrimitiveType.Sphere, transform, new Vector3(0, 1.15f, 0), new Vector3(0.25f, 0.32f, 0.25f), Look.Emissive("relic", new Color(1f, 0.75f, 0.3f), 2.2f), "Relic");
                gem = g.transform;
            }
            GameObject lg = new GameObject("Glow"); lg.transform.SetParent(transform, false); lg.transform.localPosition = new Vector3(0, 1.6f, 0);
            glow = lg.AddComponent<Light>(); glow.type = LightType.Point; glow.color = new Color(1f, 0.7f, 0.3f); glow.range = 4f; glow.intensity = 1.6f;
            BoxCollider bc = gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.7f, 0); bc.size = new Vector3(0.7f, 1.4f, 0.7f);
            pedestal.name = "Pedestal";
        }
        void Update()
        {
            if (taken || gem == null) return;
            gem.Rotate(0, 60f * Time.deltaTime, 0, Space.World);
            glow.intensity = 1.3f + Mathf.Sin(Time.time * 3f) * 0.4f;
        }
        public override string Prompt(PlayerController p) { return taken ? "" : "[E] Ambil relik: " + Names[index % Names.Length]; }
        public override void Use(PlayerController p)
        {
            if (taken) return;
            taken = true;
            if (gem) gem.gameObject.SetActive(false);
            glow.enabled = false;
            GameManager.I.OnRelic(this);
        }
    }

    public class BatteryPickup : Interactable
    {
        public void Build()
        {
            HouseBuilder.LPrim(PrimitiveType.Cylinder, transform, new Vector3(0, 0.12f, 0), new Vector3(0.14f, 0.12f, 0.14f), Look.Emissive("battery", new Color(0.3f, 1f, 0.5f), 1.2f), "Cell");
            BoxCollider bc = gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.3f, 0); bc.size = new Vector3(0.6f, 0.6f, 0.6f); bc.isTrigger = true;
        }
        public override string Prompt(PlayerController p) { return "[E] Ambil baterai senter"; }
        public override void Use(PlayerController p)
        {
            p.battery = Mathf.Min(100f, p.battery + 45f);
            GameManager.I.Toast("Baterai +45%");
            AudioDirector.I.Pickup();
            Destroy(gameObject);
        }
    }

    public class ExitDoor : Interactable
    {
        Renderer[] leaves; bool open;
        public void Build()
        {
            leaves = new Renderer[2];
            for (int s = 0; s < 2; s++)
            {
                GameObject g = HouseBuilder.LBox(transform, new Vector3((s == 0 ? -0.33f : 0.33f), 1.15f, 0), new Vector3(0.64f, 2.3f, 0.1f), Look.Mat("exitDoor", new Color(0.25f, 0.13f, 0.08f), 0.3f, Look.Planks(new Color(0.4f, 0.25f, 0.15f))), "Leaf");
                leaves[s] = g.GetComponent<Renderer>();
            }
            HouseBuilder.LBox(transform, new Vector3(0, 2.45f, -0.12f), new Vector3(0.7f, 0.18f, 0.04f), Look.Emissive("exitSign", new Color(0.2f, 1f, 0.35f), 1.5f), "Sign");
            BoxCollider bc = gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 1.15f, 0); bc.size = new Vector3(1.3f, 2.3f, 0.3f);
        }
        public override string Prompt(PlayerController p)
        {
            int need = GameManager.I.RelicsNeeded - GameManager.I.relicsFound;
            return need > 0 ? "Pintu terkunci - butuh " + need + " relik lagi" : "[E] Buka pintu dan keluar";
        }
        public override void Use(PlayerController p)
        {
            if (GameManager.I.RelicsNeeded - GameManager.I.relicsFound > 0) { AudioDirector.I.Door(); return; }
            if (open) return;
            open = true;
            GameManager.I.OnWin();
        }
    }
}
