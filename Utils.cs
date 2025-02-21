using System.IO.Compression;
using System.Runtime.CompilerServices;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace kg_Blueprint;

public static class Utils
{
    public static void ThrowIfBad(this IList<Piece> pieces, string Name, [CallerMemberName] string caller = "", [CallerLineNumber] int line = 0)
    {
        string msg = $"[{Name}] {caller}({line})";
        if (pieces == null || pieces.Count == 0) throw new Exception($"No pieces found [{msg}]");
        for (int i = 0; i < pieces.Count; ++i) if (pieces[i] == null) throw new Exception($"List contains a null piece [{msg}]");
    }
    public static int CustomCountItems(string prefab)
    {
        int num = 0;
        foreach (ItemDrop.ItemData itemData in Player.m_localPlayer.m_inventory.m_inventory)
        {
            if (itemData.m_dropPrefab.name == prefab)
            {
                num += itemData.m_stack;
            }
        }
        return num;
    }
    public static string Localize(this string str) => Localization.instance.Localize(str);
    public static string ValidPath(this string path, [CallerMemberName] string caller = "", [CallerLineNumber] int line = 0)
    {
        string msg = $"{caller}({line})";
        if (string.IsNullOrWhiteSpace(path)) throw new Exception($"Path is null or empty [{msg}]");
        return Path.GetInvalidPathChars().Aggregate(path, (current, c) => current.Replace(c.ToString(), string.Empty));
    }
    public static void WriteNoDupes(this string path, string data) => File.WriteAllText(path.ValidPath(), data);
    public static void WriteWithDupes(this string path, string data)
    {
        path = path.ValidPath();
        string directory = Path.GetDirectoryName(path);
        string fileNameNoExt = Path.GetFileNameWithoutExtension(path);
        string ext = Path.GetExtension(path);
        int increment = 1;
        string newPath = path;
        while (File.Exists(newPath)) newPath = Path.Combine(directory, $"{fileNameNoExt} ({increment++}){ext}");
        File.WriteAllText(newPath, data);
    }
    public static T[] GetObjectsInside<T>(this BoxCollider box, T exclude) where T : Component
    {
        box.gameObject.SetActive(true);
        HashSet<T> hs = [];
        Vector3 center = box.transform.position + box.center;
        Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, box.transform.lossyScale);
        Quaternion rotation = box.transform.rotation;
        Collider[] colliders = Physics.OverlapBox(center, halfExtents, rotation, Piece.s_pieceRayMask);
        box.gameObject.SetActive(false);
        foreach (Collider collider in colliders) if (collider.GetComponentInParent<T>() is {} p) hs.Add(p);
        hs.Remove(exclude);
        T[] result = new T[hs.Count];
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
    public static byte[] Compress(this byte[] array, [CallerMemberName] string caller = "", [CallerLineNumber] int line = 0)
    {
        if (array == null || array.Length == 0) throw new Exception($"Array is null or empty [{caller}({line})]");
        using (MemoryStream memoryStream = new MemoryStream())
        {
            using (GZipStream gzipStream = new GZipStream(memoryStream, CompressionLevel.Optimal)) gzipStream.Write(array, 0, array.Length);
            return memoryStream.ToArray();
        }
    }
    public static byte[] Decompress(this byte[] array, [CallerMemberName] string caller = "", [CallerLineNumber] int line = 0)
    {
        if (array == null || array.Length == 0) throw new Exception($"Array is null or empty [{caller}({line})]");
        using (MemoryStream memoryStream = new MemoryStream(array))
        {
            using (GZipStream gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
            {
                using (MemoryStream resultStream = new MemoryStream())
                {
                    gzipStream.CopyTo(resultStream);
                    byte[] result = resultStream.ToArray();
                    return result;
                }
            }
        }
    }
    public static void Explorer_SelectFile(this string path)
    {
        if (!File.Exists(path)) return;
        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
    }
    private static CraftingStation _internal_fakeStation;
    public static CraftingStation GetBlueprintFakeStation() => _internal_fakeStation ??= kg_Blueprint.Asset.LoadAsset<GameObject>("kg_BlueprintCS").GetComponent<CraftingStation>();
}