using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Goal post with net - visual component for goal posts with proper net physics.
    /// Attach to goal post prefabs.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class Goal : MonoBehaviour
    {
        [Header("Goal Configuration")]
        public bool isHomeTeam = true;
        public float postThickness = 0.1f;
        public float crossbarHeight = 2.44f;
        public float goalWidth = 7.32f;
        public float goalDepth = 2.0f;

        [Header("Net Physics")]
        public bool enableNetPhysics = true;
        public float netStiffness = 50f;
        public float netDamping = 0.8f;
        public int netResolution = 20;

        [Header("Materials")]
        public Material postMaterial;
        public Material netMaterial;

        private MeshFilter _postFilter;
        private MeshFilter _netFilter;
        private MeshRenderer _postRenderer;
        private MeshRenderer _netRenderer;
        private Cloth _netCloth;
        private Vector3[] _netVertices;
        private int[] _netTriangles;

        void Awake()
        {
            InitializeGoal();
        }

        void InitializeGoal()
        {
            _postFilter = gameObject.AddComponent<MeshFilter>();
            _postRenderer = gameObject.GetComponent<MeshRenderer>(); if (_postRenderer == null) _postRenderer = gameObject.AddComponent<MeshRenderer>();

            // Create goal posts mesh
            var postMesh = CreatePostMesh();
            _postFilter.mesh = postMesh;
            _postRenderer.material = postMaterial ?? CreateDefaultPostMaterial();

            // Create net as separate child
            var netObj = new GameObject("Net");
            netObj.transform.SetParent(transform, false);
            _netFilter = netObj.AddComponent<MeshFilter>();
            _netRenderer = netObj.AddComponent<MeshRenderer>();

            var netMesh = CreateNetMesh();
            _netFilter.mesh = netMesh;
            _netRenderer.material = netMaterial ?? CreateDefaultNetMaterial();

            if (enableNetPhysics)
            {
                SetupNetPhysics();
            }
        }

        Mesh CreatePostMesh()
        {
            var mesh = new Mesh { name = "GoalPosts" };
            var mk = new MeshKit();

            float halfW = goalWidth * 0.5f;
            float r = postThickness * 0.5f;

            // Left post
            mk.Cylinder(
                new Vector3(-halfW, 0, 0),
                new Vector3(-halfW, crossbarHeight, 0),
                r, 12);
            // Right post
            mk.Cylinder(
                new Vector3(halfW, 0, 0),
                new Vector3(halfW, crossbarHeight, 0),
                r, 12);
            // Crossbar
            mk.Cylinder(
                new Vector3(-halfW, crossbarHeight, 0),
                new Vector3(halfW, crossbarHeight, 0),
                r, 12);
            // Depth supports (back)
            mk.Cylinder(
                new Vector3(-halfW, 0, goalDepth),
                new Vector3(-halfW, crossbarHeight, goalDepth),
                r * 0.7f, 8);
            mk.Cylinder(
                new Vector3(halfW, 0, goalDepth),
                new Vector3(halfW, crossbarHeight, goalDepth),
                r * 0.7f, 8);
            mk.Cylinder(
                new Vector3(-halfW, crossbarHeight, goalDepth),
                new Vector3(halfW, crossbarHeight, goalDepth),
                r * 0.7f, 8);

            return mk.Build("GoalPosts");
        }

        Mesh CreateNetMesh()
        {
            var mesh = new Mesh { name = "GoalNet" };
            var vertices = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var triangles = new System.Collections.Generic.List<int>();

            float halfW = goalWidth * 0.5f;
            float halfH = crossbarHeight * 0.5f;
            float halfD = goalDepth * 0.5f;

            // Net planes: back, left, right, top
            CreateNetPlane(vertices, uvs, triangles,
                new Vector3(-halfW, crossbarHeight, goalDepth), // top-left back
                new Vector3(halfW, crossbarHeight, goalDepth),   // top-right back
                new Vector3(halfW, 0, goalDepth),                // bottom-right back
                new Vector3(-halfW, 0, goalDepth));              // bottom-left back

            CreateNetPlane(vertices, uvs, triangles,
                new Vector3(-halfW, crossbarHeight, 0),          // top-left front
                new Vector3(-halfW, crossbarHeight, goalDepth),  // top-left back
                new Vector3(-halfW, 0, goalDepth),               // bottom-left back
                new Vector3(-halfW, 0, 0));                      // bottom-left front

            CreateNetPlane(vertices, uvs, triangles,
                new Vector3(halfW, crossbarHeight, 0),           // top-right front
                new Vector3(halfW, crossbarHeight, goalDepth),   // top-right back
                new Vector3(halfW, 0, goalDepth),                // bottom-right back
                new Vector3(halfW, 0, 0));                       // bottom-right front

            CreateNetPlane(vertices, uvs, triangles,
                new Vector3(-halfW, crossbarHeight, 0),          // top-left front
                new Vector3(halfW, crossbarHeight, 0),           // top-right front
                new Vector3(halfW, crossbarHeight, goalDepth),   // top-right back
                new Vector3(-halfW, crossbarHeight, goalDepth)); // top-left back

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            _netVertices = vertices.ToArray();
            _netTriangles = triangles.ToArray();

            return mesh;
        }

        void CreateNetPlane(System.Collections.Generic.List<Vector3> verts,
            System.Collections.Generic.List<Vector2> uvs,
            System.Collections.Generic.List<int> tris,
            Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl)
        {
            int start = verts.Count;
            float stepU = 1f / netResolution;
            float stepV = 1f / netResolution;

            for (int v = 0; v <= netResolution; v++)
            {
                float vt = v * stepV;
                Vector3 left = Vector3.Lerp(bl, tl, vt);
                Vector3 right = Vector3.Lerp(br, tr, vt);

                for (int u = 0; u <= netResolution; u++)
                {
                    float ut = u * stepU;
                    Vector3 pos = Vector3.Lerp(left, right, ut);
                    verts.Add(pos);
                    uvs.Add(new Vector2(ut, vt));
                }
            }

            for (int v = 0; v < netResolution; v++)
            {
                for (int u = 0; u < netResolution; u++)
                {
                    int a = v * (netResolution + 1) + u;
                    int b = a + 1;
                    int c = a + netResolution + 1;
                    int d = c + 1;
                    tris.Add(start + a);
                    tris.Add(start + c);
                    tris.Add(start + b);
                    tris.Add(start + b);
                    tris.Add(start + c);
                    tris.Add(start + d);
                }
            }
        }

        void SetupNetPhysics()
        {
            _netCloth = _netFilter.gameObject.AddComponent<Cloth>();
            _netCloth.useGravity = true;
            // Use legacy damping property (stiffness not available in this Unity version)
            _netCloth.damping = netDamping;
            _netCloth.externalAcceleration = Vector3.zero;
            _netCloth.randomAcceleration = Vector3.zero;
            _netCloth.friction = 0.3f;

            // Constrain corners to goal posts
            var constraints = new ClothSkinningCoefficient[_netVertices.Length];
            for (int i = 0; i < _netVertices.Length; i++)
            {
                Vector3 v = _netVertices[i];
                float edgeDist = Mathf.Min(
                    Mathf.Abs(v.x + goalWidth * 0.5f),
                    Mathf.Abs(v.x - goalWidth * 0.5f),
                    Mathf.Abs(v.y - crossbarHeight),
                    Mathf.Abs(v.y),
                    Mathf.Abs(v.z),
                    Mathf.Abs(v.z - goalDepth)
                );

                constraints[i] = new ClothSkinningCoefficient
                {
                    maxDistance = edgeDist < 0.1f ? 0f : 0.1f,
                    collisionSphereDistance = 0.05f
                };
            }
            _netCloth.coefficients = constraints;
        }

        // cel shading como o resto do jogo ("Standard" é do pipeline antigo: no URP saía rosa)
        Material CreateDefaultPostMaterial() => Gfx.Character(Color.white, null, 0.6f);

        Material CreateDefaultNetMaterial() => Gfx.Transparent(new Color(1f, 1f, 1f, 0.6f));

        public void OnBallHit(Vector3 hitPoint, Vector3 velocity)
        {
            if (!enableNetPhysics || _netCloth == null) return;

            // Net reaction handled by Cloth physics simulation
            // Velocity manipulation API not available in this Unity version
        }

        void OnValidate()
        {
            if (_postFilter != null && _postFilter.mesh != null)
            {
                _postFilter.mesh = CreatePostMesh();
            }
            if (_netFilter != null && _netFilter.mesh != null)
            {
                _netFilter.mesh = CreateNetMesh();
                if (_netCloth != null) SetupNetPhysics();
            }
        }
    }
}