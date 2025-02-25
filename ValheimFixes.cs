namespace kg_Blueprint;

public static class ValheimFixes
{
    /*[HarmonyPatch(typeof(Hud),nameof(Hud.Awake))]
    public static class Hud_Awake_Patch
    {
        public static Vector2 OriginalSize;
        public static RectTransform Rect;
        [UsedImplicitly] private static void Postfix(Hud __instance) 
        {
            OriginalSize = (__instance.m_requirementItems[0].transform.parent.parent.transform as RectTransform)!.sizeDelta;
            Rect = (__instance.m_requirementItems[0].transform.parent.parent.transform as RectTransform);
            if (__instance.m_requirementItems[0].transform.parent.gameObject.GetComponent<HorizontalLayoutGroup>()) return;
            HorizontalLayoutGroup HorizontalLayoutGroup = __instance.m_requirementItems[0].transform.parent.gameObject.AddComponent<HorizontalLayoutGroup>();
            RectTransform rect = HorizontalLayoutGroup.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(0, rect.anchoredPosition.y);
            HorizontalLayoutGroup.childControlWidth = false;
            HorizontalLayoutGroup.childControlHeight = false;
            HorizontalLayoutGroup.childForceExpandWidth = false;
            HorizontalLayoutGroup.childForceExpandHeight = false;
            HorizontalLayoutGroup.spacing = 6;
            HorizontalLayoutGroup.childAlignment = TextAnchor.MiddleCenter;
            ContentSizeFitter SizeFitter = __instance.m_requirementItems[0].transform.parent.gameObject.AddComponent<ContentSizeFitter>();
            SizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    } */
    [HarmonyPatch(typeof(Hud),nameof(Hud.Awake))]
    public static class Hud_Awake_Patch
    {
        public static Vector2 OriginalSize;
        public static RectTransform Rect;
        [UsedImplicitly] private static void Postfix(Hud __instance) 
        {
            OriginalSize = (__instance.m_requirementItems[0].transform.parent.parent.transform as RectTransform)!.sizeDelta;
            Rect = (__instance.m_requirementItems[0].transform.parent.parent.transform as RectTransform);
            if (__instance.m_requirementItems[0].transform.parent.gameObject.GetComponent<GridLayoutGroup>()) return;
            GridLayoutGroup gridlayoutgroup = __instance.m_requirementItems[0].transform.parent.gameObject.AddComponent<GridLayoutGroup>();
            RectTransform rect = gridlayoutgroup.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(0, rect.anchoredPosition.y);
            gridlayoutgroup.cellSize = new Vector2(60, 60);
            gridlayoutgroup.spacing = new Vector2(6, 6); 
            gridlayoutgroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridlayoutgroup.constraintCount = 20;
            gridlayoutgroup.childAlignment = TextAnchor.MiddleCenter;
            gridlayoutgroup.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridlayoutgroup.startCorner = GridLayoutGroup.Corner.LowerLeft;
            gridlayoutgroup.childAlignment = TextAnchor.MiddleCenter;
            ContentSizeFitter SizeFitter = __instance.m_requirementItems[0].transform.parent.gameObject.AddComponent<ContentSizeFitter>();
            SizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            SizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var parent = __instance.m_requirementItems[0].transform.parent.transform as RectTransform;
            parent.anchorMin = new Vector2(0.5f, 0);
            parent.anchorMax = new Vector2(0.5f, 0);
            parent.pivot = new Vector2(0.5f, 0);
            parent.anchoredPosition = new Vector2(0, 10);
        }
    }
    [HarmonyPatch(typeof(Hud),nameof(Hud.SetupPieceInfo))]
    private static class Hud_SetupPieceInfo_Patch
    {
        private static void EnsureArray(Hud hud, int newAmount)
        {
            int oldAmount = hud.m_requirementItems.Length;
            hud.m_requirementItems = hud.m_requirementItems.Concat(new GameObject[newAmount - hud.m_requirementItems.Length]).ToArray();
            for (int i = oldAmount; i < newAmount; ++i)
            {
                GameObject first = hud.m_requirementItems[0];
                GameObject newRequirement = Object.Instantiate(first, first.transform.parent);
                newRequirement.SetActive(false);
                hud.m_requirementItems[i] = newRequirement;
            } 
            for (int i = 0; i < newAmount; ++i) hud.m_requirementItems[i].transform.SetAsFirstSibling();
        }
        [UsedImplicitly] private static void Prefix(Hud __instance, Piece piece)
        { 
            if (!piece || piece.m_resources == null || piece.m_resources.Length == 0) return;
            int pieceReqs = piece.m_resources.Length; 
            if (pieceReqs + 1 > __instance.m_requirementItems.Length) EnsureArray(__instance, pieceReqs + 1);
            int amountX = Mathf.Clamp(pieceReqs, 6, 20);
            int amountY = (pieceReqs - 1) / 20; 
            Hud_Awake_Patch.Rect.sizeDelta = new Vector2(amountX * 66f + 20f, Hud_Awake_Patch.OriginalSize.y + amountY * 66f);
        }
    }
    [HarmonyPatch(typeof(StaticPhysics),nameof(StaticPhysics.Awake))] 
    private static class StaticPhysics_Awake_Patch
    {
        private static void ActivateSolid(StaticPhysics physics)
        {
            physics.m_checkSolids = true;
            Transform[] children = physics.GetComponentsInChildren<Transform>();
            foreach (Transform child in children) child.gameObject.layer = nonsolidlayer;
        }
        private static readonly LayerMask nonsolidlayer = LayerMask.NameToLayer("character_noenv");
        [UsedImplicitly] private static void Postfix(StaticPhysics __instance)
        {
            if (__instance.m_nview && __instance.m_nview.IsValid() && __instance.m_nview.m_zdo.GetBool("kg_Blueprint"))
            {
                ActivateSolid(__instance);
                return;
            }
            if (!BlueprintPiece.IsInside(__instance.transform.position)) return;
            __instance.m_nview?.m_zdo.Set("kg_Blueprint", true);
            ActivateSolid(__instance);
        }
    }
    
    [HarmonyPatch(typeof(Terminal),nameof(Terminal.IsCheatsEnabled))]
    private static class Terminal_IsCheatsEnabled_Patch
    {
        [UsedImplicitly] private static void Postfix(ref bool __result) => __result = true;
    }
    [HarmonyPatch]
    private static class Player_CleanupGhostMaterials_Patch
    {
        [UsedImplicitly] private static IEnumerable<MethodInfo> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Player), nameof(Player.CleanupGhostMaterials)).MakeGenericMethod(typeof(MeshRenderer)); 
            yield return AccessTools.Method(typeof(Player), nameof(Player.CleanupGhostMaterials)).MakeGenericMethod(typeof(SkinnedMeshRenderer));
        }
        [UsedImplicitly] public static bool Prefix(GameObject ghost) => ghost.name != "kg_Blueprint_Internal_PlacePiece";
    }
    [HarmonyPatch(typeof(Piece),nameof(Piece.GetSnapPoints), typeof(List<Transform>))]
    private static class Piece_GetSnapPoints_Patch
    {
        [UsedImplicitly] private static bool Prefix(Piece __instance) => __instance.name != "kg_Blueprint_Internal_PlacePiece";
    }
    [HarmonyPatch(typeof(Player),nameof(Player.FindClosestSnapPoints))]
    private static class Player_FindClosestSnapPoints_Patch
    {
        [UsedImplicitly] private static bool Prefix(Transform ghost) => ghost.name != "kg_Blueprint_Internal_PlacePiece";
    }
    [HarmonyPatch(typeof(Player),nameof(Player.CheckPlacementGhostVSPlayers))]
    private static class Player_CheckPlacementGhostVSPlayers_Patch
    {
        [UsedImplicitly] private static bool Prefix(Player __instance) => __instance.m_placementGhost && __instance.m_placementGhost.name != "kg_Blueprint_Internal_PlacePiece";
    }
    [HarmonyPatch(typeof(Player),nameof(Player.SetPlacementGhostValid))]
    private static class Player_SetPlacementGhostValid_Patch
    {
        [UsedImplicitly] private static bool Prefix(Player __instance) => __instance.m_placementGhost.name != "kg_Blueprint_Internal_PlacePiece";
    }
}