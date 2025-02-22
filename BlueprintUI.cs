namespace kg_Blueprint;

public static class BlueprintUI
{
    private static KeyValuePair<Piece, BlueprintRoot> _Internal_SelectedPiece;
    private static GameObject CopyFrom;
    private static GameObject UI;
    private static GameObject BlueprintEntry;
    private static GameObject ResourcesTab;
    private static Transform ResourceContent;
    private static GameObject PiecesTab;
    private static Transform PiecesContent;
    private static GameObject ResourceEntry;
    private static Transform Content;
    public static bool IsVisible => UI && UI.activeSelf;
    public static void Init()
    {
        UI = Object.Instantiate(kg_Blueprint.Asset.LoadAsset<GameObject>("kg_BlueprintUI"));
        CopyFrom = kg_Blueprint.Asset.LoadAsset<GameObject>("kg_BlueprintCopyFrom");
        Object.DontDestroyOnLoad(UI);
        UI.SetActive(false);
        BlueprintEntry = UI.transform.Find("Canvas/UI/Scroll View/Viewport/Content/BlueprintEntry").gameObject;
        ResourcesTab = UI.transform.Find("Canvas/UI/Resources").gameObject;
        ResourceEntry = ResourcesTab.transform.Find("Scroll View/Viewport/Content/ResourceEntry").gameObject;
        ResourceContent = ResourcesTab.transform.Find("Scroll View/Viewport/Content");
        PiecesTab = UI.transform.Find("Canvas/UI/Pieces").gameObject;
        PiecesContent = UI.transform.Find("Canvas/UI/Pieces/Scroll View/Viewport/Content");
        BlueprintEntry.SetActive(false);
        ResourceEntry.SetActive(false);
        Content = BlueprintEntry.transform.parent;
        ResourcesTab.SetActive(false);  
        PiecesTab.SetActive(false);
        Localization.instance.Localize(UI.transform);
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
    public static void Load(IList<BlueprintRoot> blueprints)
    {
        if (_Internal_SelectedPiece.Key) Object.DestroyImmediate(_Internal_SelectedPiece.Key.gameObject);
        _Internal_SelectedPiece = default;
        Player.m_localPlayer?.SetupPlacementGhost();
        Content.RemoveAllChildrenExceptFirst();
        for (int i = 0; i < blueprints.Count; i++)
        {
            BlueprintRoot blueprint = blueprints[i];
            AddEntry(blueprint, false);
        }
        UpdateCanvases();
    }
    public static void AddEntry(BlueprintRoot root, bool updateCanvases)
    {
        GameObject entry = Object.Instantiate(BlueprintEntry, Content);
        entry.SetActive(true);
        entry.transform.Find("Name").GetComponent<TMP_Text>().text = root.Name;
        if (root.Icon.ToIcon() is {} icon) entry.transform.Find("Icon").GetComponent<RawImage>().texture = icon;

        Transform selection = entry.transform.Find("Selection");
        UIInputHandler selectionHandler = selection.GetComponent<UIInputHandler>();
        selectionHandler.m_onLeftClick += (_) =>
        {
            Hide();
            OnSelect(root);
        };
        selectionHandler.m_onPointerEnter += (_) =>
        { 
            Image img = selection.GetComponent<Image>();
            img.color = new Color(img.color.r, img.color.g, img.color.b, 0.2f);
            ShowResources(root);
        };
        selectionHandler.m_onPointerExit += (_) =>
        { 
            Image img = selection.GetComponent<Image>();
            img.color = new Color(img.color.r, img.color.g, img.color.b, 0f);
            HideResources();
        };
        entry.transform.Find("Delete").GetComponent<Button>().onClick.AddListener(() =>
        {
            Hide();
            UnifiedPopup.Push(new YesNoPopup("$kg_blueprint_delete", $"$kg_blueprint_confirmdelete <color=yellow>{root.Name}</color>?", () =>
            {
               UnifiedPopup.Pop();
               root.Delete();
               Object.Destroy(entry);
            }, UnifiedPopup.Pop));
        });
        entry.transform.Find("ShowFile").GetComponent<Button>().onClick.AddListener(() =>
        {
            if (root.TryGetFilePath(out string path)) path.Explorer_SelectFile();
        });
        entry.transform.Find("Rename").GetComponent<Button>().onClick.AddListener(() =>
        {
            Hide();
            RenameBlueprintRoot renamer = new(root, (newName) =>
            {
                entry.transform.Find("Name").GetComponent<TMP_Text>().text = newName;
            });
            TextInput.instance.RequestText(renamer, "$kg_blueprint_rename", 40);
        });
        entry.transform.Find("Load").GetComponent<Button>().onClick.AddListener(() =>
        { 
            Hide();
            if (!PlayerState.PlayerInsideBlueprint || !PlayerState.BlueprintPiece)
            {
                UnifiedPopup.Push(new WarningPopup("$kg_blueprint_load_error", "$kg_blueprint_load_error_desc", UnifiedPopup.Pop));
                return;
            }
            UnifiedPopup.Push(new YesNoPopup("$kg_blueprint_load", "$kg_blueprint_confirmload".Localize(root.Name), () =>
            {
                UnifiedPopup.Pop();
                PlayerState.BlueprintPiece.DestroyAllPiecesInside();
                PlayerState.BlueprintPiece.Load(root);
            }, UnifiedPopup.Pop));
        });
        if (root.Previews.Length > 0)
        {
            for (int p = 3; p >= 1; --p)
            {
                if (p > root.Previews.Length) continue;
                entry.transform.Find($"Preview{p}").gameObject.SetActive(true);
                entry.transform.Find($"Preview{p}/Img").GetComponent<RawImage>().texture = root.GetPreview(p - 1);
            }
        }
        if (updateCanvases) UpdateCanvases();
    }
    private static void OnSelect(BlueprintRoot blueprint) 
    {
        if (_Internal_SelectedPiece.Key) Object.DestroyImmediate(_Internal_SelectedPiece.Key.gameObject);
        _Internal_SelectedPiece = new KeyValuePair<Piece, BlueprintRoot>(Object.Instantiate(CopyFrom, Vector3.zero, Quaternion.identity).GetComponent<Piece>(), blueprint);
        _Internal_SelectedPiece.Key.gameObject.SetActive(false); 
        _Internal_SelectedPiece.Key.name = $"kg_Blueprint_Internal_PlacePiece";
        _Internal_SelectedPiece.Key.m_name = blueprint.Name;
        _Internal_SelectedPiece.Key.m_icon = string.IsNullOrWhiteSpace(blueprint.Icon) ? _Internal_SelectedPiece.Key.m_icon : ZNetScene.instance.GetPrefab(blueprint.Icon)?.GetComponent<ItemDrop>().m_itemData.GetIcon();
        for (int i = 0; i < blueprint.Objects.Length; ++i)
        {
            BlueprintObject obj = blueprint.Objects[i];
            GameObject prefab = ZNetScene.instance.GetPrefab(obj.Id);
            if (!prefab) continue;
            Piece piece = prefab.GetComponent<Piece>();  
            if (!piece) continue;
            GameObject go = Object.Instantiate(prefab, _Internal_SelectedPiece.Key.transform);
            go.transform.position = obj.RelativePosition;
            go.transform.rotation = Quaternion.Euler(obj.Rotation);
            foreach (Component comp in go.GetComponentsInChildren<Component>(true).Reverse())
            {
                if (comp is not Renderer and not MeshFilter and not Transform and not Animator) Object.DestroyImmediate(comp);
            }
        }
        _Internal_SelectedPiece.Key.m_resources = blueprint.GetRequirements();
        _Internal_SelectedPiece.Key.gameObject.SetActive(true);
        Player.m_localPlayer?.SetupPlacementGhost();
    }
    private static void ShowResources(BlueprintRoot root)
    {
        ResourcesTab.SetActive(true);
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
        PiecesTab.SetActive(true);
        PiecesContent.RemoveAllChildrenExceptFirst();
        foreach (KeyValuePair<Piece, int> pair in root.GetPiecesNumbered())
        {
            GameObject entry = Object.Instantiate(ResourceEntry, PiecesContent);
            entry.SetActive(true);
            entry.transform.Find("Icon").GetComponent<Image>().sprite = pair.Key.m_icon;
            entry.transform.Find("Name").GetComponent<TMP_Text>().text = $"{pair.Key.m_name} x{pair.Value}".Localize();
        }
    }
    private static void HideResources() { ResourcesTab.SetActive(false); PiecesTab.SetActive(false); }
    private static void Show() => UI.SetActive(true);
    public static void Hide()
    {
        UI.SetActive(false);
        foreach (var componentsInChild in Content.GetComponentsInChildren<UIInputHandler>())
        {
            Image img = componentsInChild.GetComponent<Image>();
            img.color = new Color(img.color.r, img.color.g, img.color.b, 0f);
        }
        HideResources();
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
            if (item?.m_dropPrefab.name == "kg_BlueprintHammer") Hide();
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
            if (piece.name.Replace("(Clone)", "") != "kg_Blueprint_Internal_PlacePiece") return;
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
        [UsedImplicitly]
        private static void Postfix(FejdStartup __instance)
        {
            if (done) return;
            done = true;
            if (__instance.transform.Find("StartGame/Panel/JoinPanel/serverCount")?.GetComponent<TextMeshProUGUI>() is not { } tmp) return;
            foreach (var componentsInChild in UI.GetComponentsInChildren<TMP_Text>()) componentsInChild.font = tmp.font;
        }
    }
}