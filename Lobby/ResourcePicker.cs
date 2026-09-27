using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using KaCMultiplayer.Net;

namespace KaCMultiplayer.Lobby
{
    /// <summary>
    /// "Demand what, and how much." A grid of every resource a kingdom can be asked for, with an
    /// amount, opened from the Demand button on a diplomacy row.
    ///
    /// BUILT IN CODE, not from a prefab. Every other screen in the mod comes from the asset
    /// bundle, and this one deliberately does not: a picker over <c>FreeResourceType</c> has to
    /// match whatever that enum holds, and a prefab would freeze today's eleven resources into
    /// art that nobody would remember to update. Reading the list from
    /// <see cref="PlayerRelations.Demandable"/> means a game update that adds a resource shows it
    /// here on its own.
    ///
    /// Styling is deliberately plain and self-contained, because it has no prefab to inherit
    /// from. It sits over the diplomacy window and closes on any choice or on Escape.
    /// </summary>
    public static class ResourcePicker
    {
        /// <summary>Amounts offered per resource. Enough range to be useful, few enough to click.</summary>
        private static readonly int[] Amounts = new int[] { 10, 25, 50, 100, 250, 500, 1000 };

        private static GameObject root;
        private static int localTeam;
        private static int targetTeam;
        private static FreeResourceType chosen = FreeResourceType.Gold;
        private static int chosenAmount = 100;

        private static readonly List<Button> resourceButtons = new List<Button>();
        private static readonly List<Button> amountButtons = new List<Button>();

        /// <summary>
        /// Grid buttons that fell back to a flat rectangle because the real art could not be
        /// cloned, so <see cref="Tint"/> knows which colour "unselected" means for THIS button:
        /// a flat rectangle has no art of its own and needs cButton's fill to read as a button at
        /// all, where a real clone already looks right with no tint.
        /// </summary>
        private static readonly HashSet<Button> fallbackButtons = new HashSet<Button>();

        private static TextMeshProUGUI summary;

        /// <summary>
        /// True when the picker was opened to GIVE rather than to ask.
        ///
        /// The grid is identical either way, so the window is shared rather than duplicated; only
        /// its heading and the two action buttons change, and the buttons read this at click time
        /// so one set of handlers serves both.
        /// </summary>
        private static bool aidMode;

        private static TextMeshProUGUI titleLabel, primaryLabel, secondaryLabel;

        private static readonly Color cPanel = new Color(0.10f, 0.14f, 0.20f, 0.97f);
        private static readonly Color cBorder = new Color(0.27f, 0.35f, 0.46f, 1f);
        private static readonly Color cButton = new Color(0.16f, 0.22f, 0.30f, 1f);
        private static readonly Color cChosen = new Color(0.30f, 0.52f, 0.36f, 1f);
        private static readonly Color cText = new Color(0.88f, 0.92f, 0.96f, 1f);

        // A grid cell cloned from the real button keeps that button's own art -- border, bevel,
        // gradient -- and reads correctly with no tint at all, the same as the Demand/Offer/Cancel
        // row right below it. cButton exists only for a cell that fell back to a flat rectangle,
        // which has no art of its own to show through and needs an explicit fill to read as a
        // button at all.
        private static readonly Color cUnselectedReal = Color.white;

        public static bool IsOpen { get { return root != null && root.activeSelf; } }

        /// <summary>Opens the picker to demand from one target kingdom.</summary>
        public static void Open(int fromTeam, int toTeam)
        {
            Open(fromTeam, toTeam, false);
        }

        /// <summary>Opens the picker, either to demand from a kingdom or to send it aid.</summary>
        public static void Open(int fromTeam, int toTeam, bool aid)
        {
            localTeam = fromTeam;
            targetTeam = toTeam;
            aidMode = aid;
            chosen = FreeResourceType.Gold;
            chosenAmount = 100;

            try
            {
                if (root == null) Build();
                if (root == null) return;

                root.SetActive(true);
                Refresh();
            }
            catch (Exception e) { NetLog.Error("opening the resource picker", e); }
        }

        public static void Close()
        {
            if (root != null) root.SetActive(false);
        }

        /// <summary>Escape closes it. Called from the diplomacy window's own tick.</summary>
        public static void Tick()
        {
            if (!IsOpen) return;
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        /// <summary>Forgets the built UI, for a torn-down session.</summary>
        public static void Reset()
        {
            try { if (root != null) UnityEngine.Object.Destroy(root); }
            catch { }

            try { if (canvasObj != null) UnityEngine.Object.Destroy(canvasObj); }
            catch { }

            canvasObj = null;

            root = null;
            summary = null;
            titleLabel = null;
            primaryLabel = null;
            secondaryLabel = null;
            resourceButtons.Clear();
            amountButtons.Clear();
            fallbackButtons.Clear();
            restingColour.Clear();
        }

        /// <summary>
        /// The picker's own screen-space canvas.
        ///
        /// It used to be parented to MenuUi.Root, which is the MAIN MENU's UI. That object is
        /// switched off while a game is running, so during play the picker was built correctly,
        /// activated correctly, and drawn nowhere: clicking Demand appeared to do nothing at all.
        ///
        /// Its own overlay canvas is what AllianceRequestWindow already does, for the same reason
        /// and with the same result, and it removes the dependency on the game's menu hierarchy
        /// entirely. Ordered just under the alliance prompt so a request that arrives while this is
        /// open still lands on top.
        /// </summary>
        private static GameObject canvasObj;

        private static Transform EnsureCanvas()
        {
            if (canvasObj != null) return canvasObj.transform;

            try
            {
                canvasObj = new GameObject("ResourcePickerCanvas",
                    typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

                Canvas c = canvasObj.GetComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 5090;

                CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                return canvasObj.transform;
            }
            catch (Exception e)
            {
                NetLog.Error("creating the resource picker canvas", e);
                return null;
            }
        }

        private static void Build()
        {
            Transform parent = EnsureCanvas();
            if (parent == null) { NetLog.Warn("resource picker: could not create a canvas"); return; }

            root = Panel("ResourcePicker", parent, 560f, 430f);

            // A donor built once, harvested for both its background art and its button's real art
            // (see KacModalStyle), then discarded. The grid below keeps its own plain style: it
            // is a custom resource-type toggle, not a panel this prefab was ever meant to hold,
            // and it is the one part of this screen that has already caused a real, shipped bug
            // (a Demand button once cloned inactive). Everything AROUND the grid -- the panel it
            // sits in, and the buttons a player actually commits with -- is worth the real art.
            KacModalStyle.Panel donor = KacModalStyle.Build(parent);
            ApplyRealBackground(donor);

            titleLabel = Label(root.transform, "Demand from another kingdom", 20f, FontStyles.Bold, 0f, 180f, 520f, 34f);

            // Resources, four to a row. Plain rectangles, not cloned from the donor's real button:
            // an earlier attempt at cloning it for every grid cell (18 clones from one donor
            // button, in a screen already cloning it three more times for the commit row) broke
            // the panel's layout badly enough that buttons rendered entirely outside it. That is
            // reverted here rather than chased further blind -- this grid's own style, unchanged
            // since before that attempt, is the confirmed-working one.
            resourceButtons.Clear();
            FreeResourceType[] types = PlayerRelations.Demandable;
            const float bw = 124f, bh = 34f, gapX = 6f, gapY = 6f;

            for (int i = 0; i < types.Length; i++)
            {
                int col = i % 4, rowIdx = i / 4;
                float x = (col - 1.5f) * (bw + gapX);
                float y = 130f - rowIdx * (bh + gapY);

                FreeResourceType t = types[i];
                Button b = MakeButton(root.transform, PlayerRelations.ResourceLabel(t), x, y, bw, bh,
                                      delegate { chosen = t; Refresh(); });
                fallbackButtons.Add(b);
                resourceButtons.Add(b);
            }

            Label(root.transform, "How much", 16f, FontStyles.Normal, 0f, -20f, 520f, 26f);

            amountButtons.Clear();
            const float aw = 68f, ah = 32f, agap = 6f;
            for (int i = 0; i < Amounts.Length; i++)
            {
                float x = (i - (Amounts.Length - 1) / 2f) * (aw + agap);
                int amount = Amounts[i];
                Button b = MakeButton(root.transform, amount.ToString(), x, -56f, aw, ah,
                                      delegate { chosenAmount = amount; Refresh(); });
                fallbackButtons.Add(b);
                amountButtons.Add(b);
            }

            summary = Label(root.transform, "", 17f, FontStyles.Normal, 0f, -104f, 520f, 30f);

            // Both handlers read aidMode when they are CLICKED rather than when they are built,
            // so the same two buttons serve a demand and a gift and simply swap which is which.
            // Not positioned yet: a real cloned button carries whatever height the artist gave
            // it, which the first version of this screen did not account for, and its bottom edge
            // ended up outside the panel. LayoutActionButtons below places all three from their
            // own MEASURED heights once they all exist, real or fallback alike.
            Button primary = ActionButton(donor, "Demand it", delegate
            {
                PlayerRelations.Send(localTeam, targetTeam,
                    aidMode ? Net.Messages.DealKind.Offer : Net.Messages.DealKind.Demand,
                    chosenAmount, chosen);
                Close();
            });
            primaryLabel = primary == null ? null : primary.GetComponentInChildren<TextMeshProUGUI>();

            Button secondary = ActionButton(donor, "Offer it instead", delegate
            {
                PlayerRelations.Send(localTeam, targetTeam,
                    aidMode ? Net.Messages.DealKind.Demand : Net.Messages.DealKind.Offer,
                    chosenAmount, chosen);
                Close();
            });
            secondaryLabel = secondary == null ? null : secondary.GetComponentInChildren<TextMeshProUGUI>();

            Button cancel = ActionButton(donor, "Cancel", delegate { Close(); });

            LayoutActionButtons(primary, secondary, cancel);

            // The donor's own panel, backdrop, title and description are of no further use once
            // its background has been copied and its button cloned three times; the clones were
            // reparented onto this screen as each was made, so destroying it here takes nothing
            // they depend on with it.
            if (donor != null) UnityEngine.Object.Destroy(donor.Root);

            // The game's own Hall of Diplomacy art over all of it, the same as the diplomacy
            // window. Each button's resting colour is remembered afterwards so the selection
            // highlight can be put back to exactly what the game drew.
            KacGameArt.SkinPanel(root);
            restingColour.Clear();
            foreach (Button b in root.GetComponentsInChildren<Button>(true))
            {
                Image img = b.GetComponent<Image>();
                if (img != null) restingColour[b] = img.color;
            }
        }

        private static readonly System.Collections.Generic.Dictionary<Button, Color> restingColour =
            new System.Collections.Generic.Dictionary<Button, Color>();

        /// <summary>A warm highlight for the chosen resource and amount, readable on the game's art.</summary>
        private static readonly Color cChosenSkinned = new Color(1f, 0.82f, 0.38f, 1f);

        /// <summary>
        /// Paints the real modal's background onto THIS panel's own Image, rather than reusing
        /// the donor's Container directly the way AllianceRequestWindow and DealRequestWindow do.
        /// Doing it that way here would put the donor's own Title and Description at whatever
        /// fixed positions the artist gave them, colliding with the resource grid this screen
        /// actually needs. This borrows just the art -- sprite, slicing and colour -- onto a
        /// panel this screen still fully owns the size and children of.
        /// </summary>
        private static void ApplyRealBackground(KacModalStyle.Panel donor)
        {
            if (donor == null || root == null) return;

            Sprite sprite; Image.Type type; Color colour;
            if (!KacModalStyle.TryGetBackground(donor, out sprite, out type, out colour)) return;

            Image img = root.GetComponent<Image>();
            if (img == null) return;

            img.sprite = sprite;
            img.type = type;
            img.color = colour;

            // The flat fallback drew its own border with a plain Outline effect; the real sprite
            // is expected to carry its border baked into the art, and layering a second,
            // flat-coloured one on top of real border art would look wrong rather than better.
            Outline outline = root.GetComponent<Outline>();
            if (outline != null) UnityEngine.Object.Destroy(outline);
        }

        /// <summary>
        /// One of the three commit buttons (Demand it / Offer it instead / Cancel). Clones its
        /// art from <paramref name="donor"/> when one was built; falls back to the plain flat
        /// button, unchanged from before that art existed, if it was not. Left unpositioned
        /// either way; see <see cref="LayoutActionButtons"/>.
        /// </summary>
        private static Button ActionButton(KacModalStyle.Panel donor, string caption,
                                           UnityEngine.Events.UnityAction onClick)
        {
            if (donor != null)
            {
                Button real = KacModalStyle.CloneButton(donor, caption, root.transform);
                if (real != null)
                {
                    KacModalStyle.SetLabel(real, caption);
                    KacModalStyle.SetClick(real, onClick);
                    return real;
                }
            }

            return MakeButton(root.transform, caption, 0f, 0f, 190f, 40f, onClick);
        }

        /// <summary>
        /// Places the three commit buttons below the summary line and grows the panel to actually
        /// contain them, using each button's own MEASURED height rather than the size the old
        /// flat rectangles happened to be. That old, guessed size is exactly what put a real,
        /// taller button's bottom edge outside its own panel: this is that fix.
        ///
        /// The panel grows from its centre, since every one of its children (including these
        /// three) is positioned relative to that centre. So it grows equally in both directions;
        /// the trade-off is a little empty room above the title if the real buttons turn out to
        /// be a lot taller than the old guess, rather than any child moving from where it already
        /// is, correctly, today.
        /// </summary>
        private static void LayoutActionButtons(Button primary, Button secondary, Button cancel)
        {
            const float fallbackHeight = 40f;
            const float gapAboveRow = 22f;
            const float gapBetweenRows = 14f;
            const float bottomMargin = 26f;

            // Where the summary label (y=-104, 30 tall) actually ends; the anchor everything
            // below is measured from, so the row can never overlap it no matter how tall the
            // real buttons are.
            const float summaryBottom = -104f - 15f;

            float rowHeight = Mathf.Max(KacModalStyle.Height(primary, fallbackHeight),
                                        KacModalStyle.Height(secondary, fallbackHeight));
            float cancelHeight = KacModalStyle.Height(cancel, fallbackHeight);

            float rowCenterY = summaryBottom - gapAboveRow - rowHeight / 2f;
            float rowBottom = rowCenterY - rowHeight / 2f;

            float cancelCenterY = rowBottom - gapBetweenRows - cancelHeight / 2f;
            float cancelBottom = cancelCenterY - cancelHeight / 2f;

            SetPosition(primary, -110f, rowCenterY);
            SetPosition(secondary, 110f, rowCenterY);
            SetPosition(cancel, 0f, cancelCenterY);

            float neededHalfHeight = -cancelBottom + bottomMargin;
            RectTransform panelRect = root == null ? null : root.GetComponent<RectTransform>();
            if (panelRect != null && neededHalfHeight * 2f > panelRect.sizeDelta.y)
                panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, neededHalfHeight * 2f);
        }

        /// <summary>
        /// Positions a button relative to the panel's CENTRE. Anchors are reset first: a button
        /// cloned from the real dialog keeps that dialog's anchoring, which is its bottom edge, so
        /// the centre-relative offsets below used to put the whole commit row a panel's height
        /// under the panel, off the bottom of the window.
        /// </summary>
        private static void SetPosition(Button b, float x, float y)
        {
            if (b == null) return;
            RectTransform rt = b.GetComponent<RectTransform>();
            if (rt == null) return;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
        }

        /// <summary>Repaints the selection highlight and the sentence under it.</summary>
        private static void Refresh()
        {
            // Retitled per opening, because the same grid means two opposite things and the only
            // thing telling them apart is the wording.
            if (titleLabel != null)
            {
                string who = Main.KingdomNameForTeam(targetTeam);
                titleLabel.text = aidMode ? ("Send aid to " + who) : ("Demand from " + who);
            }
            if (primaryLabel != null) primaryLabel.text = aidMode ? "Send it" : "Demand it";
            if (secondaryLabel != null) secondaryLabel.text = aidMode ? "Demand it instead" : "Offer it instead";

            FreeResourceType[] types = PlayerRelations.Demandable;
            for (int i = 0; i < resourceButtons.Count && i < types.Length; i++)
                Tint(resourceButtons[i], types[i] == chosen);

            for (int i = 0; i < amountButtons.Count && i < Amounts.Length; i++)
                Tint(amountButtons[i], Amounts[i] == chosenAmount);

            if (summary != null)
            {
                // Says what will actually happen, in the order it happens, because "Demand" alone
                // does not make clear that accepting also ends a war.
                string what = chosenAmount + " " + PlayerRelations.ResourceLabel(chosen);
                bool atWar = PlayerRelations.Get(localTeam, targetTeam) == World.Relations.Enemy
                          || PlayerRelations.WarCountdown(localTeam, targetTeam) > 0;
                bool ai = AiDiplomacy.IsAiTeam(targetTeam);

                if (aidMode)
                    summary.text = ai
                        ? ("Send " + what + " as a gift. Gifts raise their opinion of you.")
                        : ("Send " + what + " as a gift.");
                else if (ai)
                    summary.text = "Demand " + what + ". They pay only if they like you, or fear your armies.";
                else
                    summary.text = atWar
                        ? ("Ask for " + what + " to end the war. They may accept or refuse.")
                        : ("Ask for " + what + ". They may accept or refuse.");
            }
        }

        private static void Tint(Button b, bool selected)
        {
            if (b == null) return;
            Image img = b.GetComponent<Image>();
            if (img == null) return;

            Color resting;
            if (restingColour.TryGetValue(b, out resting) && KacGameArt.Skinned)
            {
                img.color = selected ? cChosenSkinned : resting;
                return;
            }

            Color unselected = fallbackButtons.Contains(b) ? cButton : cUnselectedReal;
            img.color = selected ? cChosen : unselected;
        }

        // ---- small UI builders ----------------------------------------------------------
        //
        // Deliberately minimal. This is the only screen in the mod without a prefab, so these
        // exist to keep Build() readable rather than to be a general UI toolkit.

        private static GameObject Panel(string name, Transform parent, float w, float h)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;

            Image img = go.GetComponent<Image>();
            img.color = cPanel;

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = cBorder;
            outline.effectDistance = new Vector2(2f, -2f);

            go.transform.SetAsLastSibling();   // over the diplomacy window, not behind it
            return go;
        }

        private static TextMeshProUGUI Label(Transform parent, string text, float size,
                                             FontStyles style, float x, float y, float w, float h)
        {
            GameObject go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = cText;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button MakeButton(Transform parent, string text, float x, float y,
                                         float w, float h, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);

            go.GetComponent<Image>().color = cButton;

            Button b = go.GetComponent<Button>();
            b.onClick.AddListener(onClick);

            TextMeshProUGUI tmp = Label(go.transform, text, 15f, FontStyles.Normal, 0f, 0f, w, h);
            tmp.enableWordWrapping = false;

            return b;
        }
    }
}
