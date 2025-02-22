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
public class BlueprintPiece : MonoBehaviour, Interactable, Hoverable, TextReceiver
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
    private Vector3 StartPoint_BottomCenter
    { 
        get
        {
            _blueprintArea.gameObject.SetActive(true);
            float y = _blueprintArea.bounds.min.y;
            Vector3 result = new Vector3(_blueprintArea.bounds.center.x, y, _blueprintArea.bounds.center.z);
            _blueprintArea.gameObject.SetActive(false);
            return result;
        }
    }
    private bool CreateBlueprint(string bpName, out string reason)
    {
        reason = null;
        Vector3 start = StartPoint_BottomCenter;
        Piece[] pieces = _blueprintArea.GetObjectsInside(_piece);
        if (pieces.Length == 0)
        {
            reason = "$kg_blueprint_createblueprint_no_objects";
            return false;
        }
        BlueprintRoot root = BlueprintRoot.CreateNew(bpName, transform.rotation.eulerAngles, start, pieces);
        Quaternion oldRotation = transform.rotation;
        _view.gameObject.SetActive(false);
        _interact.gameObject.SetActive(false);
        bool projectorsActive = _projectors.gameObject.activeSelf;
        _projectors.gameObject.SetActive(true);
        for (int i = 0; i < pieces.Length; ++i)
        {
            Piece p = pieces[i];
            p.transform.SetParent(transform);
        }
        string[] previews = new string[3];
        previews[0] = PhotoManager.MakeSprite(gameObject, 1f, 1f, Quaternion.Euler(30f, 0f, 0f));
        previews[1] = PhotoManager.MakeSprite(gameObject, 1f, 1f, Quaternion.Euler(-30f, 180f, 0f));
        previews[2] = PhotoManager.MakeSprite(gameObject, 1f, 1f, Quaternion.Euler(60f, 330f, 330f));
        
        root.SetPreviews(previews); 
        transform.rotation = oldRotation; 
        for (int i = 0; i < pieces.Length; ++i)
        {
            Piece p = pieces[i];
            p.transform.SetParent(null); 
        }
        _view.gameObject.SetActive(true);
        _interact.gameObject.SetActive(true);
        _projectors.gameObject.SetActive(projectorsActive);
        string path = Path.Combine(kg_Blueprint.BlueprintsPath, bpName + ".yml");
        string data = new Serializer().Serialize(root);
        path.WriteWithDupes(data);
        root.AssignPath(path);
        if (Configs.AutoAddBlueprintsToUI.Value) BlueprintUI.AddEntry(root, true);
        return true;
    }
    public void DestroyAllPiecesInside()
    {
        Piece[] pieces = _blueprintArea.GetObjectsInside(_piece);
        for (int i = 0; i < pieces.Length; ++i)
        {
            Piece p = pieces[i];
            p.m_nview?.ClaimOwnership();
            ZNetScene.instance.Destroy(p.gameObject);
        }   
    }
    public void Load(BlueprintRoot root)
    {
        if (root == null) return;
        StartCoroutine(_Internal_Load(root));
    }
    private IEnumerator _Internal_Load(BlueprintRoot root)
    {
        Vector3 center = StartPoint_BottomCenter;
        const int maxPerFrame = 1;
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
            Quaternion rot = Quaternion.Euler(obj.Rotation) * deltaRotation;
            GameObject go = Object.Instantiate(prefab, pos, rot);
            Piece p = go.GetComponent<Piece>();
            p.m_nview.m_zdo.Set("kg_Blueprint", true);
            Piece_Awake_Patch.DeactivatePiece(p);
            yield return null; yield return null; yield return null;
        }
    }
    public bool Interact(Humanoid user, bool hold, bool alt)
    {
        if (user.GetHoverObject() is not {} obj) return false;
        if (obj != _interact.gameObject) return false;
        if (Input.GetKey(KeyCode.C))
        {
            DestroyAllPiecesInside();
            this._znv.ClaimOwnership();
            ZNetScene.instance.Destroy(gameObject);
            return true;
        }
        if (!alt) TextInput.instance.RequestText(this, "$kg_blueprint_createblueprint_title", 30);
        else
        {
            UnifiedPopup.Push(new YesNoPopup("$kg_blueprint_clear", "$kg_blueprint_clear_desc", () =>
            { 
                UnifiedPopup.Pop();
                DestroyAllPiecesInside();
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
               "[<color=yellow><b>C + $KEY_Use</b></color>] <color=red>$kg_blueprint_delete $kg_blueprint_box</color>".Localize();
    } 
    public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;
    public string GetHoverName() => "$kg_blueprint_piece";
    public string GetText() => "";
    public void SetText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (CreateBlueprint(text, out string reason)) return;
        MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, reason.Localize());
    }
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
    private static bool Prefix(Piece piece, ref bool __result)
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