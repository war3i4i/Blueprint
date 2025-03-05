using UnityEngine.EventSystems;

namespace kg_Blueprint.Managers;

public static class ModelPreview
{
    private static readonly Camera renderCamera;
    private static readonly Light Light;
    private static readonly Vector3 SpawnPoint = new(25000f, 25000f, 25000f);
    private static float OriginalYPos;
    private static float OriginalXPos;
    private static float OriginalCameraZPos;
    private static GameObject CurrentPreviewGO;
    private static readonly MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
    static ModelPreview()
    {
        renderCamera = new GameObject("Blueprint_ModelPreviewCamera", typeof(Camera)).GetComponent<Camera>();
        renderCamera.backgroundColor = new Color(0, 0, 0, 0);
        renderCamera.clearFlags = CameraClearFlags.SolidColor;
        renderCamera.transform.position = SpawnPoint;
        renderCamera.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        renderCamera.fieldOfView = 1f;
        renderCamera.farClipPlane = 100000;
        renderCamera.targetTexture = new RenderTexture(2048, 2048, 32);
        Object.DontDestroyOnLoad(renderCamera);
        Light = new GameObject("Blueprint_ModelPreviewLight", typeof(Light)).GetComponent<Light>();
        Light.transform.position = SpawnPoint;
        Light.transform.rotation = Quaternion.Euler(5f, 180f, 5f);
        Light.type = LightType.Point;
        Light.intensity = 2f;
        Light.range = 30f;
        Object.DontDestroyOnLoad(Light);
        renderCamera.gameObject.SetActive(false);
        Light.gameObject.SetActive(false);
    }

    private static float currentMaxSize;

    public static void SetAsCurrent(RawImage img, GameObject go)
    {
        StopPreview();
        if (!go) return;
        img.texture = renderCamera.targetTexture;
        CurrentPreviewGO = go;
        CurrentPreviewGO.transform.position = Vector3.zero;
        CurrentPreviewGO.transform.rotation = Quaternion.Euler(23f, 51f, 25.8f);
        CurrentPreviewGO.SetActive(true);

        Vector3 min = new Vector3(1000f, 1000f, 1000f);
        Vector3 max = new Vector3(-1000f, -1000f, -1000f);
        foreach (Renderer meshRenderer in CurrentPreviewGO.GetComponentsInChildren<Renderer>())
        {
            if (meshRenderer is ParticleSystemRenderer) continue;
            min = Vector3.Min(min, meshRenderer.bounds.min);
            max = Vector3.Max(max, meshRenderer.bounds.max);
        }
        CurrentPreviewGO.transform.position = SpawnPoint - (min + max) / 2f;
        OriginalYPos = CurrentPreviewGO.transform.position.y;
        OriginalXPos = CurrentPreviewGO.transform.position.x;
        Light.transform.position = SpawnPoint + new Vector3(0, 2f, 2f);

        Vector3 size = new Vector3(Mathf.Abs(min.x) + Mathf.Abs(max.x), Mathf.Abs(min.y) + Mathf.Abs(max.y), Mathf.Abs(min.z) + Mathf.Abs(max.z));
        float maxMeshSize = Mathf.Max(size.x, size.y) + 0.1f;
        currentMaxSize = maxMeshSize;
        float distance = maxMeshSize / Mathf.Tan(renderCamera.fieldOfView * Mathf.Deg2Rad) * 1f;
        renderCamera.transform.position = SpawnPoint + new Vector3(0, 0f, distance);
        OriginalCameraZPos = renderCamera.transform.position.z;
        renderCamera.gameObject.SetActive(true);
        Light.gameObject.SetActive(true);
    }

    public static void StopPreview()
    {
        if (CurrentPreviewGO)
        {
            Object.Destroy(CurrentPreviewGO);
            CurrentPreviewGO = null;
        }

        renderCamera.gameObject.SetActive(false);
        Light.gameObject.SetActive(false);
    }

    public class PreviewModelAngleController : MonoBehaviour, IDragHandler
    {
        public void OnDrag(PointerEventData eventData) 
        {
            if (!CurrentPreviewGO) return;
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                CurrentPreviewGO.transform.Rotate(new Vector3(0, 1, 0), -eventData.delta.x / 8f, Space.World);
                CurrentPreviewGO.transform.Rotate(new Vector3(-1, 0, 0), eventData.delta.y / 12f, Space.World);
            }
            if (eventData.button == PointerEventData.InputButton.Middle)
            {
                float currentY = CurrentPreviewGO.transform.position.y;
                float newY = currentY + (eventData.delta.y / 400f * currentMaxSize * 0.16f);
                newY = Mathf.Clamp(newY, OriginalYPos - 2f * currentMaxSize * 0.16f, OriginalYPos + 2f * currentMaxSize * 0.16f);
                float currentX = CurrentPreviewGO.transform.position.x;
                float newX = currentX - (eventData.delta.x / 400f * currentMaxSize * 0.16f);
                newX = Mathf.Clamp(newX, OriginalXPos - 2f * currentMaxSize * 0.16f, OriginalXPos + 2f * currentMaxSize * 0.16f);
                Vector3 position = CurrentPreviewGO.transform.position;
                position = new Vector3(newX, newY, position.z);
                CurrentPreviewGO.transform.position = position;
            }
        }
        public void Update()
        {
            if (!CurrentPreviewGO) return;
            float ScrollWheel = Input.GetAxis("Mouse ScrollWheel");
            if (ScrollWheel == 0) return;
            bool isMouseInside = RectTransformUtility.RectangleContainsScreenPoint(this.transform as RectTransform, Input.mousePosition);
            if (!isMouseInside) return;
            float currentZ = renderCamera.transform.position.z;
            float newZ = currentZ - (ScrollWheel * 400f * currentMaxSize * 0.16f);
            newZ = Mathf.Clamp(newZ, OriginalCameraZPos - currentMaxSize * 40f, OriginalCameraZPos + currentMaxSize * 40f);
            Vector3 position = renderCamera.transform.position;
            position = new Vector3(position.x, position.y, newZ); 
            renderCamera.transform.position = position;
        } 
    }
}