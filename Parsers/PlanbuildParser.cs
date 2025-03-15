using System.Globalization;

namespace kg_Blueprint;

public static class PlanbuildParser
{
    private const string Base64 = "iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAi6SURBVFhH7VcLUFTXGf7uvrjAsuwDcBEtLzVCsBFNrMSoQDRikjFVq7ZoEh+jTao2aWqmM8ZEba0mo2N08DGaRiWpxqpxYhvBaiIqUXzUgFYJ1gfEUQFXFJfHvvf2P+fehUVAbWNnnKnfzLfnP497z3f/8///vYv/J+RlJEdI1P5M7j5kYOI+n5kizcqMYSIfGFRK+9DigQm0GnRwOX2IMeqUkYcLOURpdLpFevZxHof/KXoRzbLZFg/Cgz1IWOGWab0wrp8FQ3tGYtjjEeeVufsBE8fbWa++xB6uh9yVoVba/xqfTet1LDU2zEwikRIbBkGQUHzBbr5md2/2+XBTWXY3WNiPVF1QphIEpKQk/Xp/celCPkP4wR6safD0qG3wcPuizQmIRsBHcejiQ/eD5PMlq88BDvRINuNQ8UllWMYPFvibbZUZBWduYV95Pe8frqjjLem7wI17oHDLvALF5HA66SGDcDeBLPAZRxOziGlKGxgPMCs5SkTegWqkLSylrowQmmMkk10TILvHwGBeqKqhpnNolLYNTn61POfr4rJCZkdFhmDqWx+NIdOmMHPzotw8Nsfw3sqd+HB8IvMkkGrEoN48pBgK547the8tTBPYPVK4oWDD8uklrP32n1eQk5XOxzpChwIXLP208K+b5ik9AUajceeYqUu5KCsxN/cFZqLo0GlMfjoGBeW3YXe68VpmV+QXXcTqX3TD0Ut2PD1pBrKNkXxtSu/E7zKen5vNLrtwdDUvRcnxVpjNJ2G3O+H3AzotxS4lWTA6PGJ2YTBGj8xAdema2cwuKVrOW4a58zdhYLIR+87cwNgnrRjz7kLccKhw8F+NGJKkQ8nxM8pKOsv03igpWLx/79Z50qWq6nZ7dIZOY3DfwdZ4YiAvwlG5ZXZCfBfe3/jJXmQNfYI85+WMGzGej895cyKKSGBaVxHLVmzmYwEwkUkJsRg+tPVIJe/d071DgWvzC3coJkcgs0RR5MJyJ3+AuQvyMTw7nXuvihJ4+YqtmJD7Lhe1r7QGqbEiUmJUiO75CqbObgnZDj0XotMqVnsISnsnZhFX5a94XTIZRQzJ6ItIg55PsFhZ8vYihHpvIs6kw97SOqzfvoYKtAqS5IdKo8G2P8xFWZUdQ1PN+MtpNzZ+upZfy+D3N0mgdbQQks+FA0fOCSaTAX1Sk1Fru4lJry3GwWPftejq7Ij3sJ9X31wrjJr8oXDqbNuSdqjsIvrF6/FNRQMmzvg5HxPUWqKcc+PfW4Lvbzi4HRUmoOwf33KbQVCHCIJaJzCRgkqLx3rF41LlVWi1HeZrpwJZ/QLz4KpFuRLzYAAbVvwJI9JMqLW7eT/7hSyodeHcZu5lXmTeZN47WH4TQxI0WPJOy5uLQ9CEQqUl5QRjZAQXd+XadbjdHvgkH18TQIcCSVgeEzfquf6YOW2sMipj++7DmDq4C/fe8+NeVEZlSH4PF+f3uDB98Qc4fdmOQfFqWLvGtnhRUKn5ugBY/GUMSMPJ0greF+h9HIwOBVZdrsUrE7JhNFEN01BsCV5yjhd7t/0Ng3sacKDCDjf1R44dQZux06JMlNx0fLQW5AE1bUL17KV+XbH+cDNS46OwZ+M6urMXHo9LcjbZJa/PC2jDBQ1ckkbwSlAJ0KklKoL3UQeD4fc4qRTQcQp+WKzROHu1EYWnb2DK1Fw4mynO2JEw0pOr1DpILPiJHp8fufPfwcnbcnKZoqN5S7FKThIoLETev15nx/4j5UL/tESujIVIMO4pUPKQCNoQ5LH0Z55URqmm5QxWLLYp+4qmo/W54XbJJUkbEoowgwnTJ47EvPxjSEpN5eMqOmJduFHwedy03oOjpRcEfVgI4qwmPn8nOhW4fdc3vJWzk+oUE0lenJ7ZDSs3LedzYlgoW8AeW/YiVS1diAi1Rodm+y04G+3IHPRjbF7zOwzNGYZbNhslh44L83mcUsCLcdbWj2kWw8HoqA6yfP8V8e/5K96oOHv8BK7Z6iG45axV3xHEgtoPtUoFHyuQBLXUdgOfX4KO4jgAj1det3Lj+9Drw7F7zxF0i7WgT0o8rlTXYdLMpSg+dq5lk848eJFx1HMDsKuoDE/Fm1FZW4/kWCPszS50izYgRKuG2RCKyDAd5ZGAOHM4GumVFx8TgapaO/omReNoRTX6Jkbh+PlaJHWJpHvY0T1KD4fbi2vXauWd7oF2AosWLKAy88aXRF4LtJSZRr2IYX0TkGg1wkDxYokIQe/uZiRbDTCSQFGnhkUfgn6JFphoLeuzB8jpn0Deo5gjsrG+idEI1ckF2U0nwshwvrIaDvoa6gideRDMe8Fg4hgGpcZCL2pajo150EKimPdEEtXs8uInj1lxq9GFyHAdbHYHhj/RHbbbDn4N8x5jAAPSe6LJ4cL8ZVvx0eZ9aGiQ30ABtBOYX3VAc+LURaqBRs6aeieamlrZ2CjzZn0T5xV6tdb71JzMdjgoOynOAq3bw6oH20bmdVUYfGIoRI0Av6sRpvAmqrnPYs6MMehiiURZ+WUmowXtkiQzM1MUhQZH4Zbfy/3sKbwNwEMeCoYoNSuWDJeaMjsIrJAHEC5qIZoNuHLVhm2fLEC3uGjoeIXS4nqtC5WXazDul+/jak1dy0XtBE6enCnqBb0jb/HryogAG2Ux8154uAhtqPxP1ev1QUOxBbUXNVevo/TEGYz8aTYkpw9qdeu/WZ+/7bv11OlzGPiUXBMZ7hS4bN1O7Cwo6TyLN206gFUbv8wlKiNAdLQRCQlW3rLSwBgVZaKjbkbhF/u5uNy3PhaYUJfLBYPBQA8TzluTMYLzVn0j1tE9syb8UTh6ohw7vjjI7y1/OESg/nYjduwuZuJavtgZ2nlQQR/iyy+Pe+ZtW13QEfJi3Oqd5EQrVm8oYH+oehPDiM27NsxZvPbPX7FpGUGvrgkvDsSU367nAr7+fFHewmWfUZWQJST8KAYfb9nP5tYQ277vHuERHuER/lcA/g3pHj7iRfkYkQAAAABJRU5ErkJggg==";
    public static Texture2D PB_Icon;
    public static void CreateIcon() 
    {
        PB_Icon = new Texture2D(1, 1);
        PB_Icon.LoadImage(Convert.FromBase64String(Base64));
    }
    
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
                    string[] parts = line.Replace(',','.').Split(';');
                    string id = parts[0];
                    float posX = InvariantFloat(parts[2]);
                    float posY = InvariantFloat(parts[3]);
                    float posZ = InvariantFloat(parts[4]);
                    float rotX = InvariantFloat(parts[5]); 
                    float rotY = InvariantFloat(parts[6]);
                    float rotZ = InvariantFloat(parts[7]);
                    float rotW = InvariantFloat(parts[8]);
                    Vector3 pos = new Vector3(posX, posY, posZ);
                    Quaternion rot = new Quaternion(rotX, rotY, rotZ, rotW).normalized;
                    objects.Add(new BlueprintObject() { Id = id, RelativePosition = pos, Rotation = rot.eulerAngles, Prefab = parts[0]});
                }
            } 
            newRoot.Objects = objects.ToArray();
            newRoot.BoxRotation = Quaternion.identity.eulerAngles;
            newRoot.NormalizeVectors();
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