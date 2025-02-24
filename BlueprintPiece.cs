namespace kg_Blueprint;
public static class PlayerState
{
    public static bool PlayerInsideBlueprint;
    public static BlueprintPiece BlueprintPiece;
    private static float _counter;
    public static void Update()
    {
        if (!Player.m_localPlayer) return;
        _counter += Time.fixedDeltaTime;
        if (_counter < 1f) return;
        _counter = 0f;
        PlayerInsideBlueprint = BlueprintPiece.IsInside(Player.m_localPlayer.transform.position, out BlueprintPiece);
    }
}
public class BlueprintSharing : MonoBehaviour, Interactable, Hoverable, ForeignBlueprintSource
{
    private ZNetView _znv;
    private List<BlueprintRoot> Current = null;
    private void Awake()
    {
        _znv = GetComponent<ZNetView>();
    }
    private BlueprintRoot[] _Internal_Blueprints
    {
        get
        {
            byte[] data = _znv.GetZDO().GetByteArray("Blueprints");
            if (data == null) return [];
            ZPackage pkg = new(data);
            pkg.Decompress();
            int count = pkg.ReadInt();
            BlueprintRoot[] result = new BlueprintRoot[count];
            for (int i = 0; i < count; ++i)
            {
                result[i] = new(); 
                result[i].Deserialize(ref pkg);
            }
            return result;
        }
        set
        { 
            ZPackage pkg = new();
            pkg.Write(value.Length);
            for (int i = 0; i < value.Length; ++i) value[i].Serialize(ref pkg);
            pkg.Compress();
            _znv.GetZDO().Set("Blueprints", pkg.GetArray());
        }
    }
    public bool Interact(Humanoid user, bool hold, bool alt)
    {
        _znv.ClaimOwnership();
        BlueprintUI.Show(this);
        return true;
    }
    public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;
    public string GetHoverText() => "[<color=yellow><b>$KEY_Use</b></color>] $kg_blueprint_opensharing".Localize();
    public string GetHoverName() => "$kg_blueprint_sharing_piece".Localize();
    public BlueprintRoot[] Blueprints
    {
        get
        {
            BlueprintRoot[] bp = _Internal_Blueprints;
            Current = bp.ToList();
            return bp;
        }
    }
    private void SetBlueprints(BlueprintRoot[] blueprints) => _Internal_Blueprints = blueprints;
    public void Delete(BlueprintRoot blueprint)
    {
        Current.Remove(blueprint);
        SetBlueprints(Current.ToArray());
    }
    public void Add(BlueprintRoot blueprint)
    {
        Current.Add(blueprint);
        if (Current.Count > 10) Current.RemoveAt(0);
        SetBlueprints(Current.ToArray());
    }
}
public class BlueprintPiece : MonoBehaviour, Interactable, Hoverable, BlueprintSource
{
    private static readonly List<BlueprintPiece> _instances = [];
    private ZNetView _znv;
    private Piece _piece;
    private Transform _view;
    private Transform _interact;
    private BoxCollider _blueprintArea;
    private Transform _projectors;
    public static bool IsInside(Vector3 point) => _instances.Any(t => t._blueprintArea.IsInside(point));
    public static bool IsInside(Vector3 point, out BlueprintPiece piece)
    {
        piece = _instances.FirstOrDefault(t => t._blueprintArea.IsInside(point));
        return piece;
    } 
    private void Awake()
    {
        _znv = GetComponent<ZNetView>();  
        _projectors = transform.Find("Scale/Projectors");
        _projectors.gameObject.SetActive(false);
        if (!_znv.IsValid()) return;
        _instances.Add(this);
        _piece = GetComponent<Piece>();
        _view = transform.Find("Scale/View");
        _interact = transform.Find("Interact");
        _blueprintArea = transform.Find("Scale/BlueprintArea").GetComponent<BoxCollider>();
        
        _projectors.Find("Side1").GetComponent<SquareProjector>().rotation = transform.rotation.eulerAngles.y;
        _projectors.Find("Side2").GetComponent<SquareProjector>().rotation = transform.rotation.eulerAngles.y;
        _projectors.Find("Side3").GetComponent<SquareProjector>().rotation = transform.rotation.eulerAngles.y + 90f;
        _projectors.Find("Side4").GetComponent<SquareProjector>().rotation = transform.rotation.eulerAngles.y + 90f;
        
    }
    private float _counter = 1f;
    private void FixedUpdate()
    {
        _counter -= Time.fixedDeltaTime;
        if (!(_counter < 0f)) return;
        _counter = 1f;
        _projectors.gameObject.SetActive(PlayerState.BlueprintPiece == this);
    }
    private void OnDestroy() => _instances.Remove(this);
    public Texture2D[] CreatePreviews(GameObject[] inside)
    {
        _view.gameObject.SetActive(false);
        _interact.gameObject.SetActive(false);
        bool projectorsActive = _projectors.gameObject.activeSelf;
        _projectors.gameObject.SetActive(false);
        for (int i = 0; i < inside.Length; ++i) inside[i].transform.SetParent(transform);
        Texture2D[] previews = PhotoManager.MakeBulkSprites(gameObject, 1f, 
            Quaternion.Euler(30f, 0f, 0f),
            Quaternion.Euler(23f, 51f, 25.8f),
            Quaternion.Euler(23f, 51f, 25.8f) * Quaternion.Euler(0f, 180f, 0f));
        for (int i = 0; i < inside.Length; ++i) inside[i].transform.SetParent(null);
        _view.gameObject.SetActive(true);
        _interact.gameObject.SetActive(true);
        _projectors.gameObject.SetActive(projectorsActive);  
        return previews;
    } 
    public GameObject[] GetObjectedInside => _blueprintArea.GetObjectsInside([_piece.gameObject], typeof(Piece), Configs.IncludeTrees.Value ? typeof(TreeBase) : null, Configs.IncludeDestructibles.Value ? typeof(Destructible) : null);
    public Vector3 StartPoint
    {
        get
        {
            _blueprintArea.gameObject.SetActive(true);
            Vector3 result = new Vector3(_blueprintArea.bounds.center.x, _blueprintArea.bounds.min.y, _blueprintArea.bounds.center.z);
            _blueprintArea.gameObject.SetActive(false);
            return result;
        }    
    }
    public Vector3 Rotation => transform.rotation.eulerAngles;
    public bool SnapToLowest => false;
    public void DestroyAllPiecesInside(bool onlyBlueprint)
    {
        GameObject[] objects = _blueprintArea.GetObjectsInside([_piece.gameObject], typeof(Piece), typeof(TreeBase), typeof(Destructible));
        for (int i = 0; i < objects.Length; ++i)
        {
            GameObject go = objects[i];
            ZNetView znv = go.GetComponent<ZNetView>();
            if (onlyBlueprint && (!znv || !znv.m_zdo.GetBool("kg_Blueprint"))) continue;
            znv?.ClaimOwnership();
            ZNetScene.instance.Destroy(go);
        }
    }
    public void Load(BlueprintRoot root)
    {
        if (root == null) return;
        StartCoroutine(_Internal_Load(root));
    }
    private IEnumerator _Internal_Load(BlueprintRoot root)
    {
        Vector3 center = StartPoint;
        for (int i = 0; i < root.Objects.Length; ++i)
        {
            BlueprintObject obj = root.Objects[i]; 
            GameObject prefab = ZNetScene.instance.GetPrefab(obj.Id);
            if (!prefab)
            {
                kg_Blueprint.Logger.LogError($"Failed to load {obj.Id}");
                continue;
            }
            Quaternion deltaRotation = transform.rotation * Quaternion.Inverse(Quaternion.Euler(root.BoxRotation));
            Vector3 pos = center + deltaRotation * obj.RelativePosition;
            if (!IsInside(pos)) continue;
            Quaternion rot = Quaternion.Euler(obj.Rotation) * deltaRotation;
            GameObject go = Object.Instantiate(prefab, pos, rot);
            if (go.GetComponent<Piece>() is { } p) 
            { 
                if (go.GetComponent<ItemDrop>() is {} item) item.MakePiece(true);
                p.m_nview?.m_zdo.Set("kg_Blueprint", true);
                Piece_Awake_Patch.DeactivatePiece(p);
            }
            yield return Utils.WaitFrames(Configs.BlueprintLoadFrameSkip.Value);
        }
    }
    public bool Interact(Humanoid user, bool hold, bool alt)
    {
        if (user.GetHoverObject() != _interact.gameObject) return false;
        if (Input.GetKey(KeyCode.C))
        {
            UnifiedPopup.Push(new YesNoPopup("$kg_blueprint_piece_delete", "$kg_blueprint_piece_delete_desc", () =>
            {
                UnifiedPopup.Pop();
                DestroyAllPiecesInside(true);
                _znv.ClaimOwnership();
                ZNetScene.instance.Destroy(gameObject);
                MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "$kg_blueprint_piece_deleted".Localize());
            }, UnifiedPopup.Pop));
            return true;
        }
        if (!alt) InteractionUI.Show(this);
        else
        {
            UnifiedPopup.Push(new YesNoPopup("$kg_blueprint_clear", "$kg_blueprint_clear_desc", () =>
            { 
                UnifiedPopup.Pop();
                DestroyAllPiecesInside(false);
                MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "$kg_blueprint_cleared".Localize());
            }, UnifiedPopup.Pop));
        } 
        return true; 
    }
    public string GetHoverText()
    {
        if (Player.m_localPlayer.GetHoverObject() is not {} obj) return string.Empty;
        if (obj != _interact.gameObject) return string.Empty;
        return "[<color=yellow><b>$KEY_Use</b></color>] $kg_blueprint_saveblueprint\n".Localize() +
               "[<color=yellow><b>L.Shift + $KEY_Use</b></color>] $kg_blueprint_clear\n".Localize() +
               "[<color=yellow><b>C + $KEY_Use</b></color>] <color=red>$kg_blueprint_piece_delete</color>".Localize();
    } 
    public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;
    public string GetHoverName() => "$kg_blueprint_piece";
}
[HarmonyPatch(typeof(Player),nameof(Player.HaveRequirements), typeof(Piece), typeof(Player.RequirementMode))]
public static class Player_HaveRequirements_Patch
{
    [UsedImplicitly] private static bool Prefix(Player.RequirementMode mode, ref bool __result)
    {
        if (mode != Player.RequirementMode.CanBuild) return true;
        if (!PlayerState.PlayerInsideBlueprint) return true;
        __result = true; return false;
    }
}
[HarmonyPatch(typeof(Hud),nameof(Hud.SetupPieceInfo))]
public static class Hud_SetupPieceInfo_Patch
{
    private class StateTransfer
    {
        public CraftingStation Station;
        public Piece.Requirement[] Requirements;
    }
    [UsedImplicitly] private static void Prefix(Hud __instance, Piece piece, out StateTransfer __state)
    {
        __state = null;
        if (!piece || !PlayerState.PlayerInsideBlueprint) return;
        __state = new StateTransfer
        {
            Station = piece.m_craftingStation,
            Requirements = piece.m_resources 
        };
        piece.m_craftingStation = Utils.GetBlueprintFakeStation();
        piece.m_resources = [];
    }
    [UsedImplicitly] private static void Finalizer(Piece piece, StateTransfer __state)
    {
        if (__state == null) return;
        piece.m_craftingStation = __state.Station;
        piece.m_resources = __state.Requirements; 
    }
}
[HarmonyPatch(typeof(Player),nameof(Player.CheckCanRemovePiece))]
public static class Player_CheckCanRemovePiece_Patch
{
    [UsedImplicitly] private static bool Prefix(Piece piece, ref bool __result)
    {
        if (piece.name.Contains("kg_BlueprintBox")) return false;
        if (!PlayerState.PlayerInsideBlueprint) return true;
        __result = true;
        return false;
    }
}
[HarmonyPatch(typeof(Player),nameof(Player.TryPlacePiece))]
public static class Player_TryPlacePiece_Patch 
{ 
    private static bool _skip;
    [UsedImplicitly] private static bool Prefix(Player __instance, Piece piece, ref bool __result)
    {
        if (_skip) return true;
        if (!PlayerState.PlayerInsideBlueprint) return true;
        if (piece.name == "kg_Blueprint_Internal_PlacePiece")
        {
            MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "$kg_blueprint_cantbuildblueprintinsidebox".Localize());
            __result = false;
            return false;
        }
        __instance.UpdatePlacementGhost(false);
        if (__instance.m_placementGhost && !BlueprintPiece.IsInside(__instance.m_placementGhost.transform.position))
        {
            MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "$kg_blueprint_cantbuildobjectsoutsideblueprint".Localize());
            __result = false;
            return false;
        }
        try
        {
            _skip = true;
            __instance.TryPlacePiece(piece);
            _skip = false;
        } 
        finally { _skip = false; }
        return false;
    }
}
[HarmonyPatch(typeof(Hud), nameof(Hud.Awake))]
public static class Hud_Awake_Patch
{
    public static int ChildNumber;
    [UsedImplicitly] private static void Postfix(Hud __instance)
    {
        GameObject go = Object.Instantiate(kg_Blueprint.Asset.LoadAsset<GameObject>("kg_BlueprintMarker"));
        go.name = "BPMarker";
        go.transform.SetParent(__instance.m_pieceIconPrefab.transform);
        go.transform.localPosition = new Vector3(14, -48, 0);
        go.SetActive(false); 
        ChildNumber = __instance.m_pieceIconPrefab.transform.childCount - 1;
        go.transform.SetAsLastSibling(); 
    } 
}
[HarmonyPatch(typeof(Piece),nameof(Piece.Awake))]
public static class Piece_Awake_Patch
{
    private static readonly HashSet<Type> _permittedComponents = [typeof(Piece), typeof(ZNetView), typeof(ZSyncTransform), typeof(Door)];
    public static void DeactivatePiece(Piece p)
    { 
        foreach (Component c in p.GetComponentsInChildren<Component>(true).Reverse())
        {
            if (c is Renderer or MeshFilter or Transform or Animator) continue;
            if (c is Collider || _permittedComponents.Contains(c.GetType())) continue;
            Object.Destroy(c);
        } 
        for (int i = 0; i < p.m_resources.Length; i++)
        {
            p.m_resources[i].m_amount = 0;
            p.m_resources[i].m_recover = false;
        }
        p.m_canBeRemoved = true;
    }
    [UsedImplicitly] private static void Postfix(Piece __instance) 
    {
        if (!__instance.m_nview || !__instance.m_nview.IsValid()) return;
        if (!__instance.m_nview.m_zdo.GetBool("kg_Blueprint")) return;
        DeactivatePiece(__instance);
    }
}
[HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
public static class Player_PlacePiece_Patch
{
    public static void PlacedPiece(GameObject obj)
    {
        if (obj?.GetComponent<Piece>() is not { } piece || !piece.m_nview) return;
        if (!PlayerState.PlayerInsideBlueprint) return;
        int id = obj.name.Replace("(Clone)", "").GetStableHashCode();
        if (Configs.SaveZDOHashset.Contains(id)) return;
        piece.m_nview.m_zdo.Set("kg_Blueprint", true);
        Piece_Awake_Patch.DeactivatePiece(piece);
    } 
    
    [UsedImplicitly] private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo method = AccessTools.Method(typeof(Player_PlacePiece_Patch), nameof(PlacedPiece), [typeof(GameObject)]);
        foreach (CodeInstruction instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode != OpCodes.Stloc_0) continue;
            yield return new CodeInstruction(OpCodes.Ldloc_S, 0);
            yield return new CodeInstruction(OpCodes.Call, method);
        }
    }
}
[HarmonyPatch(typeof(Hud), nameof(Hud.UpdatePieceList))]
public static class Hud_UpdatePieceList_Patch
{
    private static void SpriteEnabler(Piece p, Hud.PieceIconData data)
    {
        GameObject t = data.m_go.transform.GetChild(Hud_Awake_Patch.ChildNumber).gameObject;
        if (!p)
        {
            t.SetActive(false);
            return;
        }
        t.SetActive(PlayerState.PlayerInsideBlueprint);
    }
    [UsedImplicitly] private static void Prefix(Hud __instance)
    {
        if (__instance.m_pieceIcons == null) return;
        foreach (Hud.PieceIconData data in __instance.m_pieceIcons)
        {
            data.m_go.transform.GetChild(Hud_Awake_Patch.ChildNumber).gameObject.SetActive(false);
        }
    }
    [UsedImplicitly] private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        MethodInfo method = AccessTools.DeclaredPropertySetter(typeof(Image), nameof(Image.sprite));
        foreach (CodeInstruction instruction in code)
        { 
            yield return instruction;
            if (instruction.opcode != OpCodes.Callvirt || !ReferenceEquals(instruction.operand, method)) continue;
            yield return new CodeInstruction(OpCodes.Ldloc_S, 12);
            yield return new CodeInstruction(OpCodes.Ldloc_S, 11);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(typeof(Hud_UpdatePieceList_Patch), nameof(SpriteEnabler)));
        }
    }
}
[HarmonyPatch(typeof(CraftingStation),nameof(CraftingStation.HaveBuildStationInRange))]
public static class CraftingStation_HaveBuildStationInRange_Patch
{
    [UsedImplicitly] private static void Postfix(string name, ref CraftingStation __result)
    {
        if (name == "$kg_blueprint_piece") __result = Utils.GetBlueprintFakeStation();
    }
}