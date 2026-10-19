using UnityEngine;

namespace Musketeers.Data
{
    // One wardrobe item (GAME_DESIGN §10). Offset and Scale place the sprite's centre relative to the
    // pose anchor for its slot (CosmeticLayout); the Front values are used on front-facing poses.
    [CreateAssetMenu(menuName = "Musketeers/Cosmetic", fileName = "Cosmetic")]
    public sealed class CosmeticDef : ScriptableObject
    {
        public string Id = "item";
        public string DisplayName = "Item";
        public CosmeticSlot Slot;
        public Sprite Sprite;          // side view, facing right
        public Sprite FrontSprite;     // facing the camera (victory pose); null = use Sprite
        public Vector2 Offset;
        public float Scale = 1f;
        public Vector2 FrontOffset;
        public float FrontScale = 1f;
        public int SortOrder;          // relative to the body (0); negative draws behind it
        public bool Tuned;             // true once placed by hand; the importer then leaves Offset/Scale alone

        public static int DefaultOrder(CosmeticSlot slot)
        {
            switch (slot)
            {
                case CosmeticSlot.Cape: return -10;
                case CosmeticSlot.Boots: return 5;
                case CosmeticSlot.Face: return 10;
                default: return 20;
            }
        }
    }
}
