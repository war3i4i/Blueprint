namespace kg_Blueprint;

public static class BlueprintUI
{
    private static KeyValuePair<Piece, BlueprintRoot> _Internal_SelectedPiece;
    private static GameObject CopyFrom;
    private static GameObject UI;
    private static GameObject Entry;
    private static Transform Content;
    public static bool IsVisible => UI && UI.activeSelf;
    public static void Init()
    {
        UI = Object.Instantiate(kg_Blueprint.Asset.LoadAsset<GameObject>("kg_BlueprintUI"));
        CopyFrom = kg_Blueprint.Asset.LoadAsset<GameObject>("kg_BlueprintCopyFrom");
        Object.DontDestroyOnLoad(UI);
        UI.SetActive(false);
        Entry = UI.transform.Find("Canvas/UI/Scroll View/Viewport/Content/BlueprintEntry").gameObject;
        Entry.SetActive(false);
        Content = Entry.transform.parent;
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
        GameObject entry = Object.Instantiate(Entry, Content);
        entry.SetActive(true);
        entry.transform.Find("Name").GetComponent<TMP_Text>().text = root.Name;
        if (!string.IsNullOrWhiteSpace(root.Icon) && ObjectDB.instance.m_itemByHash.TryGetValue(root.Icon.GetStableHashCode(), out GameObject item)) 
            entry.transform.Find("Icon").GetComponent<Image>().sprite = item.GetComponent<ItemDrop>().m_itemData.GetIcon();
        entry.transform.Find("Selection").GetComponent<Button>().onClick.AddListener(() =>
        {
            Hide();
            OnSelect(root);
        });
        entry.transform.Find("Delete").GetComponent<Button>().onClick.AddListener(() =>
        {
            UnifiedPopup.Pop();
            UnifiedPopup.Push(new YesNoPopup("Delete Blueprint", $"Are you sure you want to delete blueprint {root.Name}?", () =>
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
            RenameBlueprintRoot renamer = new(root, (newName) =>
            {
                entry.transform.Find("Name").GetComponent<TMP_Text>().text = newName;
            });
            TextInput.instance.RequestText(renamer, "Rename Blueprint", 40);
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
            foreach (Component comp in go.GetComponentsInChildren<Component>(true))
            {
                if (comp is not Renderer and not MeshFilter and not Transform and not Animator) Object.DestroyImmediate(comp);
            }
        }
        _Internal_SelectedPiece.Key.m_resources = blueprint.GetRequirements();
        _Internal_SelectedPiece.Key.gameObject.SetActive(true);
        Player.m_localPlayer.SetupPlacementGhost();
    }

    private static void Show() => UI.SetActive(true);
    public static void Hide() => UI.SetActive(false);
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
            if (piece.name != "kg_Blueprint_Internal_PlacePiece") return;
            Vector3 pos = obj.transform.position;
            Quaternion rot = obj.transform.rotation;
            Object.Destroy(obj);
            BlueprintRoot blueprint = _Internal_SelectedPiece.Value;
            if (!Input.GetKey(KeyCode.LeftShift))
            {
                Object.Destroy(_Internal_SelectedPiece.Key);
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
}