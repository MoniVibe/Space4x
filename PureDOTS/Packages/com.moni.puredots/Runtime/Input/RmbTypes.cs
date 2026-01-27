using PureDOTS.Runtime.Camera;

namespace PureDOTS.Input
{
    public enum RmbPhase
    {
        Started,
        Performed,
        Canceled
    }

    public interface IRmbHandler
    {
        int Priority { get; }
        bool CanHandle(in RmbContext context);
        void OnRmb(in RmbContext context, RmbPhase phase);
    }
}
