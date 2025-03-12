namespace kg_Blueprint;

public static class PhotoManager
{
    private static readonly GameObject INACTIVE;
    private static readonly Camera rendererCamera;
    private static readonly Light Light;
    private static readonly Vector3 SpawnPoint = new(10000f, 10000f, 10000f);
    private class RenderObject(GameObject spawn, Vector3 size)
    {
        public readonly GameObject Spawn = spawn;
        public readonly Vector3 Size = size;
        public RenderRequest Request; 
    }
    private class RenderRequest(GameObject target)
    {
        public readonly GameObject Target = target;
        public int Width { get; set; } = 256;
        public int Height { get; set; } = 256;
        public float FieldOfView { get; set; } = 0.5f;
        public float DistanceMultiplier { get; set; } = 1f;
    }

    private const int MAINLAYER = 31;

    static PhotoManager()
    {
        INACTIVE = new GameObject("INACTIVEscreenshotHelper")
        {
            layer = MAINLAYER,
            transform =
            {
                localScale = Vector3.one
            }
        };
        INACTIVE.SetActive(false);
        Object.DontDestroyOnLoad(INACTIVE);
        rendererCamera = new GameObject("Render Camera", typeof(Camera)).GetComponent<Camera>();
        rendererCamera.backgroundColor = new Color(0, 0, 0, 0);
        rendererCamera.clearFlags = CameraClearFlags.SolidColor;
        rendererCamera.transform.position = SpawnPoint;
        rendererCamera.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        rendererCamera.fieldOfView = 0.5f;
        rendererCamera.farClipPlane = 100000;
        rendererCamera.cullingMask = 1 << MAINLAYER;
        Object.DontDestroyOnLoad(rendererCamera);

        Light = new GameObject("Render Light", typeof(Light)).GetComponent<Light>();
        Light.transform.position = SpawnPoint;
        Light.transform.rotation = Quaternion.Euler(5f, 180f, 5f);
        Light.type = LightType.Directional;
        Light.intensity = 2f; 
        Light.cullingMask = 1 << MAINLAYER;
        Object.DontDestroyOnLoad(Light);

        rendererCamera.gameObject.SetActive(false);
        Light.gameObject.SetActive(false);
    }
 
    private static void ClearRendering()
    {
        rendererCamera.gameObject.SetActive(false);
        Light.gameObject.SetActive(false);
    }

    private static bool IsVisualComponent(Component component)
    {
        return component is Renderer or MeshFilter or Transform;
    }
     
    private static GameObject SpawnAndRemoveComponents(RenderRequest obj)
    {
        GameObject tempObj = Object.Instantiate(obj.Target, INACTIVE.transform);
        foreach (Component comp in tempObj.GetComponentsInChildren<Component>(true).Reverse())
        {
            if (!IsVisualComponent(comp)) Object.DestroyImmediate(comp);
        }
        tempObj.layer = MAINLAYER;
        foreach (Transform VARIABLE in tempObj.GetComponentsInChildren<Transform>()) VARIABLE.gameObject.layer = MAINLAYER;
        tempObj.transform.SetParent(null);
        tempObj.SetActive(true);
        tempObj.name = obj.Target.name;
        return tempObj;
    }
    
    public static Texture2D[] MakeBulkSprites(GameObject prefabArg, float scale, params Quaternion[] rotations)
    {
        try
        { 
            Texture2D[] tex = new Texture2D[rotations.Length];
            rendererCamera.gameObject.SetActive(true);
            Light.gameObject.SetActive(true);
            RenderRequest request = new(prefabArg) { DistanceMultiplier = scale };
            GameObject spawn = SpawnAndRemoveComponents(request);
            Renderer[] renderers = spawn.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < rotations.Length; i++)
            {
                spawn.transform.position = Vector3.zero;
                spawn.transform.rotation = rotations[i];
                Vector3 min = new Vector3(1000f, 1000f, 1000f);
                Vector3 max = new Vector3(-1000f, -1000f, -1000f); 
                foreach (Renderer meshRenderer in renderers) 
                {
                    if (meshRenderer is ParticleSystemRenderer) continue; 
                    min = Vector3.Min(min, meshRenderer.bounds.min);
                    max = Vector3.Max(max, meshRenderer.bounds.max);
                }
                spawn.transform.position = SpawnPoint - (min + max) / 2f;
                Vector3 size = new Vector3(Mathf.Abs(min.x) + Mathf.Abs(max.x), Mathf.Abs(min.y) + Mathf.Abs(max.y), Mathf.Abs(min.z) + Mathf.Abs(max.z));
                RenderObject go = new RenderObject(spawn, size) { Request = request };
                tex[i] = RenderSprite(go);
            }
            Object.DestroyImmediate(spawn);
            ClearRendering();
            return tex;
        } 
        catch (Exception)
        {
            ClearRendering();
            return null; 
        }
    }
    private static Texture2D RenderSprite(RenderObject renderObject)
    {
        int width = renderObject.Request.Width;
        int height = renderObject.Request.Height;
        RenderTexture oldRenderTexture = RenderTexture.active;
        RenderTexture temp = RenderTexture.GetTemporary(width, height, 32);
        rendererCamera.targetTexture = temp;
        rendererCamera.fieldOfView = renderObject.Request.FieldOfView;
        RenderTexture.active = rendererCamera.targetTexture;
        float maxMeshSize = Mathf.Max(renderObject.Size.x, renderObject.Size.y) + 0.1f;
        float distance = maxMeshSize / Mathf.Tan(rendererCamera.fieldOfView * Mathf.Deg2Rad) * renderObject.Request.DistanceMultiplier;
        rendererCamera.transform.position = SpawnPoint + new Vector3(0, 0, distance);
        rendererCamera.Render();
        Texture2D previewImage = new Texture2D(width, height, TextureFormat.RGBA32, false);
        previewImage.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        previewImage.Apply();
        RenderTexture.active = oldRenderTexture;
        rendererCamera.targetTexture = null;
        RenderTexture.ReleaseTemporary(temp); 
        return previewImage;
    }
}