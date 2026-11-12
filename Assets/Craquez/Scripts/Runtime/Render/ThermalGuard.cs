using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Controle de temperatura do celular/TV. Três medidas:
    /// 1) nos menus (sem partida) o jogo desenha a 30 quadros — antes ficava a 60 o tempo todo, esquentando
    ///    o aparelho parado na tela de escolha;
    /// 2) lê o estado térmico do Android (PowerManager, Android 10+) a cada 5 s e, se o aparelho esquenta,
    ///    reduz por etapas a resolução do 3D, as sombras e o limite de quadros;
    /// 3) também reduz se o quadro não se sustenta (o aparelho já está estrangulando a CPU/GPU).
    /// Volta aos poucos quando esfria. Só mexe no que está em "automático" nas Configurações.
    /// </summary>
    public class ThermalGuard : MonoBehaviour
    {
        static ThermalGuard _inst;
        /// <summary>Partida (ou treino) em andamento: fora dela, 30 quadros bastam.</summary>
        public static bool InMatch;
        /// <summary>Etapa de economia atual (0 normal .. 3 máxima).</summary>
        public static int Level { get; private set; }

        float _baseScale = 1f, _baseShadow = 50f;
        int _baseFps = 60;
        bool _autoFps, _autoScale;
        float _check, _coolT, _slowT, _ft;
        int _frames;
        bool _lastInMatch;
        AndroidJavaObject _power;
        bool _noThermal;

        public static void Ensure()
        {
            if (_inst != null) return;
            var go = new GameObject("Controle de temperatura");
            DontDestroyOnLoad(go);
            _inst = go.AddComponent<ThermalGuard>();
        }

        /// <summary>Chamado por UrpSetup.Quality: guarda os valores da qualidade escolhida (a referência).</summary>
        public static void Rebase(float renderScale, float shadowDistance, int fps, bool autoFps, bool autoScale)
        {
            Ensure();
            _inst._baseScale = renderScale;
            _inst._baseShadow = shadowDistance;
            _inst._baseFps = fps;
            _inst._autoFps = autoFps;
            _inst._autoScale = autoScale;
            _inst.Apply();
        }

        int ThermalStatus()
        {
            // 0 nenhum, 1 leve, 2 moderado, 3 severo, 4 crítico, 5 emergência, 6 desligando
            if (_noThermal || Application.platform != RuntimePlatform.Android) return -1;
            try
            {
                if (_power == null)
                {
                    using (var ver = new AndroidJavaClass("android.os.Build$VERSION"))
                        if (ver.GetStatic<int>("SDK_INT") < 29) { _noThermal = true; return -1; }
                    using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var act = up.GetStatic<AndroidJavaObject>("currentActivity"))
                        _power = act.Call<AndroidJavaObject>("getSystemService", "power");
                }
                return _power != null ? _power.Call<int>("getCurrentThermalStatus") : -1;
            }
            catch (System.Exception) { _noThermal = true; return -1; }
        }

        void Update()
        {
            if (InMatch != _lastInMatch) { _lastInMatch = InMatch; Apply(); }
            if (!Application.isMobilePlatform) return;
            _ft += Time.unscaledDeltaTime;
            _frames++;
            _check -= Time.unscaledDeltaTime;
            if (_check > 0f) return;
            float span = 5f;
            _check = span;
            float fps = _frames / Mathf.Max(0.01f, _ft);
            _frames = 0; _ft = 0f;
            int st = ThermalStatus();
            int want = Level;
            // quadro caindo bem abaixo do alvo numa partida (estrangulamento térmico): conta como aquecido
            int target = Application.targetFrameRate > 0 ? Application.targetFrameRate : 60;
            if (InMatch && fps < target * 0.7f) _slowT += span; else _slowT = 0f;
            if (st >= 3) want = 3;
            else if (st == 2) want = Mathf.Max(Level, 2);
            else if (st == 1 || _slowT >= 15f) want = Mathf.Max(Level, 1);
            if (want > Level) { Level = want; _coolT = 0f; Apply(); return; }
            // esfriou: volta uma etapa a cada 60 s sem aviso de calor
            if (st <= 0 && _slowT <= 0f && Level > 0)
            {
                _coolT += span;
                if (_coolT >= 60f) { Level--; _coolT = 0f; Apply(); }
            }
            else _coolT = 0f;
        }

        void Apply()
        {
            bool mobile = Application.isMobilePlatform;
            float k = Level == 0 ? 1f : Level == 1 ? 0.85f : Level == 2 ? 0.72f : 0.6f;
            UrpSetup.Thermal(_autoScale ? Mathf.Clamp(_baseScale * k, 0.45f, 2f) : -1f,
                _baseShadow * (Level == 0 ? 1f : Level == 1 ? 0.75f : Level == 2 ? 0.5f : 0f));
            if (_autoFps && QualitySettings.vSyncCount == 0)
            {
                int fps = _baseFps;
                if (mobile && !InMatch) fps = Mathf.Min(fps, 30);          // menus: 30 bastam
                if (mobile && Level >= 2) fps = Mathf.Min(fps, 30);
                else if (mobile && Level == 1) fps = Mathf.Min(fps, 45);
                Application.targetFrameRate = fps;
            }
        }
    }
}
