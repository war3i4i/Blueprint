using System.Runtime.CompilerServices;

namespace kg_Blueprint;

public static class Utils
{
    public static void ThrowIfBad(this IList<GameObject> pieces, string Name, [CallerMemberName] string caller = "", [CallerLineNumber] int line = 0)
    {
        string msg = $"[{Name}] {caller}({line})";
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
            for (int i = 0; i < types.Length; ++i) if (collider.GetComponentInParent(types[i]) is {} p) hs.Add(p.gameObject);
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
    public static IEnumerator WaitFrames(int frames) { frames = Mathf.Max(4, frames); for (int i = 0; i < frames; ++i) yield return null; }
}