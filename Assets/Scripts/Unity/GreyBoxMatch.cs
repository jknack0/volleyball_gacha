using UnityEngine;
using VG.Data;
using VG.Gameplay.Ball;
using VG.Gameplay.Match;
using VG.Gameplay.Rally;

namespace VG.Unity
{
    /// <summary>
    /// VB-12 grey-box: watches the tick-driven MatchSim play AI-vs-AI in 3D. Self-bootstrapping —
    /// builds the court/capsules/ball from primitives at runtime (m0 spec §8.1 Court_GreyBox),
    /// so ANY empty scene + Play works with zero editor setup.
    ///
    /// Sim discipline [structural, m0-hardening §1]: sim advances ONLY in FixedUpdate at 1/60 s
    /// (Time.fixedDeltaTime pinned in Awake); rendering interpolates between the last two tick
    /// positions. Render fps never changes sim tick count.
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
        [SerializeField] private DifficultyTier _homeTier = DifficultyTier.Normal;
        [SerializeField] private DifficultyTier _awayTier = DifficultyTier.Normal;

        private MatchSim _sim;
        private Transform _ballView;
        private Vector3 _ballPrev, _ballCur;
        private float _restartAt = -1f;
        private string _lastRallyLine = "";

        private void Awake()
        {
            Time.fixedDeltaTime = 1f / 60f; // [structural] sim tick — never scaled, never changed
            BuildCourt();
            EnsureCameraAndLight();
            StartMatch();
        }

        private void StartMatch()
        {
            _sim = new MatchSim(new MatchConfig(
                TeamSpec.Uniform("H", 100, _homeTier),
                TeamSpec.Uniform("A", 100, _awayTier),
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
            _sim.Tick(); // exactly one sim step per fixed step [structural]
            _ballPrev = _ballCur;
            _ballCur = ToWorld(_sim.BallPosition);
        }

        private void Update()
        {
            // Interpolate render between the last two sim ticks (render fps independent of sim).
            float alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            _ballView.position = Vector3.Lerp(_ballPrev, _ballCur, alpha);
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(12, 8, 900, 24),
                $"HOME {_sim.HomeScore} — {_sim.AwayScore} AWAY    {_sim.CurrentState}    serve: {_sim.ServingSide}");
            GUI.Label(new Rect(12, 30, 900, 24),
                $"Hype H {_sim.Hype.Hype(TeamSide.Home)}{(_sim.Hype.IsIgnited(TeamSide.Home) ? " IGNITED" : "")} · " +
                $"A {_sim.Hype.Hype(TeamSide.Away)}{(_sim.Hype.IsIgnited(TeamSide.Away) ? " IGNITED" : "")}");
            GUI.Label(new Rect(12, 52, 900, 24), _lastRallyLine);
            if (_sim.Done)
                GUI.Label(new Rect(12, 74, 900, 24), $"FINAL — {(_sim.Result.HomeWon ? "HOME" : "AWAY")} wins. Restarting…");
        }

        // ---- grey-box construction (m0 spec §8.1) --------------------------------------------------

        private static Vector3 ToWorld(Vec3 v) => new Vector3(v.X, v.Y, v.Z);

        private void BuildCourt()
        {
            // Floor: 18×9 court plane (CourtGeometry frame: x 0..9 along net, z −9..+9).
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Court";
            floor.transform.position = new Vector3(4.5f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(9f, 0.1f, 18f);
            Tint(floor, new Color(0.85f, 0.72f, 0.5f)); // gym floor

            // Out-of-bounds apron for orientation.
            var apron = GameObject.CreatePrimitive(PrimitiveType.Cube);
            apron.name = "Apron";
            apron.transform.position = new Vector3(4.5f, -0.06f, 0f);
            apron.transform.localScale = new Vector3(13f, 0.1f, 24f);
            Tint(apron, new Color(0.35f, 0.45f, 0.55f));

            // Net: thin quad along x at z = 0, top at NetHeight.
            var net = GameObject.CreatePrimitive(PrimitiveType.Cube);
            net.name = "Net";
            net.transform.position = new Vector3(4.5f, CourtGeometry.NetHeight - 0.5f, 0f);
            net.transform.localScale = new Vector3(9f, 1f, 0.02f);
            Tint(net, new Color(0.15f, 0.15f, 0.18f));

            // Antenna posts at x = 0 and x = 9.
            for (int i = 0; i < 2; i++)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = i == 0 ? "AntennaL" : "AntennaR";
                post.transform.position = new Vector3(i * 9f, 1.4f, 0f);
                post.transform.localScale = new Vector3(0.06f, 1.4f, 0.06f);
                Tint(post, Color.red);
            }

            // Capsules: 6 per side at court slots, team-tinted (m0 §8.1).
            foreach (TeamSide side in new[] { TeamSide.Home, TeamSide.Away })
            {
                for (int pos = 1; pos <= 6; pos++)
                {
                    var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    cap.name = $"{side}_{pos}";
                    Vec3 p = CourtSlots.Position(side, pos);
                    cap.transform.position = new Vector3(p.X, 1f, p.Z);
                    Tint(cap, side == TeamSide.Home ? new Color(0.2f, 0.45f, 0.9f) : new Color(0.9f, 0.35f, 0.25f));
                }
            }

            // Ball.
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Ball";
            ball.transform.localScale = Vector3.one * 0.22f;
            Tint(ball, new Color(1f, 0.9f, 0.2f));
            _ballView = ball.transform;
        }

        private void EnsureCameraAndLight()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            // Behind-baseline default framing (m0 §5 C1), Home side, slight high angle.
            cam.transform.position = new Vector3(4.5f, 7.5f, -16.5f);
            cam.transform.rotation = Quaternion.Euler(24f, 0f, 0f);
            cam.fieldOfView = 55f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.16f);

            if (FindAnyObjectByType<Light>() == null)
            {
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
                light.intensity = 1.1f;
            }
        }

        private static void Tint(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r != null) r.material.color = c;
        }
    }
}
