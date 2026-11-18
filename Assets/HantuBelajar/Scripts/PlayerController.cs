using UnityEngine;

namespace HantuBelajar
{
    public class PlayerController : MonoBehaviour
    {
        public float walk = 3.2f, run = 5.6f, crouchSpeed = 1.7f, sens = 2.2f;
        public float battery = 100f, stamina = 100f;
        public bool flashOn, crouch, running, hidden, moving;
        public HidingSpot spot;
        public Camera cam; public Light flash;
        public float hideTime;      // lama bersembunyi saat ini
        public string focusPrompt = "";
        // autopilot untuk rekaman demo
        public bool autopilot, autoRun, autoCrouch; public Vector3 autoDir; public float autoPitch;
        CharacterController cc; Transform pivot;
        float yaw, pitch, stepT, camH = 1.65f, bob, vy, hideYaw;
        Interactable focus;

        public float Yaw { get { return yaw; } set { yaw = value; } }
        public Vector3 HeadPos { get { return cam.transform.position; } }
        public Vector2Int Cell { get { return G.C(hidden && spot != null ? spot.ExitPos : transform.position); } }

        public static PlayerController Create()
        {
            GameObject g = new GameObject("Player");
            g.layer = 2;
            PlayerController p = g.AddComponent<PlayerController>();
            p.cc = g.AddComponent<CharacterController>();
            p.cc.height = 1.8f; p.cc.radius = 0.3f; p.cc.center = new Vector3(0, 0.9f, 0); p.cc.stepOffset = 0.3f;
            p.pivot = new GameObject("Pivot").transform; p.pivot.SetParent(g.transform, false); p.pivot.localPosition = new Vector3(0, 1.65f, 0);
            GameObject cg = new GameObject("Main Camera"); cg.tag = "MainCamera";
            cg.transform.SetParent(p.pivot, false);
            p.cam = cg.AddComponent<Camera>(); p.cam.nearClipPlane = 0.05f; p.cam.fieldOfView = 70f; p.cam.backgroundColor = Color.black; p.cam.clearFlags = CameraClearFlags.SolidColor;
            cg.AddComponent<AudioListener>();
            GameObject fl = new GameObject("Flashlight"); fl.transform.SetParent(cg.transform, false); fl.transform.localPosition = new Vector3(0.25f, -0.25f, 0.1f);
            p.flash = fl.AddComponent<Light>(); p.flash.type = LightType.Spot; p.flash.range = 20f; p.flash.spotAngle = 62f; p.flash.innerSpotAngle = 30f;
            p.flash.intensity = 2.6f; p.flash.color = new Color(1f, 0.95f, 0.85f); p.flash.shadows = LightShadows.Soft;
            // senter kecil di tangan (viewmodel sederhana)
            GameObject torch = HouseBuilder.LPrim(PrimitiveType.Cylinder, cg.transform, new Vector3(0.28f, -0.3f, 0.45f), new Vector3(0.06f, 0.14f, 0.06f), Look.Mat("torch", new Color(0.12f, 0.12f, 0.13f), 0.6f), "TorchModel");
            torch.transform.localRotation = Quaternion.Euler(90, 0, 0);
            torch.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return p;
        }

        public void Spawn(Vector3 pos, float yawDeg)
        {
            ExitHideInstant();
            cc.enabled = false; transform.position = pos; cc.enabled = true;
            yaw = yawDeg; pitch = 0; battery = 100f; stamina = 100f; flashOn = true; crouch = false;
        }

        void Update()
        {
            GameManager gm = GameManager.I;
            if (gm == null || !gm.Playing) { flash.enabled = flashOn && battery > 0f; return; }
            float dt = Time.deltaTime;
            float mx = Input.GetAxisRaw("Mouse X") * sens, my = Input.GetAxisRaw("Mouse Y") * sens;
            if (Cursor.lockState != CursorLockMode.Locked || autopilot) { mx = 0; my = 0; }
            yaw += mx; pitch = Mathf.Clamp(pitch - my, -80f, 80f);

            if (Input.GetKeyDown(KeyCode.F) && battery > 0f) { flashOn = !flashOn; AudioDirector.I.Click(); }
            DDAManager dda = gm.dda;
            if (flashOn) battery = Mathf.Max(0f, battery - dda.batteryDrain * dt);
            if (battery <= 0f) flashOn = false;
            flash.enabled = flashOn;
            float flick = battery < 15f ? (Mathf.PerlinNoise(Time.time * 9f, 0) > 0.35f ? 1f : 0.15f) : 1f;
            flash.intensity = 2.6f * flick * Mathf.Lerp(0.6f, 1f, battery / 100f);

            if (hidden)
            {
                hideTime += dt;
                yaw = Mathf.Clamp(Mathf.DeltaAngle(hideYaw, yaw), -50f, 50f) + hideYaw;
                pitch = Mathf.Clamp(pitch, -30f, 30f);
                pivot.rotation = Quaternion.Euler(pitch, yaw, 0);
                stamina = Mathf.Min(100f, stamina + 25f * dt);
                if (Input.GetKeyDown(KeyCode.E)) ExitHide();
                focusPrompt = "[E] Keluar dari persembunyian";
                return;
            }

            transform.rotation = Quaternion.Euler(0, yaw, 0);
            pivot.localRotation = Quaternion.Euler(pitch, 0, 0);

            crouch = Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl);
            Vector3 inp = new Vector3((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0), 0, (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
            if (autopilot)
            {
                crouch = autoCrouch;
                pitch = Mathf.MoveTowards(pitch, autoPitch, 60f * dt);
                inp = Vector3.zero;
                if (autoDir.sqrMagnitude > 0.0001f)
                {
                    float ty = Mathf.Atan2(autoDir.x, autoDir.z) * Mathf.Rad2Deg;
                    yaw = Mathf.MoveTowardsAngle(yaw, ty, 300f * dt);
                    transform.rotation = Quaternion.Euler(0, yaw, 0);
                    inp = Quaternion.Inverse(transform.rotation) * autoDir.normalized;
                    if (Mathf.Abs(Mathf.DeltaAngle(yaw, ty)) > 50f) inp *= 0.2f;
                }
            }
            if (inp.sqrMagnitude > 1f) inp.Normalize();
            moving = inp.sqrMagnitude > 0.01f;
            running = moving && !crouch && (Input.GetKey(KeyCode.LeftShift) || (autopilot && autoRun)) && stamina > 1f;
            float sp = crouch ? crouchSpeed : running ? run : walk;
            stamina = running ? Mathf.Max(0f, stamina - 18f * dt) : Mathf.Min(100f, stamina + 12f * dt);
            Vector3 v = transform.TransformDirection(inp) * sp;
            vy = cc.isGrounded ? -1f : vy - 20f * dt;
            v.y = vy;
            cc.Move(v * dt);
            cc.height = Mathf.Lerp(cc.height, crouch ? 1.1f : 1.8f, dt * 10f); cc.center = new Vector3(0, cc.height * 0.5f, 0);
            camH = Mathf.Lerp(camH, crouch ? 1.0f : 1.65f, dt * 10f);
            if (moving) bob += dt * sp * 2.2f;
            pivot.localPosition = new Vector3(0, camH + (moving ? Mathf.Sin(bob) * 0.04f : 0f), 0);

            // langkah kaki -> suara -> hantu bisa mendengar
            if (moving)
            {
                stepT -= dt * sp;
                if (stepT <= 0f)
                {
                    stepT = 2.1f;
                    float noise = crouch ? 2.5f : running ? 13f : 6.5f;
                    AudioDirector.I.Step(transform.position, running ? 0.9f : crouch ? 0.25f : 0.55f);
                    if (gm.ghost != null) gm.ghost.HearNoise(transform.position, noise);
                }
            }

            // interaksi
            focus = null; focusPrompt = "";
            if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, 2.6f, ~(1 << 2), QueryTriggerInteraction.Collide))
            {
                focus = hit.collider.GetComponentInParent<Interactable>();
                if (focus != null) focusPrompt = focus.Prompt(this);
            }
            if (focus != null && Input.GetKeyDown(KeyCode.E)) focus.Use(this);
        }

        public void EnterHide(HidingSpot s)
        {
            spot = s; s.occupied = true; hidden = true; hideTime = 0f;
            cc.enabled = false;
            transform.position = s.InsidePos - Vector3.up * 1.65f;
            pivot.localPosition = new Vector3(0, 1.65f, 0);
            transform.rotation = Quaternion.identity;
            hideYaw = s.transform.eulerAngles.y; yaw = hideYaw; pitch = 0;
            flashOn = false;
            s.usesThisHouse++;
            GhostMemory.RecordHide(s);
            GameManager.I.ghost.OnPlayerHid(s);
            AudioDirector.I.Door();
        }

        public void ExitHide()
        {
            if (!hidden) return;
            Vector3 p = spot.ExitPos;
            spot.occupied = false; hidden = false; spot = null;
            transform.position = p; transform.rotation = Quaternion.Euler(0, yaw, 0);
            pivot.localRotation = Quaternion.identity;
            cc.enabled = true;
            AudioDirector.I.Door();
        }
        public void ExitHideInstant() { if (hidden && spot != null) spot.occupied = false; hidden = false; spot = null; if (cc != null) cc.enabled = true; }
    }
}
