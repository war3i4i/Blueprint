﻿namespace kg_Blueprint;

public static class BuildProgress
{
    private static readonly int Visibility = Shader.PropertyToID("_Visibility");
    public static GameObject _piece;
    public static Material _ghostMat;
    public static Shader _shader;
    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    private static class ZNetScene_Awake_Patch
    {
        [UsedImplicitly] private static void Postfix(ZNetScene __instance) => __instance.m_namedPrefabs[_piece.name.GetStableHashCode()] = _piece;
    }
    public static void Init()
    {
        _shader = kg_Blueprint.Asset.LoadAsset<Shader>("kg_Blueprint_BuildShader");
        _ghostMat = kg_Blueprint.Asset.LoadAsset<Material>("kg_Blueprint_GhostMat");
        _piece = kg_Blueprint.Asset.LoadAsset<GameObject>("kg_Blueprint_BuildProgressPiece");
        _piece.AddComponent<BuildProgressComponent>();
    }
    public class BuildProgressComponent : MonoBehaviour, Hoverable
    {
        private ZNetView _znet;
        private Transform PieceObject;
        private readonly List<Material> _materials = [];

        public string _Prefab
        { 
            get => _znet.m_zdo.GetString("Prefab");
            set => _znet.m_zdo.Set("Prefab", value);
        }

        public string _ZDOData
        {
            get => _znet.m_zdo.GetString("ZDOData");
            set => _znet.m_zdo.Set("ZDOData", value);
        }

        public long _Creator
        {
            get => _znet.m_zdo.GetLong("Creator");
            set => _znet.m_zdo.Set("Creator", value);
        }

        public float _MaxTime
        {
            get => _znet.m_zdo.GetFloat("MaxTime");
            set => _znet.m_zdo.Set("MaxTime", value);
        }

        public float _Time
        {
            get => _znet.m_zdo.GetFloat("Time");
            set => _znet.m_zdo.Set("Time", value);
        }

        public float _Health
        {
            get => _znet.m_zdo.GetFloat(ZDOVars.s_health);
            set => _znet.m_zdo.Set(ZDOVars.s_health, value);
        }

        private float MinY, MaxY;

        private void Awake()
        {
            _znet = GetComponent<ZNetView>();
            if (!_znet.IsValid()) return;
            PieceObject = transform.Find("PieceObject");
            _znet.Register<string, long, float, int, string>("Setup", RPC_Setup);
            CreatePieceObject(_Prefab, _Health > 0);
        }

        public void Setup(string prefab, long creatorID, float maxTime, string zdodata, int health = 0) =>
            _znet.InvokeRPC(ZNetView.Everybody, "Setup", prefab, creatorID, maxTime, health, zdodata ?? "");

        private void RPC_Setup(long sender, string prefab, long creator, float maxTime, int health, string zdoData)
        {
            if (_znet.IsOwner())
            {
                _Prefab = prefab;
                _Creator = creator;
                _MaxTime = maxTime;
                _Health = health;
                if (!string.IsNullOrEmpty(zdoData)) _ZDOData = zdoData;
            }
            CreatePieceObject(prefab, false);
        }

        private static readonly LayerMask layer_nonsolid = LayerMask.NameToLayer("piece_nonsolid");
        private static readonly LayerMask layer = LayerMask.NameToLayer("piece");

        private void CreatePieceObject(string prefab, bool isSolid)
        {
            if (string.IsNullOrEmpty(prefab)) return;
            foreach (Transform child in PieceObject)
                Destroy(child.gameObject);

            Collider[] colliders = PieceObject.GetComponentsInChildren<Collider>();
            foreach (Collider collider in colliders) Destroy(collider);

            PieceObject.gameObject.SetActive(false);
            GameObject piece = ZNetScene.instance.GetPrefab(prefab);
            if (piece.GetComponent<Piece>() is {} p) GetComponent<Piece>().m_resources = p.m_resources;
 
            if (piece.GetComponent<BoxCollider>())
                Utils.CopyComponent(piece.GetComponent<BoxCollider>(), PieceObject.gameObject);

            PieceObject.gameObject.layer = isSolid ? layer : layer_nonsolid;
            foreach (Transform o in piece.transform)
            {
                if (!o.gameObject.activeSelf && o.tag != "snappoint") continue;
                GameObject newObject = Instantiate(o.gameObject, PieceObject);
                newObject.transform.localPosition = o.localPosition;
                newObject.transform.localRotation = o.localRotation;
                newObject.transform.localScale = o.localScale;
                newObject.layer = isSolid ? layer : layer_nonsolid;
            }

            bool checkComponent(Component component) =>
                component is Renderer or MeshFilter or Transform or Animator or Collider;

            foreach (Component comp in PieceObject.GetComponentsInChildren<Component>(true).Reverse())
                if (!checkComponent(comp))
                    Destroy(comp);

            foreach (Collider collider in PieceObject.GetComponentsInChildren<Collider>())
            {
                collider.isTrigger = !isSolid;
            }

            Renderer[] rendereres = PieceObject.GetComponentsInChildren<Renderer>(false);
            MinY = -99999999f;
            MaxY = 99999999f;
            for (int i = 0; i < rendereres.Length; i++)
            {
                if (rendereres[i].name == "largelod")
                {
                    rendereres[i].gameObject.SetActive(false);
                    continue;
                }

                if (rendereres[i] is ParticleSystemRenderer ps)
                {
                    ps.enabled = false;
                    continue;
                }

                if (rendereres[i] is MeshRenderer mesh)
                    mesh.shadowCastingMode = ShadowCastingMode.Off;

                if (rendereres[i] is SkinnedMeshRenderer skinnedmesh)
                    skinnedmesh.shadowCastingMode = ShadowCastingMode.Off;

                Bounds worldBounds = rendereres[i].bounds;
                MinY = Mathf.Max(MinY, worldBounds.min.y);
                MaxY = Mathf.Min(MaxY, worldBounds.max.y);
            }

            if (MinY > MaxY) (MinY, MaxY) = (MaxY, MinY);
            
            foreach (Material material in rendereres.SelectMany(renderer => renderer.materials))
            {
                material.shader = _shader;
                _materials.Add(material);
            }

            PieceObject.gameObject.SetActive(true);
            while (PieceObject.childCount > 0)
            {
                Transform child = PieceObject.GetChild(0);
                child.SetParent(transform, true);
            }
            /*if (isSolid)
            {
                WearNTear wnt = gameObject.AddComponent<WearNTear>();
                wnt.m_autoCreateFragments = false;
            }*/ 
        }
        private void FixedUpdate()
        {
            if (_znet.IsOwner())
            {
                float time = _Time;
                time += Time.fixedDeltaTime;
                _Time = time;
                if (time >= _MaxTime)
                { 
                    GameObject orig = ZNetScene.instance.GetPrefab(_Prefab);
                    if (orig)
                    {
                        GameObject newObj = Instantiate(orig, transform.position, transform.rotation);
                        Piece p = newObj.GetComponent<Piece>();
                        if (p)
                        {
                            p.SetCreator(_Creator);
                            p.m_placeEffect.Create(p.transform.position, p.transform.rotation, p.transform);  
                            if (p.GetComponent<ItemDrop>() is {} item) item.MakePiece(true);
                        }
                        try
                        {
                            string zdoData = _ZDOData;
                            if (!string.IsNullOrEmpty(zdoData) && newObj.GetComponent<ZNetView>() is {} znv)
                            {
                                znv.m_zdo.DeserializeZDO(new(zdoData));
                            }
                        } catch (Exception e) { kg_Blueprint.Logger.LogError(e); }
                    }
                    _znet.Destroy();
                    return; 
                }
            }
            float progress = _Time / _MaxTime;
            float setCurrentWorldSpaceVisibility = Mathf.Lerp(MinY, MaxY, progress);
            foreach (Material material in _materials)
                material.SetFloat(Visibility, setCurrentWorldSpaceVisibility);
        }

        public string GetHoverText() => $"$kg_blueprint_building... (<color=yellow>{Math.Round(_MaxTime - _Time, 0)}s</color>)".Localize();
        public string GetHoverName() => "";
        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;
    }
}