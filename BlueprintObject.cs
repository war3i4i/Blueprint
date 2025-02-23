namespace kg_Blueprint;

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
[Serializable]
public class BlueprintObject
{
    public int Id;
    public SimpleVector3 RelativePosition;
    public SimpleVector3 Rotation;
    public string ZDOData;
}
[Serializable]
public class BlueprintRoot
{
    private string FilePath;
    public string Name;
    public string Icon;
    public SimpleVector3 BoxRotation;
    public BlueprintObject[] Objects;
    public string[] Previews;
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
    public static BlueprintRoot CreateNew(string Name, Vector3 boxRotation, Vector3 start, GameObject[] objects)
    {
        objects.ThrowIfBad(Name);
        BlueprintRoot root = new BlueprintRoot
        {
            Name = Name,
            Objects = new BlueprintObject[objects.Length],
            BoxRotation = boxRotation
        };
        objects = objects.OrderBy(x => x.transform.position.y).ToArray();
        for (int i = 0; i < objects.Length; ++i)
        { 
            root.Objects[i] = new BlueprintObject
            { 
                Id = objects[i].name.Replace("(Clone)", "").GetStableHashCode(),
                RelativePosition = objects[i].transform.position - start,
                Rotation = objects[i].transform.rotation.eulerAngles
            };
            if (!Configs.SaveZDOHashset.Contains(root.Objects[i].Id)) continue;
            ZPackage pkg = new();
            ZDO zdo = objects[i].GetComponent<ZNetView>()?.GetZDO();
            if (zdo == null) continue;
            zdo.SerializeZDO(pkg);
            byte[] data = pkg.GetArray();
            root.Objects[i].ZDOData = Convert.ToBase64String(data);
        }
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
    }
    public Texture2D GetPreview(int index)
    {
        if (index < 0 || index >= Previews.Length || string.IsNullOrEmpty(Previews[index])) return null;
        byte[] data = Convert.FromBase64String(Previews[index]);
        Texture2D tex = new Texture2D(1, 1);
        tex.LoadImage(data);
        return tex;
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
    public Piece.Requirement[] GetRequirements()
    {
        GameObject[] gameObjects = new GameObject[Objects.Length];
        for (int i = 0; i < Objects.Length; ++i) gameObjects[i] = ZNetScene.instance.GetPrefab(Objects[i].Id);
        gameObjects.ThrowIfBad(Name);
        Dictionary<ItemDrop, int> requirements = new Dictionary<ItemDrop, int>();
        for (int i = 0; i < gameObjects.Length; ++i)
        {
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
    public IOrderedEnumerable<KeyValuePair<Piece, int>> GetPiecesNumbered()
    {
        GameObject[] pieces = new GameObject[Objects.Length];
        for (int i = 0; i < Objects.Length; ++i) pieces[i] = ZNetScene.instance.GetPrefab(Objects[i].Id);
        pieces.ThrowIfBad(Name);
        Dictionary<Piece, int> numbered = new Dictionary<Piece, int>();
        for (int i = 0; i < pieces.Length; ++i)
        {
            Piece p = pieces[i].GetComponent<Piece>();
            if (!p) continue;
            if (numbered.ContainsKey(p)) numbered[p]++;
            else numbered[p] = 1;
        }
        return numbered.OrderByDescending(x => x.Value);
    }
    public void Apply(Vector3 center, Quaternion rootRot) => ZNetScene.instance.StartCoroutine(Internal_Apply(Configs.InstantBuild.Value, Input.GetKey(KeyCode.LeftControl), center, rootRot));
    private IEnumerator Internal_Apply(bool instantBuild, bool snapToGround, Vector3 center, Quaternion rootRot)
    {
        for (int i = 0; i < Objects.Length; ++i)
        {
            GameObject prefab = ZNetScene.instance.GetPrefab(Objects[i].Id);
            if (prefab == null)
            {
                kg_Blueprint.Logger.LogError($"Failed to find prefab with id {Objects[i].Id} while applying blueprint ({Name})");
                continue; 
            }
            Vector3 pos = center + rootRot * Objects[i].RelativePosition;
            Quaternion rot = Quaternion.Euler(Objects[i].Rotation) * rootRot;
            if (snapToGround)
            {
                pos.y = Mathf.Max(ZoneSystem.instance.GetGroundHeight(pos), pos.y);
                ZoneSystem.instance.FindFloor(pos, out pos.y);
            }
            if (instantBuild)
            {
                GameObject newObj = Object.Instantiate(prefab, pos, rot);
                Piece p = newObj.GetComponent<Piece>();
                if (p)
                {
                    p.m_placeEffect.Create(pos, rot, p.transform);
                    p.SetCreator(Game.instance.m_playerProfile.m_playerID);
                }
                try
                {
                    if (!string.IsNullOrEmpty(Objects[i].ZDOData) && newObj.GetComponent<ZNetView>() is {} znv)
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
            yield return Utils.WaitFrames(Configs.BlueprintBuildFrameSkip.Value);
        }
    }
    public void Save()
    {
        if (!TryGetFilePath(out string path)) return;
        BlueprintRoot clone = (BlueprintRoot)MemberwiseClone();
        Task.Run(() =>
        {  
            string data = new SerializerBuilder().ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults).Build().Serialize(clone);
            path.WriteNoDupes(data, false);
        });
    }
}