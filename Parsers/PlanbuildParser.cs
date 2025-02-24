using System.Globalization;

namespace kg_Blueprint;

public static class PlanbuildParser
{
    private static float InvariantFloat(string s) =>
        string.IsNullOrEmpty(s) ? 0f : float.Parse(s, NumberStyles.Any, NumberFormatInfo.InvariantInfo);
    
    public static BlueprintRoot Parse(string[] lines)
    {
        try
        {
            BlueprintRoot newRoot = new();
            List<BlueprintObject> objects = [];
            List<string> previews = [];
            bool readingPieces = false;
            for (int i = 0; i < lines.Length; ++i)
            {
                string line = lines[i];
                if (!readingPieces)
                {
                    if (line.Contains("#Name")) newRoot.Name = line.Split(':')[1];
                    if (line.Contains("#Creator")) newRoot.Author = line.Split(':')[1];
                    if (line.Contains("#Description")) newRoot.Description = line.Split(':')[1];
                    if (line.Contains("#Preview")) previews.Add(line.Split(':')[1]);
                    if (line == "#Pieces")  readingPieces = true;
                }
                else
                {
                    string[] parts = line.Split(';');
                    int id = parts[0].GetStableHashCode();
                    float posX = InvariantFloat(parts[2]);
                    float posY = InvariantFloat(parts[3]);
                    float posZ = InvariantFloat(parts[4]);
                    float rotX = InvariantFloat(parts[5]); 
                    float rotY = InvariantFloat(parts[6]);
                    float rotZ = InvariantFloat(parts[7]);
                    float rotW = InvariantFloat(parts[8]);
                    Vector3 pos = new Vector3(posX, posY, posZ);
                    Quaternion rot = new Quaternion(rotX, rotY, rotZ, rotW).normalized;
                    if (pos == Vector3.zero) kg_Blueprint.Logger.LogWarning($"Zero position in PB blueprint {parts[0]}");
                    objects.Add(new BlueprintObject() { Id = id, RelativePosition = pos, Rotation = rot.eulerAngles, Prefab = parts[0]});
                }
            } 
            newRoot.Objects = objects.ToArray();
            newRoot.BoxRotation = Quaternion.identity.eulerAngles;
            newRoot.NormalizeVectors(false);
            newRoot.Previews = previews.ToArray();
            if (string.IsNullOrWhiteSpace(newRoot.Name)) newRoot.Name = "Unnamed";
            return newRoot;
        }
        catch (Exception e)
        {
            kg_Blueprint.Logger.LogError($"Error parsing PB blueprint: {e}");
            return null;
        }
    }
}