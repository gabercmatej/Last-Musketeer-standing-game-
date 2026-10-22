using System;
using System.Collections.Generic;
using Musketeers.Data;
using Musketeers.Presentation;
using Musketeers.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Musketeers.UI
{
    // "Pick your Musketeer" (FIXES_v0.2 §2.3): four round portraits on the left (over the dimmed lobby stage, see AppFlow), the selected musketeer's bio on
    // the right (name, title, strength, weakness, story, signature), Select and Back. Everything personal comes
    // from CharacterDef. A musketeer someone else holds (Day 5, LobbyModel.TakenBy) can be previewed, not picked.
    public sealed class CharacterSelectUI
    {
        public event Action<int> Selected;
        public event Action Back;

        readonly VisualElement _root;
        readonly Label _name, _title, _strength, _weakness, _bio, _sigName, _sigCost, _sigDesc;
        readonly VisualElement _portrait, _sigIcon;
        readonly Button _select;
        readonly List<VisualElement> _picks = new List<VisualElement>();
        readonly List<VisualElement> _glows = new List<VisualElement>();
        readonly List<Label> _ribbons = new List<Label>();
        readonly LobbyModel _model;
        readonly ArtLibrary _art;
        int _sel;

        public bool Visible { get; private set; }

        public CharacterSelectUI(VisualElement root, LobbyModel model, ArtLibrary art)
        {
            _model = model;
            _art = art;
            _root = root.Q("charselect");
            _name = root.Q<Label>("cs-name");
            _title = root.Q<Label>("cs-title");
            _strength = root.Q<Label>("cs-strength");
            _weakness = root.Q<Label>("cs-weakness");
            _bio = root.Q<Label>("cs-bio");
            _sigName = root.Q<Label>("cs-sig-name");
            _sigCost = root.Q<Label>("cs-sig-cost");
            _sigDesc = root.Q<Label>("cs-sig-desc");
            _portrait = root.Q("cs-portrait");
            _sigIcon = root.Q("cs-sig-icon");
            _select = root.Q<Button>("cs-select");
            _select.AddToClassList("name-clip");
            _select.clicked += Confirm;
            var back = root.Q<Button>("cs-back");
            back.clicked += () => Back?.Invoke();
            foreach (var b in _root.Query<Button>().ToList())
                b.focusable = false;

            var picker = root.Q("cs-picker");
            for (int i = 0; i < model.RosterCount; i++)
            {
                int index = i;
                var def = art.Character(i);
                var color = art.CharacterColor(i);
                var holder = new VisualElement();
                holder.AddToClassList("cs-pick-holder");
                var glow = new VisualElement { pickingMode = PickingMode.Ignore };
                glow.AddToClassList("cs-pick-glow");
                glow.style.backgroundColor = new Color(color.r, color.g, color.b, 0.4f);
                var pick = UiArt.Image("cs-pick", def != null ? def.Portrait : null, color);
                pick.pickingMode = PickingMode.Position;
                pick.style.borderLeftColor = pick.style.borderRightColor = pick.style.borderTopColor = pick.style.borderBottomColor = color;
                pick.tooltip = def != null ? def.DisplayName : "";
                pick.RegisterCallback<ClickEvent>(_ => Preview(index));
                var ribbon = new Label("Taken") { pickingMode = PickingMode.Ignore };
                ribbon.AddToClassList("cs-ribbon");
                holder.Add(glow);
                holder.Add(pick);
                holder.Add(ribbon);
                picker.Add(holder);
                _picks.Add(pick);
                _glows.Add(glow);
                _ribbons.Add(ribbon);
            }
            SetVisible(false);
        }

        void SetVisible(bool visible)
        {
            Visible = visible;
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Show(int currentCharacter)
        {
            _sel = Mathf.Clamp(currentCharacter, 0, _model.RosterCount - 1);
            SetVisible(true);
            Refresh();
        }

        public void Hide() => SetVisible(false);

        void Preview(int index)
        {
            if (index == _sel)
                return;
            _sel = index;
            UiArt.Sfx("SFX/ui/wardrobe_equip", 0.6f);
            Refresh();
        }

        void Confirm()
        {
            if (!_model.CanPick(_sel))
            {
                UiArt.Sfx("SFX/ui/ui_error");
                return;
            }
            UiArt.Sfx("SFX/ui/ready");
            Selected?.Invoke(_sel);
        }

        // Arrow keys switch, Enter selects, Esc goes back; AppFlow calls this only while the overlay is up.
        public void Tick()
        {
            var kb = Keyboard.current;
            if (kb == null)
                return;
            int n = _model.RosterCount;
            if (kb.leftArrowKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
                Preview((_sel + n - 1) % n);
            else if (kb.rightArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)
                Preview((_sel + 1) % n);
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                Confirm();
            if (kb.escapeKey.wasPressedThisFrame)
                Back?.Invoke();
        }

        public void Refresh()
        {
            for (int i = 0; i < _picks.Count; i++)
            {
                bool on = i == _sel;
                _picks[i].EnableInClassList("cs-pick--on", on);
                _glows[i].style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                _ribbons[i].style.display = _model.TakenBy(i) != null ? DisplayStyle.Flex : DisplayStyle.None;
            }
            var def = _art.Character(_sel);
            var color = _art.CharacterColor(_sel);
            UiArt.Set(_portrait, def != null ? def.Portrait : null, color);
            _portrait.style.borderLeftColor = _portrait.style.borderRightColor = _portrait.style.borderTopColor = _portrait.style.borderBottomColor = color;
            _name.text = def != null ? def.DisplayName : "";
            _name.style.color = Color.Lerp(color, Color.white, 0.2f);
            _title.text = def != null ? def.Title : "";
            _strength.text = def != null ? def.Strength : "";
            _weakness.text = def != null ? def.Weakness : "";
            _bio.text = def != null ? def.Bio : "";

            var ability = def != null ? def.Ability : Ability.Grappin;
            _sigIcon.style.backgroundImage = new StyleBackground(ActionInfo.Icon(ActionType.Signature, ability));
            _sigIcon.style.unityBackgroundImageTintColor = ActionInfo.IconTint(ActionType.Signature, ability);
            _sigName.text = def != null ? def.AbilityName : "";
            int cost = GameConfig.Default.SignatureCost;
            _sigCost.text = ability == Ability.TripleCharge ? $"{cost} Grit + loaded musket" : $"{cost} Grit";
            _sigDesc.text = def != null ? def.SignatureText : "";

            string taken = _model.TakenBy(_sel);
            _select.SetEnabled(taken == null);
            _select.text = taken == null ? "Select" : "Taken by " + taken;
        }
    }
}
