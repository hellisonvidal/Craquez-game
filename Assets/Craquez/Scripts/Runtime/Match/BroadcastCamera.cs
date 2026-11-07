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
        public static readonly string[] Modes = { "TV", "TV Próxima", "Alta (tática)", "Atrás do Jogador", "Dinâmica", "Beira do Campo", "Atrás do Gol", "Drone", "Isométrica", "Transmissão Aberta" };
        public static readonly string[] Zooms = { "Bem aberto", "Aberto", "Normal", "Perto", "Bem perto" };
        /// <summary>Zoom: -2 (aberto) .. +2 (perto).</summary>
        public int zoom;
        /// <summary>Time humano seguido pela câmera "Atrás do Jogador".</summary>
        public int followTeam;
        Vector3 _attackDir = Vector3.right;
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
            mode = Mathf.Clamp(m, 0, Modes.Length - 1);
            zoom = Settings.CameraZoom;
            _focus = Vector3.zero;
            // faixa de profundidade justa: menos "briga" de superfícies (pontos piscando) ao longe
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 650f;
        }

        public void Shake(float amount) => _shake = Mathf.Max(_shake, amount);

        float _sceneT;
        bool _sceneWasCine;

        /// <summary>Plano curto (cobrança de falta): atrás da bola, olhando para o gol; depois volta ao normal.</summary>
        public void Scene(Vector3 pos, Vector3 look, float seconds, float fov = 34f)
        {
            if (_sceneT <= 0f) _sceneWasCine = cinematic;
            _sceneT = seconds;
            cinematic = true;
            cinePos = pos;
            cineLook = look;
            cineFov = fov;
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation(look - pos);
            cam.fieldOfView = fov;
        }

        // ---------------------------------------------------------------- paredes na frente
        readonly System.Collections.Generic.List<Renderer> _occ = new System.Collections.Generic.List<Renderer>();
        readonly System.Collections.Generic.List<UnityEngine.Rendering.ShadowCastingMode> _occMode = new System.Collections.Generic.List<UnityEngine.Rendering.ShadowCastingMode>();
        readonly System.Collections.Generic.List<bool> _occHidden = new System.Collections.Generic.List<bool>();

        /// <summary>
        /// Estruturas grandes do estádio/ginásio (paredes, arquibancadas, cobertura) que podem
        /// ficar entre a câmera e o jogo: somem enquanto taparem a visão (a sombra continua).
        /// </summary>
        public void SetOccluders(Transform venueRoot)
        {
            _occ.Clear(); _occMode.Clear(); _occHidden.Clear();
            if (venueRoot == null) return;
            foreach (var r in venueRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                var b = r.bounds;
                bool floor = b.size.y < 0.6f && b.max.y < 0.8f;
                if (floor || Mathf.Max(b.size.x, b.size.y, b.size.z) < 1.2f) continue;
                // gols, redes e o que fica dentro do campo nunca somem
                if (sim != null && Mathf.Abs(b.center.x) < sim.halfLen + 3.5f && Mathf.Abs(b.center.z) < sim.halfW + 1.5f && b.max.y < 6f) continue;
                _occ.Add(r);
                _occMode.Add(r.shadowCastingMode);
                _occHidden.Add(false);
            }
        }

        readonly Vector3[] _targets = new Vector3[7];

        void UpdateOccluders(Vector3 focus)
        {
            if (_occ.Count == 0) return;
            var c = cam.transform.position;
            float hw = sim.halfW;
            // alvos: o foco, um pouco antes/depois dele e a linha lateral mais próxima
            var targets = _targets;
            targets[0] = focus + Vector3.up * 0.8f; targets[1] = focus + new Vector3(7f, 0.8f, 0f); targets[2] = focus + new Vector3(-7f, 0.8f, 0f);
            targets[3] = new Vector3(focus.x, 0.5f, Mathf.Sign(c.z) * hw * 0.95f);
            // cantos do campo do lado da câmera e o centro: nada pode tapar a área de jogo
            float hl = sim.halfLen;
            targets[4] = new Vector3(Mathf.Clamp(focus.x - 15f, -hl, hl), 0.5f, Mathf.Sign(c.z) * hw);
            targets[5] = new Vector3(Mathf.Clamp(focus.x + 15f, -hl, hl), 0.5f, Mathf.Sign(c.z) * hw);
            targets[6] = new Vector3(focus.x, 1.5f, 0f);
            for (int i = 0; i < _occ.Count; i++)
            {
                var r = _occ[i];
                if (r == null) continue;
                var b = r.bounds;
                bool block = false;
                foreach (var t in targets)
                {
                    var d = t - c;
                    float len = d.magnitude;
                    if (len < 0.01f) continue;
                    if (b.Contains(c) || (b.IntersectRay(new Ray(c, d / len), out float hit) && hit < len - 0.5f)) { block = true; break; }
                }
                // colado na câmera (poste, mureta, alambrado) também some
                if (!block && b.SqrDistance(c) < 16f) block = true;
                if (block == _occHidden[i]) continue;
                _occHidden[i] = block;
                r.shadowCastingMode = block ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : _occMode[i];
            }
        }

        void LateUpdate()
        {
            if (sim == null || cam == null) return;
            if (_sceneT > 0f)
            {
                _sceneT -= Time.deltaTime;
                if (_sceneT <= 0f) { cinematic = _sceneWasCine; SnapToBall(); }
            }
            Move();
            UpdateOccluders(cinematic ? cineLook : _focus);
        }

        void Move()
        {
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
            float zk = 1f - zoom * 0.14f;   // zoom afasta/aproxima mantendo o ângulo
            if (mode == 3 || mode == 4)
            {
                // câmeras "por trás": seguem a direção do ataque (jogador controlado ou time com a bola)
                Vector3 anchor;
                float dir;
                if (mode == 3)
                {
                    var pl = sim.Controlled(followTeam);
                    anchor = new Vector3(pl.position.x, 0, pl.position.y);
                    dir = sim.TeamDir(followTeam);
                }
                else
                {
                    int t = sim.ball.owner >= 0 ? sim.players[sim.ball.owner].team : (sim.ball.lastTouchTeam >= 0 ? sim.ball.lastTouchTeam : 0);
                    anchor = ball;
                    dir = sim.TeamDir(t);
                }
                _attackDir = Vector3.Slerp(_attackDir, new Vector3(dir, 0, 0), 1f - Mathf.Exp(-dt * 1.2f));
                float bk = (field ? (mode == 3 ? 10f : 20f) : (mode == 3 ? 7f : 11f)) * zk;
                float up = (field ? (mode == 3 ? 4.2f : 9f) : (mode == 3 ? 3.2f : 6f)) * zk;
                var focus = Vector3.Lerp(anchor, ball, mode == 3 ? 0.35f : 1f);
                _focus = Vector3.SmoothDamp(_focus, focus, ref _vel, 0.25f, 80f, dt);
                var p3 = _focus - _attackDir * bk + Vector3.up * up;
                if (_shake > 0f) { _shake = Mathf.MoveTowards(_shake, 0f, dt * 2.5f); p3 += Random.insideUnitSphere * _shake * 0.2f; }
                cam.transform.position = Vector3.Lerp(cam.transform.position, p3, 1f - Mathf.Exp(-dt * 6f));
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(_focus + _attackDir * (field ? 8f : 4f) + Vector3.up * 0.5f - cam.transform.position), 1f - Mathf.Exp(-dt * 6f));
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 52f, 1f - Mathf.Exp(-dt * 2f));
                return;
            }
            if (mode >= 6 && mode <= 8)
            {
                Vector3 p6, look6;
                float fov6;
                if (mode == 6)
                {
                    // atrás do gol que o time seguido defende, olhando o campo no sentido do ataque
                    float dir = sim.TeamDir(followTeam);
                    float behind = (field ? 16f : 8f) * zk;
                    p6 = new Vector3(-dir * (hl + behind) + _focus.x * 0.35f, (field ? 11f : 6.5f) * zk, _focus.z * 0.6f);
                    look6 = new Vector3(_focus.x, 0.5f, _focus.z);
                    fov6 = field ? 46f : 50f;
                }
                else if (mode == 7)
                {
                    // drone: quase de cima, um pouco atrás, acompanha a bola de perto
                    p6 = _focus + new Vector3(0f, (field ? 30f : 15f) * zk, -(field ? 9f : 5f) * zk);
                    look6 = _focus;
                    fov6 = 42f;
                }
                else
                {
                    // isométrica: diagonal de um canto, estilo jogo de estratégia
                    float dd = (field ? 34f : 17f) * zk;
                    p6 = _focus + new Vector3(-dd * 0.7f, dd * 0.8f, -dd * 0.7f);
                    look6 = _focus;
                    fov6 = 30f;
                }
                if (_shake > 0f) { _shake = Mathf.MoveTowards(_shake, 0f, dt * 2.5f); p6 += Random.insideUnitSphere * _shake * 0.25f; }
                cam.transform.position = Vector3.Lerp(cam.transform.position, p6, 1f - Mathf.Exp(-dt * 5f));
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(look6 - cam.transform.position), 1f - Mathf.Exp(-dt * 6f));
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov6, 1f - Mathf.Exp(-dt * 2f));
                return;
            }
            float height, back, fov;
            switch (mode)
            {
                case 1: height = field ? 11f : 7f; back = field ? 28f : 15f; fov = 34f; break;
                case 2: height = field ? 42f : 20f; back = field ? 36f : 18f; fov = 38f; break;
                case 5: height = field ? 3.2f : 2.4f; back = field ? 14f : 6f; fov = 44f; break;
                case 9: height = field ? 30f : 14f; back = field ? 58f : 24f; fov = 34f; break;   // transmissão aberta: o campo todo
                default: height = field ? 18f : 10f; back = field ? 38f : 16f; fov = field ? 31f : 38f; break;
            }
            height *= zk;
            back *= zk;
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
