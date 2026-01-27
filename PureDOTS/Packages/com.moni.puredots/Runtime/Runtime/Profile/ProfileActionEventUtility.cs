using Unity.Entities;

namespace PureDOTS.Runtime.Profile
{
    public static class ProfileActionEventUtility
    {
        public static bool TryAppend(
            ref ProfileActionEventStream stream,
            DynamicBuffer<ProfileActionEvent> buffer,
            in ProfileActionEvent actionEvent,
            int maxEvents)
        {
            if (maxEvents <= 0)
            {
                maxEvents = 256;
            }

            if (buffer.Length >= maxEvents)
            {
                stream.DroppedEvents++;
                stream.EventCount = buffer.Length;
                stream.LastWriteTick = actionEvent.Tick;
                return false;
            }

            buffer.Add(actionEvent);
            stream.Version++;
            stream.EventCount = buffer.Length;
            stream.LastWriteTick = actionEvent.Tick;
            return true;
        }
    }
}
