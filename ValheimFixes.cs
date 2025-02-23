namespace kg_Blueprint;

public static class ValheimFixes
{
    [HarmonyPatch(typeof(Hud),nameof(Hud.Awake))]
    public static class Hud_Awake_Patch
    {
        public static Vector2 OriginalSize;
        public static RectTransform Rect;
        [UsedImplicitly] private static void Postfix(Hud __instance)
        {
            OriginalSize = (__instance.m_requirementItems[0].transform.parent.parent.transform as RectTransform)!.sizeDelta;
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
            Rect = (__instance.m_requirementItems[0].transform.parent.parent.transform as RectTransform);
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
            int amount = Mathf.Max(0, piece.m_resources.Length - 5);
            Hud_Awake_Patch.Rect.sizeDelta = new Vector2(Hud_Awake_Patch.OriginalSize.x + amount * 50, Hud_Awake_Patch.Rect.sizeDelta.y);
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
        private static void Postfix(ref bool __result) => __result = true;
    }
}