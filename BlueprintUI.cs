using System.Diagnostics;
using kg_Blueprint.Managers;

namespace kg_Blueprint;
public static class InteractionUI
{
    private static GameObject UI;

    private static bool IsVisible => UI && UI.activeSelf;
    private static BlueprintSource Current;
    private static TMP_InputField InputField;
    private static Transform ReqsContent;
    private static Transform PiecesContent;
    private static GameObject Entry;
    private static RawImage Icon;
    private static Texture OriginalIcon;
    private static readonly RawImage[] Previews = new RawImage[3];
    public static void Init()
    { 
        UI = Object.Instantiate(kg_Blueprint.Asset.LoadAsset<GameObject>("kg_BlueprintInteractUI"));
        Object.DontDestroyOnLoad(UI);
        UI.SetActive(false);
        Entry = UI.transform.Find("Canvas/UI/Pieces/Viewport/Content/Entry").gameObject;
        ReqsContent = UI.transform.Find("Canvas/UI/Reqs/Viewport/Content");
        PiecesContent = UI.transform.Find("Canvas/UI/Pieces/Viewport/Content");
        Icon = UI.transform.Find("Canvas/UI/Icon").GetComponent<RawImage>();
        OriginalIcon = Icon.texture;
        InputField = UI.transform.Find("Canvas/UI/Input").GetComponent<TMP_InputField>();
        InputField.onSubmit.AddListener(SaveBlueprint);
        Previews[0] = UI.transform.Find("Canvas/UI/Preview1/Img").GetComponent<RawImage>();
        Previews[1] = UI.transform.Find("Canvas/UI/Preview2/Img").GetComponent<RawImage>();
        Previews[2] = UI.transform.Find("Canvas/UI/Preview3/Img").GetComponent<RawImage>();
        Button paste = UI.transform.Find("Canvas/UI/Paste").GetComponent<Button>();
        paste.onClick.AddListener(() =>
        {
            Stopwatch dbg_clipboard_watch = Stopwatch.StartNew();
            Texture2D icon = ClipboardHandler_Image.GetImage(256, 256);
            kg_Blueprint.Logger.LogDebug($"Trying to paste textured via clipboard. Icon is {icon}. Took {dbg_clipboard_watch.ElapsedMilliseconds}ms");
            Icon.texture = icon ? icon : OriginalIcon;
        });
        Localization.instance.Localize(UI.transform);
    }
    public static void Update()
    {
        bool isVisible = IsVisible;
        if (Input.GetKeyDown(KeyCode.Escape) && isVisible) 
        {
            Hide();
            return;
        }
        if (isVisible) InputField.Select();
        if (isVisible && Current is MonoBehaviour mono && !mono) Hide();
    }
    private static void UpdateCanvases() 
    {
        List<ContentSizeFitter> fitters = UI.GetComponentsInChildren<ContentSizeFitter>().ToList();
        Canvas.ForceUpdateCanvases();
        foreach (ContentSizeFitter fitter in fitters)
        {
            fitter.enabled = false;
            fitter.enabled = true;
        }
        Canvas.ForceUpdateCanvases();
        foreach (ContentSizeFitter fitter in fitters)
        {
            fitter.enabled = false;
            fitter.enabled = true;
        } 
    }
    private static void SaveBlueprint(string name)
    {
        Hide();
        if (string.IsNullOrWhiteSpace(name) || Current == null) return;
        Texture2D icon = Icon.texture == OriginalIcon ? null : Icon.texture as Texture2D;
        if (Current.CreateBlueprint(name, null, Game.instance.m_playerProfile.m_playerName, icon, out string reason)) MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, $"<color=green>{name}</color> $kg_blueprint_saved".Localize());
        else MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, reason.Localize());
    } 
    public static void Show(BlueprintSource source) 
    {
        if (source == null) return; 
        InputField.text = "";
        Icon.texture = OriginalIcon;
        ReqsContent.RemoveAllChildrenExceptFirst();
        PiecesContent.RemoveAllChildrenExceptFirst();
        Current = source;
        GameObject[] inside = source.GetObjectedInside;
        int[] objects = inside.Select(o => o.name.Replace("(Clone)", "").GetStableHashCode()).ToArray();
        Piece.Requirement[] reqs = objects.GetRequirements();
        for (int i = 0; i < reqs.Length; i++)
        {
            Piece.Requirement req = reqs[i];
            GameObject entry = Object.Instantiate(Entry, ReqsContent); 
            entry.SetActive(true);
            entry.transform.Find("Icon").GetComponent<Image>().sprite = req.m_resItem.m_itemData.GetIcon();
            entry.transform.Find("Name").GetComponent<TMP_Text>().text = $"{req.m_resItem.m_itemData.m_shared.m_name} x{req.m_amount}".Localize();
        }
        foreach (KeyValuePair<string, Utils.NumberedData> pair in objects.GetPiecesNumbered())
        {
            GameObject entry = Object.Instantiate(Entry, PiecesContent);
            entry.SetActive(true); 
            entry.transform.Find("Icon").GetComponent<Image>().sprite = pair.Value.Icon ?? BlueprintUI.NoIcon;
            entry.transform.Find("Name").GetComponent<TMP_Text>().text = $"{pair.Key} x{pair.Value.Amount}".Localize();
        }
        Texture2D[] previews = Current.CreatePreviews(inside);
        for (int i = 0; i < 3; ++i) Previews[i].texture = previews[i];
        UI.SetActive(true);
        UpdateCanvases();
    }
    private static void Hide()
    {
        UI.SetActive(false);
    }
    [HarmonyPatch(typeof(TextInput), nameof(TextInput.IsVisible))]
    private static class TextInput_IsVisible_Patch
    {
        [UsedImplicitly] private static void Postfix(ref bool __result) => __result |= IsVisible;
    }
    [HarmonyPatch(typeof(StoreGui), nameof(StoreGui.IsVisible))]
    private static class StoreGui_IsVisible_Patch
    {
        [UsedImplicitly] private static void Postfix(ref bool __result) => __result |= IsVisible;
    }
    [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.Awake))]
    private static class FejdStartup_Awake_Patch
    {
        private static bool done;
        [UsedImplicitly] private static void Postfix(FejdStartup __instance)
        {
            if (done) return;
            done = true;
            if (__instance.transform.Find("StartGame/Panel/JoinPanel/serverCount")?.GetComponent<TextMeshProUGUI>() is not { } tmp) return;
            foreach (var componentsInChild in UI.GetComponentsInChildren<TMP_Text>(true)) componentsInChild.font = tmp.font;
        }
    }
}
public static class BlueprintUI
{
    private static BlueprintRoot Current;
    private static GameObject LastPressedEntry;
    private static KeyValuePair<Piece, BlueprintRoot> _Internal_SelectedPiece;
    private static GameObject CopyFrom;
    private static GameObject UI;
    private static GameObject BlueprintEntry;
    private static Transform ResourceContent;
    private static Transform PiecesContent;
    private static GameObject ResourceEntry;
    private static Transform Content;
    public static Sprite NoIcon;
    private static GameObject Projector;
    private static int CreatorRadius = 5;
    private static bool IsVisible => UI && UI.activeSelf;

    private static Transform Main;
    private static readonly RawImage[] Previews = new RawImage[3];
    private static TMP_Text BlueprintName, BlueprintDescription, BlueprintAuthor;
    private static RawImage ModelView;
    private static Button ModelViewStart;
    
    public static void Init()
    {
        UI = Object.Instantiate(kg_Blueprint.Asset.LoadAsset<GameObject>("kg_BlueprintUI"));
        CopyFrom = kg_Blueprint.Asset.LoadAsset<GameObject>("kg_BlueprintCopyFrom");
        NoIcon = kg_Blueprint.Asset.LoadAsset<Sprite>("kg_Blueprint_NoIcon");
        Projector = kg_Blueprint.Asset.LoadAsset<GameObject>("kg_Blueprint_Projector");
        Object.DontDestroyOnLoad(UI); 
        UI.SetActive(false);
        Main = UI.transform.Find("Canvas/UI/Main");
        BlueprintEntry = UI.transform.Find("Canvas/UI/List/Scroll View/Viewport/Content/Entry").gameObject;
        ResourceEntry = Main.transform.Find("Reqs/Viewport/Content/Entry").gameObject;
        PiecesContent = Main.transform.Find("Pieces/Viewport/Content");
        ResourceContent = Main.transform.Find("Reqs/Viewport/Content");
        BlueprintEntry.SetActive(false); 
        ResourceEntry.SetActive(false);
        Content = BlueprintEntry.transform.parent;  
        UI.transform.Find("Canvas/UI/Create").GetComponent<Button>().onClick.AddListener(() =>
        {
            Hide(); 
            SelectBlueprintCreator();
        });
        Previews[0] = Main.transform.Find("Previews/Preview1/Img").GetComponent<RawImage>();
        Previews[1] = Main.transform.Find("Previews/Preview2/Img").GetComponent<RawImage>();
        Previews[2] = Main.transform.Find("Previews/Preview3/Img").GetComponent<RawImage>();
        Main.transform.Find("Select").GetComponent<Button>().onClick.AddListener(() =>
        {
            Hide();
            OnSelect();
        }); 
        Main.transform.Find("Buttons/Delete").GetComponent<Button>().onClick.AddListener(() =>
        {
            if (Current == null) return;
            Hide();
            UnifiedPopup.Push(new YesNoPopup("$kg_blueprint_delete", $"$kg_blueprint_confirmdelete <color=yellow>{Current.Name}</color>?", () =>
            {
                UnifiedPopup.Pop();
                Current.Delete();
                if (LastPressedEntry) Object.Destroy(LastPressedEntry);
                ResetMain();
            }, UnifiedPopup.Pop));
        }); 
        Main.transform.Find("Buttons/ShowFile").GetComponent<Button>().onClick.AddListener(() =>
        { 
            if (Current == null) return;
            if (Current.TryGetFilePath(out string path)) path.Explorer_SelectFile();
        });
        Main.transform.Find("Buttons/Rename").GetComponent<Button>().onClick.AddListener(() =>
        {
            if (Current == null) return;
            Hide();
            RenameBlueprintRoot renamer = new(Current, (newName) =>
            {
                if (LastPressedEntry) LastPressedEntry.transform.Find("Name").GetComponent<TMP_Text>().text = newName;
            });
            TextInput.instance.RequestText(renamer, "$kg_blueprint_rename", 40);
        });
        Main.transform.Find("Buttons/Load").GetComponent<Button>().onClick.AddListener(() =>
        { 
            if (Current == null) return;
            Hide();
            if (!PlayerState.PlayerInsideBlueprint || !PlayerState.BlueprintPiece)
            {
                UnifiedPopup.Push(new WarningPopup("$kg_blueprint_load_error", "$kg_blueprint_load_error_desc", UnifiedPopup.Pop));
                return;
            }
            UnifiedPopup.Push(new YesNoPopup("$kg_blueprint_load", "$kg_blueprint_confirmload".Localize(Current.Name), () =>
            {
                UnifiedPopup.Pop();
                PlayerState.BlueprintPiece.DestroyAllPiecesInside(false);
                PlayerState.BlueprintPiece.Load(Current);
            }, UnifiedPopup.Pop));
        });
        ModelView = Main.Find("ModelView/View").GetComponent<RawImage>();
        ModelView.transform.parent.gameObject.AddComponent<ModelPreview.PreviewModelAngleController>();
        ModelViewStart = Main.Find("ModelView/Show").GetComponent<Button>();
        ModelViewStart.onClick.AddListener(() =>
        {
            if (Current == null) return;
            ModelPreview.SetAsCurrent(ModelView, Current.CreateViewGameObjectForBlueprint());
            ModelViewStart.gameObject.SetActive(false);
            ModelView.gameObject.SetActive(true);
        });
        
        BlueprintName = Main.transform.Find("Name").GetComponent<TMP_Text>();
        BlueprintDescription = Main.transform.Find("Description").GetComponent<TMP_Text>();
        BlueprintAuthor = Main.transform.Find("Author").GetComponent<TMP_Text>();
        Localization.instance.Localize(UI.transform);
        ResetMain();
        InteractionUI.Init();
    }
    private static void ResetMain()
    {
        Current = null;
        Main.gameObject.SetActive(false);
        foreach (var rawImage in Previews)
        {
            rawImage.texture = null;
            rawImage.transform.parent.gameObject.SetActive(false);
        }
        PiecesContent.RemoveAllChildrenExceptFirst();
        ResourceContent.RemoveAllChildrenExceptFirst();
        BlueprintName.text = "";
        BlueprintDescription.text = "";
        BlueprintAuthor.text = "";
        ClearSelections();
        StopPreview();
    }
    public static void Update() { if (Input.GetKeyDown(KeyCode.Escape) && IsVisible) Hide(); }
    private static void UpdateCanvases() 
    {
        List<ContentSizeFitter> fitters = UI.GetComponentsInChildren<ContentSizeFitter>().ToList();
        Canvas.ForceUpdateCanvases();
        foreach (ContentSizeFitter fitter in fitters)
        {
            fitter.enabled = false;
            fitter.enabled = true;
        }
        Canvas.ForceUpdateCanvases();
        foreach (ContentSizeFitter fitter in fitters)
        {
            fitter.enabled = false;
            fitter.enabled = true;
        }
    }
    public static void Load(IList<BlueprintRoot> blueprints)
    {
        if (_Internal_SelectedPiece.Key) Object.DestroyImmediate(_Internal_SelectedPiece.Key.gameObject);
        _Internal_SelectedPiece = default;
        Player.m_localPlayer?.SetupPlacementGhost();
        Content.RemoveAllChildrenExceptFirst();
        for (int i = 0; i < blueprints.Count; i++)
        {
            BlueprintRoot blueprint = blueprints[i];
            blueprint.CachePreviews();
            AddEntry(blueprint, false);
        }
        SortEntriesByName();
        UpdateCanvases();
    }
    private static void ClearSelections()
    {
        UIInputHandler[] UIInputHandlers = Content.GetComponentsInChildren<UIInputHandler>();
        for (int i = 0; i < UIInputHandlers.Length; i++) UIInputHandlers[i].transform.Find("Selection").GetComponent<Image>().color = Color.clear;
    }
    public static void AddEntry(BlueprintRoot root, bool updateCanvases)
    {
        GameObject entry = Object.Instantiate(BlueprintEntry, Content);
        entry.SetActive(true);
        entry.transform.Find("Name").GetComponent<TMP_Text>().text = root.Name;
        if (root.Icon.ToIcon() is {} icon) entry.transform.Find("Icon").GetComponent<RawImage>().texture = icon;

        UIInputHandler handler = entry.GetComponent<UIInputHandler>();
        handler.m_onLeftClick += (_) =>
        {
            ShowBlueprint(entry, root);
            handler.transform.Find("Selection").GetComponent<Image>().color = Color.green;
        };
        handler.m_onPointerEnter += (_) => 
        {
            var img = entry.transform.Find("Selection").GetComponent<Image>();
            if (img.color != Color.green) img.color = Color.white;
        };
        handler.m_onPointerExit += (_) => 
        {
            var img = entry.transform.Find("Selection").GetComponent<Image>();
            if (img.color != Color.green) img.color = Color.clear;
        };
        for (int i = 3; i >= 1; --i)
        {
            Texture2D preview = root.GetPreview(i - 1);
            var previewGo = entry.transform.Find($"Preview{i}").gameObject;
            previewGo.SetActive(preview);
            if (!preview) continue;
            entry.transform.Find($"Preview{i}/Img").GetComponent<RawImage>().texture = preview;
        } 
        
        if (updateCanvases)
        {
            SortEntriesByName();
            UpdateCanvases();
        }
    }
    private static void SortEntriesByName()
    {
        List<Transform> children = new(Content.childCount - 1);
        for (int i = 1; i < Content.childCount; ++i) children.Add(Content.GetChild(i));
        children.Sort((a, b) => string.Compare(a.Find("Name").GetComponent<TMP_Text>().text, b.Find("Name").GetComponent<TMP_Text>().text, StringComparison.CurrentCultureIgnoreCase));
        foreach (Transform child in children) child.SetAsLastSibling();
    }
    private static void ShowBlueprint(GameObject obj, BlueprintRoot root)
    {
        ResetMain(); 
        if (Current == root || root == null) return;
        Current = root; 
        LastPressedEntry = obj;
        BlueprintName.text = Current.Name;
        BlueprintDescription.text = string.IsNullOrWhiteSpace(Current.Description) ? "$kg_blueprint_nodescription".Localize() : Current.Description;
        BlueprintAuthor.text = $"$kg_blueprint_author\n<color=green>{(string.IsNullOrWhiteSpace(Current.Author) ? "$kg_blueprint_noauthor" : Current.Author)}</color>".Localize();
        for (int i = 0; i < 3; ++i)
        {
            Previews[i].texture = Current.GetPreview(i);
            Previews[i].transform.parent.gameObject.SetActive(Previews[i].texture);
        }
        ShowResources(Current);
        Main.gameObject.SetActive(true);
        UpdateCanvases();
    }
    private static void OnSelect() 
    {
        if (_Internal_SelectedPiece.Key) Object.DestroyImmediate(_Internal_SelectedPiece.Key.gameObject);
        _Internal_SelectedPiece = default;
        if (Current == null) return;
        _Internal_SelectedPiece = new KeyValuePair<Piece, BlueprintRoot>(Object.Instantiate(CopyFrom, Vector3.zero, Quaternion.identity).GetComponent<Piece>(), Current);
        _Internal_SelectedPiece.Key.gameObject.SetActive(false);
        _Internal_SelectedPiece.Key.name = "kg_Blueprint_Internal_PlacePiece";
        _Internal_SelectedPiece.Key.m_name = Current.Name;
        _Internal_SelectedPiece.Key.m_extraPlacementDistance = 20;
        for (int i = 0; i < Current.Objects.Length; ++i)
        {
            BlueprintObject obj = Current.Objects[i]; 
            GameObject prefab = ZNetScene.instance.GetPrefab(obj.Id);
            if (!prefab) continue;
            GameObject go = Object.Instantiate(prefab, _Internal_SelectedPiece.Key.transform);
            go.transform.position = obj.RelativePosition;
            go.transform.rotation = Quaternion.Euler(obj.Rotation);
            foreach (Component comp in go.GetComponentsInChildren<Component>(true).Reverse())
            {
                if (comp is not Renderer and not MeshFilter and not Transform and not Animator) Object.DestroyImmediate(comp);
            }
        }
        _Internal_SelectedPiece.Key.m_resources = Current.GetRequirements();
        _Internal_SelectedPiece.Key.gameObject.SetActive(false);
        Player.m_localPlayer?.SetupPlacementGhost();
    }
    private static void SelectBlueprintCreator() 
    {
        if (_Internal_SelectedPiece.Key) Object.DestroyImmediate(_Internal_SelectedPiece.Key.gameObject);
        _Internal_SelectedPiece = new KeyValuePair<Piece, BlueprintRoot>(Object.Instantiate(CopyFrom, Vector3.zero, Quaternion.identity).GetComponent<Piece>(), null);
        _Internal_SelectedPiece.Key.gameObject.SetActive(false);
        _Internal_SelectedPiece.Key.name = "kg_Blueprint_Internal_Creator";
        _Internal_SelectedPiece.Key.m_name = "$kg_blueprint_creator";
        _Internal_SelectedPiece.Key.m_description = "$kg_blueprint_creator_desc";
        _Internal_SelectedPiece.Key.m_extraPlacementDistance = 20;
        _Internal_SelectedPiece.Key.m_resources = [];
        _Internal_SelectedPiece.Key.m_clipEverything = true;
        _Internal_SelectedPiece.Key.m_noInWater = false;
        var proj = _Internal_SelectedPiece.Key.gameObject.AddComponent<CircleProjector>();
        proj.m_prefab = BlueprintUI.Projector; 
        proj.m_radius = CreatorRadius; 
        proj.m_nrOfSegments = CreatorRadius * 4; 
        proj.m_mask.value = 2048;
        Player.m_localPlayer?.SetupPlacementGhost();
    }
    private static void ShowResources(BlueprintRoot root)
    {
        ResourceContent.RemoveAllChildrenExceptFirst();
        Piece.Requirement[] reqs = root.GetRequirements();
        Recipe temp = ScriptableObject.CreateInstance<Recipe>();
        temp.name = "kg_Blueprint_Temp";
        temp.m_resources = new Piece.Requirement[1];
        for (int i = 0; i < reqs.Length; i++)
        { 
            Piece.Requirement req = reqs[i];
            temp.m_resources[0] = req;
            string text = $"{req.m_resItem.m_itemData.m_shared.m_name} x{req.m_amount}";
            GameObject entry = Object.Instantiate(ResourceEntry, ResourceContent);
            entry.SetActive(true);
            entry.transform.Find("Icon").GetComponent<Image>().sprite = req.m_resItem.m_itemData.GetIcon();
            entry.transform.Find("Name").GetComponent<TMP_Text>().text = text.Localize();
            entry.transform.Find("Name").GetComponent<TMP_Text>().color = Player.m_localPlayer.HaveRequirementItems(temp, false, 1) ? Color.green : Color.red;
        }
        Object.DestroyImmediate(temp);
        PiecesContent.RemoveAllChildrenExceptFirst(); 
        foreach (KeyValuePair<string, Utils.NumberedData> pair in root.GetPiecesNumbered())
        {
            GameObject entry = Object.Instantiate(ResourceEntry, PiecesContent);
            entry.SetActive(true);
            entry.transform.Find("Icon").GetComponent<Image>().sprite = pair.Value.Icon ?? NoIcon;
            entry.transform.Find("Name").GetComponent<TMP_Text>().text = $"{pair.Key} x{pair.Value.Amount}".Localize();
        }
    }
    private static void Show() => UI.SetActive(true);
    private static void StopPreview()
    {
        ModelViewStart.gameObject.SetActive(true);
        ModelView.texture = null;
        ModelView.gameObject.SetActive(false);
        ModelPreview.StopPreview();
    }
    private static void Hide()
    {
        StopPreview();
        UI.SetActive(false);
    }

    [HarmonyPatch(typeof(Hud),nameof(Hud.IsPieceSelectionVisible))]
    private static class Hud_IsPieceSelectionVisible_Patch
    {
        [UsedImplicitly] private static void Postfix(ref bool __result) => __result |= IsVisible;
    }
    [HarmonyPatch(typeof(Hud),nameof(Hud.HidePieceSelection))]
    private static class Hud_HidePieceSelection_Patch
    {
        [UsedImplicitly] private static void Postfix(Hud __instance) => Hide();
    }
    [HarmonyPatch(typeof(Humanoid),nameof(Humanoid.UnequipItem))]
    private static class Humanoid_UnequipItem_Patch
    {
        [UsedImplicitly] private static void Postfix(Humanoid __instance, ItemDrop.ItemData item)
        {
            if (Player.m_localPlayer != __instance) return;
            if (item?.m_dropPrefab.name == "kg_BlueprintHammer")
            {
                Hide();
                if (Configs.RemoveBlueprintPlacementOnUnequip.Value)
                {
                    if (_Internal_SelectedPiece.Key) Object.Destroy(_Internal_SelectedPiece.Key.gameObject);
                    _Internal_SelectedPiece = default;
                }
            }
        }
    }
    private static bool IsHoldingHammer => Player.m_localPlayer?.m_rightItem?.m_dropPrefab?.name == "kg_BlueprintHammer";
    [HarmonyPatch(typeof(Hud),nameof(Hud.TogglePieceSelection))]
    private static class Hud_UpdateBuild_Patch
    {
        [UsedImplicitly] private static bool Prefix(Hud __instance)
        {
            if (IsVisible)
            {
                Hide();
                return false;
            }
            if (!IsHoldingHammer) return true;
            Show();
            return false;
        }
    }
    [HarmonyPatch(typeof(PieceTable),nameof(PieceTable.GetSelectedPiece))]
    private static class PieceTable_GetSelectedPiece_Patch
    {
        [UsedImplicitly] private static void Postfix(PieceTable __instance, ref Piece __result)
        {
            if (!IsHoldingHammer) return;
            __result = _Internal_SelectedPiece.Key;
        }
    }
    [HarmonyPatch(typeof(Player),nameof(Player.GetBuildSelection))]
    private static class Player_GetBuildSelection_Patch
    {
        [UsedImplicitly] private static void Postfix(Player __instance, ref Piece go)
        {
            if (!IsHoldingHammer) return;
            go = _Internal_SelectedPiece.Key;
        }
    }
    [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
    public static class Player_PlacePiece_Patch
    {
        public static void PlacedPiece(GameObject obj)
        {
            if (obj?.GetComponent<Piece>() is not { } piece) return;
            string name = piece.name.Replace("(Clone)", "");
            if (name == "kg_Blueprint_Internal_PlacePiece")
            {
                Vector3 pos = obj.transform.position;
                Quaternion rot = obj.transform.rotation;
                Object.Destroy(obj);
                BlueprintRoot blueprint = _Internal_SelectedPiece.Value;
                if (!Input.GetKey(KeyCode.LeftShift))
                {
                    if (_Internal_SelectedPiece.Key) Object.Destroy(_Internal_SelectedPiece.Key);
                    _Internal_SelectedPiece = default;
                    Player.m_localPlayer.SetupPlacementGhost();
                }
                blueprint?.Apply(pos, rot);
            }
            if (name == "kg_Blueprint_Internal_Creator")
            {
                Vector3 pos = obj.transform.position;
                pos.y = Mathf.Max(30f, ZoneSystem.instance.GetGroundHeight(pos));
                Object.Destroy(obj);
                BlueprintCircleCreator circleCreator = new(pos, CreatorRadius, 80f);
                InteractionUI.Show(circleCreator);
            }
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
    [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.Awake))]
    private static class FejdStartup_Awake_Patch
    {
        private static bool done;
        [UsedImplicitly] private static void Postfix(FejdStartup __instance)
        {
            if (done) return;
            done = true;
            if (__instance.transform.Find("StartGame/Panel/JoinPanel/serverCount")?.GetComponent<TextMeshProUGUI>() is not { } tmp) return;
            foreach (var componentsInChild in UI.GetComponentsInChildren<TMP_Text>(true)) componentsInChild.font = tmp.font;
        }
    }
    [HarmonyPatch(typeof(KeyHints),nameof(KeyHints.Awake))]
    private static class KeyHints_Awake_Patch 
    {
        public static GameObject KeyHint_LeftControl_Snap;
        [UsedImplicitly] private static void Postfix(KeyHints __instance)
        {
            var copyFrom = __instance.m_buildHints.transform.Find("Keyboard/Place");
            if (copyFrom is null) return;
            KeyHint_LeftControl_Snap = Object.Instantiate(copyFrom.gameObject, copyFrom.parent);
            KeyHint_LeftControl_Snap.name = "KeyHint_LeftControl_Snap";
            KeyHint_LeftControl_Snap.transform.SetAsFirstSibling();
            KeyHint_LeftControl_Snap.transform.Find("Text").GetComponent<TMP_Text>().text = "$kg_blueprint_snap".Localize();
            KeyHint_LeftControl_Snap.transform.Find("Text").GetComponent<TMP_Text>().color = new Color(0.16f, 0.53f, 1f);
            KeyHint_LeftControl_Snap.transform.Find("key_bkg/Key").GetComponent<TMP_Text>().text = "LeftCtrl";
            KeyHint_LeftControl_Snap.transform.Find("key_bkg/Key").GetComponent<TMP_Text>().color = new Color(0.16f, 0.53f, 1f);
            KeyHint_LeftControl_Snap.SetActive(false);
        }
    }
    [HarmonyPatch(typeof(KeyHints),nameof(KeyHints.UpdateHints))]
    private static class KeyHints_UpdateHints_Patch
    {
        [UsedImplicitly] private static void Postfix(KeyHints __instance)
        {
            if(__instance.m_buildHints.activeSelf) KeyHints_Awake_Patch.KeyHint_LeftControl_Snap.SetActive(IsHoldingHammer);
        } 
    } 
    [HarmonyPatch(typeof(Player),nameof(Player.UpdatePlacement))]
    private static class Player_UpdatePlacement_Patch
    {
        private static void MouseScroll(Piece p, bool add)
        {
            if (p.name != "kg_Blueprint_Internal_Creator") return;
            CircleProjector proj = p.GetComponent<CircleProjector>();
            CreatorRadius = Mathf.Clamp(CreatorRadius + (add ? 1 : -1), 5, 17);
            proj.m_radius = CreatorRadius;
            proj.m_nrOfSegments = CreatorRadius * 4;
        }
        [UsedImplicitly] private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
        { 
            CodeMatcher matcher = new(code);
            var firstTarget = AccessTools.Method(typeof(ZInput), nameof(ZInput.GetMouseScrollWheel));
            matcher.MatchForward(false, new CodeMatch(OpCodes.Call, firstTarget));
            var field = AccessTools.Field(typeof(Player), nameof(Player.m_placeRotation));
            matcher.MatchForward(false, new CodeMatch(OpCodes.Stfld, field));
            matcher.Advance(1);
            matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_S, 8));
            matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Ldc_I4_1));
            matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Player_UpdatePlacement_Patch), nameof(MouseScroll))));
            matcher.MatchForward(false, new CodeMatch(OpCodes.Stfld, field));
            matcher.Advance(1);
            matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_S, 8)); 
            matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Ldc_I4_0));
            matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Player_UpdatePlacement_Patch), nameof(MouseScroll))));
            matcher.MatchForward(false, new CodeMatch((i) => i.opcode == OpCodes.Leave || i.opcode == OpCodes.Leave_S));
            if (!matcher.IsValid) return matcher.Instructions();
            matcher.Advance(1);
            matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Nop));
            return matcher.Instructions();
        }
    } 
}