// Unity Physics Backend System Native Plugin API Data Protocol copyright © 2025 Unity Technologies ApS
//
// Licensed under the Unity Companion License for Unity - dependent projects--see[Unity Companion License](http://www.unity3d.com/legal/licenses/Unity_Companion_License).
//
// Unless expressly provided otherwise, the Software under this license is made available strictly on an “AS IS” BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.Please review the license for details on these and other terms and conditions.
//
// Please note that the following data protocol types represent the data used internally by Unity's Physics Module in order to communicate with the current SDK Integration. As such these types are subject to rapid change between different Unity versions.

#pragma once
#include <cstdint>
#include "PhysicsCommands.h"
#include "PhysicsQueryTypes.h"
#include "PhysicsShapeTypes.h"
#include "PhysicsPose.h"

namespace PhysicsCommands
{
    namespace QueryData
    {
        struct ComputeShapePenetration : Command
        {
            static constexpr auto command = Query::ComputeShapePenetration;

            void* shapeA;
            void* shapeB;
            Physics::Pose poseA;
            Physics::Pose poseB;

            float outDirection[3];
            float outDistance;
            bool areOverlapping;
        };

        struct ClosestPointOnShape : Command
        {
            static constexpr auto command = Query::ClosestPointOnShape;

            Physics::Pose pose; //28 //aligned 4
            float point[3]; // 12

            float outPoint[3]; //12
            float outDistance; // 4
        };

        struct RayCast : Command
        {
            static constexpr auto command = Query::RayCast;

            Physics::QueryHit hit;
            Physics::QueryFilterData filter;
            float origin[3];
            float direction[3];
            uint32_t hitCount;
            float maxDistance;
        };

        struct RayCastAgainstShape : Command
        {
            static constexpr auto command = Query::RayCastAgainstShape;

            float origin[3];
            float direction[3];
            Physics::QueryFilterFlags filterOptions;
            Physics::QueryHit hit;
            uint32_t hitCount;
            float maxDistance;
        };

        struct RaycastWithCollector : Command
        {
            static constexpr auto command = Query::RayCastWithCollector;

            Physics::QueryHitResultCollectorCallback collector;
            void* dataContext;
            Physics::QueryFilterData filter;
            float origin[3];
            float direction[3];
            float maxDistance;
        };

        struct Overlap : Command
        {
            static constexpr auto command = Query::Overlap;

            Physics::ShapeGeometry geometry;
            Physics::Pose pose;
            Physics::QueryFilterData filter;

            void* shapeUserData;
        };

        struct OverlapWithCollector : Command
        {
            static constexpr auto command = Query::OverlapWithCollector;

            Physics::ShapeGeometry geometry;
            Physics::Pose pose;
            Physics::QueryFilterData filter;
            void* dataContext;
            Physics::QueryShapesResultCollectorCallback collector;
        };

        struct OverlapWithBroadphaseCollector : Command
        {
            static constexpr auto command = Query::OverlapWithBroadphaseCollector;

            Physics::ShapeGeometry geometry;
            Physics::Pose pose;
            Physics::QueryFilterData filter;
            void* dataContext;
            Physics::BroadPhaseQueryShapeCollectorCallback collector;

        };

        struct ShapeCast : Command
        {
            static constexpr auto command = Query::ShapeCast;

            Physics::ShapeGeometry geometry;
            Physics::Pose pose;
            Physics::QueryFilterData filter;
            float direction[3];
            float maxDistance;

            Physics::QueryHit hit;
            uint32_t hitCount;
        };

        struct ShapeCastAgainstShape : Command
        {
            static constexpr auto command = Query::ShapeCastAgainstShape;

            Physics::ShapeGeometry targetGeometry;
            Physics::Pose targetPose;

            Physics::QueryFilterData filter;
            float direction[3];
            float maxDistance;

            Physics::QueryHit hit;
            uint32_t hitCount;
            bool distanceAsDepth;
        };

        struct ShapeCastWithCollector : Command
        {
            static constexpr auto command = Query::ShapeCastWithCollector;

            Physics::ShapeGeometry geometry;
            Physics::Pose pose;
            //added only for supporting the current behaviour of Physics.XCastAll/NonAlloc(...) where initial overlap hits are also reported
            //this behavior should be unified in later versions. As the other ShapeCast command usages discard the initial overlaps
            bool keepInitialOverlaps;
            Physics::QueryFilterData filter;
            float direction[3];
            float maxDistance;

            void* dataContext;
            Physics::QueryHitResultCollectorCallback collector;
        };

        struct BodyCast : Command
        {
            static constexpr auto command = Query::BodyCast;

            float direction[3];
            float maxDistance;
            Physics::QueryFilterData filter;
            Physics::SDKObjectHandle body;

            Physics::QueryHit hit;
            uint32_t hitCount;
        };

        struct BodyCastWithCollector : Command
        {
            static constexpr auto command = Query::BodyCastWithCollector;

            float direction[3];
            float maxDistance;
            Physics::QueryFilterData filter;
            Physics::SDKObjectHandle body;

            void* dataContext;
            Physics::QueryHitResultCollectorCallback collector;
        };
    }
}
