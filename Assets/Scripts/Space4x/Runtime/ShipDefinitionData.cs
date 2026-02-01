using System;
using System.Collections.Generic;

namespace Space4X.Runtime
{
    /// <summary>
    /// Lightweight ship definition DTO for constructing a hull + module loadout.
    /// </summary>
    [Serializable]
    public class ShipDefinitionData
    {
        public string hullId;
        public float massCap;
        public List<string> modules;
    }
}
