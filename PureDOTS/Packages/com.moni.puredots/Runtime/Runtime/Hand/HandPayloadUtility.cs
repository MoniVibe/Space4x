using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Hand
{
    public static class HandPayloadUtility
    {
        public static void AddAmount(ref DynamicBuffer<HandPayload> payload, ushort resourceType, float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            for (int i = 0; i < payload.Length; i++)
            {
                if (payload[i].ResourceTypeIndex != resourceType)
                {
                    continue;
                }

                var entry = payload[i];
                entry.Amount += amount;
                payload[i] = entry;
                return;
            }

            payload.Add(new HandPayload
            {
                ResourceTypeIndex = resourceType,
                Amount = amount
            });
        }

        public static float RemoveAmount(ref DynamicBuffer<HandPayload> payload, ushort resourceType, float amount)
        {
            if (amount <= 0f)
            {
                return 0f;
            }

            float remaining = amount;
            float removed = 0f;

            for (int i = 0; i < payload.Length && remaining > 0f;)
            {
                var entry = payload[i];
                if (entry.ResourceTypeIndex != resourceType)
                {
                    i++;
                    continue;
                }

                float take = math.min(entry.Amount, remaining);
                entry.Amount -= take;
                remaining -= take;
                removed += take;

                if (entry.Amount <= 0.0001f)
                {
                    payload.RemoveAt(i);
                }
                else
                {
                    payload[i] = entry;
                    i++;
                }
            }

            return removed;
        }

        public static float GetTotalAmount(DynamicBuffer<HandPayload> payload)
        {
            float total = 0f;
            for (int i = 0; i < payload.Length; i++)
            {
                total += payload[i].Amount;
            }
            return total;
        }

        public static ushort ResolveDominantType(DynamicBuffer<HandPayload> payload, ushort fallback = 0)
        {
            if (payload.Length == 0)
            {
                return fallback;
            }

            ushort dominantType = payload[0].ResourceTypeIndex;
            float dominantAmount = payload[0].Amount;

            for (int i = 1; i < payload.Length; i++)
            {
                var entry = payload[i];
                if (entry.Amount > dominantAmount)
                {
                    dominantAmount = entry.Amount;
                    dominantType = entry.ResourceTypeIndex;
                }
            }

            return dominantType;
        }
    }
}

