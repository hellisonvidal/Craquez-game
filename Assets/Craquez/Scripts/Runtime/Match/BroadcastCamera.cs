using Craquez.Sim;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Câmera de transmissão (estilo TV): fica na lateral, acompanha a bola com
    /// antecipação e suavização, aproxima em lances perto da área. Modos: TV,
    /// TV próxima, alta (tática) e cinema (cerimônia / replays).
    /// </summary>
    public class BroadcastCamera : MonoBehaviour
    {
        public static readonly string[] Modes = { "TV", "TV Próxima", "Alta" };
        public Camera cam;
        public MatchSim sim;
        public int mode;
        Vector3 _focus, _vel;
        float _dist, _shake;
        Vector3 _lead;
        public bool cinematic;
        public Vector3 cinePos, cineLook;
        public float cineFov = 40f;

        public void Setup(Camera c, MatchSim s, int m)
        {
            cam = c;
            sim = s;
            mode = m;
            _focus = Vector3.zero;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 900f;
        }

        public void Shake(float amount) => _shake = Mathf.Max(_shake, amount);

        void LateUpdate()
        {
            if (sim == null || cam == null) return;
            float dt = Time.deltaTime;
            if (cinematic)
            {
                cam.transform.position = Vector3.Lerp(cam.transform.position, cinePos, 1f - Mathf.Exp(-dt * 2.5f));
                var q = Quaternion.LookRotation(cineLook - cam.transform.position);
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, q, 1f - Mathf.Exp(-dt * 3f));
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, cineFov, 1f - Mathf.Exp(-dt * 2f));
                return;
            }
            float hl = sim.halfLen, hw = sim.halfW;
            bool field = sim.mode.id == GameModes.Field;
            var ball = new Vector3(sim.ball.pos.x, 0, sim.ball.pos.y);
            // antecipa para onde a bola/portador está indo
            var vel = new Vector3(sim.ball.vel.x, 0, sim.ball.vel.y);
            _lead = Vector3.Lerp(_lead, Vector3.ClampMagnitude(vel * 0.35f, field ? 8f : 4f), 1f - Mathf.Exp(-dt * 2f));
            var target = ball + _lead;
            target.x = Mathf.Clamp(target.x, -hl * 0.88f, hl * 0.88f);
            target.z = Mathf.Clamp(target.z * 0.55f, -hw * 0.5f, hw * 0.5f);
            _focus = Vector3.SmoothDamp(_focus, target, ref _vel, 0.35f, 60f, dt);
            // perto do gol: aproxima um pouco
            float nearGoal = Mathf.Clamp01((Mathf.Abs(_focus.x) - hl * 0.55f) / (hl * 0.35f));
            float height, back, fov;
            switch (mode)
            {
                case 1: height = field ? 11f : 7f; back = field ? 28f : 15f; fov = 34f; break;
                case 2: height = field ? 42f : 20f; back = field ? 36f : 18f; fov = 38f; break;
                default: height = field ? 17f : 9f; back = field ? 40f : 20f; fov = 31f; break;
            }
            fov -= nearGoal * 4f;
            _dist = Mathf.Lerp(_dist <= 0 ? back : _dist, back * (1f - nearGoal * 0.1f), 1f - Mathf.Exp(-dt * 1.5f));
            var pos = new Vector3(_focus.x * 0.82f, height, -(hw + _dist) + _focus.z * 0.25f);
            if (_shake > 0f)
            {
                _shake = Mathf.MoveTowards(_shake, 0f, dt * 2.5f);
                pos += Random.insideUnitSphere * _shake * 0.25f;
            }
            cam.transform.position = pos;
            var look = _focus + Vector3.up * 0.6f;
            cam.transform.rotation = Quaternion.LookRotation(look - pos);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-dt * 2f));
        }

        public void SnapToBall()
        {
            _focus = new Vector3(sim.ball.pos.x, 0, sim.ball.pos.y);
            _vel = Vector3.zero;
            _lead = Vector3.zero;
        }
    }
}
