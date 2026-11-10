using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Sons: apito, chute, rede, trave e interface gerados por código; torcida com gravações
    /// reais (Resources/Audio, licenças livres — ver CREDITS.md) em duas camadas de ambiente,
    /// gol, aplauso, vaia e grito, com os sintetizados de reserva; trilha sonora dos menus.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        static Sfx _i;
        const int Rate = 44100;
        AudioSource[] _pool;
        int _next;
        AudioSource _crowd, _chant, _crowd2, _music;
        AudioClip _applause, _hurray;
        float _musicTarget;
        AudioClip _boo, _drums;
        AudioSource _drumSrc;
        float _drumTarget;
        AudioClip _flutter, _bark;
        AudioClip _ui, _move, _kick, _kickHard, _whistle, _whistleLong, _whistleEnd, _net, _post, _crowdLoop, _cheer, _ooh, _chantLoop, _thud;
        float _crowdTarget = 0f, _chantTarget;   // nos menus, silêncio (o ruído da torcida soava como chiado)

        public static void Init()
        {
            if (_i != null) return;
            var go = new GameObject("Sfx");
            DontDestroyOnLoad(go);
            _i = go.AddComponent<Sfx>();
            _i.Build();
            AudioListener.volume = Settings.Volume;
        }

        void Build()
        {
            _pool = new AudioSource[12];
            for (int i = 0; i < _pool.Length; i++)
            {
                _pool[i] = gameObject.AddComponent<AudioSource>();
                _pool[i].playOnAwake = false;
            }
            var rng = new System.Random(7);
            _ui = Tone(0.06f, 880, 1320, 0.25f, 0f, rng);
            _move = Tone(0.03f, 1500, 1500, 0.12f, 0f, rng);
            _kick = Kick(0.12f, 0.6f, rng);
            _kickHard = Kick(0.16f, 1f, rng);
            _thud = Kick(0.09f, 0.35f, rng);
            _whistle = Whistle(0.35f, rng);
            _whistleLong = Whistle(0.9f, rng);
            _whistleEnd = Whistle3(rng);
            _net = NetSwish(rng);
            _post = Metal(rng);
            _flutter = Flutter(rng);
            _bark = Bark(rng);
            AudioClip R(string n) => Resources.Load<AudioClip>("Audio/" + n);
            _crowdLoop = R("crowd_loop") ?? CrowdLoop(rng, 6f, 0.5f);
            _chantLoop = Chant(rng);
            _cheer = R("cheer") ?? Swell(3.5f, rng);
            _ooh = Ooh(rng);
            _boo = R("boo") ?? Boo(rng);
            _applause = R("applause") ?? _cheer;
            _hurray = R("hurray");
            _drums = Drums(rng);
            _drumSrc = gameObject.AddComponent<AudioSource>();
            _drumSrc.clip = _drums;
            _drumSrc.loop = true;
            _drumSrc.volume = 0f;
            _drumSrc.Play();
            _crowd = gameObject.AddComponent<AudioSource>();
            _crowd.clip = _crowdLoop;
            _crowd.loop = true;
            _crowd.volume = 0f;
            _crowd.Play();
            _chant = gameObject.AddComponent<AudioSource>();
            _chant.clip = _chantLoop;
            _chant.loop = true;
            _chant.volume = 0f;
            _chant.Play();
            // segunda camada (outra multidão): o ambiente não soa como um loop repetido
            var c2 = R("crowd_loop2");
            if (c2 != null)
            {
                _crowd2 = gameObject.AddComponent<AudioSource>();
                _crowd2.clip = c2;
                _crowd2.loop = true;
                _crowd2.volume = 0f;
                _crowd2.Play();
                _crowd2.time = c2.length * 0.37f;
            }
            if (_crowdLoop.length > 10f) _crowd.time = Random.value * _crowdLoop.length * 0.8f;
            // 12 trilhas originais: cada abertura do jogo começa por uma sorteada e, ao fim de
            // cada uma, vem outra (sem repetir a anterior)
            _music = gameObject.AddComponent<AudioSource>();
            _music.loop = false;
            _music.volume = 0f;
            _music.ignoreListenerPause = true;
            _track = Random.Range(0, Tracks);
            LoadTrack(_track);
        }

        public const int Tracks = 12;
        int _track = -1;
        bool _musicHeld;   // pausada por nós (fade a zero), não terminou

        void LoadTrack(int i)
        {
            var old = _music.clip;
            var c = Resources.Load<AudioClip>("Audio/Music/track_" + (i + 1).ToString("00"));
            _music.clip = c;
            if (old != null && old != c) Resources.UnloadAsset(old);
        }

        void NextTrack()
        {
            int n = Random.Range(0, Tracks - 1);
            _track = n >= _track ? n + 1 : n;
            LoadTrack(_track);
            _music.Play();
        }

        public const bool MusicEnabled = false;

        /// <summary>Trilha sonora dos menus (liga/desliga com fade).</summary>
        public static void MenuMusic(bool on)
        {
            if (_i == null || _i._music == null) return;
            // trilha desligada por enquanto (o autor vai compor músicas novas, livres de direitos):
            // troque MusicEnabled para true quando houver faixas novas em Resources/Audio/Music
            _i._musicTarget = MusicEnabled && on && Settings.Music ? Mathf.Clamp01(Settings.MusicVolume) : 0f;
            _inMenus = on;
            if (on) { _i._crowdTarget = _i._chantTarget = _i._drumTarget = 0f; }   // menus: sem ambiente de torcida
            if (_i._musicTarget > 0f && !_i._music.isPlaying)
            {
                if (_i._musicHeld) _i._music.UnPause(); else _i._music.Play();
                _i._musicHeld = false;
            }
        }

        void Update()
        {
            _crowd.volume = Mathf.MoveTowards(_crowd.volume, _crowdTarget, Time.unscaledDeltaTime * 0.4f);
            _chant.volume = Mathf.MoveTowards(_chant.volume, _chantTarget, Time.unscaledDeltaTime * 0.2f);
            _drumSrc.volume = Mathf.MoveTowards(_drumSrc.volume, _drumTarget, Time.unscaledDeltaTime * 0.3f);
            if (_crowd2 != null) _crowd2.volume = Mathf.MoveTowards(_crowd2.volume, _crowdTarget * 0.55f, Time.unscaledDeltaTime * 0.3f);
            if (_music != null)
            {
                _music.volume = Mathf.MoveTowards(_music.volume, _musicTarget, Time.unscaledDeltaTime * 0.5f);
                if (_music.volume <= 0f && _musicTarget <= 0f && _music.isPlaying) { _music.Pause(); _musicHeld = true; }
                else if (_musicTarget > 0f && !_music.isPlaying && !_musicHeld && _music.clip != null) NextTrack();   // acabou: próxima
            }
        }

        static void Play(AudioClip c, float vol = 1f, float pitch = 1f)
        {
            if (_i == null || c == null) return;
            var s = _i._pool[_i._next];
            _i._next = (_i._next + 1) % _i._pool.Length;
            s.pitch = pitch;
            s.PlayOneShot(c, vol);
        }

        public static void Ui() => Play(_i?._ui, 0.5f);
        public static void Move() => Play(_i?._move, 0.35f);
        public static void Kick(float power) => Play(power > 0.6f ? _i?._kickHard : _i?._kick, 0.55f + power * 0.45f, 0.9f + Random.value * 0.2f);
        public static void Touch() => Play(_i?._thud, 0.35f, 0.9f + Random.value * 0.3f);
        public static void Whistle() => Play(_i?._whistle, 0.6f);
        public static void WhistleLong() => Play(_i?._whistleLong, 0.6f);
        public static void WhistleEnd() => Play(_i?._whistleEnd, 0.65f);
        public static void Net() => Play(_i?._net, 0.75f);
        public static void Post() => Play(_i?._post, 0.7f);
        /// <summary>Bater de asas (pombos levantando voo).</summary>
        public static void Flutter() => Play(_i?._flutter, 0.22f, 0.85f + Random.value * 0.3f);
        /// <summary>Latido (vira-lata da rua).</summary>
        public static void Bark() => Play(_i?._bark, 0.4f, 0.9f + Random.value * 0.2f);
        /// <summary>
        /// Sem torcida (society e rua): nenhum som de multidão — ambiente, canto, batucada,
        /// gol, aplauso, vaia e "uuh" ficam mudos; só bola, apito, rede e trave.
        /// </summary>
        public static bool NoCrowd
        {
            get => _noCrowd;
            set
            {
                _noCrowd = value;
                if (value && _i != null)
                {
                    _i._crowdTarget = _i._chantTarget = _i._drumTarget = 0f;
                    _i._crowd.volume = _i._chant.volume = _i._drumSrc.volume = 0f;
                    if (_i._crowd2 != null) _i._crowd2.volume = 0f;
                }
            }
        }
        static bool _noCrowd;

        public static void Cheer() { if (!_noCrowd) Play(_i?._cheer, 1f); }
        public static void Boo() { if (!_noCrowd) Play(_i?._boo, 0.7f, 0.95f + Random.value * 0.1f); }
        /// <summary>Batucada da torcida (0..1).</summary>
        public static void Drums(float v) { if (_i != null) _i._drumTarget = _noCrowd || _inMenus ? 0f : v; }
        public static void Ooh() { if (!_noCrowd) Play(_i?._ooh, 0.7f, 0.9f + Random.value * 0.2f); }
        /// <summary>Aplauso/vibração curta da torcida (desarme, drible, boa defesa).</summary>
        public static void Applause(float v = 0.4f) { if (!_noCrowd) Play(_i?._applause, Mathf.Clamp01(v * 1.3f), 0.97f + Random.value * 0.08f); }
        /// <summary>Grito curto da torcida (drible, lance bonito).</summary>
        public static void Hurray(float v = 0.4f) { if (_noCrowd) return; if (_i?._hurray != null) Play(_i._hurray, v, 0.95f + Random.value * 0.1f); else Applause(v); }

        /// <summary>Volume da torcida (0 = silêncio, menu) e canto organizado.</summary>
        static bool _inMenus = true;

        public static void Crowd(float level, float chant)
        {
            // nos menus (inclusive com a partida de fundo rodando) a torcida fica muda: só a trilha
            if (_inMenus) { level = 0f; chant = 0f; }
            if (_i == null) return;
            _i._crowdTarget = _noCrowd ? 0f : level;
            _i._chantTarget = _noCrowd ? 0f : chant;
        }

        // ------------------------------------------------------------- síntese
        static AudioClip Make(string name, float[] data)
        {
            var c = AudioClip.Create(name, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        static AudioClip Tone(float dur, float f0, float f1, float vol, float noise, System.Random r)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            float ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                ph += 2 * Mathf.PI * Mathf.Lerp(f0, f1, t) / Rate;
                float env = Mathf.Min(1f, t * 20f) * (1f - t);
                d[i] = Mathf.Sin(ph) * env * vol;
            }
            return Make("tone", d);
        }

        static AudioClip Kick(float dur, float hard, System.Random r)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            float ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = Mathf.Lerp(180f, 60f, Mathf.Clamp01(t * 25f));
                ph += 2 * Mathf.PI * f / Rate;
                float env = Mathf.Exp(-t * (40f - hard * 12f));
                float click = (float)(r.NextDouble() * 2 - 1) * Mathf.Exp(-t * 300f) * 0.6f;
                d[i] = (Mathf.Sin(ph) * 0.9f + click) * env * (0.6f + hard * 0.4f);
            }
            return Make("kick", d);
        }

        static AudioClip Whistle(float dur, System.Random r)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            float ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float trill = Mathf.Sin(2 * Mathf.PI * 32f * t) * 60f;
                ph += 2 * Mathf.PI * (2900f + trill) / Rate;
                float env = Mathf.Min(1f, t * 40f) * Mathf.Min(1f, (dur - t) * 25f);
                float breath = (float)(r.NextDouble() * 2 - 1) * 0.08f;
                d[i] = (Mathf.Sin(ph) * 0.55f + Mathf.Sin(ph * 2f) * 0.12f + breath) * env * 0.5f;
            }
            return Make("whistle", d);
        }

        static AudioClip Whistle3(System.Random r)
        {
            var a = Whistle(0.3f, r);
            var b = Whistle(1.0f, r);
            var da = new float[a.samples];
            var db = new float[b.samples];
            a.GetData(da, 0);
            b.GetData(db, 0);
            int gap = (int)(0.12f * Rate);
            var d = new float[da.Length * 2 + db.Length + gap * 2];
            da.CopyTo(d, 0);
            da.CopyTo(d, da.Length + gap);
            db.CopyTo(d, da.Length * 2 + gap * 2);
            return Make("whistle3", d);
        }

        static AudioClip Flutter(System.Random r)
        {
            // rajadas de ruído abafado a ~13 batidas por segundo, sumindo
            int n = (int)(0.7f * Rate);
            var d = new float[n];
            float lp = 0, k = 1f - Mathf.Exp(-2 * Mathf.PI * 1400f / Rate);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                lp += ((float)(r.NextDouble() * 2 - 1) - lp) * k;
                float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 13f * Mathf.PI * 2f)), 3f);
                d[i] = lp * beat * Mathf.Exp(-t * 3.5f) * 2.2f;
            }
            return Make("flutter", d);
        }

        static AudioClip Bark(System.Random r)
        {
            // dois "au" curtos: tom que cai, com aspereza
            int n = (int)(0.42f * Rate);
            var d = new float[n];
            float ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float tt = t < 0.16f ? t : t - 0.24f;
                if (tt < 0f || tt > 0.14f) continue;
                float u = tt / 0.14f;
                float f = Mathf.Lerp(620f, 380f, u);
                ph += 2 * Mathf.PI * f / Rate;
                float env = Mathf.Min(1f, u * 25f) * (1f - u) * (1f - u);
                float v = Mathf.Sin(ph) + 0.5f * Mathf.Sin(ph * 2f) + 0.3f * Mathf.Sin(ph * 3.01f) + (float)(r.NextDouble() * 2 - 1) * 0.35f;
                d[i] = Mathf.Clamp(v * 0.6f, -1f, 1f) * env * 0.7f;
            }
            return Make("bark", d);
        }

        static AudioClip Noise(float dur, float vol, float cutoff, System.Random r, float decay)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            float lp = 0, k = 1f - Mathf.Exp(-2 * Mathf.PI * cutoff / Rate);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                lp += ((float)(r.NextDouble() * 2 - 1) - lp) * k;
                d[i] = lp * vol * Mathf.Exp(-t * decay) * Mathf.Min(1f, t * 200f) * 3f;
            }
            return Make("noise", d);
        }

        /// <summary>
        /// Bola na rede: baque abafado do couro no náilon + o "chuá" do tecido (ruído em banda média
        /// com o tremor das cordas), cauda curta das cordas assentando.
        /// </summary>
        /// <summary>
        /// Bola na rede: a pancada surda da bola no náilon, o "chuá" (ruído em banda que desce de ~2 kHz a
        /// ~500 Hz enquanto o pano estufa) e o chacoalhar dos fios e da armação depois, cada vez mais fraco.
        /// </summary>
        static AudioClip NetSwish(System.Random r)
        {
            int n = (int)(1.0f * Rate);
            var d = new float[n];
            float lpA = 0, lpB = 0, prev = 0;
            float click = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float w = (float)(r.NextDouble() * 2 - 1);
                // filtro de banda que desce (o pano estufando abafa o som)
                float hi = Mathf.Lerp(2400f, 650f, Mathf.Clamp01(t / 0.35f)), lo = hi * 0.25f;
                float k1 = 1f - Mathf.Exp(-2 * Mathf.PI * hi / Rate), k2 = 1f - Mathf.Exp(-2 * Mathf.PI * lo / Rate);
                lpA += (w - lpA) * k1;
                lpB += (lpA - lpB) * k2;
                float band = lpA - lpB;
                float swish = band * Mathf.Min(1f, t * 90f) * Mathf.Exp(-t * 6.5f) * 2.1f;
                // pancada: bola batendo no pano (grave e curta)
                float thump = Mathf.Sin(2 * Mathf.PI * (78f - 34f * t) * t) * Mathf.Exp(-t * 26f) * 0.75f
                            + Mathf.Sin(2 * Mathf.PI * 160f * t) * Mathf.Exp(-t * 45f) * 0.2f;
                // fios chacoalhando: estalinhos esparsos que rareiam
                float rate = 900f * Mathf.Exp(-t * 4f);
                if (r.NextDouble() < rate / Rate) click = (float)(r.NextDouble() * 0.5 + 0.5) * (r.NextDouble() < 0.5 ? 1f : -1f);
                click *= 0.82f;
                float rattle = click * 0.22f * Mathf.Exp(-t * 3f) * (t > 0.06f ? 1f : 0f);
                float s2 = swish + thump + rattle;
                d[i] = Mathf.Lerp(prev, s2, 0.9f);
                prev = d[i];
            }
            return Make("rede", d);
        }

        static AudioClip Metal(System.Random r)
        {
            int n = (int)(0.9f * Rate);
            var d = new float[n];
            float[] fr = { 523f, 1187f, 1965f, 2810f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float s = 0;
                for (int k = 0; k < fr.Length; k++) s += Mathf.Sin(2 * Mathf.PI * fr[k] * t) * Mathf.Exp(-t * (4f + k * 3f)) / (k + 1);
                d[i] = s * 0.5f;
            }
            return Make("post", d);
        }

        /// <summary>Murmúrio de estádio: ruído rosa filtrado com "ondas" lentas, emendado para loop.</summary>
        static AudioClip CrowdLoop(System.Random r, float dur, float vol)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            float b0 = 0, b1 = 0, b2 = 0, lp = 0;
            for (int i = 0; i < n; i++)
            {
                float w = (float)(r.NextDouble() * 2 - 1);
                b0 = 0.99765f * b0 + w * 0.099046f;
                b1 = 0.963f * b1 + w * 0.2965164f;
                b2 = 0.57f * b2 + w * 1.0526913f;
                float pink = (b0 + b1 + b2 + w * 0.1848f) * 0.2f;
                lp += (pink - lp) * 0.25f;
                float t = i / (float)n;
                float swell = 0.75f + 0.25f * Mathf.Sin(2 * Mathf.PI * t * 3f) * Mathf.Sin(2 * Mathf.PI * t);
                d[i] = lp * vol * swell;
            }
            int fade = Rate / 2;
            for (int i = 0; i < fade; i++)
            {
                float a = i / (float)fade;
                d[i] = d[i] * a + d[n - fade + i] * (1 - a);
            }
            var outD = new float[n - fade];
            System.Array.Copy(d, outD, n - fade);
            return Make("crowd", outD);
        }

        /// <summary>Canto ritmado da torcida (palmas + vozes em 4 tempos).</summary>
        static AudioClip Chant(System.Random r)
        {
            float bpm = 112f;
            float beat = 60f / bpm;
            int n = (int)(beat * 8 * Rate);
            var d = new float[n];
            float[] notes = { 220f, 220f, 247f, 262f, 247f, 220f, 196f, 220f };
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                int bi = (int)(t / beat);
                float bt = t - bi * beat;
                float clap = bi % 2 == 1 ? (float)(r.NextDouble() * 2 - 1) * Mathf.Exp(-bt * 60f) : 0f;
                float f = notes[bi % notes.Length];
                float voice = 0;
                for (int k = 0; k < 6; k++)
                {
                    float det = 1f + (k - 3) * 0.006f;
                    voice += Mathf.Sin(2 * Mathf.PI * f * det * t + k) + 0.4f * Mathf.Sin(2 * Mathf.PI * f * 2 * det * t);
                }
                voice *= Mathf.Min(1f, bt * 12f) * Mathf.Exp(-bt * 2.5f) * 0.06f;
                lp += ((float)(r.NextDouble() * 2 - 1) - lp) * 0.08f;
                d[i] = (voice * (1f + lp) + clap * 0.35f) * 0.6f;
            }
            return Make("chant", d);
        }

        static AudioClip Swell(float dur, System.Random r)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            float lp = 0, lp2 = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float w = (float)(r.NextDouble() * 2 - 1);
                lp += (w - lp) * 0.35f;
                lp2 += (lp - lp2) * 0.5f;
                float env = Mathf.Min(1f, t * 8f) * Mathf.Pow(1f - t, 0.7f);
                float roar = Mathf.Sin(2 * Mathf.PI * 180f * t * dur) * 0.05f * env;
                d[i] = (lp2 * 1.6f + roar) * env;
            }
            return Make("cheer", d);
        }

        static AudioClip Boo(System.Random r)
        {
            int n = (int)(1.8f * Rate);
            var d = new float[n];
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, u = t / 1.8f;
                float s = 0;
                for (int k = 0; k < 10; k++) s += Mathf.Sin(2 * Mathf.PI * (120f + k * 7f) * t + k);
                lp += ((float)(r.NextDouble() * 2 - 1) - lp) * 0.05f;
                float env = Mathf.Min(1f, u * 5f) * (1f - u);
                d[i] = (s * 0.035f * (1f + lp * 2f) + lp * 0.2f) * env;
            }
            return Make("boo", d);
        }

        /// <summary>Batucada (surdo, caixa e repique) em loop.</summary>
        static AudioClip Drums(System.Random r)
        {
            float beat = 60f / 128f;
            int n = (int)(beat * 8 * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                int bi = (int)(t / beat);
                float bt = t - bi * beat;
                float surdo = (bi % 2 == 0) ? Mathf.Sin(2 * Mathf.PI * 60f * bt) * Mathf.Exp(-bt * 9f) : 0f;
                float half = bt - beat * 0.5f;
                float caixa = half > 0f ? (float)(r.NextDouble() * 2 - 1) * Mathf.Exp(-half * 35f) * 0.5f : 0f;
                float rep = (bi % 4 == 3 && bt > beat * 0.25f) ? Mathf.Sin(2 * Mathf.PI * 420f * bt) * Mathf.Exp(-(bt - beat * 0.25f) * 25f) * 0.3f : 0f;
                d[i] = (surdo * 0.8f + caixa + rep) * 0.5f;
            }
            return Make("drums", d);
        }

        static AudioClip Ooh(System.Random r)
        {
            int n = (int)(1.3f * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float u = t / 1.3f;
                float f = Mathf.Lerp(260f, 180f, u);
                float s = 0;
                for (int k = 0; k < 8; k++) s += Mathf.Sin(2 * Mathf.PI * f * (1f + (k - 4) * 0.012f) * t + k * 1.3f);
                float env = Mathf.Min(1f, u * 6f) * (1f - u);
                d[i] = (s * 0.05f + (float)(r.NextDouble() * 2 - 1) * 0.08f) * env;
            }
            return Make("ooh", d);
        }
    }
}
