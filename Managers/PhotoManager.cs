namespace kg_Blueprint;

public static class PhotoManager
{
    private static readonly int Hue = Shader.PropertyToID("_Hue");
    private static readonly int Saturation = Shader.PropertyToID("_Saturation");
    private static readonly int Value = Shader.PropertyToID("_Value");

    private static readonly GameObject INACTIVE;
    private static readonly Camera rendererCamera;
    private static readonly Light Light;
    private static readonly Vector3 SpawnPoint = new(10000f, 10000f, 10000f);


    private class RenderObject
    {
        public readonly GameObject Spawn;
        public readonly Vector3 Size;
        public RenderRequest Request;

        public RenderObject(GameObject spawn, Vector3 size)
        {
            Spawn = spawn;
            Size = size;
        }
    }

    private class RenderRequest
    {
        public readonly GameObject Target;
        public int Width { get; set; } = 512;
        public int Height { get; set; } = 512;
        public Quaternion Rotation { get; set; } = Quaternion.Euler(0f, -24f, 0); //25.8f);
        public float FieldOfView { get; set; } = 0.5f;
        public float offset = 0.25f;

        public float DistanceMultiplier { get; set; } = 1f;

        public RenderRequest(GameObject target)
        {
            Target = target;
        }
    }

    private const int MAINLAYER = 29;

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
        Light.intensity = 0.5f;
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
        return component is Renderer or MeshFilter or Transform or Animator or LevelEffects;
    }


    private static GameObject SpawnAndRemoveComponents(RenderRequest obj)
    {
        GameObject tempObj = Object.Instantiate(obj.Target, INACTIVE.transform);
        List<Component> components = tempObj.GetComponentsInChildren<Component>(true).ToList();
        List<Component> ToRemove = [];
        foreach (Component comp in components)
        {
            if (!IsVisualComponent(comp))
            {
                ToRemove.Add(comp);
            }
        }

        ToRemove.Reverse();
        ToRemove.ForEach(Object.DestroyImmediate);
        GameObject retObj = Object.Instantiate(tempObj);
        retObj.layer = MAINLAYER;
        foreach (Transform VARIABLE in retObj.GetComponentsInChildren<Transform>())
        {
            VARIABLE.gameObject.layer = MAINLAYER;
        }

        Animator animator = retObj.GetComponentInChildren<Animator>();
        if (animator)
        {
            if (animator.HasState(0, Movement))
                animator.Play(Movement); 
            animator.Update(0f);
        }

        retObj.SetActive(true);
        retObj.name = obj.Target.name;
        Object.Destroy(tempObj);
        return retObj;
    }
    

    private static readonly int Movement = Animator.StringToHash("Movement");


    public static string MakeSprite(GameObject prefabArg, float scale, float offset, Quaternion rotation)
    {
        try
        {
            int hashcode = prefabArg.name.GetStableHashCode();
            rendererCamera.gameObject.SetActive(true);
            Light.gameObject.SetActive(true);
            RenderRequest request = new(prefabArg) { DistanceMultiplier = scale, offset = offset, Rotation = rotation };
            GameObject spawn = SpawnAndRemoveComponents(request);
            spawn.transform.position = Vector3.zero;
            spawn.transform.rotation = request.Rotation;

            Vector3 min = new Vector3(1000f, 1000f, 1000f);
            Vector3 max = new Vector3(-1000f, -1000f, -1000f);
            foreach (Renderer meshRenderer in spawn.GetComponentsInChildren<Renderer>())
            {
                if (meshRenderer is ParticleSystemRenderer) continue;
                min = Vector3.Min(min, meshRenderer.bounds.min);
                max = Vector3.Max(max, meshRenderer.bounds.max);
            }

            spawn.transform.position = SpawnPoint - (min + max) / 2f;
            Vector3 size = new Vector3(Mathf.Abs(min.x) + Mathf.Abs(max.x), Mathf.Abs(min.y) + Mathf.Abs(max.y), Mathf.Abs(min.z) + Mathf.Abs(max.z));
            TimedDestruction timedDestruction = spawn.AddComponent<TimedDestruction>();
            timedDestruction.Trigger(1f);

            RenderObject go = new RenderObject(spawn, size)
            {
                Request = request
            };
            string base64 = RenderSprite(go);
            ClearRendering();
            return base64;
        }
        catch (Exception ex)
        {
            ClearRendering();
            return null;
        }
    }

    private static string RenderSprite(RenderObject renderObject)
    {
        int width = renderObject.Request.Width;
        int height = renderObject.Request.Height;

        RenderTexture oldRenderTexture = RenderTexture.active;
        RenderTexture temp = RenderTexture.GetTemporary(width, height, 32);
        rendererCamera.targetTexture = temp;
        rendererCamera.fieldOfView = renderObject.Request.FieldOfView;
        RenderTexture.active = rendererCamera.targetTexture;

        renderObject.Spawn.SetActive(true);
        float maxMeshSize = Mathf.Max(renderObject.Size.x, renderObject.Size.y) + 0.1f;
        float distance = maxMeshSize / Mathf.Tan(rendererCamera.fieldOfView * Mathf.Deg2Rad) * renderObject.Request.DistanceMultiplier;
        rendererCamera.transform.position = SpawnPoint + new Vector3(0, renderObject.Request.offset, distance);
        rendererCamera.Render();
        renderObject.Spawn.SetActive(false);
        Object.Destroy(renderObject.Spawn);
        Texture2D previewImage = new Texture2D(width, height, TextureFormat.RGBA32, false);
        previewImage.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        previewImage.Apply();
        RenderTexture.active = oldRenderTexture;
        rendererCamera.targetTexture = null;
        RenderTexture.ReleaseTemporary(temp); 
        rendererCamera.gameObject.SetActive(false);
        Light.gameObject.SetActive(false);
        return Convert.ToBase64String(previewImage.EncodeToPNG());
    }
}