using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Stats
{
    /// <summary>
    /// Helper API for querying trait axis values from entities.
    /// </summary>
    [BurstCompile]
    public static class TraitAxisLookup
    {
        /// <summary>
        /// Try to get a trait axis value for an entity.
        /// </summary>
        /// <param name="entity">Entity to query.</param>
        /// <param name="axisId">Axis identifier.</param>
        /// <param name="value">Output value if found.</param>
        /// <param name="buffer">TraitAxisValue buffer from entity.</param>
        /// <returns>True if axis was found, false otherwise.</returns>
        public static bool TryGetValue(Entity entity, FixedString32Bytes axisId, out float value, DynamicBuffer<TraitAxisValue> buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].AxisId.Equals(axisId))
                {
                    value = buffer[i].Value;
                    return true;
                }
            }
            
            value = 0f;
            return false;
        }

        /// <summary>
        /// Get a trait axis value, returning a default if not found.
        /// </summary>
        /// <param name="entity">Entity to query.</param>
        /// <param name="axisId">Axis identifier.</param>
        /// <param name="defaultValue">Default value if axis not found.</param>
        /// <param name="buffer">TraitAxisValue buffer from entity.</param>
        /// <returns>Axis value or default.</returns>
        public static float GetValueOrDefault(Entity entity, FixedString32Bytes axisId, float defaultValue, DynamicBuffer<TraitAxisValue> buffer)
        {
            if (TryGetValue(entity, axisId, out float value, buffer))
            {
                return value;
            }
            return defaultValue;
        }

        /// <summary>
        /// Get a trait axis value, returning 0 if not found.
        /// </summary>
        /// <param name="entity">Entity to query.</param>
        /// <param name="axisId">Axis identifier.</param>
        /// <param name="buffer">TraitAxisValue buffer from entity.</param>
        /// <returns>Axis value or 0.</returns>
        public static float GetValueOrDefault(Entity entity, FixedString32Bytes axisId, DynamicBuffer<TraitAxisValue> buffer)
        {
            return GetValueOrDefault(entity, axisId, 0f, buffer);
        }

        /// <summary>
        /// Set or update a trait axis value.
        /// </summary>
        /// <param name="axisId">Axis identifier.</param>
        /// <param name="value">Value to set.</param>
        /// <param name="buffer">TraitAxisValue buffer to modify.</param>
        public static void SetValue(FixedString32Bytes axisId, float value, ref DynamicBuffer<TraitAxisValue> buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].AxisId.Equals(axisId))
                {
                    var element = buffer[i];
                    element.Value = value;
                    buffer[i] = element;
                    return;
                }
            }
            
            // Not found, add new entry
            buffer.Add(new TraitAxisValue { AxisId = axisId, Value = value });
        }

        /// <summary>
        /// Apply a delta to a trait axis value (adds to existing value or creates new entry).
        /// </summary>
        /// <param name="axisId">Axis identifier.</param>
        /// <param name="delta">Delta to apply.</param>
        /// <param name="buffer">TraitAxisValue buffer to modify.</param>
        public static void ApplyDelta(FixedString32Bytes axisId, float delta, ref DynamicBuffer<TraitAxisValue> buffer)
        {
            float currentValue = 0f;
            bool found = false;
            
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].AxisId.Equals(axisId))
                {
                    currentValue = buffer[i].Value;
                    found = true;
                    break;
                }
            }
            
            SetValue(axisId, currentValue + delta, ref buffer);
        }

        /// <summary>
        /// Get all trait axes for an entity (allocates a NativeList).
        /// </summary>
        /// <param name="buffer">TraitAxisValue buffer from entity.</param>
        /// <param name="allocator">Allocator for the result list.</param>
        /// <returns>List of all trait axis values.</returns>
        public static NativeList<TraitAxisValue> GetAllAxes(DynamicBuffer<TraitAxisValue> buffer, Allocator allocator)
        {
            var result = new NativeList<TraitAxisValue>(buffer.Length, allocator);
            for (int i = 0; i < buffer.Length; i++)
            {
                result.Add(buffer[i]);
            }
            return result;
        }

        /// <summary>
        /// Get trait axes filtered by tag (requires catalog lookup).
        /// </summary>
        /// <param name="buffer">TraitAxisValue buffer from entity.</param>
        /// <param name="catalog">Trait axis catalog blob.</param>
        /// <param name="tag">Tag to filter by.</param>
        /// <param name="allocator">Allocator for the result list.</param>
        /// <returns>List of trait axis values matching the tag.</returns>
        public static NativeList<TraitAxisValue> GetAxesByTag(
            DynamicBuffer<TraitAxisValue> buffer,
            BlobAssetReference<TraitAxisCatalogBlob> catalog,
            TraitAxisTag tag,
            Allocator allocator)
        {
            var result = new NativeList<TraitAxisValue>(buffer.Length, allocator);
            
            if (!catalog.IsCreated)
            {
                return result;
            }
            
            ref var catalogData = ref catalog.Value;
            
            for (int i = 0; i < buffer.Length; i++)
            {
                var axisValue = buffer[i];
                
                // Find axis definition in catalog
                for (int j = 0; j < catalogData.Axes.Length; j++)
                {
                    ref var axisDef = ref catalogData.Axes[j];
                    if (axisDef.AxisId.Equals(axisValue.AxisId) && (axisDef.Tags & tag) != 0)
                    {
                        result.Add(axisValue);
                        break;
                    }
                }
            }
            
            return result;
        }
    }
}

