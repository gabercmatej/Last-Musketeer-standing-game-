using UnityEngine;

namespace Musketeers.Data
{
    // The four musketeers in a fixed order. Unit.Character and MatchSetup.Characters index into this.
    [CreateAssetMenu(menuName = "Musketeers/Character Roster", fileName = "CharacterRoster")]
    public sealed class CharacterRoster : ScriptableObject
    {
        public CharacterDef[] Characters = new CharacterDef[0];

        public int Count => Characters.Length;

        public CharacterDef Get(int index) =>
            index >= 0 && index < Characters.Length ? Characters[index] : null;
    }
}
