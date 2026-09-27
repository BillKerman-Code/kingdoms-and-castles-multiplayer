using System;
using System.Collections.Generic;
using KaCMultiplayer.Lobby;
using KaCMultiplayer.Net;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KaCMultiplayer.Trade
{
    /// <summary>
    /// "What my kingdom charges." One row per commodity, a price you can raise, lower, or refuse.
    ///
    /// Hand-built rather than taken from the prefab bundle, for the reason the other mod windows
    /// are: nothing in the bundle is this shape. Its own overlay canvas, because MenuUi.Root is the
    /// main-menu UI and is not active while a game is running.
    ///
    /// DRESSED IN THE GAME'S OWN ART, the same as the Ctrl+Shift+D diplomacy window it sits next
    /// to: the Hall of Diplomacy's panel, fonts and buttons (KacGameArt.SkinPanel), each good on
    /// the game's own row art (KacGameArt.SkinRow), and the game's resource icons beside names and
    /// prices. It used to be flat navy rectangles, which read as a debug panel rather than part of
    /// Kingdoms and Castles. Every piece of borrowed art is optional: whatever cannot be found is
    /// left in the plain style below, so the window always works.
    ///
    /// GOLD IS NOT LISTED. It is what everything else is priced IN, so a price for gold in gold is
    /// not a thing a player can mean, and offering the row would only invite the question.
    ///
    /// Every change goes out as it is made. There is no Save button, deliberately: a price list
    /// that is only half sent is worse than one that is a second old, and the alternative is
    /// remembering to press something before the buyer's window opens.
    /// </summary>
    public static class ExportPricesWindow
    {
        private const int SortingOrder = 5200;

        private const float PanelWidth = 700f;
        private const float RowWidth = 640f;
        private const float RowHeight = 44f;
        private const float RowGap = 6f;

        private static GameObject canvasObj;
        private static GameObject root;
        private static int localTeam;
        private static TextMeshProUGUI titleLabel;

        /// <summary>One good's row, and the parts of it that change.</summary>
        private class Row
        {
            public FreeResourceType Type;
            public GameObject Obj;
            public TextMeshProUGUI Price;
            public TextMeshProUGUI HoldCaption;
            public Button Hold;
            public readonly List<Button> Steps = new List<Button>();
        }

        private static readonly List<Row> rows = new List<Row>();

        // The plain style, used for anything the game's art could not replace.
        private static readonly Color cPanel = new Color(0.10f, 0.14f, 0.20f, 0.97f);
        private static readonly Color cBorder = new Color(0.27f, 0.35f, 0.46f, 1f);
        private static readonly Color cRow = new Color(0.14f, 0.19f, 0.26f, 1f);
        private static readonly Color cButton = new Color(0.16f, 0.22f, 0.30f, 1f);
        private static readonly Color cRefuse = new Color(0.42f, 0.19f, 0.19f, 1f);
        private static readonly Color cText = new Color(0.88f, 0.92f, 0.96f, 1f);

        /// <summary>"Not for sale" in the colour the diplomacy window uses for war.</summary>
        private static readonly Color cHeld = new Color(0.90f, 0.42f, 0.38f);

        public static bool IsOpen { get { return root != null && root.activeSelf; } }

        public static void Toggle()
        {
            if (IsOpen) { Close(); return; }

            if (Player.inst == null || Player.inst.PlayerLandmassOwner == null) return;
            localTeam = Player.inst.PlayerLandmassOwner.teamId;

            Build();
            if (root == null) return;

            Refresh();
            root.SetActive(true);
        }

        public static void Close()
        {
            if (root != null) root.SetActive(false);
        }

        public static void Tick()
        {
            if (!IsOpen) return;

            // Escape closes, and nothing else here reads the keyboard: the prices are set with the
            // mouse so a stray keypress cannot change what a kingdom charges.
            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }

            // A session can end while the window is up.
            if (!NetClient.client.IsConnected) Close();
        }

        public static void Reset()
        {
            rows.Clear();
            root = null;
            titleLabel = null;

            if (canvasObj != null)
            {
                UnityEngine.Object.Destroy(canvasObj);
                canvasObj = null;
            }
        }

        // ---- the list ------------------------------------------------------

        private static void Step(FreeResourceType type, int delta)
        {
            // Held goods have no price to move; Sell puts them back on the market first. Without
            // this, "-" on a held good quietly offered it for 1 gold.
            if (!ExportPrices.ForSale(localTeam, type)) return;

            // Never down to zero by stepping: zero means "not for sale", and that is what Hold is
            // for. A price walked down one click too far used to take the goods off the market.
            int now = ExportPrices.PriceFor(localTeam, type);
            ExportPrices.SetLocal(localTeam, type, Mathf.Max(1, now + delta));
            Refresh();
        }

        private static void Withhold(FreeResourceType type)
        {
            // A second click puts it back at the game's own price rather than at zero, so "not for
            // sale" is a toggle and not a one-way door.
            bool selling = ExportPrices.ForSale(localTeam, type);
            ExportPrices.SetLocal(localTeam, type,
                                  selling ? ExportPrices.NotForSale : ExportPrices.DefaultPrice(type));
            Refresh();
        }

        private static void Refresh()
        {
            if (titleLabel != null)
            {
                string kingdom = Main.KingdomNameForTeam(localTeam);
                titleLabel.text = string.IsNullOrEmpty(kingdom) ? "Export Prices" : kingdom + " - Export Prices";
            }

            string gold = KacGameArt.ResourceIcon(FreeResourceType.Gold);

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                bool selling = ExportPrices.ForSale(localTeam, row.Type);

                if (row.Price != null)
                {
                    if (selling)
                    {
                        int price = ExportPrices.PriceFor(localTeam, row.Type);
                        row.Price.text = gold.Length > 0 ? price + " " + gold : price + "g";
                        row.Price.color = KacGameArt.BodyText(cText);
                    }
                    else
                    {
                        row.Price.text = "Not for sale";
                        row.Price.color = KacGameArt.Relation(cHeld);
                    }
                }

                // The price buttons only mean something while the goods are on offer, and greying
                // them out says so better than letting a click do nothing.
                for (int s = 0; s < row.Steps.Count; s++)
                    if (row.Steps[s] != null) row.Steps[s].interactable = selling;

                if (row.HoldCaption != null) row.HoldCaption.text = selling ? "Hold" : "Sell";

                // Only the plain style shows Hold in red; the game's own button art stays as the
                // game draws it, and the price line above already says the goods are held.
                if (row.Hold != null && !KacGameArt.Skinned)
                {
                    Image img = row.Hold.GetComponent<Image>();
                    if (img != null) img.color = selling ? cRefuse : cButton;
                }
            }
        }

        // ---- building ------------------------------------------------------

        private static Transform EnsureCanvas()
        {
            if (canvasObj != null) return canvasObj.transform;

            canvasObj = new GameObject("KcmExportPricesCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            Canvas c = canvasObj.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = SortingOrder;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
            {
                GameObject es = new GameObject("KcmExportEventSystem",
                    typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(canvasObj.transform, false);
            }

            return canvasObj.transform;
        }

        private static void Build()
        {
            if (root != null) return;

            try
            {
                Transform parent = EnsureCanvas();

                rows.Clear();

                // Everything tradeable except gold, which is the unit of account.
                List<FreeResourceType> goods = new List<FreeResourceType>();
                FreeResourceType[] all = PlayerRelations.Demandable;
                for (int i = 0; i < all.Length; i++)
                    if (all[i] != FreeResourceType.Gold) goods.Add(all[i]);

                float listHeight = goods.Count * (RowHeight + RowGap) - RowGap;
                float panelHeight = 118f + listHeight + 84f;
                float halfHeight = panelHeight / 2f;

                root = Panel("ExportPrices", parent, PanelWidth, panelHeight);

                // The biggest text on the panel, which is how SkinPanel knows to give it the
                // game's title font. Set properly in Refresh, where the kingdom name is current.
                titleLabel = Label(root.transform, "Export Prices", 26f, FontStyles.Bold,
                                   0f, halfHeight - 40f, PanelWidth - 60f, 38f);

                Label(root.transform, "What other kingdoms pay for each unit your merchants carry to them.",
                      16f, FontStyles.Normal, 0f, halfHeight - 76f, PanelWidth - 60f, 26f);

                float firstRowY = halfHeight - 118f - RowHeight / 2f;
                for (int i = 0; i < goods.Count; i++)
                    rows.Add(BuildRow(goods[i], firstRowY - i * (RowHeight + RowGap)));

                MakeButton(root.transform, "Close", 0f, -halfHeight + 40f, 180f, 40f, Close);

                // The game's own Hall of Diplomacy art, exactly as the diplomacy window wears it:
                // the panel, its buttons and its text first, then each row's own background.
                KacGameArt.SkinPanel(root);
                for (int i = 0; i < rows.Count; i++)
                    KacGameArt.SkinRow(rows[i].Obj);

                root.SetActive(false);
            }
            catch (Exception e)
            {
                NetLog.Error("building the export price window", e);
                Reset();
            }
        }

        /// <summary>
        /// One good: its icon and name, then  -10  -  price  +  +10, then Hold.
        /// </summary>
        private static Row BuildRow(FreeResourceType type, float y)
        {
            GameObject obj = new GameObject("Row " + type, typeof(RectTransform), typeof(Image));
            Row row = new Row { Type = type, Obj = obj };

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.SetParent(root.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(RowWidth, RowHeight);
            rt.anchoredPosition = new Vector2(0f, y);
            obj.GetComponent<Image>().color = cRow;

            Transform t = obj.transform;

            // The game's own icon, where it can be drawn. Rich text is on for these two labels only,
            // and safely: what they show is fixed text and a number, never anything a player typed.
            string icon = KacGameArt.ResourceIcon(type);
            string caption = icon.Length > 0 ? icon + " " + PlayerRelations.ResourceLabel(type)
                                             : PlayerRelations.ResourceLabel(type);
            TextMeshProUGUI name = Label(t, caption, 18f, FontStyles.Normal, -212f, 0f, 190f, RowHeight - 6f);
            name.alignment = TextAlignmentOptions.Left;
            UseIcons(name);

            // Captured per row, which is the whole reason these are locals: a loop variable shared
            // by every handler would leave all ten rows editing the last resource.
            FreeResourceType captured = type;

            row.Steps.Add(MakeButton(t, "-10", -78f, 0f, 52f, 32f, delegate { Step(captured, -10); }));
            row.Steps.Add(MakeButton(t, "-", -30f, 0f, 38f, 32f, delegate { Step(captured, -1); }));

            row.Price = Label(t, "", 18f, FontStyles.Bold, 40f, 0f, 96f, RowHeight - 6f);
            UseIcons(row.Price);

            // "Not for sale" is wider than any price; shrink it to fit between the buttons rather
            // than let it run under them.
            row.Price.fontSizeMax = 18f;
            row.Price.fontSizeMin = 11f;
            row.Price.enableAutoSizing = true;

            row.Steps.Add(MakeButton(t, "+", 110f, 0f, 38f, 32f, delegate { Step(captured, 1); }));
            row.Steps.Add(MakeButton(t, "+10", 158f, 0f, 52f, 32f, delegate { Step(captured, 10); }));

            row.Hold = MakeButton(t, "Hold", 262f, 0f, 92f, 32f, delegate { Withhold(captured); });
            row.HoldCaption = row.Hold.GetComponentInChildren<TextMeshProUGUI>();

            return row;
        }

        /// <summary>Lets a label draw the game's resource icons.</summary>
        private static void UseIcons(TextMeshProUGUI label)
        {
            TMP_SpriteAsset icons = KacGameArt.IconSprites;
            if (icons == null) return;

            label.richText = true;
            label.spriteAsset = icons;
        }

        private static GameObject Panel(string name, Transform parent, float w, float h)
        {
            // One Image, so the game's panel art (KacGameArt.SkinPanel) can replace it outright.
            // The outline is the plain style's border, and SkinPanel removes it along with the flat
            // fill, because the game's art carries its own.
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = Vector2.zero;
            go.GetComponent<Image>().color = cPanel;

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = cBorder;
            outline.effectDistance = new Vector2(2f, -2f);

            return go;
        }

        private static TextMeshProUGUI Label(Transform parent, string text, float size,
                                             FontStyles style, float x, float y, float w, float h)
        {
            GameObject obj = new GameObject("Label", typeof(RectTransform));
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);

            TextMeshProUGUI t = obj.AddComponent<TextMeshProUGUI>();
            t.font = TMP_Settings.defaultFontAsset;
            if (GameUI.inst != null)
            {
                TextMeshProUGUI existing = GameUI.inst.GetComponentInChildren<TextMeshProUGUI>(true);
                if (existing != null) t.font = existing.font;
            }

            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = cText;

            // Kingdom names are player-typed and reach this window, so a name containing something
            // that looks like a tag must appear as itself rather than as markup.
            t.richText = false;
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = false;
            t.raycastTarget = false;
            return t;
        }

        private static Button MakeButton(Transform parent, string text, float x, float y,
                                         float w, float h, UnityEngine.Events.UnityAction onClick)
        {
            GameObject obj = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);

            Image img = obj.GetComponent<Image>();
            img.color = cButton;

            Button b = obj.GetComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(onClick);

            Label(obj.transform, text, 16f, FontStyles.Normal, 0f, 0f, w - 4f, h - 4f);
            return b;
        }
    }
}
