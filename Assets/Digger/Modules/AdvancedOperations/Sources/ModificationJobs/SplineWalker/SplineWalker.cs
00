using Digger.Modules.AdvancedOperations.Splines;
using Digger.Modules.Core.Sources;
using Unity.Jobs;
using UnityEngine;

namespace Digger.Modules.AdvancedOperations.Sources.ModificationJobs.SplineWalker
{
    public class SplineWalker
    {
        private readonly DiggerSystem[] diggerSystems;

        public delegate IOperation<T> OperationAt<T>(Vector3 position) where T : struct, IJobParallelFor;

        public SplineWalker(DiggerSystem[] diggerSystems)
        {
            this.diggerSystems = diggerSystems;
        }

        public async Awaitable WalkAlongSpline<T>(BezierSpline spline, float step, OperationAt<T> getOperationAt, bool useBackgroundThreads = false) where T : struct, IJobParallelFor
        {
            var length = spline.GetApproxLength();
            step /= length;
            for (var t = 0f; t < 1f; t += step) {
                var operation = getOperationAt(spline.GetPoint(t));
                await DoOperation(operation, useBackgroundThreads);
            }

            foreach (var diggerSystem in diggerSystems) {
                await diggerSystem.BuildPendingMeshesAsync(useBackgroundThreads);
            }
        }

        private async Awaitable DoOperation<T>(IOperation<T> operation, bool useBackgroundThreads) where T : struct, IJobParallelFor
        {
            foreach (var diggerSystem in diggerSystems) {
                var area = operation.GetAreaToModify(diggerSystem);
                if (!area.NeedsModification)
                    continue;

                await diggerSystem.ModifyWithoutMeshes(operation, useBackgroundThreads);
            }
        }
    }
}