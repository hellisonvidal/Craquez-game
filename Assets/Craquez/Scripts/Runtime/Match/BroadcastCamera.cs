using Craquez.Sim;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Câmera da partida. Três famílias:
    /// transmissão (TV, TV próxima, aberta, tática) na lateral, acompanhando a bola com antecipação;
    /// próximas ao jogador (perto do jogador, atrás do jogador, ombro, rente ao gramado), que
    /// seguem o atleta controlado; e especiais (dinâmica, atrás do gol, drone).
    /// </summary>
    public class BroadcastCamera : MonoBehaviour
    {
        public enum Kind { Tv, TvClose, Wide, Tactical, PlayerSide, Behind, Shoulder, Pitchside, Dynamic, BehindGoal, Drone, WideClose, Close, Low }
        public static readonly string[] Modes =
        {
            "TV", "TV Próxima", "Transmissão Aberta", "Alta (tática)",
            "Perto do Jogador", "Atrás do Jogador", "Ombro (bem perto)", "Rente ao Gramado",
            "Dinâmica", "Atrás do Gol", "Drone", "Aberta Aproximada", "TV Bem Perto", "Rente ao Lance",
        };
        /// <summary>Índices antigos (versão 2.0) -> novos, para manter a escolha salva.</summary>
        public static int FromOld(int old) => old switch { 0 => 0, 1 => 1, 2 => 3, 3 => 5, 4 => 8, 5 => 7, 6 => 9, 7 => 10, 9 => 2, _ => 0 };
        /// <summary>Câmeras oferecidas nos menus (drone, atrás do gol e ombro foram retiradas).</summary>
        public static readonly Kind[] Offered =
        {
            Kind.Tv, Kind.TvClose, Kind.Close, Kind.WideClose, Kind.Wide, Kind.Tactical,
            Kind.PlayerSide, Kind.Low, Kind.Shoulder, Kind.Behind, Kind.Pitchside, Kind.Dynamic,
        };
        public static string[] OfferedNames
        {
            get
            {
                var n = new string[Offered.Length];
                for (int i = 0; i < n.Length; i++) n[i] = Modes[(int)Offered[i]];
                return n;
            }
        }
        /// <summary>Modo salvo -> posição na lista dos menus (modo retirado vira TV).</summary>
        public static int ToOffered(int mode)
        {
            for (int i = 0; i < Offered.Length; i++) if ((int)Offered[i] == mode) return i;
            return 0;
        }
        public static int FromOffered(int i) => (int)Offered[Mathf.Clamp(i, 0, Offered.Length - 1)];
        /// <summary>Modo válido (um modo retirado, salvo em versão antiga, vira TV).</summary>
        public static int Sanitize(int mode) => FromOffered(ToOffered(mode));
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
        /// <summary>Entrada dos vestiários (do lado da câmera): só aparece nas cenas (cerimônia, replays).</summary>
        public Transform entrance;

        public void Setup(Camera c, MatchSim s, int m)
        {
            cam = c;
            sim = s;
            mode = Sanitize(m);
            zoom = Settings.CameraZoom;
            _focus = Vector3.zero;
            // faixa de profundidade justa: menos "briga" de superfícies (pontos piscando) ao longe
            cam.nearClipPlane = 0.8f;   // (mais precisão de profundidade: nada de pele atravessando o uniforme)
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
            _baseFov = fov;
        }

        // ---------------------------------------------------------------- paredes na frente
        readonly System.Collections.Generic.List<Renderer> _occ = new System.Collections.Generic.List<Renderer>();
        readonly System.Collections.Generic.List<UnityEngine.Rendering.ShadowCastingMode> _occMode = new System.Collections.Generic.List<UnityEngine.Rendering.ShadowCastingMode>();
        readonly System.Collections.Generic.List<bool> _occHidden = new System.Collections.Generic.List<bool>();
        readonly System.Collections.Generic.List<bool> _occNear = new System.Collections.Generic.List<bool>();
        readonly System.Collections.Generic.List<float> _occClear = new System.Collections.Generic.List<float>();

        /// <summary>
        /// Estruturas grandes do estádio/ginásio (paredes, arquibancadas, cobertura) que podem
        /// ficar entre a câmera e o jogo: somem enquanto taparem a visão (a sombra continua).
        /// </summary>
        public void SetOccluders(params Transform[] roots)
        {
            _occ.Clear(); _occMode.Clear(); _occHidden.Clear(); _occNear.Clear(); _occClear.Clear();
            if (roots == null) return;
            foreach (var venueRoot in roots)
            if (venueRoot != null)
            foreach (var r in venueRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                var b = r.bounds;
                bool floor = b.size.y < 0.6f && b.max.y < 0.8f;
                if (floor || Mathf.Max(b.size.x, b.size.y, b.size.z) < 1.2f) continue;
                // gols, redes e o que fica inteiramente dentro do campo nunca somem (antes bastava o
                // centro estar dentro: guarda-corpos e placas que dão a volta no campo nunca sumiam)
                if (sim != null && Mathf.Abs(b.min.x) < sim.halfLen + 3.5f && Mathf.Abs(b.max.x) < sim.halfLen + 3.5f
                    && Mathf.Abs(b.min.z) < sim.halfW + 1.2f && Mathf.Abs(b.max.z) < sim.halfW + 1.2f && b.max.y < 6f) continue;
                _occ.Add(r);
                _occMode.Add(r.shadowCastingMode);
                _occHidden.Add(false);
                // inteiro do lado da câmera (além da lateral de baixo): nas câmeras de lado fica escondido
                // a partida toda (antes sumia e voltava a cada movimento da câmera: arquibancada e
                // torcedores "piscando", trocando de camada)
                // (pelo centro: cantos, coberturas e refletores que passam um pouco da lateral também contam)
                _occNear.Add(sim != null && (b.max.z < -(sim.halfW + 0.6f) || (b.center.z < -(sim.halfW + 1.5f) && b.min.z < -(sim.halfW + 2f))));
                _occClear.Add(0f);
            }
        }

        readonly Vector3[] _targets = new Vector3[7];
        readonly System.Collections.Generic.List<Vector3> _tList = new System.Collections.Generic.List<Vector3>();

        /// <summary>Tomadas de fora do estádio: nada some (a fachada e o entorno são o assunto).</summary>
        public bool showAll;

        void UpdateOccluders(Vector3 focus)
        {
            if (_occ.Count == 0) return;
            var kNow = (Kind)Mathf.Clamp(mode, 0, Modes.Length - 1);
            bool sideView = !cinematic && kNow != Kind.Behind && kNow != Kind.Shoulder && kNow != Kind.Dynamic && kNow != Kind.BehindGoal;
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
            // e uma grade sobre todo o campo visível: nada pode tapar nenhum pedaço da área de jogo
            _tList.Clear();
            _tList.AddRange(targets);
            for (int gx = 0; gx <= 6; gx++)
                for (int gz = 0; gz <= 3; gz++)
                {
                    var g = new Vector3(Mathf.Lerp(-hl, hl, gx / 6f), 0.6f, Mathf.Lerp(-hw, hw, gz / 3f));
                    var vp = cam.WorldToViewportPoint(g);
                    if (vp.z > 0f && vp.x > -0.05f && vp.x < 1.05f && vp.y > -0.05f && vp.y < 1.05f) _tList.Add(g);
                }
            // retângulo do campo na tela e a profundidade do ponto do campo mais perto da câmera:
            // qualquer coisa inteira antes do campo que caia sobre ele na tela está na frente
            var fwd = cam.transform.forward;
            float dmin = float.MaxValue;
            float sx0 = 1f, sy0 = 1f, sx1 = 0f, sy1 = 0f;
            for (int k = 0; k < 4; k++)
            {
                var fc = new Vector3((k & 1) == 0 ? -hl : hl, 0f, (k & 2) == 0 ? -hw : hw);
                dmin = Mathf.Min(dmin, Vector3.Dot(fc - c, fwd));
                var vp = cam.WorldToViewportPoint(fc);
                if (vp.z <= 0f) { sx0 = 0f; sy0 = 0f; sx1 = 1f; sy1 = 1f; continue; }
                sx0 = Mathf.Min(sx0, vp.x); sy0 = Mathf.Min(sy0, vp.y); sx1 = Mathf.Max(sx1, vp.x); sy1 = Mathf.Max(sy1, vp.y);
            }
            sx0 = Mathf.Max(sx0, 0f); sy0 = Mathf.Max(sy0, 0f); sx1 = Mathf.Min(sx1, 1f); sy1 = Mathf.Min(sy1, 1f);
            for (int i = 0; i < _occ.Count; i++)
            {
                var r = _occ[i];
                if (r == null) continue;
                var b = r.bounds;
                bool block = !showAll && InFrontOfField(b, c, fwd, dmin, sx0, sy0, sx1, sy1);
                if (!block && !showAll) foreach (var t in _tList)
                {
                    var d = t - c;
                    float len = d.magnitude;
                    if (len < 0.01f) continue;
                    if (b.Contains(c) || (b.IntersectRay(new Ray(c, d / len), out float hit) && hit < len - 0.5f)) { block = true; break; }
                }
                // colado na câmera (poste, mureta, alambrado) também some
                if (!block && !showAll && b.SqrDistance(c) < 25f) block = true;
                // câmeras de lado: o lado da câmera fica sempre escondido (sem piscar)
                if (!showAll && sideView && _occNear[i]) block = true;
                // some na hora; só volta depois de ~1,5 s sem tapar (nada de piscar a cada passe)
                if (block) _occClear[i] = 0f;
                else if (_occHidden[i])
                {
                    _occClear[i] += Time.deltaTime;
                    if (_occClear[i] < 1.5f && !showAll && !cinematic) block = true;
                }
                if (block == _occHidden[i]) continue;
                _occHidden[i] = block;
                // escondido: quem já fazia sombra continua fazendo (só a sombra); quem não fazia some de vez
                // (antes passava a projetar sombra: treliças e vigas do lado da câmera riscavam a quadra)
                if (_occMode[i] == UnityEngine.Rendering.ShadowCastingMode.Off) r.forceRenderingOff = block;
                else r.shadowCastingMode = block ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : _occMode[i];
            }
        }

        readonly Vector3[] _corners = new Vector3[8];

        /// <summary>
        /// O objeto está inteiro entre a câmera e o campo (mais perto que o ponto do campo mais próximo)
        /// e, na tela, cai sobre o campo? Então tapa o jogo (placa, letreiro, porta, arquibancada da frente).
        /// </summary>
        bool InFrontOfField(Bounds b, Vector3 c, Vector3 fwd, float dmin, float sx0, float sy0, float sx1, float sy1)
        {
            var mn = b.min; var mx = b.max;
            float dmax = float.MinValue, x0 = 2f, y0 = 2f, x1 = -1f, y1 = -1f;
            bool any = false;
            for (int k = 0; k < 8; k++)
            {
                var p = new Vector3((k & 1) == 0 ? mn.x : mx.x, (k & 2) == 0 ? mn.y : mx.y, (k & 4) == 0 ? mn.z : mx.z);
                dmax = Mathf.Max(dmax, Vector3.Dot(p - c, fwd));
                var vp = cam.WorldToViewportPoint(p);
                if (vp.z <= 0.05f) continue;
                any = true;
                x0 = Mathf.Min(x0, vp.x); y0 = Mathf.Min(y0, vp.y); x1 = Mathf.Max(x1, vp.x); y1 = Mathf.Max(y1, vp.y);
            }
            if (!any || dmax > dmin + 0.3f) return false;
            // sobreposição real com o campo na tela (não só encostando na borda)
            float ox = Mathf.Min(x1, sx1) - Mathf.Max(x0, sx0), oy = Mathf.Min(y1, sy1) - Mathf.Max(y0, sy0);
            return ox > 0.005f && oy > 0.005f;
        }

        void LateUpdate()
        {
            if (sim == null || cam == null) return;
            if (_sceneT > 0f)
            {
                _sceneT -= Time.deltaTime;
                if (_sceneT <= 0f) { cinematic = _sceneWasCine; SnapToBall(); }
            }
            // o enquadramento é pensado em 16:9; em telas mais estreitas (4:3, 16:10, retrato)
            // abre o campo de visão vertical para mostrar a mesma largura de campo
            if (_baseFov <= 0f) _baseFov = cam.fieldOfView;
            cam.fieldOfView = _baseFov;
            Move();
            // plano de corte próximo conforme a distância do assunto: de perto (closes, replay) 0,8 m cortava a
            // frente da mão mais próxima e sobrava só o contorno escuro — a mão virava um "borrão"
            float subj = Vector3.Distance(cam.transform.position, cinematic ? cineLook : _focus);
            cam.nearClipPlane = Mathf.Clamp(subj * 0.06f, 0.08f, 0.8f);
            _baseFov = cam.fieldOfView;
            cam.fieldOfView = FitAspect(_baseFov, cam.aspect);
            UpdateOccluders(cinematic ? cineLook : _focus);
            if (entrance != null && entrance.gameObject.activeSelf != cinematic) entrance.gameObject.SetActive(cinematic);
        }

        float _baseFov;

        /// <summary>FOV vertical que mantém a largura vista em 16:9 (telas mais largas ficam como estão).</summary>
        public static float FitAspect(float fov16x9, float aspect)
        {
            const float A = 16f / 9f;
            if (aspect <= 0.01f || aspect >= A) return fov16x9;
            float t = Mathf.Tan(fov16x9 * 0.5f * Mathf.Deg2Rad) * A / aspect;
            return Mathf.Min(80f, 2f * Mathf.Atan(t) * Mathf.Rad2Deg);
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
            var kind = (Kind)Mathf.Clamp(mode, 0, Modes.Length - 1);
            if (kind == Kind.Close || kind == Kind.Low) target.z = Mathf.Clamp(target.z, -hw, hw);
            else target.z = Mathf.Clamp(target.z * 0.55f, -hw * 0.5f, hw * 0.5f);
            bool onPlayer = kind == Kind.PlayerSide || kind == Kind.Behind || kind == Kind.Shoulder || kind == Kind.Dynamic;
            if (!onPlayer) _focus = Vector3.SmoothDamp(_focus, target, ref _vel, kind == Kind.Close || kind == Kind.Low ? 0.28f : 0.35f, 60f, dt);
            // perto do gol: aproxima um pouco
            float nearGoal = Mathf.Clamp01((Mathf.Abs(_focus.x) - hl * 0.55f) / (hl * 0.35f));
            float zk = 1f - zoom * 0.14f;   // zoom afasta/aproxima mantendo o ângulo
            switch (kind)
            {
                case Kind.PlayerSide: PlayerSide(ball, field, zk, dt); return;
                case Kind.Behind:
                case Kind.Shoulder:
                case Kind.Dynamic: BehindCam(kind, ball, field, zk, dt); return;
                case Kind.BehindGoal:
                case Kind.Drone: Special(kind, field, hl, zk, dt); return;
            }
            float height, back, fov;
            switch (kind)
            {
                case Kind.TvClose: height = field ? 9f : 7f; back = field ? 19f : 15f; fov = field ? 32f : 34f; break;
                case Kind.Tactical: height = field ? 42f : 20f; back = field ? 36f : 18f; fov = 38f; break;
                case Kind.Pitchside: height = field ? 2.6f : 2.1f; back = field ? 10f : 5f; fov = 46f; break;
                case Kind.Wide: height = field ? 30f : 14f; back = field ? 58f : 24f; fov = 34f; break;   // o campo todo
                // aberta aproximada: alta como a aberta (vê a jogada e o posicionamento), mas fechada
                // na bola — atletas grandes o bastante para ver os detalhes
                case Kind.WideClose: height = field ? 21f : 11f; back = field ? 40f : 18f; fov = field ? 24f : 30f; break;
                // TV bem perto: a mesma vista de lado da TV, baixa e fechada na bola (atletas grandes)
                case Kind.Close: height = field ? 6.5f : 5f; back = field ? 13f : 9f; fov = field ? 30f : 34f; break;
                // rente ao lance: quase na altura dos atletas, à beira do campo, acompanhando a bola de perto
                case Kind.Low: height = field ? 3.4f : 2.6f; back = field ? 8.5f : 5.5f; fov = field ? 38f : 42f; break;
                // TV (padrão): no campo, mais perto do jogo e de frente (antes 18 m de altura a 38 m da
                // lateral: os atletas ficavam pequenos)
                default: height = field ? 13f : 10f; back = field ? 26f : 16f; fov = field ? 30f : 38f; break;
            }
            height *= zk;
            back *= zk;
            fov -= nearGoal * 4f;
            _dist = Mathf.Lerp(_dist <= 0 ? back : _dist, back * (1f - nearGoal * 0.1f), 1f - Mathf.Exp(-dt * 1.5f));
            bool tight = kind == Kind.Close || kind == Kind.Low;
            // câmeras fechadas: acompanham a bola de verdade (x igual e chegando perto na profundidade)
            if (kind == Kind.Low)
            {
                // fica na beira do campo (fora das quatro linhas) e fecha a lente conforme a bola se afasta
                float dz = _focus.z + hw + 2f;
                fov = Mathf.Lerp(field ? 40f : 44f, field ? 16f : 24f, Mathf.Clamp01((dz - 6f) / (hw * 2f)));
            }
            var pos = kind == Kind.Low ? new Vector3(_focus.x - 3f, height, -(hw + 2f))
                : tight ? new Vector3(_focus.x, height, _focus.z - _dist - hw * 0.15f)
                : new Vector3(_focus.x * (kind == Kind.Pitchside ? 0.95f : field && (kind == Kind.Tv || kind == Kind.TvClose) ? 0.9f : 0.82f), height, -(hw + _dist) + _focus.z * 0.25f);
            pos += ShakeOffset(dt, 0.25f);
            cam.transform.position = pos;
            var look = _focus + Vector3.up * (kind == Kind.Pitchside ? 1.1f : 0.6f);
            cam.transform.rotation = Quaternion.LookRotation(look - pos);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-dt * 2f));
        }

        Vector3 ShakeOffset(float dt, float k)
        {
            if (_shake <= 0f) return Vector3.zero;
            _shake = Mathf.MoveTowards(_shake, 0f, dt * 2.5f);
            return Random.insideUnitSphere * _shake * k;
        }

        Vector3 _player;

        /// <summary>Posição do atleta controlado (suavizada), puxada um pouco para a bola.</summary>
        Vector3 PlayerAnchor(Vector3 ball, float toBall, float dt)
        {
            var pl = sim.Controlled(Mathf.Clamp(followTeam, 0, 1));
            var p = new Vector3(pl.position.x, 0, pl.position.y);
            var want = Vector3.Lerp(p, ball, toBall);
            _player = _player == Vector3.zero ? want : Vector3.Lerp(_player, want, 1f - Mathf.Exp(-dt * 5f));
            return _player;
        }

        /// <summary>"Perto do jogador": lateral, baixa e fechada no atleta controlado (estilo câmera pro).</summary>
        void PlayerSide(Vector3 ball, bool field, float zk, float dt)
        {
            var a = PlayerAnchor(ball, 0.3f, dt);
            _focus = Vector3.SmoothDamp(_focus, a, ref _vel, 0.22f, 80f, dt);
            float dist = (field ? 15f : 8.5f) * zk, h = (field ? 6.2f : 4.2f) * zk;
            var p = new Vector3(_focus.x, h, _focus.z - dist) + ShakeOffset(dt, 0.2f);
            cam.transform.position = Vector3.Lerp(cam.transform.position, p, 1f - Mathf.Exp(-dt * 6f));
            var q = Quaternion.LookRotation(_focus + Vector3.up * 1.0f - cam.transform.position);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, q, 1f - Mathf.Exp(-dt * 7f));
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, field ? 36f : 42f, 1f - Mathf.Exp(-dt * 2f));
        }

        /// <summary>Câmeras "por trás", no sentido do ataque: atrás do jogador, ombro (bem perto) e dinâmica (segue a bola).</summary>
        void BehindCam(Kind kind, Vector3 ball, bool field, float zk, float dt)
        {
            Vector3 anchor;
            float dir;
            if (kind == Kind.Dynamic)
            {
                int t = sim.ball.owner >= 0 ? sim.players[sim.ball.owner].team : (sim.ball.lastTouchTeam >= 0 ? sim.ball.lastTouchTeam : 0);
                anchor = ball;
                dir = sim.TeamDir(t);
            }
            else
            {
                anchor = PlayerAnchor(ball, kind == Kind.Shoulder ? 0.12f : 0.35f, dt);
                dir = sim.TeamDir(Mathf.Clamp(followTeam, 0, 1));
            }
            _attackDir = Vector3.Slerp(_attackDir, new Vector3(dir, 0, 0), 1f - Mathf.Exp(-dt * 1.2f));
            float bk, up, fov, ahead;
            switch (kind)
            {
                case Kind.Shoulder: bk = field ? 4.6f : 3.6f; up = field ? 2.3f : 2.0f; fov = 60f; ahead = field ? 12f : 7f; break;
                case Kind.Dynamic: bk = field ? 20f : 11f; up = field ? 9f : 6f; fov = 52f; ahead = field ? 8f : 4f; break;
                default: bk = field ? 10f : 7f; up = field ? 4.2f : 3.2f; fov = 52f; ahead = field ? 8f : 4f; break;
            }
            bk *= zk; up *= zk;
            _focus = Vector3.SmoothDamp(_focus, anchor, ref _vel, kind == Kind.Shoulder ? 0.15f : 0.25f, 80f, dt);
            // ombro: um pouco para o lado, para o atleta não tapar a jogada
            var side = Vector3.Cross(Vector3.up, _attackDir).normalized * (kind == Kind.Shoulder ? -1.1f : 0f);
            var p3 = _focus - _attackDir * bk + Vector3.up * up + side + ShakeOffset(dt, 0.2f);
            float follow = kind == Kind.Shoulder ? 9f : 6f;
            cam.transform.position = Vector3.Lerp(cam.transform.position, p3, 1f - Mathf.Exp(-dt * follow));
            var look = _focus + _attackDir * ahead + Vector3.up * (kind == Kind.Shoulder ? 1.0f : 0.5f);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(look - cam.transform.position), 1f - Mathf.Exp(-dt * follow));
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-dt * 2f));
        }

        void Special(Kind kind, bool field, float hl, float zk, float dt)
        {
            Vector3 p6, look6;
            float fov6;
            if (kind == Kind.BehindGoal)
            {
                // atrás do gol que o time seguido defende, olhando o campo no sentido do ataque
                float dir = sim.TeamDir(followTeam);
                float behind = (field ? 16f : 8f) * zk;
                p6 = new Vector3(-dir * (hl + behind) + _focus.x * 0.35f, (field ? 11f : 6.5f) * zk, _focus.z * 0.6f);
                look6 = new Vector3(_focus.x, 0.5f, _focus.z);
                fov6 = field ? 46f : 50f;
            }
            else
            {
                // drone: quase de cima, um pouco atrás, acompanha a bola de perto
                p6 = _focus + new Vector3(0f, (field ? 30f : 15f) * zk, -(field ? 9f : 5f) * zk);
                look6 = _focus;
                fov6 = 42f;
            }
            p6 += ShakeOffset(dt, 0.25f);
            cam.transform.position = Vector3.Lerp(cam.transform.position, p6, 1f - Mathf.Exp(-dt * 5f));
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(look6 - cam.transform.position), 1f - Mathf.Exp(-dt * 6f));
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov6, 1f - Mathf.Exp(-dt * 2f));
        }

        public void SnapToBall()
        {
            _focus = new Vector3(sim.ball.pos.x, 0, sim.ball.pos.y);
            _vel = Vector3.zero;
            _lead = Vector3.zero;
        }
    }
}
