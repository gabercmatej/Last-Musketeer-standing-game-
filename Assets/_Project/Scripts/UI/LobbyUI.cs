using System;
using System.Collections.Generic;
using Musketeers.Data;
using Musketeers.Presentation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Musketeers.UI
{
    // The lobby overlay on top of the tavern stage: map choice, character portraits, add/remove bots,
    // Wardrobe and Start. The stage itself (StageView) is redrawn by AppFlow when Changed fires.
    public sealed class LobbyUI
    {
        public event Action StartClicked;
        public event Action BackClicked;
        public event Action Changed;            // character, players or map changed: rebuild the stage
        public event Action<bool> WardrobeToggled;

        readonly VisualElement _root;
        readonly VisualElement _bottom;
        readonly Label _mapName, _mapDesc, _ability, _count;
        readonly Button _add, _remove;
        readonly List<VisualElement> _picks = new List<VisualElement>();
        readonly LobbyModel _model;
        readonly ArtLibrary _art;

        public WardrobeUI Wardrobe { get; }

        public LobbyUI(VisualElement root, LobbyModel model, ArtLibrary art)
        {
            _model = model;
            _art = art;
            _root = root.Q("lobby");
            _bottom = root.Q("lobby-bottom");
            _mapName = root.Q<Label>("map-name");
            _mapDesc = root.Q<Label>("map-desc");
            _ability = root.Q<Label>("char-ability");
            _count = root.Q<Label>("player-count");
            _add = root.Q<Button>("add-bot");
            _remove = root.Q<Button>("remove-bot");

            var back = root.Q<Button>("lobby-back");
            back.AddToClassList("btn--back");
            back.clicked += () => BackClicked?.Invoke();
            root.Q<Button>("map-prev").clicked += () => StepMap(-1);
            root.Q<Button>("map-next").clicked += () => StepMap(1);
            _add.clicked += () => SetPlayers(_model.Players + 1);
            _remove.clicked += () => SetPlayers(_model.Players - 1);
            root.Q<Button>("start").clicked += () => { UiArt.Sfx("SFX/ui/ready"); StartClicked?.Invoke(); };
            root.Q<Button>("wardrobe-open").clicked += () => SetWardrobe(true);

            var picker = root.Q("char-picker");
            for (int i = 0; i < model.RosterCount; i++)
            {
                int index = i;
                var def = art.Character(i);
                var pick = UiArt.Image("char-pick", def != null ? def.Portrait : null, art.CharacterColor(i));
                pick.pickingMode = PickingMode.Position;
                pick.tooltip = def != null ? $"{def.DisplayName}: {def.AbilityName}" : "";
                pick.RegisterCallback<ClickEvent>(_ => PickCharacter(index));
                picker.Add(pick);
                _picks.Add(pick);
            }

            Wardrobe = new WardrobeUI(root, model, art);
            Wardrobe.Closed += () => SetWardrobe(false);
            foreach (var b in _root.Query<Button>().ToList())
                b.focusable = false;
            Refresh();
        }

        public void SetVisible(bool visible)
        {
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible)
            {
                // Leaving with the wardrobe open: close it and bring the bottom panel back for next time.
                Wardrobe.SetVisible(false);
                _bottom.style.display = DisplayStyle.Flex;
            }
        }

        public void SetWardrobe(bool open)
        {
            Wardrobe.SetVisible(open);
            _bottom.style.display = open ? DisplayStyle.None : DisplayStyle.Flex;
            WardrobeToggled?.Invoke(open);
        }

        void PickCharacter(int index)
        {
            if (index == _model.Character)
                return;
            _model.Character = index;
            _model.Save();
            UiArt.Sfx("SFX/ui/wardrobe_equip", 0.6f);
            Refresh();
            Changed?.Invoke();
        }

        void SetPlayers(int n)
        {
            n = Mathf.Clamp(n, 2, Mathf.Min(4, _model.RosterCount));
            if (n == _model.Players)
                return;
            if (n > _model.Players)
            {
                _model.RerollBot(n - 1);
                UiArt.Sfx("SFX/ui/player_join");
            }
            else
                UiArt.Sfx("SFX/ui/player_leave");
            _model.Players = n;
            _model.Save();
            Refresh();
            Changed?.Invoke();
        }

        void StepMap(int d)
        {
            _model.Map = (_model.Map + d + _model.MapCount) % _model.MapCount;
            _model.Save();
            Refresh();
            Changed?.Invoke();
        }

        public void Refresh()
        {
            for (int i = 0; i < _picks.Count; i++)
                _picks[i].EnableInClassList("char-pick--on", i == _model.Character);
            var def = _art.Character(_model.Character);
            _ability.text = def != null ? $"{def.DisplayName}: {def.AbilityName}. {def.AbilityDescription}" : "";
            var map = _art.Maps != null ? _art.Maps.Get(_model.Map) : null;
            _mapName.text = map != null ? map.DisplayName : "The Courtyard";
            _mapDesc.text = map != null ? map.Description : "";
            _count.text = _model.Players.ToString();
            _add.SetEnabled(_model.Players < Mathf.Min(4, _model.RosterCount));
            _remove.SetEnabled(_model.Players > 2);
        }
    }
}
