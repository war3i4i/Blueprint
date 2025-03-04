using System.Globalization;

namespace kg_Blueprint;

public class VBuildParser
{
    
    private static float InvariantFloat(string s) =>
        string.IsNullOrEmpty(s) ? 0f : float.Parse(s, NumberStyles.Any, NumberFormatInfo.InvariantInfo);
    
    public static BlueprintRoot Parse(string name, string[] lines)
    {
        try
        {
            BlueprintRoot newRoot = new()
            {
                Name = name
            };
            List<BlueprintObject> objects = [];
            for (int i = 0; i < lines.Length; ++i)
            {
                string line = lines[i];
                string[] parts = line.Split(' ');
                string id = parts[0];
                float rotX = InvariantFloat(parts[1]);
                float rotY = InvariantFloat(parts[2]);
                float rotZ = InvariantFloat(parts[3]);
                float rotW = InvariantFloat(parts[4]);
                float posX = InvariantFloat(parts[5]);
                float posY = InvariantFloat(parts[6]);
                float posZ = InvariantFloat(parts[7]);
                Vector3 pos = new Vector3(posX, posY, posZ);
                Quaternion rot = new Quaternion(rotX, rotY, rotZ, rotW).normalized;
                objects.Add(new BlueprintObject() { Id = id, RelativePosition = pos, Rotation = rot.eulerAngles, Prefab = parts[0]});
            }
            newRoot.Objects = objects.ToArray();
            newRoot.BoxRotation = Quaternion.identity.eulerAngles;
            newRoot.NormalizeVectors();
            if (string.IsNullOrWhiteSpace(newRoot.Name)) newRoot.Name = "Unnamed";
            return newRoot;
        }
        catch (Exception e)
        {
            kg_Blueprint.Logger.LogError($"Error parsing VBuild blueprint: {e}");
            return null;
        }
    }
}