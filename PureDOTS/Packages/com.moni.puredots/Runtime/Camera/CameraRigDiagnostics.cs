namespace PureDOTS.Runtime.Camera
{
    /// <summary>
    /// Diagnostics data for <see cref="CameraRigService"/> publish activity.
    /// </summary>
    public struct CameraRigDiagnostics
    {
        /// <summary>Frame number of the most recent publish (-1 if none yet).</summary>
        public int LastPublishFrame;

        /// <summary>Number of publishes that occurred during the current frame.</summary>
        public int PublishCountThisFrame;

        /// <summary>Rig type that most recently published.</summary>
        public CameraRigType LastRigType;

        /// <summary>Total number of publishes since service initialization.</summary>
        public int TotalPublishCount;
    }
}


