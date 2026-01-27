namespace PureDOTS.Runtime.Communication
{
    public static class CommunicationDoctrineUtilities
    {
        public static float GetDoctrineWeight(CommOrderVerb verb)
        {
            return verb switch
            {
                CommOrderVerb.Hold => 0.7f,
                CommOrderVerb.Regroup => 0.65f,
                CommOrderVerb.Screen => 0.6f,
                CommOrderVerb.Defend => 0.55f,
                CommOrderVerb.MoveTo => 0.5f,
                CommOrderVerb.Patrol => 0.45f,
                CommOrderVerb.Suppress => 0.4f,
                CommOrderVerb.Flank => 0.35f,
                CommOrderVerb.FocusFire => 0.35f,
                CommOrderVerb.Retreat => 0.3f,
                CommOrderVerb.Attack => 0.25f,
                CommOrderVerb.DrawFire => 0.2f,
                CommOrderVerb.Spearhead => 0.15f,
                _ => 0.3f
            };
        }

        public static CommOrderVerb ResolveInferredVerb(CommOrderVerb verb, float contextFit, float risk)
        {
            if (contextFit >= 0.6f)
            {
                return verb;
            }

            if (risk >= 0.7f)
            {
                return GetFallbackVerb(verb);
            }

            return verb;
        }

        private static CommOrderVerb GetFallbackVerb(CommOrderVerb verb)
        {
            return verb switch
            {
                CommOrderVerb.Spearhead => CommOrderVerb.Screen,
                CommOrderVerb.DrawFire => CommOrderVerb.Screen,
                CommOrderVerb.Attack => CommOrderVerb.Defend,
                CommOrderVerb.Flank => CommOrderVerb.Screen,
                CommOrderVerb.FocusFire => CommOrderVerb.Suppress,
                CommOrderVerb.Retreat => CommOrderVerb.Regroup,
                CommOrderVerb.MoveTo => CommOrderVerb.Hold,
                CommOrderVerb.Patrol => CommOrderVerb.Hold,
                CommOrderVerb.Regroup => CommOrderVerb.Hold,
                _ => CommOrderVerb.Hold
            };
        }
    }
}
