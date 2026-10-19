using UnityEngine;

namespace Musketeers.Data
{
    [CreateAssetMenu(menuName = "Musketeers/Map Catalog", fileName = "MapCatalog")]
    public sealed class MapCatalog : ScriptableObject
    {
        public MapDef[] Maps = new MapDef[0];

        public int Count => Maps.Length;

        public MapDef Get(int index) => index >= 0 && index < Maps.Length ? Maps[index] : null;

        public int IndexOf(string id)
        {
            for (int i = 0; i < Maps.Length; i++)
            {
                if (Maps[i] != null && Maps[i].Id == id)
                    return i;
            }
            return -1;
        }
    }
}
