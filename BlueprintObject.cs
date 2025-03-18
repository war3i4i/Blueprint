using ItemDataManager;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace kg_Blueprint;
public interface ForeignBlueprintSource
{
    public IReadOnlyList<BlueprintRoot> Blueprints { get; }
    public void Delete(BlueprintRoot blueprint);
    public bool Add(BlueprintRoot blueprint);
}
public interface BlueprintSource
{
    public Texture2D[] CreatePreviews(GameObject[] inside);
    public GameObject[] GetObjectedInside { get; }
    public Vector3 StartPoint { get; }
    public Vector3 Rotation { get; }
}
public class BlueprintItemDataSource : ItemData, ForeignBlueprintSource
{
    public class Wrapper : ISerializableParameter
    {
        public List<BlueprintRoot> Blueprints = [];
        public void Serialize(ref ZPackage pkg)
        {
            pkg.Write(Blueprints.Count);
            foreach (BlueprintRoot blueprint in Blueprints) blueprint.Serialize(ref pkg);
        }
        public void Deserialize(ref ZPackage pkg)
        {
            Blueprints = new List<BlueprintRoot>(pkg.ReadInt());
            for (int i = 0; i < Blueprints.Capacity; ++i)
            {
                BlueprintRoot blueprint = new();
                blueprint.Deserialize(ref pkg);
                Blueprints.Add(blueprint);
            }
        }
    }
    public override void FirstLoad()
    {
        _blueprints = new();
        Save();
    }
    [SerializeField] private Wrapper _blueprints;
    private const int Max = 3;
    public IReadOnlyList<BlueprintRoot> Blueprints => _blueprints.Blueprints;
    private bool PlayerAlive => Player.m_localPlayer && !Player.m_localPlayer.IsDead();
    public void Delete(BlueprintRoot blueprint)
    {
        if (_blueprints.Blueprints.Count == 0 || !PlayerAlive) return;
        _blueprints.Blueprints.Remove(blueprint);
        Save();
    }
    public bool Add(BlueprintRoot blueprint)
    {
        if (!PlayerAlive) return false;
        if (_blueprints.Blueprints.Count >= Max) return false;
        _blueprints.Blueprints.Add(blueprint);
        Save();
        return true;
    }
    public override string Description => (_blueprints.Blueprints.Count == 0) ? "$kg_blueprint_book_desc\n\n$kg_blueprint_itemdata_noblueprints" : 
        _blueprints.Blueprints.Aggregate("$kg_blueprint_book_desc\n\n$kg_blueprint_itemdata_blueprints\n", (current, blueprint) => current + $"<color=yellow>{blueprint.Name}</color>\n") 
        + $"\n\n(Data Size: {DataSize})";
}
public class BlueprintCircleCreator(Vector3 pos, float radius, float height) : BlueprintSource
{
    public Texture2D[] CreatePreviews(GameObject[] inside)
    { 
        GameObject empty = new GameObject("BlueprintCircle");
        empty.transform.position = pos;
        for (int i = 0; i < inside.Length; ++i) inside[i].transform.SetParent(empty.transform);
        float addRot = Rotation.y + 135f;
        Texture2D[] previews = PhotoManager.MakeBulkSprites(empty, 1f, 
            Quaternion.Euler(30f, 0f, 0f) * Quaternion.Euler(0f, addRot, 0f),
            Quaternion.Euler(23f, 51f, 25.8f) * Quaternion.Euler(0f, addRot, 0f),
            Quaternion.Euler(23f, 51f, 25.8f) * Quaternion.Euler(0f, addRot + 180f, 0f));
        for (int i = 0; i < inside.Length; ++i) inside[i].transform.SetParent(null);
        Object.Destroy(empty);
        return previews; 
    }
    
    public GameObject[] GetObjectedInside => Utils.GetObjectsInsideCylinder(pos, radius, height, null, [typeof(BlueprintPiece)], typeof(Piece), Configs.IncludeTrees.Value ? typeof(TreeBase) : null, Configs.IncludeDestructibles.Value ? typeof(Destructible) : null);
    public Vector3 StartPoint => pos;
    public Vector3 Rotation => Player.m_localPlayer 
        ? new Vector3(0f, Mathf.Repeat(Mathf.Atan2(Player.m_localPlayer.transform.position.x - pos.x, pos.z - Player.m_localPlayer.transform.position.z) * Mathf.Rad2Deg, 360f) + 45f, 0f) 
        : Vector3.zero;

}
[Serializable]
public class SimpleVector3 
{
    public float x;
    public float y; 
    public float z;
    public static implicit operator Vector3(SimpleVector3 v) => new Vector3(v.x, v.y, v.z);
    public static implicit operator SimpleVector3(Vector3 v) => new SimpleVector3 { x = v.x, y = v.y, z = v.z };
}
public class RenameBlueprintRoot(BlueprintRoot root, Action<string> callback) : TextReceiver
{
    public string GetText() => root.Name;
    public void SetText(string text)
    { 
        if (!root.TryGetFilePath(out string path)) return;
        text = text.ValidPath();
        if (string.IsNullOrEmpty(text) || !File.Exists(path) || Path.GetFileNameWithoutExtension(path) == text) return;
        try
        {
            File.Delete(path);
            string newPath = Path.Combine(Path.GetDirectoryName(path)!, $"{text}.yml");
            root.Name = text;
            root.AssignPath(newPath, false);
            root.Save();
            callback?.Invoke(text);
        }
        catch (Exception e)
        {
            kg_Blueprint.Logger.LogError($"Failed to rename blueprint {root.Name}: {e}");
        }
    }
}
public readonly struct IntOrString
{
    public IntOrString(string strVal)
    {
        _strValue = strVal;
        _intValue = strVal.GetStableHashCode();
    }
    public IntOrString(int intVal) 
    {
        _intValue = intVal;
    }
    private readonly string _strValue;
    private readonly int _intValue;
    public static explicit operator int(IntOrString value) => value._intValue;
    public static implicit operator IntOrString(int value) => new(value);
    public static implicit operator IntOrString(string value) => new(value);
    public override string ToString() => _strValue ?? _intValue.ToString();
}
public class IntOrStringConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(IntOrString);
    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        Scalar scalar = parser.Consume<Scalar>();
        return int.TryParse(scalar.Value, out int intValue) ? new IntOrString(intValue) : new IntOrString(scalar.Value);
    }
    public void WriteYaml(IEmitter emitter, object value, Type type, ObjectSerializer serializer) =>
        emitter.Emit(new Scalar(value?.ToString() ?? "ERROR"));
}
[Serializable]
public class BlueprintObject
{
    public IntOrString Id;
    public SimpleVector3 RelativePosition;
    public SimpleVector3 Rotation;
    public string ZDOData;
    public string Prefab;
}
[Serializable]
public class BlueprintRoot : ISerializableParameter
{
    private string FilePath;
    private Texture2D[] CachedPreviews;
    public string Name;
    public string Category;
    public string CategoryColor;
    public string Author;
    public string Description;
    public string Icon;
    public SimpleVector3 BoxRotation;
    public BlueprintObject[] Objects;
    public string[] Previews;
    public enum SourceType { Native, Planbuild, VBuild }
    public SourceType Source = SourceType.Native;
    public void AssignPath(string path, bool force)
    {
        path = path.ValidPath();
        if (string.IsNullOrWhiteSpace(path)) return;
        if (force)
        {
            FilePath = path;
            return;
        }
        string directory = Path.GetDirectoryName(path)!;
        string fileNameNoExt = Path.GetFileNameWithoutExtension(path);
        string ext = Path.GetExtension(path);
        int increment = 1;
        while (File.Exists(path)) path = Path.Combine(directory, $"{fileNameNoExt} ({increment++}){ext}");
        FilePath = path;
    }
    public bool TryGetFilePath(out string path) { path = FilePath; return !string.IsNullOrEmpty(FilePath); }
    public static BlueprintRoot CreateNew(string Name, string Description, string Author, Vector3 boxRotation, Vector3 start, GameObject[] objects, Texture2D icon)
    {
        objects.ThrowIfBad(Name);
        BlueprintRoot root = new BlueprintRoot
        {
            Name = Name,
            Description = Description,
            Author = Author,
            Objects = new BlueprintObject[objects.Length],
            BoxRotation = boxRotation,
            Icon = icon ? Convert.ToBase64String(icon.EncodeToPNG()) : null
        };
        objects = objects.OrderBy(x => x.transform.position.y).ToArray(); 
        List<Renderer> views = new();
        for (int i = 0; i < objects.Length; ++i)
        {
            string id = objects[i].name.Replace("(Clone)", "");
            if (!ZNetScene.instance.GetPrefab(id)) continue;
            root.Objects[i] = new BlueprintObject
            {
                Id = id, 
                RelativePosition = objects[i].transform.position - start,
                Rotation = objects[i].transform.rotation.eulerAngles
            };
            views.AddRange(objects[i].GetComponentsInChildren<Renderer>());
            if (!Configs.SaveZDOHashset.Contains((int)root.Objects[i].Id)) continue;
            ZDO zdo = objects[i].GetComponent<ZNetView>()?.GetZDO();
            if (zdo == null) continue;
            ZPackage pkg = new();
            zdo.SerializeZDO(pkg);
            root.Objects[i].ZDOData = Convert.ToBase64String(pkg.GetArray());
        }
        root.NormalizeVectors(views, start);
        return root;
    }
    public void Delete()
    {
        if (string.IsNullOrEmpty(FilePath)) return;
        Task.Run(() =>
        {
            try
            {
                File.Delete(FilePath);
            }
            catch (Exception e)
            {
                kg_Blueprint.Logger.LogError($"Failed to delete blueprint file {FilePath}: {e}");
            }
        });
    }
    public void SetPreviews(Texture2D[] previews)
    {
        if (previews == null || previews.Length == 0) return;
        string[] data = new string[previews.Length];
        for (int i = 0; i < previews.Length; ++i) data[i] = Convert.ToBase64String(previews[i].EncodeToPNG());
        Previews = data;
        CachedPreviews = previews;
    }
    public Texture2D GetPreview(int index)
    {
        if (CachedPreviews == null) CachePreviews();
        if (index < 0 || index >= CachedPreviews!.Length) return null;
        return CachedPreviews[index];
    }
    public void CachePreviews()
    {
        if (Previews == null || Previews.Length == 0)
        {
            CachedPreviews = [];
            return;
        }
        CachedPreviews = new Texture2D[Previews.Length];
        for (int i = 0; i < Previews.Length; ++i)
        {
            byte[] data = Convert.FromBase64String(Previews[i]);
            Texture2D tex = new Texture2D(1, 1);
            tex.LoadImage(data);
            CachedPreviews[i] = tex;
        }
    }
    public bool IsValid(out string reason)
    {
        reason = null;
        if (string.IsNullOrEmpty(Name))
        {
            reason = "Name is null or empty";
            return false;
        }
        if (Objects.Length != 0) return true;
        reason = "No objects in blueprint";
        return false;
    }
    public void NormalizeVectors(List<Renderer> views = null, Vector3 startPoint = default)
    {
        if (Objects == null || Objects.Length == 0) return; 
        if (views == null)
        {
            Vector3 center = Objects.Aggregate(Vector3.zero, (current, t) => current + t.RelativePosition);
            center /= Objects.Length;
            foreach (BlueprintObject o in Objects) o.RelativePosition -= center;
            float minY = Objects.Min(x => x.RelativePosition.y);
            foreach (BlueprintObject o in Objects) o.RelativePosition.y -= minY;
        }
        else
        {
            Vector3 center = Vector3.zero;
            foreach (Renderer r in views) center += r.bounds.center;
            center /= views.Count;
            float minY = views.Min(x => x.bounds.min.y);
            Vector3 offset = center - startPoint; 
            offset.y = minY - startPoint.y;
            foreach (BlueprintObject o in Objects) o.RelativePosition -= offset;
        }
    }
    public Piece.Requirement[] GetRequirements() => Objects.Select(x => x.Id).ToArray().GetRequirements();
    public IOrderedEnumerable<KeyValuePair<string, Utils.NumberedData>> GetPiecesNumbered() => Objects.Select(x => x.Id).ToArray().GetPiecesNumbered();
    public void Apply(Vector3 center, Quaternion rootRot, bool deactivate) => ZNetScene.instance.StartCoroutine(Internal_Apply(Configs.InstantBuild.Value, center, rootRot, deactivate));
    private IEnumerator Internal_Apply(bool instantBuild, Vector3 center, Quaternion rootRot, bool deactivate)
    {
        for (int i = 0; i < Objects.Length; ++i)
        {
            GameObject prefab = ZNetScene.instance.GetPrefab((int)Objects[i].Id);
            if (prefab == null) 
            { 
                kg_Blueprint.Logger.LogDebug($"Failed to find prefab with id {Objects[i].Id} while applying blueprint ({Name})");
                continue;
            } 
            if (prefab.GetComponent<TreeBase>() && !Configs.IncludeTrees.Value) continue;
            if (prefab.GetComponent<Destructible>() && !Configs.IncludeDestructibles.Value) continue;
            
            Quaternion deltaRotation = rootRot * Quaternion.Inverse(Quaternion.Euler(BoxRotation));
            Vector3 pos = center + deltaRotation * Objects[i].RelativePosition;
            if (deactivate && !BlueprintPiece.IsInside(pos)) continue;
            Quaternion rot = deltaRotation * Quaternion.Euler(Objects[i].Rotation); 
            if (instantBuild || deactivate)
            { 
                
                GameObject newObj = Object.Instantiate(prefab, pos, rot);
                Piece p = newObj.GetComponent<Piece>();
                if (p) 
                {
                    p.m_placeEffect.Create(pos, rot, p.transform);
                    p.SetCreator(Game.instance.m_playerProfile.m_playerID);
                    if (p.GetComponent<ItemDrop>() is {} item) item.MakePiece(true);
                    if (deactivate && !Configs.SaveZDOHashset.Contains((int)Objects[i].Id))
                    {
                        p.m_nview.m_zdo.Set("kg_Blueprint", true);
                        Piece_Awake_Patch.DeactivatePiece(p);
                    }
                }
                try
                {
                    if (!string.IsNullOrEmpty(Objects[i].ZDOData) && newObj.GetComponent<ZNetView>() is {} znv && Configs.SaveZDOHashset.Contains((int)Objects[i].Id))
                    {
                        znv.m_zdo.DeserializeZDO(new(Objects[i].ZDOData));
                    }
                } catch (Exception e) { kg_Blueprint.Logger.LogError(e); }
            } 
            else
            {
                BuildProgress.BuildProgressComponent component = Object.Instantiate(BuildProgress._piece, pos, rot).GetComponent<BuildProgress.BuildProgressComponent>();
                component.Setup(prefab.name, Game.instance.m_playerProfile.m_playerID, Mathf.Max(1f, Configs.BuildTime.Value), Objects[i].ZDOData);
            } 
            int frameSkip = Configs.BlueprintBuildFrameSkip.Value;
            if (frameSkip > 0) yield return Utils.WaitFrames(Configs.BlueprintBuildFrameSkip.Value);
        } 
    }
    public BlueprintRoot Clone() => (BlueprintRoot)MemberwiseClone();
    public void Save(bool forget = true) 
    {
        if (!TryGetFilePath(out string path)) return;
        BlueprintRoot clone = (BlueprintRoot)MemberwiseClone();
        if (forget)
            Task.Run(() =>
            {  
                string data = new SerializerBuilder().ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults).WithTypeConverter(new IntOrStringConverter()).Build().Serialize(clone);
                path.WriteNoDupes(data, false);
            });
        else
        {
            string data = new SerializerBuilder().ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults).WithTypeConverter(new IntOrStringConverter()).Build().Serialize(clone);
            path.WriteNoDupes(data, false);
        }
    }
    public void Serialize(ref ZPackage pkg)
    {
        pkg.Write(Name ?? "");
        pkg.Write(Author ?? "");
        pkg.Write(Description ?? "");
        pkg.Write(BoxRotation);
        pkg.Write(Objects.Length);
        for (int i = 0; i < Objects.Length; ++i)
        {
            pkg.Write((int)Objects[i].Id);
            pkg.Write(Objects[i].RelativePosition);
            pkg.Write(Objects[i].Rotation);
            pkg.Write(Objects[i].ZDOData != null);
            if (Objects[i].ZDOData != null) pkg.Write(Objects[i].ZDOData);
        }
    }
    public void Deserialize(ref ZPackage pkg)
    {
        Name = pkg.ReadString();
        Author = pkg.ReadString();
        Description = pkg.ReadString();
        if (string.IsNullOrEmpty(Name)) Name = "Unnamed";
        if (string.IsNullOrEmpty(Author)) Author = null;
        if (string.IsNullOrEmpty(Description)) Description = null;
        BoxRotation = pkg.ReadVector3();
        Objects = new BlueprintObject[pkg.ReadInt()];
        for (int i = 0; i < Objects.Length; ++i)
        {
            Objects[i] = new BlueprintObject
            {
                Id = pkg.ReadInt(),
                RelativePosition = pkg.ReadVector3(),
                Rotation = pkg.ReadVector3(),
                ZDOData = pkg.ReadBool() ? pkg.ReadString() : null
            };
        }
    }
}