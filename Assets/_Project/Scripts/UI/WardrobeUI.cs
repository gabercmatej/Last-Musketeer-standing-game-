using System;
using System.Collections.Generic;
using Musketeers.Data;
using Musketeers.Presentation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Musketeers.UI
{
    // The Wardrobe panel (GAME_DESIGN §9): Hat / Face / Cape / Boots tabs, a grid of items with a
    // "none" tile, live preview on the stage (AppFlow listens to Equipped), saved per character.
    public sealed class WardrobeUI
    {
        public event Action Closed;
        public event Action Equipped;

        readonly VisualElement _root;
        readonly ScrollView _grid;
        readonly Label _itemName;
        readonly List<Button> _tabs = new List<Button>();
        readonly LobbyModel _model;
        readonly ArtLibrary _art;
        CosmeticSlot _slot = CosmeticSlot.Hat;

        public bool Open { get; private set; }

        public WardrobeUI(VisualElement root, LobbyModel model, ArtLibrary art)
        {
            _model = model;
            _art = art;
            _root = root.Q("wardrobe");
            _grid = root.Q<ScrollView>("wardrobe-grid");
            _itemName = root.Q<Label>("wardrobe-item");
            var tabs = root.Q("wardrobe-tabs");
            foreach (var slot in Loadout.Slots)
            {
                var s = slot;
                var b = new Button(() => SelectTab(s)) { text = slot.ToString(), focusable = false };
                b.AddToClassList("btn");
                b.AddToClassList("info-tab");
                tabs.Add(b);
                _tabs.Add(b);
            }
            root.Q<Button>("wardrobe-close").clicked += () => Closed?.Invoke();
            root.Q<Button>("wardrobe-random").clicked += () =>
            {
                _model.RandomizeMine();
                UiArt.Sfx("SFX/ui/wardrobe_equip");
                Rebuild();
                Equipped?.Invoke();
            };
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            Open = visible;
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible)
                Rebuild();
        }

        void SelectTab(CosmeticSlot slot)
        {
            _slot = slot;
            Rebuild();
        }

        void Rebuild()
        {
            for (int i = 0; i < _tabs.Count; i++)
                _tabs[i].EnableInClassList("info-tab--on", Loadout.Slots[i] == _slot);
            _grid.Clear();
            var current = _model.MyLoadout.Get(_slot);
            _grid.Add(Tile(null, current));
            if (_art.Cosmetics != null)
            {
                foreach (var item in _art.Cosmetics.BySlot(_slot, enabledOnly: true))
                    _grid.Add(Tile(item, current));
            }
            var on = _art.Cosmetics != null ? _art.Cosmetics.Get(current) : null;
            _itemName.text = on != null ? on.DisplayName : NoneName(_slot);
        }

        static string NoneName(CosmeticSlot slot) => slot == CosmeticSlot.Boots ? "Plain boots" : slot == CosmeticSlot.Hat ? "Bare head" : "Nothing";

        VisualElement Tile(CosmeticDef item, string current)
        {
            var tile = new VisualElement();
            tile.AddToClassList("item");
            bool selected = item == null ? string.IsNullOrEmpty(current) : item.Id == current;
            tile.EnableInClassList("item--on", selected);
            if (item != null)
            {
                tile.Add(UiArt.Image("item-icon", item.Sprite, new Color(1, 1, 1, 0.1f)));
                tile.tooltip = item.DisplayName;
            }
            else
            {
                var none = new Label(NoneName(_slot));
                none.AddToClassList("item-none");
                tile.Add(none);
            }
            tile.RegisterCallback<ClickEvent>(_ =>
            {
                _model.Equip(_slot, item != null ? item.Id : "");
                UiArt.Sfx("SFX/ui/wardrobe_equip");
                Rebuild();
                Equipped?.Invoke();
            });
            return tile;
        }
    }
}
