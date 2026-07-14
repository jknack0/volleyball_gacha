using UnityEngine;
using UnityEngine.InputSystem;
using VG.Data;
using VG.Gameplay.Ball;
using VG.Gameplay.Match;
using VG.Gameplay.Rally;

namespace VG.Unity
{
    /// <summary>
    /// VB-12 grey-box with the anime pass: cel-shaded (VG/Toon) + ink outlines (VG/Outline),
    /// 2XKO-inspired read — saturated characters against a calmer stage, hue-shifted shadows,
    /// hard rim. Self-bootstrapping: press Play in ANY empty scene.
    ///
    /// Camera presets (keyboard 1/2/3): 1 = net-side low (default — "at the net"),
    /// 2 = behind-baseline high, 3 = far-corner net cam. Team size defaults to 3v3
    /// (PLAN §2.4 fallback, promoted to the grey-box default by feel feedback 2026-07-13).
    ///
    /// Sim discipline [structural]: sim advances ONLY in FixedUpdate at 1/60 s; rendering
    /// interpolates between the last two tick positions.
    /// </summary>
    public sealed class GreyBoxMatch : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindAnyObjectByType<GreyBoxMatch>() == null)
                new GameObject("GreyBoxMatch").AddComponent<GreyBoxMatch>();
        }

        [SerializeField] private ulong _seed = 42;
        [SerializeField] private int _teamSize = 3;
        [SerializeField] private DifficultyTier _homeTier = DifficultyTier.Normal;
        [SerializeField] private DifficultyTier _awayTier = DifficultyTier.Normal;

        // 2XKO-inspired palette [tunable]: stage calm, characters loud.
        private static readonly Color FloorColor = new Color(0.82f, 0.70f, 0.55f);
        private static readonly Color ApronColor = new Color(0.20f, 0.24f, 0.34f);
        private static readonly Color BackgroundColor = new Color(0.16f, 0.15f, 0.24f);
        private static readonly Color NetColor = new Color(0.12f, 0.10f, 0.14f);
        private static readonly Color HomeColor = new Color(0.15f, 0.50f, 1.00f);
        private static readonly Color AwayColor = new Color(1.00f, 0.30f, 0.25f);
        private static readonly Color BallColor = new Color(1.00f, 0.85f, 0.10f);

        private MatchSim _sim;
        private Transform _ballView;
        private Vector3 _ballPrev, _ballCur;
        private float _restartAt = -1f;
        private string _lastRallyLine = "";

        private Camera _cam;
        private int _cameraPreset = 0; // index into presets; 0 = net-side (default)
        private static readonly (Vector3 pos, Vector3 lookAt, string name)[] CameraPresets =
        {
            (new Vector3(-5.8f, 2.4f, -4.6f), new Vector3(4.5f, 1.6f, 0.6f), "net-side"),
            (new Vector3(4.5f, 7.5f, -16.5f), new Vector3(4.5f, 1.0f, 0.0f), "baseline"),
            (new Vector3(11.5f, 3.4f, 5.2f), new Vector3(4.5f, 1.4f, -0.5f), "far corner"),
        };

        private Material _toonShared;   // stage pieces (no outline)
        private Shader _toonShader;
        private Shader _outlineShader;

        private void Awake()
        {
            Time.fixedDeltaTime = 1f / 60f; // [structural] sim tick

            _toonShader = Shader.Find("VG/Toon");
            _outlineShader = Shader.Find("VG/Outline");

            BuildCourt();
            EnsureCameraAndLight();
            ApplyCameraPreset(0);
            StartMatch();
        }

        private void StartMatch()
        {
            _sim = new MatchSim(new MatchConfig(
                TeamSpec.Uniform("H", 100, _homeTier, teamSize: _teamSize),
                TeamSpec.Uniform("A", 100, _awayTier, teamSize: _teamSize),
                MatchFormat.To11, _seed));
            _sim.OnRallyEnded += rally => _lastRallyLine =
                $"{rally.Contacts} contacts · {rally.Outcome} → {(rally.WonByHome ? "HOME" : "AWAY")}";
            _ballPrev = _ballCur = ToWorld(_sim.BallPosition);
        }

        private void FixedUpdate()
        {
            if (_sim.Done)
            {
                if (_restartAt < 0f) _restartAt = Time.time + 3f;
                if (Time.time >= _restartAt) { _seed++; _restartAt = -1f; StartMatch(); }
                return;
            }
            _sim.Tick();
            _ballPrev = _ballCur;
            _ballCur = ToWorld(_sim.BallPosition);
        }

        private void Update()
        {
            float alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            _ballView.position = Vector3.Lerp(_ballPrev, _ballCur, alpha);

            // One-shot floor re-snap: the spawn-time snap measured the BIND pose; a few frames in,
            // the Animator has posed the character (idle) and skinned bounds are trustworthy.
            if (_charToSnap != null && --_charSnapCountdown <= 0)
            {
                var b = new Bounds(_charToSnap.position, Vector3.zero);
                foreach (var r in _charToSnap.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                _charToSnap.position += Vector3.down * b.min.y;
                _charToSnap = null;
            }

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) ApplyCameraPreset(0);
                if (kb.digit2Key.wasPressedThisFrame) ApplyCameraPreset(1);
                if (kb.digit3Key.wasPressedThisFrame) ApplyCameraPreset(2);
            }
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(12, 8, 900, 24),
                $"HOME {_sim.HomeScore} — {_sim.AwayScore} AWAY    {_sim.CurrentState}    serve: {_sim.ServingSide}    ({_teamSize}v{_teamSize})");
            GUI.Label(new Rect(12, 30, 900, 24),
                $"Hype H {_sim.Hype.Hype(TeamSide.Home)}{(_sim.Hype.IsIgnited(TeamSide.Home) ? " IGNITED" : "")} · " +
                $"A {_sim.Hype.Hype(TeamSide.Away)}{(_sim.Hype.IsIgnited(TeamSide.Away) ? " IGNITED" : "")}");
            GUI.Label(new Rect(12, 52, 900, 24), _lastRallyLine);
            GUI.Label(new Rect(12, 74, 900, 24),
                $"cam [{CameraPresets[_cameraPreset].name}] — keys 1/2/3");
            if (_sim.Done)
                GUI.Label(new Rect(12, 96, 900, 24), $"FINAL — {(_sim.Result.HomeWon ? "HOME" : "AWAY")} wins. Restarting…");
        }

        // ---- construction ------------------------------------------------------------------------

        private static Vector3 ToWorld(Vec3 v) => new Vector3(v.X, v.Y, v.Z);

        private void BuildCourt()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Court";
            floor.transform.position = new Vector3(4.5f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(9f, 0.1f, 18f);
            ApplyToon(floor, FloorColor, outline: false);

            var apron = GameObject.CreatePrimitive(PrimitiveType.Cube);
            apron.name = "Apron";
            apron.transform.position = new Vector3(4.5f, -0.07f, 0f);
            apron.transform.localScale = new Vector3(14f, 0.1f, 25f);
            ApplyToon(apron, ApronColor, outline: false);

            var net = GameObject.CreatePrimitive(PrimitiveType.Cube);
            net.name = "Net";
            net.transform.position = new Vector3(4.5f, CourtGeometry.NetHeight - 0.5f, 0f);
            net.transform.localScale = new Vector3(9f, 1f, 0.02f);
            ApplyToon(net, NetColor, outline: false);

            for (int i = 0; i < 2; i++)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = i == 0 ? "AntennaL" : "AntennaR";
                post.transform.position = new Vector3(i * 9f, 1.4f, 0f);
                post.transform.localScale = new Vector3(0.06f, 1.4f, 0.06f);
                ApplyToon(post, AwayColor, outline: false);
            }

            foreach (TeamSide side in new[] { TeamSide.Home, TeamSide.Away })
            {
                for (int pos = 1; pos <= _teamSize; pos++)
                {
                    Vec3 p = CourtSlots.Position(side, pos, _teamSize);

                    // Character prefabs (built by VG/Build Character Prefabs → Resources/VGCharacters)
                    // replace capsules where they exist. v0 casting: the MC plays Home setter (pos 2).
                    if (side == TeamSide.Home && pos == 2 && TrySpawnCharacter("char.mc", p))
                        continue;

                    var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    cap.name = $"{side}_{pos}";
                    cap.transform.position = new Vector3(p.X, 1f, p.Z);
                    ApplyToon(cap, side == TeamSide.Home ? HomeColor : AwayColor, outline: true);
                }
            }

            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Ball";
            ball.transform.localScale = Vector3.one * 0.24f;
            ApplyToon(ball, BallColor, outline: true);
            _ballView = ball.transform;

            // Anime motion read: bright trail on the ball [tunable].
            var trail = ball.AddComponent<TrailRenderer>();
            trail.time = 0.32f;
            trail.startWidth = 0.16f;
            trail.endWidth = 0.0f;
            trail.numCapVertices = 4;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.5f), 0f), new GradientColorKey(BallColor, 1f) },
                new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = grad;
        }

        /// <summary>Spawn a built character at a slot, normalized to human height, facing the net.</summary>
        private bool TrySpawnCharacter(string charId, Vec3 slot)
        {
            var prefab = Resources.Load<GameObject>($"VGCharacters/{charId}");
            if (prefab == null) return false;

            var go = Instantiate(prefab);
            go.name = charId;

            // Accurate per-pose skinned bounds (default local bounds are bind-pose approximations).
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                smr.updateWhenOffscreen = true;

            // Normalize to ~1.75 m regardless of the generator's export scale [tunable].
            var bounds = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            float height = Mathf.Max(bounds.size.y, 1e-4f);
            go.transform.localScale *= 1.75f / height;

            // Feet on the floor: re-measure after scaling and lift by the bounds' bottom offset.
            var scaled = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) scaled.Encapsulate(r.bounds);
            float lift = go.transform.position.y - scaled.min.y;

            go.transform.position = new Vector3(slot.X, lift, slot.Z);
            go.transform.rotation = Quaternion.LookRotation(slot.Z < 0f ? Vector3.forward : Vector3.back);
            _charToSnap = go.transform;
            _charSnapCountdown = 3; // frames until the Animator has evaluated the idle pose [tunable]
            return true;
        }

        private Transform _charToSnap;
        private int _charSnapCountdown;

        private void ApplyToon(GameObject go, Color baseColor, bool outline)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;

            if (_toonShader == null) // shaders missing (stripped build) — fall back to tinted default
            {
                r.material.color = baseColor;
                return;
            }

            var toon = new Material(_toonShader);
            toon.SetColor("_BaseColor", baseColor);

            if (outline && _outlineShader != null)
            {
                var ink = new Material(_outlineShader);
                r.materials = new[] { toon, ink }; // inverted hull renders the same mesh again
            }
            else
            {
                r.material = toon;
            }
        }

        private void EnsureCameraAndLight()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                _cam = go.AddComponent<Camera>();
            }
            _cam.fieldOfView = 55f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = BackgroundColor;

            if (FindAnyObjectByType<Light>() == null)
            {
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
                light.color = new Color(1f, 0.97f, 0.9f);
                light.intensity = 1.15f;
            }
        }

        private void ApplyCameraPreset(int index)
        {
            _cameraPreset = index;
            var (pos, lookAt, _) = CameraPresets[index];
            _cam.transform.position = pos;
            _cam.transform.rotation = Quaternion.LookRotation((lookAt - pos).normalized, Vector3.up);
        }
    }
}
