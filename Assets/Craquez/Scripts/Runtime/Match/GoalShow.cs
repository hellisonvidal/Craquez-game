using System.Collections.Generic;
using Craquez.Sim;
using Craquez.UI;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Comemoração de gol (~15 s): o autor corre para a bandeirinha, os companheiros
    /// chegam e pulam juntos, os adversários voltam cabisbaixos; depois o replay do
    /// lance em câmera lenta e uma tomada aberta do estádio antes do reinício.
    /// Tocar/apertar qualquer botão pula.
    /// </summary>
    public class GoalShow
    {
        // ---------------------------------------------------------------- gravação
        struct PState { public Vector2 pos, vel, facing; public float slide, dive, stun; }
        class Frame
        {
            public PState[] p;
            public Vector2 ballPos, ballVel;
            public float ballZ, ballSpin;
            public List<SimEvent> ev;
        }

        const int Keep = 60 * 5;           // 5 s antes do gol
        readonly Frame[] _ring = new Frame[Keep];
        int _head, _count;

        public void Record(MatchSim sim)
        {
            var f = _ring[_head] ??= new Frame { p = new PState[sim.players.Count], ev = new List<SimEvent>() };
            if (f.p.Length != sim.players.Count) f.p = new PState[sim.players.Count];
            for (int i = 0; i < sim.players.Count; i++)
            {
                var pl = sim.players[i];
                f.p[i] = new PState { pos = pl.position, vel = pl.vel, facing = pl.facing, slide = pl.slide, dive = pl.dive, stun = pl.stun };
            }
            f.ballPos = sim.ball.pos;
            f.ballVel = sim.ball.vel;
            f.ballZ = sim.ball.z;
            f.ballSpin = sim.ball.spin;
            f.ev.Clear();
            foreach (var e in sim.events)
                if (e.type != "goal" && e.type != "whistle") f.ev.Add(e);
            _head = (_head + 1) % Keep;
            _count = Mathf.Min(_count + 1, Keep);
        }

        Frame At(int i) => _ring[(_head - _count + i + Keep * 2) % Keep];

        // ---------------------------------------------------------------- show
        public const float Length = 15f;
        const float LiveEnd = 6.5f, ReplayEnd = 13f;
        const float ReplaySpeed = 0.6f, ReplaySecs = 3.8f;

        MatchSim _sim;
        MatchView _view;
        Hud _hud;
        int _team, _scorer;
        float _t, _acc, _replayPos;
        Vector2 _spot;
        PState[] _live;
        Vector2 _liveBall;
        float _liveBallZ;
        readonly HashSet<int> _hugged = new HashSet<int>();
        int _replayStart;
        bool _replaying, _done;
        public bool Done => _done;

        public void Begin(MatchSim sim, MatchView view, Hud hud, int team, int scorer)
        {
            _sim = sim;
            _view = view;
            _hud = hud;
            _team = team;
            _scorer = scorer >= 0 && scorer < sim.players.Count && sim.players[scorer].team == team ? scorer : -1;
            if (_scorer < 0)
                foreach (var p in sim.players)
                    if (p.team == team && !p.isKeeper) { _scorer = p.id; break; }
            _t = 0f;
            _acc = 0f;
            _done = false;
            _replaying = false;
            _hugged.Clear();
            // comemoração na bandeirinha do lado atacado, do lado da câmera
            float dir = Mathf.Sign(sim.ball.pos.x == 0 ? 1f : sim.ball.pos.x);
            _spot = new Vector2(dir * (sim.halfLen - 3f), -(sim.halfW - 2.5f));
            // sem corte: a câmera sai de onde estava, acompanha a bola morrendo na rede e só então
            // desliza até o autor do gol (a câmera cinematográfica interpola posição e mira)
            var bc = view.bcam;
            bc.cinematic = true;
            bc.cinePos = bc.cam.transform.position;
            bc.cineLook = new Vector3(sim.ball.pos.x, sim.ball.z, sim.ball.pos.y);
            bc.cineFov = bc.cam.fieldOfView;
            _wipeAt = -1f;
            if (_scorer >= 0) view.athletes[_scorer].Celebrate(LiveEnd);
        }

        /// <summary>Avança a comemoração; retorna false quando terminou.</summary>
        public bool Update(float dt, bool skip)
        {
            if (_done) return false;
            _t += dt / Mathf.Max(0.01f, Time.timeScale);   // tempo real (o replay usa câmera lenta)
            if (skip && _t > 1.2f && _wipeAt < 0f) { _skipping = true; Wipe.Play(_hud.Overlay, 4200f); _wipeAt = _t; }
            // as trocas de plano acontecem por trás da varredura de transmissão (nunca um corte seco)
            if (_skipping) { if (_t - _wipeAt > 0.25f) { Finish(); return false; } KeepCurrent(dt); return true; }
            float[] marks = { LiveEnd, ReplayEnd, Length };
            foreach (float m in marks)
                if (_t > m - 0.25f && _t < m && _wipeMark != m) { _wipeMark = m; Wipe.Play(_hud.Overlay, 4200f); }
            if (_t < LiveEnd) Live(dt);
            else if (_t < ReplayEnd) Replay(dt);
            else if (_t < Length) Wide();
            else { Finish(); return false; }
            return true;
        }

        float _wipeAt = -1f, _wipeMark = -1f;
        bool _skipping;

        /// <summary>Durante a varredura do "pular": mantém o plano atual vivo.</summary>
        void KeepCurrent(float dt)
        {
            if (_t < LiveEnd && !_replaying) Live(dt);
            else _view.OnTick();
        }

        // ------------------------------------------------------------ ao vivo
        void Live(float dt)
        {
            _acc += dt;
            while (_acc >= MatchSim.DT)
            {
                _acc -= MatchSim.DT;
                LiveStep(MatchSim.DT);
                _sim.events.Clear();
                _view.OnTick();
            }
            _view.Alpha = _acc / MatchSim.DT;
            // câmera: acompanha o autor do gol de perto
            var sc = _scorer >= 0 ? _sim.players[_scorer] : null;
            var c = sc != null ? new Vector3(sc.position.x, 0, sc.position.y) : Vector3.zero;
            var bc = _view.bcam;
            float k = Mathf.Clamp01(_t / LiveEnd);
            var side = new Vector3(-Mathf.Sign(_spot.x) * 2.5f, 0, 0);
            var ballW = new Vector3(_sim.ball.pos.x, _sim.ball.z + 0.2f, _sim.ball.pos.y);
            var follow = c + new Vector3(0, Mathf.Lerp(2.6f, 2f, k), -Mathf.Lerp(8.5f, 5.5f, k)) + side;
            // 0-1,3 s: olhos na bola balançando a rede; depois a mira passa suave para o autor
            float toScorer = Mathf.SmoothStep(0f, 1f, (_t - 1.1f) / 1.4f);
            var netView = ballW + new Vector3(-Mathf.Sign(_spot.x) * 9f, 3.2f, -7f);
            bc.cinePos = Vector3.Lerp(Vector3.Lerp(bc.cam.transform.position, netView, 0.6f), follow, toScorer);
            bc.cineLook = Vector3.Lerp(ballW, c + Vector3.up * 1.15f, toScorer);
            bc.cineFov = Mathf.Lerp(Mathf.Lerp(bc.cam.fieldOfView, 30f, 0.3f), Mathf.Lerp(38f, 32f, k), toScorer);
        }

        void LiveStep(float dt)
        {
            // a bola continua: entra, estufa a rede e cai lá dentro
            float imp = _sim.StepBallInNet();
            if (imp > 1.5f && _view.venue != null)
            {
                var bw = new Vector3(_sim.ball.pos.x, _sim.ball.z + _sim.ball.radius, _sim.ball.pos.y);
                foreach (var n in _view.venue.nets) n.Hit(bw, imp * 1.4f);
                if (imp > 6f) Sfx.Net();
            }
            Vector2 sPos = _scorer >= 0 ? _sim.players[_scorer].position : _spot;
            foreach (var p in _sim.players)
            {
                Vector2 target;
                float speed;
                if (p.id == _scorer)
                {
                    target = _spot;
                    speed = _t < 4f ? 7.2f : 0f;
                }
                else if (p.team == _team && !p.isKeeper)
                {
                    // abraço: chegam ao autor em roda
                    float ang = p.index * 1.3f;
                    target = sPos + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (0.8f + (p.index % 3) * 0.35f);
                    speed = 7.5f;
                    if ((target - p.position).magnitude < 1.2f && _hugged.Add(p.id)) _view.athletes[p.id].Celebrate(LiveEnd - _t);
                }
                else if (p.team == _team)
                {
                    target = p.position + new Vector2(Mathf.Sign(_spot.x) * 0.5f, 0); // goleiro comemora no lugar
                    speed = 0.6f;
                }
                else
                {
                    // adversários voltam devagar para o meio-campo
                    target = new Vector2(p.basePos.x * 0.5f, p.basePos.y * 0.6f);
                    speed = p.isKeeper ? 0.4f : 1.1f;
                }
                var d = target - p.position;
                float dist = d.magnitude;
                Vector2 want = dist > 0.25f ? d / dist * Mathf.Min(speed, dist * 2f) : Vector2.zero;
                p.vel = Vector2.MoveTowards(p.vel, want, 9f * dt);
                p.position += p.vel * dt;
                if (p.vel.sqrMagnitude > 0.2f) p.facing = Vector2.Lerp(p.facing, p.vel.normalized, 0.15f).normalized;
                else if (p.team == _team && p.id != _scorer) { var f = sPos - p.position; if (f.sqrMagnitude > 0.01f) p.facing = Vector2.Lerp(p.facing, f.normalized, 0.1f).normalized; }
                else if (p.id == _scorer) p.facing = Vector2.Lerp(p.facing, new Vector2(0, -1), 0.05f).normalized;   // de frente para a câmera/torcida
                p.slide = 0f;
                p.dive = 0f;
                p.stun = 0f;
            }
        }

        // ------------------------------------------------------------ replay
        void Replay(float dt)
        {
            if (!_replaying)
            {
                _replaying = true;
                // guarda o estado ao vivo para voltar depois
                _live = new PState[_sim.players.Count];
                for (int i = 0; i < _sim.players.Count; i++)
                {
                    var p = _sim.players[i];
                    _live[i] = new PState { pos = p.position, vel = Vector2.zero, facing = p.facing };
                }
                _liveBall = _sim.ball.pos;
                _liveBallZ = _sim.ball.z;
                _replayStart = Mathf.Max(0, _count - Mathf.RoundToInt(ReplaySecs * 60f));
                _replayPos = _replayStart;
                if (_count > 0) Apply(At(_replayStart), false);
                _view.SnapAll();
                Time.timeScale = ReplaySpeed;
                _view.bcam.cinematic = false;
                _view.bcam.SnapToBall();
            }
            if (_count == 0) return;
            float before = _replayPos;
            _replayPos = Mathf.Min(_count - 1, _replayPos + dt * 60f);
            for (int i = Mathf.FloorToInt(before) + 1; i <= Mathf.FloorToInt(_replayPos); i++)
            {
                Apply(At(i), true);
                _view.OnTick();
            }
            _view.Alpha = _replayPos - Mathf.Floor(_replayPos);
        }

        void Apply(Frame f, bool events)
        {
            for (int i = 0; i < _sim.players.Count && i < f.p.Length; i++)
            {
                var p = _sim.players[i];
                var s = f.p[i];
                p.position = s.pos; p.vel = s.vel; p.facing = s.facing;
                p.slide = s.slide; p.dive = s.dive; p.stun = s.stun;
            }
            _sim.ball.pos = f.ballPos;
            _sim.ball.vel = f.ballVel;
            _sim.ball.z = f.ballZ;
            _sim.ball.spin = f.ballSpin;
            _sim.events.Clear();
            if (events) _sim.events.AddRange(f.ev);
        }

        // ------------------------------------------------------------ tomada final
        bool _wide;

        void Wide()
        {
            if (!_wide)
            {
                _wide = true;
                Time.timeScale = MatchController.GameSpeed;
                RestoreLive();
                _view.SnapAll();
                foreach (var a in _view.athletes)
                    if (a.p.team == _team && !a.p.isKeeper) a.Celebrate(Length - LiveEnd);
                var bc = _view.bcam;
                bc.cinematic = true;
                var c = new Vector3(_spot.x, 0, _spot.y);
                // grua: sobe atrás do grupo mostrando o estádio
                bc.cam.transform.position = c + new Vector3(-Mathf.Sign(_spot.x) * 4f, 2.5f, -6f);
                bc.cam.transform.LookAt(c + Vector3.up * 1.2f);
            }
            float k = Mathf.Clamp01((_t - ReplayEnd) / (Length - ReplayEnd));
            var cc = new Vector3(_spot.x, 0, _spot.y);
            var b = _view.bcam;
            b.cinePos = cc + new Vector3(-Mathf.Sign(_spot.x) * Mathf.Lerp(4f, 12f, k), Mathf.Lerp(2.5f, 11f, k), -Mathf.Lerp(6f, 16f, k));
            b.cineLook = Vector3.Lerp(cc + Vector3.up * 1.2f, new Vector3(_spot.x * 0.4f, 4f, _sim.halfW), k);
            b.cineFov = 42f;
            _view.OnTick();
        }

        void RestoreLive()
        {
            if (_live == null) return;
            for (int i = 0; i < _sim.players.Count && i < _live.Length; i++)
            {
                var p = _sim.players[i];
                p.position = _live[i].pos;
                p.vel = Vector2.zero;
                p.facing = _live[i].facing;
                p.slide = p.dive = p.stun = 0f;
            }
            _sim.ball.pos = _liveBall;
            _sim.ball.z = _liveBallZ;
            _sim.ball.vel = Vector2.zero;
            _sim.events.Clear();
            _live = null;
        }

        void Finish()
        {
            Time.timeScale = MatchController.GameSpeed;
            RestoreLive();
            _sim.events.Clear();
            _view.SnapAll();
            _view.bcam.cinematic = false;
            _view.bcam.SnapToBall();
            _count = 0;
            _wide = false;
            _done = true;
            _skipping = false;
            _wipeAt = _wipeMark = -1f;
        }
    }
}
