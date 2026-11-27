using System;
using System.Collections.Generic;
using RajaSawit.Core;
using RajaSawit.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RajaSawit.UI
{
    public struct SheetAction
    {
        public string Label;
        public Color Fill;
        public Color TextColor;
        public bool Interactable;
        public Action OnClick;

        public SheetAction(string label, Action onClick, Color fill, Color textColor, bool interactable = true)
        {
            Label = label;
            OnClick = onClick;
            Fill = fill;
            TextColor = textColor;
            Interactable = interactable;
        }
    }

    /// <summary>
    /// Portrait HUD built in code: resources on top, a bottom sheet for the selected tile,
    /// and a bar with Panen semua, Jual CPO and Naik jabatan.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        public static readonly Color Ink = PlaceholderColors.Hex(0x1F2A1D);
        public static readonly Color Cream = PlaceholderColors.Hex(0xFFF6E5);
        public static readonly Color Green = PlaceholderColors.Hex(0x4E9A36);
        public static readonly Color Gold = PlaceholderColors.Hex(0xE3B23C);
        public static readonly Color Red = PlaceholderColors.Hex(0xD9462B);
        public static readonly Color Sand = PlaceholderColors.Hex(0xE6DCC8);
        public static readonly Color Blue = PlaceholderColors.Hex(0x2F6FA8);

        private const int SheetButtons = 3;

        private GameSession session;
        private Text rupiahText, levelText, clockText, resourceText, riskText;
        private RectTransform sheet;
        private Text sheetTitle, sheetBody;
        private readonly Button[] sheetButtons = new Button[SheetButtons];
        private readonly Text[] sheetLabels = new Text[SheetButtons];
        private readonly Action[] sheetActions = new Action[SheetButtons];
        private Text upgradeLabel;
        private RectTransform toastPanel;
        private Text toastText;
        private float toastUntil;

        private void Awake()
        {
            EnsureEventSystem();
            Build();
        }

        private void Start()
        {
            session = GameSession.Instance;
            if (session == null)
            {
                Debug.LogError("[RajaSawit] HudView needs a GameSession in the scene.");
                enabled = false;
                return;
            }
            session.StateChanged += Refresh;
            session.EventRaised += OnSimEvent;
            Refresh();
            HideSheet();

            if (session.LastOfflineTicks >= 60)
                Toast($"Kamu pergi {Format.Duration((int)session.LastOfflineTicks)}. Kebun tetap jalan.", Gold);
        }

        private void OnDestroy()
        {
            if (session == null) return;
            session.StateChanged -= Refresh;
            session.EventRaised -= OnSimEvent;
        }

        private void Update()
        {
            if (toastPanel != null && toastPanel.gameObject.activeSelf && Time.unscaledTime > toastUntil)
                toastPanel.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ public API

        public void ShowSheet(string title, string body, IReadOnlyList<SheetAction> actions)
        {
            sheet.gameObject.SetActive(true);
            sheetTitle.text = title;
            sheetBody.text = body;
            for (int i = 0; i < SheetButtons; i++)
            {
                bool used = actions != null && i < actions.Count;
                sheetButtons[i].gameObject.SetActive(used);
                if (!used) { sheetActions[i] = null; continue; }
                SheetAction a = actions[i];
                sheetLabels[i].text = a.Label;
                sheetLabels[i].color = a.TextColor;
                sheetButtons[i].GetComponent<Image>().color = a.Fill;
                sheetButtons[i].interactable = a.Interactable;
                sheetActions[i] = a.OnClick;
            }
        }

        public void HideSheet()
        {
            if (sheet != null) sheet.gameObject.SetActive(false);
        }

        public void Toast(string message, Color color)
        {
            toastPanel.gameObject.SetActive(true);
            toastPanel.GetComponent<Image>().color = color;
            toastText.color = color == Gold || color == Sand ? Ink : Cream;
            toastText.text = message;
            toastUntil = Time.unscaledTime + 3f;
        }

        public void ShowResult(CommandResult result, string successMessage = null)
        {
            if (!result.Ok) Toast(Format.Error(result.Error), Red);
            else if (!string.IsNullOrEmpty(successMessage)) Toast(successMessage, Green);
        }

        // ------------------------------------------------------------------ refresh

        private void Refresh()
        {
            GameState s = session.Sim.State;
            BalanceConfig c = session.Sim.Config;
            rupiahText.text = Format.Rupiah(s.rupiah);
            levelText.text = $"{LevelTitles.For(s.level)} · petak {s.plots.Count}/{session.Sim.PlotCap}";
            clockText.text = Format.Clock(s);
            resourceText.text =
                $"TBS kebun {Format.Kg(s.CollectionKg)}   PKS {Format.Kg(s.RampKg)}   " +
                $"CPO {Format.Kg(s.cpoKg)}/{Format.Kg(c.tankCapacityKg)}   {Format.Rupiah((long)s.cpoPricePerKg)}/kg";
            riskText.text = $"Asap {s.asap:0}   Sorotan {s.sorotan:0}";
            riskText.color = s.sorotan >= 80f ? Red : (s.asap >= c.heavySmokeThreshold ? Gold : Cream);

            upgradeLabel.text = s.level >= c.MaxLevel
                ? "Jabatan tertinggi"
                : $"Naik jabatan\n{Format.Rupiah(c.LevelUpCost(s.level))}";
        }

        private void OnSimEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.PlotBurned: Toast($"Api melahap petak #{e.PlotId}!", Red); break;
                case SimEventType.PestAppeared: Toast($"Hama menyerang petak #{e.PlotId}.", Gold); break;
                case SimEventType.TruckArrivedAtMill: Toast($"Truk tiba di PKS: {Format.Kg(e.Amount)} TBS.", Sand); break;
                case SimEventType.TankFull: Toast("Tangki CPO penuh. Jual dulu, Bos.", Gold); break;
                case SimEventType.CpoSold: Toast($"CPO terjual {Format.Rupiah((long)e.Amount)}.", Green); break;
                case SimEventType.LandCleared: Toast("Lahan baru siap ditanami.", Green); break;
                case SimEventType.FireStarted: Toast("\"Pembersihan cepat\" dimulai. Asap naik.", Red); break;
                case SimEventType.FireOut: Toast("Api sudah padam.", Sand); break;
                case SimEventType.Razia: Toast($"RAZIA! Denda {Format.Rupiah((long)e.Amount)}.", Red); break;
                case SimEventType.LevelUp: Toast($"Naik jabatan: {LevelTitles.For((int)e.Amount)}!", Gold); break;
                case SimEventType.NewDay: Toast($"Hari ke-{(int)e.Amount}. Harga CPO {Format.Rupiah((long)session.Sim.State.cpoPricePerKg)}/kg.", Sand); break;
            }
        }

        // ------------------------------------------------------------------ build

        private void Build()
        {
            var canvasGo = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform safe = UiFactory.Empty(canvasGo.transform, "SafeArea");
            UiFactory.Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            BuildTopBar(safe);
            BuildToast(safe);
            BuildBottomBar(safe);
            BuildSheet(safe);
        }

        private void BuildTopBar(RectTransform root)
        {
            RectTransform bar = UiFactory.Panel(root, "TopBar", new Color(Ink.r, Ink.g, Ink.b, 0.92f));
            UiFactory.AnchorTop(bar, 250);

            rupiahText = UiFactory.Label(bar, "Rupiah", 64, Gold);
            Place(rupiahText.rectTransform, 32, -20, 560, 80);
            clockText = UiFactory.Label(bar, "Clock", 32, Cream, TextAnchor.MiddleRight);
            PlaceRight(clockText.rectTransform, 32, -20, 440, 50);
            levelText = UiFactory.Label(bar, "Level", 30, Cream);
            Place(levelText.rectTransform, 32, -100, 1000, 44);
            resourceText = UiFactory.Label(bar, "Resources", 28, Cream);
            Place(resourceText.rectTransform, 32, -148, 1016, 44);
            riskText = UiFactory.Label(bar, "Risk", 30, Cream);
            Place(riskText.rectTransform, 32, -196, 1016, 44);
        }

        private void BuildToast(RectTransform root)
        {
            toastPanel = UiFactory.Panel(root, "Toast", Sand);
            toastPanel.anchorMin = new Vector2(0.05f, 1f);
            toastPanel.anchorMax = new Vector2(0.95f, 1f);
            toastPanel.pivot = new Vector2(0.5f, 1f);
            toastPanel.sizeDelta = new Vector2(0f, 110);
            toastPanel.anchoredPosition = new Vector2(0f, -270);
            toastText = UiFactory.Label(toastPanel, "Text", 32, Ink, TextAnchor.MiddleCenter);
            UiFactory.Stretch(toastText.rectTransform, 16);
            toastPanel.GetComponent<Image>().raycastTarget = false;
            toastPanel.gameObject.SetActive(false);
        }

        private void BuildBottomBar(RectTransform root)
        {
            RectTransform bar = UiFactory.Panel(root, "BottomBar", new Color(Ink.r, Ink.g, Ink.b, 0.92f));
            UiFactory.AnchorBottom(bar, 170);
            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.spacing = 20;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            UiFactory.Button(bar, "HarvestAll", Green, Cream, out Text harvestLabel)
                .onClick.AddListener(() =>
                {
                    int n = 0;
                    session.Run(sim => { n = sim.HarvestAll(); return CommandResult.Success; });
                    Toast(n > 0 ? $"Panen {n} petak." : "Belum ada TBS yang siap.", n > 0 ? Green : Sand);
                });
            harvestLabel.text = "Panen semua";

            UiFactory.Button(bar, "SellCpo", Gold, Ink, out Text sellLabel)
                .onClick.AddListener(() => ShowResult(session.Run(sim => sim.SellAllCpo())));
            sellLabel.text = "Jual CPO";

            UiFactory.Button(bar, "Upgrade", Blue, Cream, out upgradeLabel)
                .onClick.AddListener(() => ShowResult(session.Run(sim => sim.UpgradeLevel())));
        }

        private void BuildSheet(RectTransform root)
        {
            sheet = UiFactory.Panel(root, "BottomSheet", Cream);
            sheet.anchorMin = new Vector2(0f, 0f);
            sheet.anchorMax = new Vector2(1f, 0f);
            sheet.pivot = new Vector2(0.5f, 0f);
            sheet.sizeDelta = new Vector2(0f, 420);
            sheet.anchoredPosition = new Vector2(0f, 170);

            sheetTitle = UiFactory.Label(sheet, "Title", 44, Ink);
            Place(sheetTitle.rectTransform, 36, -24, 900, 64);
            sheetBody = UiFactory.Label(sheet, "Body", 30, PlaceholderColors.Hex(0x5A5148));
            Place(sheetBody.rectTransform, 36, -92, 1008, 150);

            UiFactory.Button(sheet, "Close", Sand, Ink, out Text closeLabel).onClick.AddListener(HideSheet);
            closeLabel.text = "Tutup";
            var closeRt = (RectTransform)closeLabel.transform.parent;
            PlaceRight(closeRt, 24, -24, 160, 72);

            RectTransform row = UiFactory.Empty(sheet, "Actions");
            row.anchorMin = new Vector2(0f, 0f);
            row.anchorMax = new Vector2(1f, 0f);
            row.pivot = new Vector2(0.5f, 0f);
            row.sizeDelta = new Vector2(0f, 150);
            row.anchoredPosition = new Vector2(0f, 12);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 12, 12);
            layout.spacing = 18;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            for (int i = 0; i < SheetButtons; i++)
            {
                int index = i;
                sheetButtons[i] = UiFactory.Button(row, $"Action{i}", Green, Cream, out sheetLabels[i]);
                sheetLabels[i].fontSize = 30;
                sheetButtons[i].onClick.AddListener(() => sheetActions[index]?.Invoke());
            }
        }

        private static void Place(RectTransform rt, float left, float top, float width, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(left, top);
            rt.sizeDelta = new Vector2(width, height);
        }

        private static void PlaceRight(RectTransform rt, float right, float top, float width, float height)
        {
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-right, top);
            rt.sizeDelta = new Vector2(width, height);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if RS_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }
    }

    public static class PlaceholderColors
    {
        public static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
