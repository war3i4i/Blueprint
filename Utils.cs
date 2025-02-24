using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace kg_Blueprint;

public static class Utils
{
    public static void dbg_PrintAll<T>(this IEnumerable<T> list, [CallerMemberName] string caller = "", [CallerLineNumber] int line = 0)
    {
        string msg = $"{caller}({line})";
        if (list == null)
        {
            kg_Blueprint.Logger.LogDebug($"List is null [{msg}]");
            return;
        }

        kg_Blueprint.Logger.LogDebug($"Printing list of {typeof(T).Name} [{msg}]");
        foreach (T t in list) kg_Blueprint.Logger.LogDebug(t);
    }

    public static void ThrowIfBad(this IList<GameObject> pieces, [CallerMemberName] string caller = "", [CallerLineNumber] int line = 0)
    {
        string msg = $"{caller}({line})";
        if (pieces == null || pieces.Count == 0) throw new Exception($"No pieces found [{msg}]");
        if (pieces.Any(t => t == null)) throw new Exception($"List contains a null piece [{msg}]");
    }

    public static string Localize(this string str) => Localization.instance.Localize(str);
    public static string Localize(this string str, params string[] args) => Localization.instance.Localize(str, args);

    public static string ValidPath(this string path, [CallerMemberName] string caller = "", [CallerLineNumber] int line = 0)
    {
        string msg = $"{caller}({line})";
        if (string.IsNullOrWhiteSpace(path)) throw new Exception($"Path is null or empty [{msg}]");
        return Path.GetInvalidPathChars().Aggregate(path, (current, c) => current.Replace(c.ToString(), string.Empty));
    }

    public static void WriteNoDupes(this string path, string data, bool forget)
    {
        if (forget) Task.Run(() => File.WriteAllText(path, data));
        else File.WriteAllText(path, data);
    }

    public static void WriteWithDupes(this string path, string data, bool forget)
    {
        if (forget) Task.Run(() => WriteWithDupes_Internal(path, data));
        else WriteWithDupes_Internal(path, data);
    }

    private static void WriteWithDupes_Internal(this string path, string data)
    {
        string directory = Path.GetDirectoryName(path)!;
        string fileNameNoExt = Path.GetFileNameWithoutExtension(path);
        string ext = Path.GetExtension(path);
        int increment = 1;
        string newPath = path;
        while (File.Exists(newPath)) newPath = Path.Combine(directory, $"{fileNameNoExt} ({increment++}){ext}");
        File.WriteAllText(newPath, data);
    }

    private static readonly LayerMask Layer = LayerMask.GetMask("piece", "piece_nonsolid", "Default", "character_noenv", "character");

    public static GameObject[] GetObjectsInside(this BoxCollider box, GameObject[] exclude, params Type[] types)
    {
        box.gameObject.SetActive(true);
        HashSet<GameObject> hs = [];
        Vector3 center = box.transform.position + box.center;
        Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, box.transform.lossyScale);
        Quaternion rotation = box.transform.rotation;
        Collider[] colliders = Physics.OverlapBox(center, halfExtents, rotation, Layer);
        box.gameObject.SetActive(false);
        foreach (Collider collider in colliders)
            for (int i = 0; i < types.Length; ++i)
                if (collider.GetComponentInParent(types[i]) is { } p)
                    hs.Add(p.gameObject);
        if (exclude != null) hs.ExceptWith(exclude);
        GameObject[] result = new GameObject[hs.Count];
        hs.CopyTo(result);
        return result;
    }

    public static GameObject[] GetObjectsInsideCylinder(Vector3 center, float radius, float height, GameObject[] exclude, params Type[] types)
    {
        HashSet<GameObject> hs = [];
        Collider[] colliders = Physics.OverlapCapsule(center, center + Vector3.up * height, radius, Layer);
        foreach (Collider collider in colliders)
            for (int i = 0; i < types.Length; ++i)
                if (collider.GetComponentInParent(types[i]) is { } p)
                    hs.Add(p.gameObject);
        if (exclude != null) hs.ExceptWith(exclude);
        GameObject[] result = new GameObject[hs.Count];
        hs.CopyTo(result);
        return result;
    }

    public static void CopyComponent<T>(T original, GameObject destination) where T : Component
    {
        Type type = original.GetType();
        Component copy = destination.AddComponent(type);
        try
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                       BindingFlags.Default | BindingFlags.DeclaredOnly;
            PropertyInfo[] pinfos = type.GetProperties(flags);
            foreach (PropertyInfo pinfo in pinfos)
                if (pinfo.CanWrite)
                    pinfo.SetValue(copy, pinfo.GetValue(original, null), null);

            FieldInfo[] fields = type.GetFields(flags);
            foreach (FieldInfo field in fields) field.SetValue(copy, field.GetValue(original));
        }
        catch
        {
            // ignored
        }
    }

    public static Texture2D ToIcon(this string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        try
        {
            byte[] bytes = Convert.FromBase64String(s);
            Texture2D tex = new(2, 2);
            tex.LoadImage(bytes);
            return tex;
        }
        catch
        {
            return null;
        }
    }

    public static void RemoveAllChildrenExceptFirst(this Transform t)
    {
        for (int i = t.childCount - 1; i > 0; --i)
        {
            Object.DestroyImmediate(t.GetChild(i).gameObject);
        }
    }

    public static bool IsInside(this BoxCollider box, Vector3 point)
    {
        box.gameObject.SetActive(true);
        Vector3 closestPoint = box.ClosestPoint(point);
        bool result = Vector3.Distance(closestPoint, point) < 0.2f;
        box.gameObject.SetActive(false);
        return result;
    }

    public static void Explorer_SelectFile(this string path)
    {
        if (!File.Exists(path)) return;
        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
    }

    public static void SerializeZDO(this ZDO zdo, ZPackage pkg)
    {
        List<KeyValuePair<int, float>> saveFloats = ZDOExtraData.GetFloats(zdo.m_uid);
        List<KeyValuePair<int, Vector3>> saveVec3s = ZDOExtraData.GetVec3s(zdo.m_uid);
        List<KeyValuePair<int, Quaternion>> saveQuaternions = ZDOExtraData.GetQuaternions(zdo.m_uid);
        List<KeyValuePair<int, int>> saveInts = ZDOExtraData.GetInts(zdo.m_uid);
        List<KeyValuePair<int, long>> saveLongs = ZDOExtraData.GetLongs(zdo.m_uid);
        List<KeyValuePair<int, string>> saveStrings = ZDOExtraData.GetStrings(zdo.m_uid);
        List<KeyValuePair<int, byte[]>> saveByteArrays = ZDOExtraData.GetByteArrays(zdo.m_uid);
        pkg.Write(saveFloats.Count > 0);
        if (saveFloats.Count > 0)
        {
            pkg.WriteNumItems(saveFloats.Count);
            foreach (KeyValuePair<int, float> keyValuePair in saveFloats)
            {
                pkg.Write(keyValuePair.Key);
                pkg.Write(keyValuePair.Value);
            }
        }

        pkg.Write(saveVec3s.Count > 0);
        if (saveVec3s.Count > 0)
        {
            pkg.WriteNumItems(saveVec3s.Count);
            foreach (KeyValuePair<int, Vector3> keyValuePair in saveVec3s)
            {
                pkg.Write(keyValuePair.Key);
                pkg.Write(keyValuePair.Value);
            }
        }

        pkg.Write(saveQuaternions.Count > 0);
        if (saveQuaternions.Count > 0)
        {
            pkg.WriteNumItems(saveQuaternions.Count);
            foreach (KeyValuePair<int, Quaternion> keyValuePair in saveQuaternions)
            {
                pkg.Write(keyValuePair.Key);
                pkg.Write(keyValuePair.Value);
            }
        }

        pkg.Write(saveInts.Count > 0);
        if (saveInts.Count > 0)
        {
            pkg.WriteNumItems(saveInts.Count);
            foreach (KeyValuePair<int, int> keyValuePair in saveInts)
            {
                pkg.Write(keyValuePair.Key);
                pkg.Write(keyValuePair.Value);
            }
        }

        pkg.Write(saveLongs.Count > 0);
        if (saveLongs.Count > 0)
        {
            pkg.WriteNumItems(saveLongs.Count);
            foreach (KeyValuePair<int, long> keyValuePair in saveLongs)
            {
                pkg.Write(keyValuePair.Key);
                pkg.Write(keyValuePair.Value);
            }
        }

        pkg.Write(saveStrings.Count > 0);
        if (saveStrings.Count > 0)
        {
            pkg.WriteNumItems(saveStrings.Count);
            foreach (KeyValuePair<int, string> keyValuePair in saveStrings)
            {
                pkg.Write(keyValuePair.Key);
                pkg.Write(keyValuePair.Value);
            }
        }

        pkg.Write(saveByteArrays.Count > 0);
        if (saveByteArrays.Count > 0)
        {
            pkg.WriteNumItems(saveByteArrays.Count);
            foreach (KeyValuePair<int, byte[]> keyValuePair in saveByteArrays)
            {
                pkg.Write(keyValuePair.Key);
                pkg.Write(keyValuePair.Value);
            }
        }
    }

    public static void DeserializeZDO(this ZDO zdo, ZPackage pkg)
    {
        if (pkg.ReadBool())
        {
            int numItems = pkg.ReadNumItems();
            ZDOExtraData.Reserve(zdo.m_uid, ZDOExtraData.Type.Float, numItems);
            for (int i = 0; i < numItems; i++)
            {
                int key = pkg.ReadInt();
                float value = pkg.ReadSingle();
                ZDOExtraData.Set(zdo.m_uid, key, value);
            }
        }

        if (pkg.ReadBool())
        {
            int numItems = pkg.ReadNumItems();
            ZDOExtraData.Reserve(zdo.m_uid, ZDOExtraData.Type.Vec3, numItems);
            for (int i = 0; i < numItems; i++)
            {
                int key = pkg.ReadInt();
                Vector3 value = pkg.ReadVector3();
                ZDOExtraData.Set(zdo.m_uid, key, value);
            }
        }

        if (pkg.ReadBool())
        {
            int numItems = pkg.ReadNumItems();
            ZDOExtraData.Reserve(zdo.m_uid, ZDOExtraData.Type.Quat, numItems);
            for (int i = 0; i < numItems; i++)
            {
                int key = pkg.ReadInt();
                Quaternion value = pkg.ReadQuaternion();
                ZDOExtraData.Set(zdo.m_uid, key, value);
            }
        }

        if (pkg.ReadBool())
        {
            int numItems = pkg.ReadNumItems();
            ZDOExtraData.Reserve(zdo.m_uid, ZDOExtraData.Type.Int, numItems);
            for (int i = 0; i < numItems; i++)
            {
                int key = pkg.ReadInt();
                int value = pkg.ReadInt();
                ZDOExtraData.Set(zdo.m_uid, key, value);
            }
        }

        if (pkg.ReadBool())
        {
            int numItems = pkg.ReadNumItems();
            ZDOExtraData.Reserve(zdo.m_uid, ZDOExtraData.Type.Long, numItems);
            for (int i = 0; i < numItems; i++)
            {
                int key = pkg.ReadInt();
                long value = pkg.ReadLong();
                ZDOExtraData.Set(zdo.m_uid, key, value);
            }
        }

        if (pkg.ReadBool())
        {
            int numItems = pkg.ReadNumItems();
            ZDOExtraData.Reserve(zdo.m_uid, ZDOExtraData.Type.String, numItems);
            for (int i = 0; i < numItems; i++)
            {
                int key = pkg.ReadInt();
                string value = pkg.ReadString();
                ZDOExtraData.Set(zdo.m_uid, key, value);
            }
        }

        if (pkg.ReadBool())
        {
            int numItems = pkg.ReadNumItems();
            ZDOExtraData.Reserve(zdo.m_uid, ZDOExtraData.Type.ByteArray, numItems);
            for (int i = 0; i < numItems; i++)
            {
                int key = pkg.ReadInt();
                byte[] value = pkg.ReadByteArray();
                ZDOExtraData.Set(zdo.m_uid, key, value);
            }
        }
    }

    public static void Register<T, U, V, B, C>(this ZNetView znv, string name, RoutedMethod<T, U, V, B, C>.Method f)
    {
        znv.m_functions.Add(name.GetStableHashCode(), new RoutedMethod<T, U, V, B, C>(f));
    }

    private static CraftingStation _internal_fakeStation;
    public static CraftingStation GetBlueprintFakeStation() => _internal_fakeStation ??= kg_Blueprint.Asset.LoadAsset<GameObject>("kg_BlueprintCS").GetComponent<CraftingStation>();

    public static IEnumerator WaitFrames(int frames)
    {
        frames = Mathf.Max(4, frames);
        for (int i = 0; i < frames; ++i) yield return null;
    }

    public static Piece.Requirement[] GetRequirements(this int[] Objects)
    {
        GameObject[] gameObjects = new GameObject[Objects.Length];
        for (int i = 0; i < Objects.Length; ++i) gameObjects[i] = ZNetScene.instance.GetPrefab(Objects[i]);
        Dictionary<ItemDrop, int> requirements = new Dictionary<ItemDrop, int>();
        for (int i = 0; i < gameObjects.Length; ++i)
        {
            if (!gameObjects[i]) continue;
            Piece p = gameObjects[i].GetComponent<Piece>();
            if (!p) continue;
            for (int r = 0; r < p.m_resources.Length; ++r)
            {
                if (requirements.ContainsKey(p.m_resources[r].m_resItem))
                    requirements[p.m_resources[r].m_resItem] += p.m_resources[r].m_amount;
                else
                    requirements[p.m_resources[r].m_resItem] = p.m_resources[r].m_amount;
            }
        }

        return requirements.Select(x => new Piece.Requirement() { m_resItem = x.Key, m_amount = x.Value }).ToArray();
    }

    public class NumberedData
    {
        public int Amount;
        public Sprite Icon;
    }

    public static IOrderedEnumerable<KeyValuePair<string, NumberedData>> GetPiecesNumbered(this int[] Objects)
    {
        GameObject[] pieces = new GameObject[Objects.Length];
        for (int i = 0; i < Objects.Length; ++i) pieces[i] = ZNetScene.instance.GetPrefab(Objects[i]);
        Dictionary<string, NumberedData> numbered = new Dictionary<string, NumberedData>();
        for (int i = 0; i < pieces.Length; ++i)
        {
            if (!pieces[i]) continue;
            Piece p = pieces[i].GetComponent<Piece>();
            Sprite icon = p?.m_icon;
            string name = p ? p.m_name.Localize() : pieces[i].name;
            if (numbered.TryGetValue(name, out var value)) value.Amount++;
            else numbered[name] = new NumberedData() { Amount = 1, Icon = icon };
        }

        return numbered.OrderByDescending(x => x.Value.Amount);
    }

    public static bool CreateBlueprint(this BlueprintSource source, string bpName, string bpDesc, string bpAuthor, Texture2D icon, out string reason)
    {
        Stopwatch dbg_watch = Stopwatch.StartNew();
        reason = null;
        Vector3 start = source.StartPoint;
        GameObject[] objects = source.GetObjectedInside;
        if (objects.Length == 0)
        {
            reason = "$kg_blueprint_createblueprint_no_objects";
            return false;
        }

        BlueprintRoot root = BlueprintRoot.CreateNew(source.SnapToLowest, bpName, bpDesc, bpAuthor, source.Rotation, start, objects, icon);
        Texture2D[] previews = source.CreatePreviews(objects);
        root.SetPreviews(previews);
        root.AssignPath(Path.Combine(kg_Blueprint.BlueprintsPath, bpName + ".yml"), false);
        root.Save();
        BlueprintUI.AddEntry(root, true);
        kg_Blueprint.Logger.LogDebug($"Blueprint {bpName} created in {dbg_watch.ElapsedMilliseconds}ms");
        return true;
    }

    public static GameObject CreateViewGameObjectForBlueprint(this BlueprintRoot root)
    {
        GameObject newObj = new GameObject("BlueprintPreview");
        newObj.transform.position = Vector3.zero;
        newObj.transform.rotation = Quaternion.identity;
        newObj.SetActive(false);
        newObj.name = "kg_Blueprint_Preview";
        for (int i = 0; i < root.Objects.Length; ++i)
        {
            BlueprintObject obj = root.Objects[i];
            GameObject prefab = ZNetScene.instance.GetPrefab(obj.Id);
            if (!prefab) continue;
            GameObject go = Object.Instantiate(prefab, newObj.transform);
            go.transform.position = obj.RelativePosition;
            go.transform.rotation = Quaternion.Euler(obj.Rotation);
            foreach (Component comp in go.GetComponentsInChildren<Component>(true).Reverse())
            {
                if (comp is not Renderer and not MeshFilter and not Transform and not Animator) Object.DestroyImmediate(comp);
            }
        }

        return newObj;
    }

    public class ThreeChoicesPopup(string header, string text, string option1, string option2, string option3, PopupButtonCallback first, PopupButtonCallback second, PopupButtonCallback third)
        : FixedPopupBase(header, text)
    {
        public static readonly PopupType _type = (PopupType)"kg_Blueprint_3ChoicePopup".GetStableHashCode();
        public override PopupType Type => _type;
        public readonly PopupButtonCallback firstChoice = first, secondChoice = second, thirdChoice = third;
        public string Option1 => option1.Localize();
        public string Option2 => option2.Localize();
        public string Option3 => option3.Localize();
    }

    private static void Show3ChoicesPopup(UnifiedPopup instance, ThreeChoicesPopup pop)
    {
        instance.headerText.text = pop.header.Localize();
        instance.bodyText.text = pop.text.Localize();
        instance.buttonLeftText.text = pop.Option1;
        instance.buttonLeft.gameObject.SetActive(true);
        instance.buttonLeft.onClick.AddListener(() => pop.firstChoice());
        instance.buttonCenterText.text = pop.Option2;
        instance.buttonCenter.gameObject.SetActive(true);
        instance.buttonCenter.onClick.AddListener(() => pop.secondChoice());
        instance.buttonRightText.text = pop.Option3;
        instance.buttonRight.gameObject.SetActive(true);
        instance.buttonRight.onClick.AddListener(() => pop.thirdChoice());
        
        var leftButton = instance.buttonLeft.GetComponent<RectTransform>();
        var centerButton = instance.buttonCenter.GetComponent<RectTransform>();
        var rightButton = instance.buttonRight.GetComponent<RectTransform>();
        leftButton.anchoredPosition -= new Vector2(50f, 0f);
        centerButton.sizeDelta = leftButton.sizeDelta;
        rightButton.anchoredPosition += new Vector2(50f, 0f);
    } 

    [HarmonyPatch(typeof(UnifiedPopup), nameof(UnifiedPopup.Show))]
    private static class UnifiedPopup_Show_Patch
    {
        private static void Postfix(UnifiedPopup __instance, PopupBase popup)
        {
            if (popup.Type == ThreeChoicesPopup._type) Show3ChoicesPopup(__instance, (ThreeChoicesPopup)popup);
        }
    }

    [HarmonyPatch(typeof(UnifiedPopup), nameof(UnifiedPopup.Awake))]
    private static class UnifiedPopup_Awake_Patch
    {
        public static readonly Vector2[] OrigPos = new Vector2[3];
        public static readonly Vector2[] OrigSize = new Vector2[3];

        private static void Postfix(UnifiedPopup __instance)
        {
            var leftButton = __instance.buttonLeft.GetComponent<RectTransform>();
            OrigPos[0] = leftButton.anchoredPosition;
            OrigSize[0] = leftButton.sizeDelta;
            var centerButton = __instance.buttonCenter.GetComponent<RectTransform>();
            OrigPos[1] = centerButton.anchoredPosition;
            OrigSize[1] = centerButton.sizeDelta;
            var rightButton = __instance.buttonRight.GetComponent<RectTransform>();
            OrigPos[2] = rightButton.anchoredPosition;
            OrigSize[2] = rightButton.sizeDelta;
        }
    }
    [HarmonyPatch(typeof(UnifiedPopup), nameof(UnifiedPopup.ResetUI))]
    private static class UnifiedPopup_ResetUI_Patch
    {
        private static void Postfix(UnifiedPopup __instance)
        {
            var leftButton = __instance.buttonLeft.GetComponent<RectTransform>();
            leftButton.anchoredPosition = UnifiedPopup_Awake_Patch.OrigPos[0];
            leftButton.sizeDelta = UnifiedPopup_Awake_Patch.OrigSize[0];
            var centerButton = __instance.buttonCenter.GetComponent<RectTransform>();
            centerButton.anchoredPosition = UnifiedPopup_Awake_Patch.OrigPos[1];
            centerButton.sizeDelta = UnifiedPopup_Awake_Patch.OrigSize[1];
            var rightButton = __instance.buttonRight.GetComponent<RectTransform>();
            rightButton.anchoredPosition = UnifiedPopup_Awake_Patch.OrigPos[2];
            rightButton.sizeDelta = UnifiedPopup_Awake_Patch.OrigSize[2];
        }
    }
}