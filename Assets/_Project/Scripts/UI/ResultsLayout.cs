using System;

namespace Musketeers.UI
{
    // Pure helpers for the results screen (FIXES_v0.2 §2.5): ordinal labels and the podium order.
    public static class ResultsLayout
    {
        // 1 -> "1st", 2 -> "2nd", 3 -> "3rd", 4 -> "4th". A shared place shows the same ordinal.
        public static string Ordinal(int place)
        {
            switch (place)
            {
                case 1: return "1st";
                case 2: return "2nd";
                case 3: return "3rd";
                default: return place + "th";
            }
        }

        // Slots sorted by place, ties by slot: the k-th entry stands on podium k+1.
        public static int[] Order(int[] places)
        {
            var order = new int[places.Length];
            for (int i = 0; i < order.Length; i++)
                order[i] = i;
            Array.Sort(order, (a, b) => places[a] != places[b] ? places[a].CompareTo(places[b]) : a.CompareTo(b));
            return order;
        }
    }
}
