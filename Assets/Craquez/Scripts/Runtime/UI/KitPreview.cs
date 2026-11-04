using Craquez.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Craquez.UI
{
    /// <summary>
    /// Prévia 3D: um atleta vestindo o uniforme escolhido, girando devagar, renderizado
    /// numa textura mostrada na tela de escolha dos times. Fica longe do campo (num
    /// "estúdio" próprio com luz de três pontos) e é destruída com a página.
    /// </summary>
    public class KitPreview : MonoBehaviour
    {
        static int _slots;
        Camera _cam;
        RenderTexture _rt;
        Transform _stage, _turn;
        AthleteRig _rig;
        AthleteAnimator _anim;
        float _angle;

        public static KitPreview Create(RawImage target)
        {
            var go = new GameObject("Prévia do uniforme");
            var kp = go.AddComponent<KitPreview>();
            kp.Build(target, _slots++);
            go.transform.SetParent(target.transform, false);
            return kp;
        }

        void Build(RawImage target, int slot)
        {
            _stage = new GameObject("Estúdio").transform;
            _stage.position = new Vector3(2000f + slot * 50f, -500f, 0f);
            _turn = new GameObject("giro").transform;
            _turn.SetParent(_stage, false);
            _rt = new RenderTexture(Application.isMobilePlatform ? 384 : 512, Application.isMobilePlatform ? 576 : 768, 24) { antiAliasing = 4 };
            var cgo = new GameObject("Câmera prévia");
            cgo.transform.SetParent(_stage, false);
            cgo.transform.localPosition = new Vector3(0f, 1.05f, 3.4f);
            cgo.transform.LookAt(_stage.position + Vector3.up * 0.98f);
            _cam = cgo.AddComponent<Camera>();
            _cam.targetTexture = _rt;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.05f, 0.07f, 0.12f, 1f);
            _cam.fieldOfView = 34f;
            _cam.nearClipPlane = 0.2f;
            _cam.farClipPlane = 12f;
            foreach (var (pos, inten, col) in new[]
            {
                (new Vector3(1.8f, 2.6f, 2.4f), 2.4f, new Color(1f, 0.97f, 0.92f)),
                (new Vector3(-2.2f, 1.6f, 1.8f), 1.1f, new Color(0.75f, 0.85f, 1f)),
                (new Vector3(0f, 2.4f, -2.4f), 2.2f, new Color(0.9f, 0.95f, 1f)),
            })
            {
                var l = new GameObject("luz").AddComponent<Light>();
                l.transform.SetParent(_stage, false);
                l.transform.localPosition = pos;
                l.type = LightType.Point;
                l.range = 8f;
                l.intensity = inten;
                l.color = col;
            }
            // piso com brilho suave
            var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(floor.GetComponent<Collider>());
            floor.transform.SetParent(_stage, false);
            floor.transform.localRotation = Quaternion.Euler(90, 0, 0);
            floor.transform.localScale = Vector3.one * 3f;
            floor.GetComponent<MeshRenderer>().sharedMaterial = Gfx.UnlitTransparent(new Color(0f, 0.9f, 0.62f, 0.35f), Gfx.Radial(64, Color.white, new Color(1, 1, 1, 0), 2f));
            target.texture = _rt;
        }

        /// <summary>Veste (ou troca) o atleta com o uniforme e a chuteira escolhidos.</summary>
        public void Show(TeamData team, Kit kit, int boots)
        {
            if (_rig != null) { _anim?.Destroy(); Destroy(_rig.gameObject); }
            var p = new SimPlayer { name = "Prévia", number = 10, pos = "MF" };
            if (team != null && team.roster.Count > 9)
            {
                var e = team.roster[9];
                p.name = e.n;
                p.number = e.num;
                p.nat = e.nat;
                p.face = e.f;
            }
            _rig = AthleteFactory.Create(p, kit, kit, _turn, 2, boots);
            _rig.transform.localPosition = Vector3.zero;
            _rig.transform.localRotation = Quaternion.identity;
            foreach (var r in _rig.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _anim = new AthleteAnimator(_rig.animator, "prévia");
        }

        void Update()
        {
            _angle += Time.unscaledDeltaTime * 28f;
            if (_turn != null) _turn.localRotation = Quaternion.Euler(0, 180f + Mathf.Sin(_angle * Mathf.Deg2Rad) * 70f, 0);
            _anim?.Tick(Time.unscaledDeltaTime, 0f, 0f, 0f, false);
        }

        void OnDestroy()
        {
            _anim?.Destroy();
            if (_stage != null) Destroy(_stage.gameObject);
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
        }
    }
}
