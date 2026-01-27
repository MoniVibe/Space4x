namespace PureDOTS.Runtime.Combat
{
    public static class ProjectileSpecSanitizer
    {
        public static void Sanitize(ref ProjectileSpec spec)
        {
            if (spec.Speed < 0f)
            {
                spec.Speed = 0f;
            }

            if (spec.Lifetime < 0f)
            {
                spec.Lifetime = 0f;
            }

            if (spec.TurnRateDeg < 0f)
            {
                spec.TurnRateDeg = 0f;
            }

            if (spec.SeekRadius < 0f)
            {
                spec.SeekRadius = 0f;
            }

            if (spec.AoERadius < 0f)
            {
                spec.AoERadius = 0f;
            }

            var kind = (ProjectileKind)spec.Kind;
            if (kind == ProjectileKind.Beam)
            {
                spec.Speed = 0f;
            }

            if (kind == ProjectileKind.Homing && spec.TurnRateDeg <= 0f)
            {
                spec.TurnRateDeg = 1f;
            }

            if (spec.HitFilter == 0 && spec.AoERadius <= 0f)
            {
                spec.HitFilter = 1u;
            }
        }
    }
}
