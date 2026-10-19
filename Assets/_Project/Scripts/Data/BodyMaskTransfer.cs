namespace Musketeers.Data
{
    // "Copy mask from <musketeer>": moves a body mask between musketeers through its anchor. Into the
    // source anchor's local space, then out through the target's, so a boot mask follows the foot.
    public static class BodyMaskTransfer
    {
        public static BezierPath Transfer(BezierPath src, Anchor from, Anchor to)
        {
            return src.Map(
                v => to.Position + CosmeticLayout.Rotate(CosmeticLayout.Rotate((v - from.Position) / from.S, -from.Rotation) * to.S, to.Rotation),
                v => CosmeticLayout.Rotate(CosmeticLayout.Rotate(v / from.S, -from.Rotation) * to.S, to.Rotation));
        }

        public static void Copy(CharacterFit from, CharacterFit to, PoseKind pose, BodyMaskId id)
        {
            var s = from != null ? from.Get(pose) : null;
            if (s == null || to == null)
                return;
            var src = s.BodyMask(id);
            if (src == null || !src.Valid)
                return;
            var d = to.GetOrAdd(pose);
            var anchor = PoseFit.BodyMaskAnchor(id);
            d.SetBodyMask(id, Transfer(src, s.Get(anchor), d.Get(anchor)));
        }
    }
}
