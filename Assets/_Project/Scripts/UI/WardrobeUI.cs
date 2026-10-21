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
                bool soon = !Loadout.Selectable(slot);
                // 4F §10: every tab opens, Boots too, as a sneak peek; its items can't be equipped (see Tile).
                var b = new Button(() => SelectTab(s)) { text = soon ? "" : slot.ToString(), focusable = false };
                b.AddToClassList("btn");
                b.AddToClassList("info-tab");
                if (soon)
                {
                    b.AddToClassList("info-tab--soon");
                    b.tooltip = "Coming soon: have a look";
                    var name = new Label(slot.ToString()) { pickingMode = PickingMode.Ignore };
                    var note = new Label("Coming soon") { pickingMode = PickingMode.Ignore };
                    note.AddToClassList("tab-soon-note");
                    name.AddToClassList("tab-soon-name");
                    b.Add(name);
                    b.Add(note);
                }
                tabs.Add(b);
                _tabs.Add(b);
            }
            root.Q<Button>("wardrobe-close").clicked += () => Closed?.Invoke();
            // Default: Hat, Face and Cape to none (FIXES_4E §3.3).
            root.Q<Button>("wardrobe-default").clicked += () =>
            {
                _model.ClearMyLoadout();
                UiArt.Sfx("SFX/ui/wardrobe_equip");
                Rebuild();
                Equipped?.Invoke();
            };
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
            if (!Loadout.Selectable(_slot))
                _itemName.text = "Boots are coming soon. Everyone wears their own for now.";
        }

        static string NoneName(CosmeticSlot slot) => slot == CosmeticSlot.Boots ? "Plain boots" : slot == CosmeticSlot.Hat ? "Bare head" : "Nothing";

        VisualElement Tile(CosmeticDef item, string current)
        {
            var tile = new VisualElement();
            tile.AddToClassList("item");
            bool locked = !Loadout.Selectable(_slot);
            // A locked slot (Boots): the body's own boots ("none") are what everyone wears.
            bool selected = item == null ? locked || string.IsNullOrEmpty(current) : !locked && item.Id == current;
            tile.EnableInClassList("item--on", selected);
            if (item != null)
            {
                tile.Add(UiArt.Image("item-icon", item.Sprite, new Color(1, 1, 1, 0.1f)));
                tile.tooltip = locked ? item.DisplayName + " (coming soon)" : item.DisplayName;
                if (locked)
                {
                    // 4F §10: greyed out with a "Coming soon" label; clicking only says so.
                    tile.AddToClassList("item--soon");
                    var soon = new Label("Coming soon") { pickingMode = PickingMode.Ignore };
                    soon.AddToClassList("item-soon");
                    tile.Add(soon);
                }
            }
            else
            {
                var none = new Label(NoneName(_slot));
                none.AddToClassList("item-none");
                tile.Add(none);
            }
            tile.RegisterCallback<ClickEvent>(_ =>
            {
                if (locked)
                {
                    _itemName.text = item != null ? item.DisplayName + ": coming soon" : "Boots are coming soon. Everyone wears their own for now.";
                    return;
                }
                _model.Equip(_slot, item != null ? item.Id : "");
                UiArt.Sfx("SFX/ui/wardrobe_equip");
                Rebuild();
                Equipped?.Invoke();
            });
            return tile;
        }
    }
}
