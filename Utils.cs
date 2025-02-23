﻿using System.IO.Compression;
using System.Runtime.CompilerServices;
using CompressionLevel = System.IO.Compression.CompressionLevel;

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
    public static void WriteNoDupes(this string path, string data) => File.WriteAllText(path.ValidPath(), data);
    public static void WriteWithDupes(this string path, string data, bool forget)
    {
        if (forget) Task.Run(() => WriteWithDupes_Internal(path, data));
        else WriteWithDupes_Internal(path, data);
    }
    private static void WriteWithDupes_Internal(this string path, string data)
    {
        path = path.ValidPath();
        string directory = Path.GetDirectoryName(path)!;
        string fileNameNoExt = Path.GetFileNameWithoutExtension(path);
        string ext = Path.GetExtension(path);
        int increment = 1;
        string newPath = path;
        while (File.Exists(newPath)) newPath = Path.Combine(directory, $"{fileNameNoExt} ({increment++}){ext}");
        File.WriteAllText(newPath, data);
    }
    private static readonly LayerMask Layer = LayerMask.GetMask("piece", "piece_nonsolid", "Default", "character_noenv");
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
    private static CraftingStation _internal_fakeStation;
    public static CraftingStation GetBlueprintFakeStation() => _internal_fakeStation ??= kg_Blueprint.Asset.LoadAsset<GameObject>("kg_BlueprintCS").GetComponent<CraftingStation>();
    public static IEnumerator WaitFrames(int frames) { for (int i = 0; i < frames; ++i) yield return null; }
}