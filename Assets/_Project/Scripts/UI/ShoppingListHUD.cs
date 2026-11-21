using System.Collections.Generic;
using Game.Core;
using Game.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Shows the shopping list in the top-left corner:
    ///   [icon] Banana 1/2
    ///   [icon] ✓ Soda 1/1   (green when done)
    ///
    /// Put it on a UI panel that has a VerticalLayoutGroup. Rows are created by code,
    /// so the prefab only needs this panel. Refreshes on GameEvents.ShoppingListChanged.
    /// </summary>
    public class ShoppingListHUD : MonoBehaviour
    {
        [SerializeField] private ShoppingListTracker tracker;

        [Tooltip("Rows are created under this. Empty = this object.")]
        [SerializeField] private RectTransform rowContainer;

        [Header("Look")]
        [SerializeField] private float fontSize = 28f;
        [SerializeField] private float iconSize = 36f;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color doneColor = new Color(0.35f, 0.9f, 0.4f);

        [Tooltip("Put before finished items. If you see a square box, the font has no ✓ — use \"OK\".")]
        [SerializeField] private string doneMark = "✓";

        // One row = icon + label.
        private class Row
        {
            public Image Icon;
            public TextMeshProUGUI Label;
        }

        private readonly List<Row> _rows = new List<Row>();

        private void OnEnable() => GameEvents.ShoppingListChanged += Refresh;

        private void OnDisable() => GameEvents.ShoppingListChanged -= Refresh;

        private void Start() => Refresh();

        /// <summary>Redraws all rows from the tracker.</summary>
        public void Refresh()
        {
            // Not assigned in the Inspector? Find the one in the scene.
            if (tracker == null) tracker = FindAnyObjectByType<ShoppingListTracker>();
            if (tracker == null)
            {
                Debug.LogWarning("[ShoppingListHUD] No ShoppingListTracker in the scene.", this);
                return;
            }

            IReadOnlyList<ShoppingListTracker.Progress> entries = tracker.Entries;

            // Create missing rows (the list does not change length during a round).
            while (_rows.Count < entries.Count)
                _rows.Add(CreateRow(_rows.Count));

            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < entries.Count;
                _rows[i].Label.transform.parent.gameObject.SetActive(used);
                if (used) FillRow(_rows[i], entries[i]);
            }
        }

        private void FillRow(Row row, ShoppingListTracker.Progress p)
        {
            Sprite icon = p.Item.icon;
            row.Icon.sprite = icon;
            row.Icon.gameObject.SetActive(icon != null);

            // Only use the mark if the font can draw it (the default font has no ✓ — it showed a box).
            bool canDrawMark = !string.IsNullOrEmpty(doneMark) && row.Label.font != null &&
                               row.Label.font.HasCharacters(doneMark, out _, true, false);
            string mark = p.IsDone && canDrawMark ? doneMark + " " : "";
            string line = $"{p.Item.displayName} {p.Collected}/{p.Required}";
            row.Label.text = p.IsDone ? $"{mark}<s>{line}</s>" : line; // done: green + struck through
            row.Label.color = p.IsDone ? doneColor : normalColor;
        }

        /// <summary>Builds "Row_i" with an Icon (Image) and a Label (TextMeshPro) side by side.</summary>
        private Row CreateRow(int index)
        {
            RectTransform parent = rowContainer != null ? rowContainer : (RectTransform)transform;

            var rowGo = new GameObject($"Row_{index}", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowGo.transform.SetParent(parent, false);
            var layout = rowGo.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconGo.transform.SetParent(rowGo.transform, false);
            var iconLayout = iconGo.GetComponent<LayoutElement>();
            iconLayout.preferredWidth = iconSize;
            iconLayout.preferredHeight = iconSize;
            var image = iconGo.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(rowGo.transform, false);
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            if (label.font == null) label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = fontSize;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;

            return new Row { Icon = image, Label = label };
        }
    }
}
