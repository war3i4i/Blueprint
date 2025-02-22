using YamlDotNet.Helpers;

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
    public string GetText() => root.TryGetFilePath(out string path) ? Path.GetFileNameWithoutExtension(path) : "";
    public void SetText(string text)
    {
        if (!root.TryGetFilePath(out string path)) return;
        text = text.ValidPath();
        if (string.IsNullOrEmpty(text) || !File.Exists(path) || Path.GetFileNameWithoutExtension(path) == text) return;
        try
        {
            string newPath = Path.Combine(Path.GetDirectoryName(path)!, $"{text}.yml");
            int count = 1;
            while (File.Exists(newPath)) newPath = Path.Combine(Path.GetDirectoryName(path)!, $"{text} ({++count}).yml");
            File.Delete(path);
            root.AssignPath(newPath);
            root.Name = text;
            File.WriteAllText(newPath, new Serializer().Serialize(root));
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
    public void AssignPath(string path) => FilePath = path;
    public bool TryGetFilePath(out string path) { path = FilePath; return !string.IsNullOrEmpty(FilePath); }
    public static BlueprintRoot CreateNew(string Name, Vector3 boxRotation, Vector3 start, Piece[] objects)
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
    public void SetPreviews(string[] previews)
    {
        if (previews.Length != 3) throw new Exception("Previews must have 3 elements");
        Previews = previews;
    }
    public Texture2D GetPreview(int index)
    {
        if (index is < 0 or >= 3) throw new Exception("Index out of bounds");
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
        if (Objects.Length == 0)
        {
            reason = "No objects in blueprint";
            return false;
        }
        return true;
    }
    public Piece.Requirement[] GetRequirements()
    {
        Piece[] pieces = new Piece[Objects.Length];
        for (int i = 0; i < Objects.Length; ++i) pieces[i] = ZNetScene.instance.GetPrefab(Objects[i].Id)?.GetComponent<Piece>();
        pieces.ThrowIfBad(Name);
        Dictionary<ItemDrop, int> requirements = new Dictionary<ItemDrop, int>();
        for (int i = 0; i < pieces.Length; ++i)
        {
            Piece p = pieces[i];
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
        Piece[] pieces = new Piece[Objects.Length];
        for (int i = 0; i < Objects.Length; ++i) pieces[i] = ZNetScene.instance.GetPrefab(Objects[i].Id)?.GetComponent<Piece>();
        pieces.ThrowIfBad(Name);
        Dictionary<Piece, int> numbered = new Dictionary<Piece, int>();
        for (int i = 0; i < pieces.Length; ++i)
        {
            if (numbered.ContainsKey(pieces[i])) numbered[pieces[i]]++;
            else numbered[pieces[i]] = 1;
        }
        return numbered.OrderByDescending(x => x.Value);
    }
    public void Apply(Vector3 center, Quaternion rootRot) => ZNetScene.instance.StartCoroutine(Internal_Apply(Configs.InstantBuild.Value, Input.GetKey(KeyCode.LeftControl), center, rootRot));
    private IEnumerator Internal_Apply(bool instantBuild, bool snapToGround, Vector3 center, Quaternion rootRot)
    {
        const int maxPerFrame = 3;
        int count = 0;
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
                Piece p = Object.Instantiate(prefab, pos, rot).GetComponent<Piece>();
                p.m_placeEffect.Create(pos, rot, p.transform);
                p.SetCreator(Game.instance.m_playerProfile.m_playerID);
            }
            else
            {
                BuildProgress.BuildProgressComponent component = Object.Instantiate(BuildProgress._piece, pos, rot).GetComponent<BuildProgress.BuildProgressComponent>();
                component.Setup(prefab.name, Game.instance.m_playerProfile.m_playerID, Mathf.Max(1f, Configs.BuildTime.Value));
            }
            count++;
            if (count < maxPerFrame) continue;
            count = 0;
            yield return null;
        }
    }
}