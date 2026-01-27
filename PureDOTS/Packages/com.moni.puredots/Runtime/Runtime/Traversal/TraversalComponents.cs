using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Traversal
{
    public enum TraversalType : byte
    {
        Jump = 0,
        Climb = 1,
        Crawl = 2,
        Squeeze = 3,
        Drop = 4
    }

    public enum TraversalStance : byte
    {
        Standing = 0,
        Crouching = 1,
        Crawling = 2
    }

    [System.Flags]
    public enum TraversalRequirementFlags : byte
    {
        None = 0,
        NeedsHands = 1 << 0,
        NeedsClimbableSurface = 1 << 1,
        NeedsSmallSize = 1 << 2
    }

    public struct BodyDimensions : IComponentData
    {
        public float Radius;
        public float StandingHeight;
        public float CrouchHeight;
        public float CrawlHeight;
    }

    public struct MobilityCaps : IComponentData
    {
        public float MaxJumpDistance;
        public float MaxJumpUp;
        public float MaxDropDown;
        public byte CanClimb;
        public float ClimbSpeed;
        public byte CanCrawl;
        public float CrawlSpeedMultiplier;
        public byte CanSqueeze;
    }

    public struct TraversalExecutionParams
    {
        public float ArcHeight;
        public float Duration;
        public float LandingSnapDistance;
        public float LandingSnapVerticalTolerance;
    }

    public struct TraversalLink : IComponentData
    {
        public TraversalType Type;
        public float3 StartPosition;
        public float3 EndPosition;
        public float MaxRadius;
        public float MaxHeight;
        public TraversalStance RequiredStance;
        public TraversalRequirementFlags Requirements;
        public float Cost;
        public TraversalExecutionParams Execution;
        public byte IsBidirectional;
    }

    [InternalBufferCapacity(2)]
    public struct TraversalRequest : IBufferElementData
    {
        public Entity LinkEntity;
        public byte Priority;
        public uint RequestedTick;
    }

    public struct TraversalExecutionState : IComponentData
    {
        public Entity LinkEntity;
        public TraversalType Type;
        public float3 StartPosition;
        public float3 EndPosition;
        public float ArcHeight;
        public float Duration;
        public float LandingSnapDistance;
        public float LandingSnapVerticalTolerance;
        public float Elapsed;
        public uint StartTick;
        public byte IsActive;
    }

    public static class TraversalUtility
    {
        public static float ResolveHeight(in BodyDimensions dimensions, TraversalStance stance)
        {
            var standing = math.max(0f, dimensions.StandingHeight);
            var crouch = dimensions.CrouchHeight > 0f ? dimensions.CrouchHeight : standing * 0.7f;
            var crawl = dimensions.CrawlHeight > 0f ? dimensions.CrawlHeight : standing * 0.4f;

            return stance switch
            {
                TraversalStance.Crouching => crouch,
                TraversalStance.Crawling => crawl,
                _ => standing
            };
        }

        public static bool MeetsClearance(in TraversalLink link, in BodyDimensions dimensions)
        {
            if (link.MaxRadius > 0f && dimensions.Radius > link.MaxRadius)
            {
                return false;
            }

            var height = ResolveHeight(dimensions, link.RequiredStance);
            if (link.MaxHeight > 0f && height > link.MaxHeight)
            {
                return false;
            }

            if ((link.Requirements & TraversalRequirementFlags.NeedsSmallSize) != 0 && link.MaxRadius > 0f)
            {
                return dimensions.Radius <= link.MaxRadius;
            }

            return true;
        }

        public static bool CanTraverse(in TraversalLink link, in BodyDimensions dimensions, in MobilityCaps caps)
        {
            if (!MeetsClearance(link, dimensions))
            {
                return false;
            }

            switch (link.Type)
            {
                case TraversalType.Jump:
                    return CanJump(link, caps);
                case TraversalType.Drop:
                    return CanDrop(link, caps);
                case TraversalType.Climb:
                    return caps.CanClimb != 0;
                case TraversalType.Crawl:
                    return caps.CanCrawl != 0;
                case TraversalType.Squeeze:
                    return caps.CanSqueeze != 0 && caps.CanCrawl != 0;
                default:
                    return false;
            }
        }

        private static bool CanJump(in TraversalLink link, in MobilityCaps caps)
        {
            var delta = link.EndPosition - link.StartPosition;
            var horizontalDistance = math.length(new float2(delta.x, delta.z));
            var vertical = delta.y;

            if (caps.MaxJumpDistance > 0f && horizontalDistance > caps.MaxJumpDistance)
            {
                return false;
            }

            if (vertical > 0f && caps.MaxJumpUp > 0f && vertical > caps.MaxJumpUp)
            {
                return false;
            }

            if (vertical < 0f && caps.MaxDropDown > 0f && math.abs(vertical) > caps.MaxDropDown)
            {
                return false;
            }

            return true;
        }

        private static bool CanDrop(in TraversalLink link, in MobilityCaps caps)
        {
            var vertical = link.EndPosition.y - link.StartPosition.y;
            if (vertical > 0f)
            {
                return false;
            }

            if (caps.MaxDropDown > 0f && math.abs(vertical) > caps.MaxDropDown)
            {
                return false;
            }

            return true;
        }
    }
}
