using System.Collections;
using UnityEngine;

namespace HantuBelajar
{
    public class PlayerController : MonoBehaviour
    {
        public const float Walk = 3.0f, Run = 5.3f, CrouchSpeed = 1.6f;
        public const float NoiseRun = 13f, NoiseWalk = 6.5f, NoiseCrouch = 2.2f;
        public float battery = 100f, stamina = 100f;
        public bool flashOn = true, crouch, running, hidden, moving, hasKey, inputLocked, exhausted;
        public HidingSpot spot;
        public Camera cam; public Light flash, spill;
        public bool focusBeam;
        const float FlashIntensity = 34f;
        float focusT, aimDist = 10f; Renderer lensR; Material lensOn, lensOff;
        // tangan pemegang senter: 1 = senter diangkat di depan, 0 = tangan turun (keluar layar) saat senter tidak dipakai
        float raise = 1f, lowerDelay;
        public float HandRaise { get { return raise; } }
        public bool HandVisible { get { return viewmodel != null && viewmodel.gameObject.activeSelf; } }
        public float hideTime, noiseLevel;
        public string focusPrompt = "";
        public Interactable focus;
        public int floor;
        public Vector3Int node;

        // autopilot (bot eksperimen / rekaman demo)
        public bool autopilot, autoRun, autoCrouch;
        public Vector3 autoDir;
        public Vector3? autoLook;

        CharacterController cc; Transform pivot, flashRig, viewmodel;
        float yaw, pitch, stepT, camH = 1.62f, bob, vy, hideYaw, hidePitch, staminaDelay, flicker, swayX, swayY, tilt;
        Vector3 vel;
        AudioSource steps, breath;
        bool transitioning;
        float landKick;

        public Vector3 HeadPos { get { return cam.transform.position; } }
        public float Yaw { get { return yaw; } set { yaw = value; } }
        public float Pitch { get { return pitch; } set { pitch = value; } }

        public static PlayerController Create()
        {
            GameObject g = new GameObject("Player");
            g.layer = 2;
            PlayerController p = g.AddComponent<PlayerController>();
            p.cc = g.AddComponent<CharacterController>();
            p.cc.height = 1.8f; p.cc.radius = 0.32f; p.cc.center = new Vector3(0f, 0.9f, 0f); p.cc.stepOffset = 0.35f; p.cc.slopeLimit = 50f; p.cc.skinWidth = 0.04f;
            p.pivot = new GameObject("Pivot").transform; p.pivot.SetParent(g.transform, false); p.pivot.localPosition = new Vector3(0f, 1.62f, 0f);
            GameObject cg = new GameObject("Main Camera"); cg.tag = "MainCamera";
            cg.transform.SetParent(p.pivot, false);
            p.cam = cg.AddComponent<Camera>(); p.cam.nearClipPlane = 0.05f; p.cam.farClipPlane = 120f; p.cam.fieldOfView = 72f;
            p.cam.clearFlags = CameraClearFlags.Skybox;
            if (Look.URP)
            {
                var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(p.cam);
                data.renderPostProcessing = true;
                data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.FastApproximateAntialiasing;
            }
            cg.AddComponent<AudioListener>();
            // senter: rig terpisah yang mengikuti kamera dengan sedikit jeda (terasa dipegang tangan)
            p.flashRig = new GameObject("FlashRig").transform; p.flashRig.SetParent(g.transform, false);
            GameObject fl = new GameObject("Flashlight"); fl.transform.SetParent(p.flashRig, false); fl.transform.localPosition = new Vector3(0.22f, -0.24f, 0.66f);
            p.flash = fl.AddComponent<Light>(); p.flash.type = LightType.Spot; p.flash.range = 28f; p.flash.spotAngle = 60f; p.flash.innerSpotAngle = 40f;
            p.flash.intensity = FlashIntensity; p.flash.color = new Color(1f, 0.95f, 0.86f); p.flash.shadows = LightShadows.Soft; p.flash.shadowNearPlane = 0.25f; p.flash.shadowStrength = 0.85f;
            p.flash.shadowResolution = UnityEngine.Rendering.LightShadowResolution.High;
            p.flash.cookie = Look.FlashCookie;
            // cahaya sebaran (spill): kerucut lebar & redup di sekitar titik terang, seperti senter sungguhan
            GameObject sp = new GameObject("FlashSpill"); sp.transform.SetParent(fl.transform, false);
            p.spill = sp.AddComponent<Light>(); p.spill.type = LightType.Spot; p.spill.range = 14f; p.spill.spotAngle = 120f; p.spill.innerSpotAngle = 70f;
            p.spill.intensity = 3.2f; p.spill.color = new Color(1f, 0.93f, 0.82f); p.spill.shadows = LightShadows.None;
            p.BuildViewmodel();
            p.steps = g.AddComponent<AudioSource>(); p.steps.loop = true; p.steps.playOnAwake = false; p.steps.spatialBlend = 0f; p.steps.volume = 0f;
            p.steps.clip = AssetLibrary.Clip("footsteps");
            if (p.steps.clip != null) { p.steps.Play(); }
            p.breath = g.AddComponent<AudioSource>(); p.breath.loop = true; p.breath.playOnAwake = false; p.breath.spatialBlend = 0f; p.breath.volume = 0f;
            p.breath.clip = AudioDirector.BreathClip(); p.breath.Play();
            return p;
        }

        void BuildViewmodel()
        {
            viewmodel = new GameObject("Viewmodel").transform;
            viewmodel.SetParent(cam.transform, false);
            viewmodel.localPosition = new Vector3(0.24f, -0.27f, 0.42f);
            Material body = Look.Mat("torchBody", new Color(0.12f, 0.12f, 0.13f), 0.35f);
            Material grip = Look.Mat("torchGrip", new Color(0.05f, 0.05f, 0.05f), 0.15f);
            Material metal = Look.Mat("torchMetal", new Color(0.32f, 0.32f, 0.34f), 0.45f, null, null, 0.3f);
            Quaternion fwd = Quaternion.Euler(90f, 0f, 0f);
            Kit.Prim(PrimitiveType.Cylinder, viewmodel, new Vector3(0f, 0f, -0.05f), new Vector3(0.045f, 0.12f, 0.045f), grip, "Grip", fwd);
            Kit.Prim(PrimitiveType.Cylinder, viewmodel, new Vector3(0f, 0f, 0.1f), new Vector3(0.05f, 0.05f, 0.05f), body, "Body", fwd);
            Kit.Prim(PrimitiveType.Cylinder, viewmodel, new Vector3(0f, 0f, 0.17f), new Vector3(0.07f, 0.03f, 0.07f), metal, "Head", fwd);
            Kit.Prim(PrimitiveType.Cylinder, viewmodel, new Vector3(0f, 0f, 0.2f), new Vector3(0.06f, 0.004f, 0.06f), Look.Emissive("torchLens", new Color(1f, 0.95f, 0.8f), 2.5f), "Lens", fwd);
            Kit.Prim(PrimitiveType.Cube, viewmodel, new Vector3(0f, 0.026f, -0.02f), new Vector3(0.012f, 0.008f, 0.03f), metal, "Switch");
            // tangan (sarung tangan gelap) memegang senter
            Material glove = Look.Mat("glove", new Color(0.13f, 0.1f, 0.08f), 0.25f);
            Kit.Prim(PrimitiveType.Capsule, viewmodel, new Vector3(0.0f, -0.035f, -0.04f), new Vector3(0.07f, 0.06f, 0.09f), glove, "Hand", Quaternion.Euler(90f, 0f, 0f));
            Kit.Prim(PrimitiveType.Capsule, viewmodel, new Vector3(0.04f, -0.09f, -0.2f), new Vector3(0.075f, 0.16f, 0.075f), Look.Mat("sleeve", new Color(0.12f, 0.13f, 0.15f), 0.1f), "Sleeve", Quaternion.Euler(70f, 0f, -15f));
            foreach (Renderer r in viewmodel.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Transform ln = viewmodel.Find("Lens");
            if (ln != null) lensR = ln.GetComponent<Renderer>();
            lensOn = Look.Emissive("torchLens", new Color(1f, 0.95f, 0.8f), 2.5f);
            lensOff = Look.Mat("torchLensOff", new Color(0.16f, 0.16f, 0.15f), 0.85f);
        }

        public void ApplySettings()
        {
            cam.fieldOfView = Settings.Fov;
        }

        public void Spawn(Vector3 pos, float yawDeg)
        {
            StopAllCoroutines(); transitioning = false;
            if (hidden && spot != null) spot.occupied = false;
            hidden = false; spot = null; inputLocked = false;
            cc.enabled = false; transform.position = pos; transform.rotation = Quaternion.Euler(0f, yawDeg, 0f); cc.enabled = true;
            yaw = yawDeg; pitch = 0f; battery = 100f; stamina = 100f; flashOn = true; crouch = false; hasKey = false; exhausted = false;
            vel = Vector3.zero; vy = 0f;
            pivot.localPosition = new Vector3(0f, 1.62f, 0f); pivot.localRotation = Quaternion.identity; cam.transform.localRotation = Quaternion.identity;
            flashRig.position = cam.transform.position; flashRig.rotation = cam.transform.rotation;
            raise = 1f; lowerDelay = 0.3f;
            viewmodel.gameObject.SetActive(true);
        }

        void Update()
        {
            GameManager gm = GameManager.I;
            float dt = Time.deltaTime;
            if (gm == null || gm.layout == null) return;
            floor = G.F(transform.position.y, gm.layout.floors);
            node = gm.layout.NodeAt(transform.position);
            if (gm.state == GameManager.State.Catching) { flash.enabled = flashOn && battery > 0f; UpdateAudio(dt, false); return; }   // kamera dikendalikan adegan tertangkap
            bool playing = gm.Playing && !inputLocked;

            // senter: F nyala/mati, tahan klik kanan = fokus (lebih terang & jauh, boros baterai)
            if (playing && !hidden && Input.GetKeyDown(KeyCode.F) && !autopilot) ToggleFlash();
            bool lightOn = flashOn && !hidden && battery > 0f;
            focusBeam = lightOn && playing && !autopilot && Input.GetMouseButton(1);
            if (flashOn && playing) battery = Mathf.Max(0f, battery - gm.dda.gp.batteryDrain * (focusBeam ? 2.2f : 1f) * dt);
            if (battery <= 0f) { flashOn = false; lightOn = false; }
            flash.enabled = lightOn; spill.enabled = lightOn;
            float k = battery > 20f ? 1f : Mathf.Lerp(0.35f, 1f, battery / 20f);              // baru meredup di bawah 20%
            if (battery < 8f && Mathf.PerlinNoise(Time.time * 6f, 0.3f) < 0.3f) k *= 0.15f;     // hampir habis: tersendat
            if (gm.ghost != null && !gm.ghost.frozen && !gm.ghost.Asleep && gm.ghost.DistanceTo(transform.position) < 7f && Random.value < dt * 3f) flicker = 0.12f;
            flicker -= dt;
            if (flicker > 0f) k *= 0.15f;
            // tangan: senter diangkat hanya saat dipakai; mati / baterai habis / bersembunyi -> jeda sebentar lalu tangan turun keluar layar
            bool wantRaise = flashOn && battery > 0f && !hidden;
            if (wantRaise) lowerDelay = 0.25f; else lowerDelay -= dt;
            raise = Mathf.MoveTowards(raise, wantRaise ? 1f : lowerDelay > 0f ? raise : 0f, dt * (wantRaise ? 3.0f : 2.0f));
            k *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, raise));                // sinar menyala saat senter terangkat ke arah bidikan
            focusT = Mathf.MoveTowards(focusT, focusBeam ? 1f : 0f, dt * 5f);
            flash.intensity = Mathf.Lerp(FlashIntensity, FlashIntensity * 1.7f, focusT) * k;
            flash.spotAngle = Mathf.Lerp(60f, 34f, focusT); flash.innerSpotAngle = Mathf.Lerp(40f, 22f, focusT);
            flash.range = Mathf.Lerp(28f, 40f, focusT);
            spill.intensity = Mathf.Lerp(3.2f, 1.2f, focusT) * k;
            if (lensR != null) lensR.sharedMaterial = lightOn ? lensOn : lensOff;
            bool showHand = !hidden && gm.state != GameManager.State.Menu && raise > 0.01f;
            if (viewmodel.gameObject.activeSelf != showHand) viewmodel.gameObject.SetActive(showHand);

            if (transitioning) { UpdateAudio(dt, false); return; }

            if (hidden)
            {
                hideTime += dt;
                UpdateLook(dt, playing);
                yaw = hideYaw + Mathf.Clamp(Mathf.DeltaAngle(hideYaw, yaw), -55f, 55f);
                pitch = Mathf.Clamp(pitch, hidePitch - 30f, hidePitch + 30f);
                pivot.rotation = Quaternion.Euler(pitch, yaw, 0f);
                stamina = Mathf.Min(100f, stamina + 22f * dt);
                if (stamina > 30f) exhausted = false;
                focus = null;
                focusPrompt = spot != null && spot.kind == SpotKind.KolongRanjang ? "[E] Keluar dari kolong ranjang" : "[E] Keluar dari lemari";
                if (playing && !autopilot && Input.GetKeyDown(KeyCode.E)) ExitHide();
                noiseLevel = 0f;
                UpdateAudio(dt, false);
                return;
            }

            UpdateLook(dt, playing);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            pivot.localRotation = Quaternion.Euler(pitch, 0f, tilt);

            // gerak
            Vector3 inp = Vector3.zero;
            if (playing)
            {
                if (autopilot)
                {
                    crouch = autoCrouch;
                    if (autoDir.sqrMagnitude > 0.0001f) inp = Quaternion.Inverse(transform.rotation) * autoDir.normalized;
                }
                else
                {
                    crouch = Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl);
                    inp = new Vector3((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0), 0f, (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
                }
            }
            if (inp.sqrMagnitude > 1f) inp.Normalize();
            moving = inp.sqrMagnitude > 0.01f;
            bool wantRun = autopilot ? autoRun : (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            if (stamina <= 1f) exhausted = true;
            if (exhausted && stamina > 30f) exhausted = false;
            running = moving && !crouch && wantRun && !exhausted && (inp.z > 0.2f || (autopilot && inp.z > -0.3f));
            float sp = crouch ? CrouchSpeed : running ? Run : Walk;
            if (running) { stamina = Mathf.Max(0f, stamina - 12f * dt); staminaDelay = 0.8f; }
            else { staminaDelay -= dt; if (staminaDelay <= 0f) stamina = Mathf.Min(100f, stamina + (moving ? 10f : 16f) * dt); }
            Vector3 target = transform.TransformDirection(inp) * sp;
            vel = Vector3.MoveTowards(vel, target, (moving ? 22f : 30f) * dt);
            bool wasGrounded = cc.isGrounded;
            vy = cc.isGrounded ? -2f : vy - 22f * dt;
            Vector3 move = vel; move.y = vy;
            cc.Move(move * dt);
            if (!wasGrounded && cc.isGrounded && vy < -6f) landKick = 0.08f;
            float targetH = crouch ? 1.15f : 1.8f;
            if (!crouch && cc.height < 1.79f)
            {
                // cegah berdiri di bawah halangan
                if (Physics.SphereCast(transform.position + Vector3.up * 0.5f, 0.3f, Vector3.up, out RaycastHit _, 1.3f, ~(1 << 2), QueryTriggerInteraction.Ignore)) targetH = cc.height;
            }
            cc.height = Mathf.MoveTowards(cc.height, targetH, dt * 4f); cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
            camH = Mathf.Lerp(camH, cc.height - 0.18f, dt * 10f);
            float spd = new Vector3(vel.x, 0f, vel.z).magnitude;
            if (spd > 0.2f && cc.isGrounded) bob += dt * spd * 2.1f;
            landKick = Mathf.MoveTowards(landKick, 0f, dt * 0.6f);
            float bobY = Mathf.Sin(bob) * 0.035f * Mathf.Clamp01(spd / Walk) * (Settings.HeadBob ? 1f : 0.2f);
            pivot.localPosition = new Vector3(Mathf.Cos(bob * 0.5f) * 0.02f * Mathf.Clamp01(spd / Walk) * (Settings.HeadBob ? 1f : 0.2f), camH + bobY - landKick, 0f);
            tilt = Mathf.Lerp(tilt, -inp.x * 1.2f, dt * 6f);

            // suara langkah -> hantu mendengar
            noiseLevel = !moving || spd < 0.3f ? 0f : crouch ? NoiseCrouch : running ? NoiseRun : NoiseWalk;
            if (moving && cc.isGrounded && spd > 0.3f)
            {
                stepT -= dt * spd;
                if (stepT <= 0f)
                {
                    stepT = 1.8f;
                    if (gm.ghost != null) gm.ghost.HearNoise(transform.position, noiseLevel);
                    gm.dda.OnNoise(noiseLevel);
                }
            }
            if (transform.position.y < -4f) { gm.Toast("Kembali ke ruang tamu"); Spawn(G.W(gm.layout.start) + Vector3.up * 0.1f, yaw); }

            // interaksi
            focus = null; focusPrompt = "";
            if (playing)
            {
                Ray ray = new Ray(cam.transform.position, cam.transform.forward);
                if (Physics.SphereCast(ray, 0.12f, out RaycastHit hit, 2.6f, ~(1 << 2), QueryTriggerInteraction.Collide))
                {
                    focus = hit.collider.GetComponentInParent<Interactable>();
                    if (focus != null) focusPrompt = focus.Prompt(this);
                }
                if (focus != null && !autopilot && Input.GetKeyDown(KeyCode.E)) focus.Use(this);
            }
            UpdateFlashRig(dt);
            UpdateAudio(dt, cc.isGrounded && spd > 0.3f);
        }

        void UpdateLook(float dt, bool playing)
        {
            if (autopilot)
            {
                if (autoLook.HasValue)
                {
                    Vector3 d = autoLook.Value - cam.transform.position;
                    float ty = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                    float tp = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
                    yaw = Mathf.MoveTowardsAngle(yaw, ty, 200f * dt);
                    pitch = Mathf.MoveTowards(pitch, Mathf.Clamp(tp, -60f, 60f), 120f * dt);
                }
                else if (autoDir.sqrMagnitude > 0.0001f)
                {
                    float ty = Mathf.Atan2(autoDir.x, autoDir.z) * Mathf.Rad2Deg;
                    yaw = Mathf.MoveTowardsAngle(yaw, ty, 260f * dt);
                    pitch = Mathf.MoveTowards(pitch, 6f, 60f * dt);
                }
                return;
            }
            if (!playing || Cursor.lockState != CursorLockMode.Locked) { swayX = Mathf.Lerp(swayX, 0f, dt * 6f); swayY = Mathf.Lerp(swayY, 0f, dt * 6f); return; }
            float mx = Input.GetAxisRaw("Mouse X") * Settings.Sensitivity, my = Input.GetAxisRaw("Mouse Y") * Settings.Sensitivity * (Settings.InvertY ? -1f : 1f);
            yaw += mx; pitch = Mathf.Clamp(pitch - my, -82f, 82f);
            swayX = Mathf.Lerp(swayX, -mx * 0.6f, dt * 8f); swayY = Mathf.Lerp(swayY, my * 0.6f, dt * 8f);
        }

        void UpdateFlashRig(float dt)
        {
            Transform c = cam.transform;
            flashRig.position = c.position;
            flashRig.rotation = Quaternion.Slerp(flashRig.rotation, c.rotation, 1f - Mathf.Exp(-dt * 14f));
            // sinar diarahkan ke titik bidik (titik terang tepat di tengah layar), tetap sedikit tertinggal saat menoleh
            float aim = 18f;
            if (Physics.Raycast(c.position, flashRig.forward, out RaycastHit ah, 30f, ~(1 << 2), QueryTriggerInteraction.Ignore)) aim = Mathf.Max(2f, ah.distance);
            aimDist = Mathf.Lerp(aimDist, aim, 1f - Mathf.Exp(-dt * 10f));
            Vector3 aimPt = c.position + flashRig.forward * aimDist;
            flash.transform.rotation = Quaternion.LookRotation(aimPt - flash.transform.position, flashRig.up);
            // dua fase: senter menunduk dulu (masih terlihat), lalu tangan turun keluar layar; naik = kebalikannya
            float r = raise * raise * (3f - 2f * raise), low = 1f - r;
            float tiltT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(low / 0.55f));
            float dropT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((low - 0.2f) / 0.8f));
            Vector3 held = new Vector3(0.23f + swayX * 0.004f, -0.25f + swayY * 0.004f + Mathf.Sin(bob) * 0.006f, 0.38f) + new Vector3(0f, 0.012f, 0.03f) * focusT;
            // tangan turun di sisi badan: tepat di bawah tepi layar (tetap tak terlihat hingga FOV 95)
            Vector3 down = new Vector3(0.25f, -0.44f, 0.30f) + new Vector3(0f, Mathf.Sin(bob) * 0.008f, 0f);
            viewmodel.localPosition = Vector3.Lerp(held, down, dropT) + new Vector3(0.015f, -0.015f, 0f) * Mathf.Sin(dropT * Mathf.PI); // sedikit melengkung
            Quaternion heldRot = Quaternion.Inverse(c.rotation) * flashRig.rotation * Quaternion.Euler(4f + swayY * 0.4f, -9f + swayX * 0.4f, -6f);
            viewmodel.localRotation = Quaternion.Slerp(heldRot, Quaternion.Euler(50f, -12f, -12f), tiltT);              // senter menunjuk ke lantai
        }

        void UpdateAudio(float dt, bool walking)
        {
            if (steps != null && steps.clip != null)
            {
                float target = walking ? (crouch ? 0.12f : running ? 0.85f : 0.45f) : 0f;
                steps.volume = Mathf.MoveTowards(steps.volume, target * Settings.Sfx, dt * 3f);
                steps.pitch = running ? 1.35f : crouch ? 0.75f : 1f;
            }
            if (breath != null)
            {
                float fear = GameManager.I != null && GameManager.I.ghost != null ? Mathf.Clamp01(1f - GameManager.I.ghost.DistanceTo(transform.position) / 10f) : 0f;
                float target = Mathf.Clamp01((1f - stamina / 100f) * 1.2f + fear * 0.6f) * (hidden ? 0.6f : 1f) * 0.55f;
                breath.volume = Mathf.MoveTowards(breath.volume, target * Settings.Sfx, dt * 0.8f);
                breath.pitch = 0.9f + (1f - stamina / 100f) * 0.35f;
            }
        }

        public void ToggleFlash()
        {
            if (battery <= 0f) { flashOn = false; return; }
            flashOn = !flashOn;
            AudioDirector.I.Click();
        }

        // ------------------------------------------------------------------ bersembunyi
        public void EnterHide(HidingSpot s)
        {
            if (hidden || transitioning || s == null || s.occupied) return;
            LearningProbe.OnHide(GameManager.I.layout, GameManager.I.house, s);    // prediksi model dihitung sebelum kebiasaan ini dicatat
            spot = s; s.occupied = true; hidden = true; hideTime = 0f;
            s.usesThisHouse++;
            GameManager.I.ghost.OnPlayerHid(s);
            GameManager.I.dda.OnHide();
            AudioDirector.I.Creak();
            cc.enabled = false;
            Vector3 e = s.insideRot.eulerAngles;
            hideYaw = e.y; hidePitch = s.kind == SpotKind.KolongRanjang ? 4f : 0f;
            StartCoroutine(MoveCam(s.insidePos, Quaternion.Euler(hidePitch, hideYaw, 0f), 0.45f, () => { yaw = hideYaw; pitch = hidePitch; }));
        }

        public void ExitHide()
        {
            if (!hidden || transitioning) return;
            HidingSpot s = spot;
            if (s != null) GhostMemory.RecordHide(s);   // kebiasaan dicatat setelah keluar (hantu tidak "tahu" lokasi saat ini)
            Telemetry.Event("keluar_sembunyi", s != null ? s.KindName : "");
            Vector3 outPos = s != null ? s.exitPos : transform.position;
            Vector3 look = s != null ? s.insideRot * Vector3.forward : transform.forward;
            float outYaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
            AudioDirector.I.Creak();
            StartCoroutine(MoveCam(outPos + Vector3.up * 1.62f, Quaternion.Euler(0f, outYaw, 0f), 0.4f, () =>
            {
                if (s != null) s.occupied = false;
                hidden = false; spot = null;
                transform.position = outPos; yaw = outYaw; pitch = 0f;
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                pivot.localPosition = new Vector3(0f, 1.62f, 0f); pivot.localRotation = Quaternion.identity;
                cc.enabled = true;
            }));
        }

        /// <summary>Paksa keluar tanpa animasi (dipakai saat tertangkap / rumah baru).</summary>
        public void ForceUnhide()
        {
            StopAllCoroutines(); transitioning = false;
            if (spot != null) spot.occupied = false;
            hidden = false; spot = null;
            if (cc != null) cc.enabled = true;
        }

        IEnumerator MoveCam(Vector3 worldPos, Quaternion worldRot, float dur, System.Action done)
        {
            transitioning = true;
            Vector3 p0 = pivot.position; Quaternion r0 = pivot.rotation;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, t / dur);
                pivot.position = Vector3.Lerp(p0, worldPos, u);
                pivot.rotation = Quaternion.Slerp(r0, worldRot, u);
                yield return null;
            }
            pivot.position = worldPos; pivot.rotation = worldRot;
            transitioning = false;
            done?.Invoke();
        }

        /// <summary>Paksa kamera menatap titik (adegan tertangkap).</summary>
        public void LookAtPoint(Vector3 p, float speed)
        {
            Vector3 d = p - cam.transform.position;
            Quaternion want = Quaternion.LookRotation(d);
            pivot.rotation = Quaternion.Slerp(pivot.rotation, want, 1f - Mathf.Exp(-Time.unscaledDeltaTime * speed));
        }
    }
}
