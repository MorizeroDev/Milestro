// Unity Physics Backend System Native Plugin API Data Protocol copyright © 2025 Unity Technologies ApS
//
// Licensed under the Unity Companion License for Unity - dependent projects--see[Unity Companion License](http://www.unity3d.com/legal/licenses/Unity_Companion_License).
//
// Unless expressly provided otherwise, the Software under this license is made available strictly on an “AS IS” BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.Please review the license for details on these and other terms and conditions.
//
// Please note that the following data protocol types represent the data used internally by Unity's Physics Module in order to communicate with the current SDK Integration. As such these types are subject to rapid change between different Unity versions.

#pragma once
#include <cstdint>
#include <type_traits>

#include "PhysicsCommands.h"
#include "PhysicsBodyTypes.h"
#include "PhysicsPose.h"

namespace PhysicsCommands
{
    namespace BodyData
    {
        struct SetFlags : Command
        {
            static constexpr auto command = Body::SetFlags;

            Physics::SDKObjectHandle scene;
            Physics::BodyFlags value;
            bool enabled;
        };

        struct GetFlags : Command
        {
            static constexpr auto command = Body::GetFlags;

            Physics::BodyFlags value;
        };

        struct SetMass : Command
        {
            static constexpr auto command = Body::SetMass;

            float value;
        };

        struct GetMass : Command
        {
            static constexpr auto command = Body::GetMass;

            float value;
        };

        struct SetDamping : Command
        {
            static constexpr auto command = Body::SetDamping;

            float linear;
            float angular;
        };

        struct GetDamping : Command
        {
            static constexpr auto command = Body::GetDamping;

            float linear;
            float angular;
        };

        struct SetPose : Command
        {
            static constexpr auto command = Body::SetPose;

            Physics::Pose value;
            bool awake;
        };

        struct GetPose : Command
        {
            static constexpr auto command = Body::GetPose;

            Physics::Pose value;
        };

        struct AttachShape : Command
        {
            static constexpr auto command = Body::AttachShape;
            Physics::SDKObjectHandle shape;
            Physics::Pose pose;
            //Temporary solution until we fix local/global pose relative to rb/ab/static body
            bool forceLocalPose;
        };

        struct DetachShape : Command
        {
            static constexpr auto command = Body::DetachShape;
            Physics::SDKObjectHandle shape;
        };

        struct GetShapeCount : Command
        {
            static constexpr auto command = Body::GetShapeCount;

            size_t value;
        };

        struct GetShapes : Command
        {
            static constexpr auto command = Body::GetShapes;

            Physics::SDKObjectHandle* buffer;
            size_t bufferSize;

            size_t written;
        };

        struct GetShapesUserData : Command
        {
            static constexpr auto command = Body::GetShapesUserData;

            void** buffer;
            size_t bufferSize;

            size_t written;
        };

        struct GetUserData : Command
        {
            static constexpr auto command = Body::GetUserData;

            void* value;
        };

        struct SetCollisionDetectionMode : Command
        {
            static constexpr auto command = Body::SetCollisionDetectionMode;

            Physics::CollisionDetectionMode value;
        };

        struct AddForce : Command
        {
            static constexpr auto command = Body::AddForce;

            float value[3];
            Physics::ForceMode mode;
            bool awake;
        };

        struct AddTorque : Command
        {
            static constexpr auto command = Body::AddTorque;

            float value[3];
            Physics::ForceMode mode;
            bool awake;
        };

        struct SetLinearVelocity : Command
        {
            static constexpr auto command = Body::SetLinearVelocity;

            float value[3];
            bool awake;
        };

        struct GetLinearVelocity : Command
        {
            static constexpr auto command = Body::GetLinearVelocity;

            float value[3];
        };

        struct SetAngularVelocity : Command
        {
            static constexpr auto command = Body::SetAngularVelocity;

            float value[3];
            bool awake;
        };

        struct GetAngularVelocity : Command
        {
            static constexpr auto command = Body::GetAngularVelocity;

            float value[3];
        };

        struct SetMaxLinearVelocity : Command
        {
            static constexpr auto command = Body::SetMaxLinearVelocity;

            float value;
        };

        struct GetMaxLinearVelocity : Command
        {
            static constexpr auto command = Body::GetMaxLinearVelocity;

            float value;
        };

        struct SetMaxAngularVelocity : Command
        {
            static constexpr auto command = Body::SetMaxAngularVelocity;

            float value;
        };

        struct GetMaxAngularVelocity : Command
        {
            static constexpr auto command = Body::GetMaxAngularVelocity;

            float value;
        };

        struct SetMaxDepenetrationVelocity : Command
        {
            static constexpr auto command = Body::SetMaxDepenetrationVelocity;

            float value;
        };

        struct GetMaxDepenetrationVelocity : Command
        {
            static constexpr auto command = Body::GetMaxDepenetrationVelocity;

            float value;
        };

        struct SetInertiaTensor : Command
        {
            static constexpr auto command = Body::SetInertiaTensor;

            float value[3];
        };

        struct GetInertiaTensor : Command
        {
            static constexpr auto command = Body::GetInertiaTensor;

            float value[3];
        };

        struct SetInertiaTensorRotation : Command
        {
            static constexpr auto command = Body::SetInertiaTensorRotation;

            float value[4];
        };

        struct GetInertiaTensorRotation : Command
        {
            static constexpr auto command = Body::GetInertiaTensorRotation;

            float value[4];
        };

        struct SetLocalCenterOfMass : Command
        {
            static constexpr auto command = Body::SetLocalCenterOfMass;

            float value[3];
        };

        struct GetLocalCenterOfMass : Command
        {
            static constexpr auto command = Body::GetLocalCenterOfMass;

            float value[3];
        };

        struct GetWorldInertiaTensorMatrix : Command
        {
            static constexpr auto command = Body::GetWorldInertiaTensorMatrix;

            //stored as row major
            float value[9];
        };

        struct RecomputeMassProperties : Command
        {
            static constexpr auto command = Body::RecomputeMassProperties;

            Physics::MassPropertiesOverride overrides;
        };

        struct GetAccumulatedForce : Command
        {
            static constexpr auto command = Body::GetAccumulatedForce;

            float deltaTime;

            float value[3];
        };

        struct GetAccumulatedTorque : Command
        {
            static constexpr auto command = Body::GetAccumulatedTorque;

            float deltaTime;

            float value[3];
        };

        struct GetLocalPointVelocity : Command
        {
            static constexpr auto command = Body::GetLocalPointVelocity;

            float localPoint[3];

            float value[3];
        };

        struct GetWorldPointVelocity : Command
        {
            static constexpr auto command = Body::GetWorldPointVelocity;

            float worldPoint[3];

            float value[3];
        };

        struct GetClosestWorldPointOnBounds : Command
        {
            static constexpr auto command = Body::GetClosestWorldPointOnBounds;

            float worldPosition[3];

            float outClosestWorldPositionOnBounds[3];
            float outSqrDistance;
        };

        struct SetKinematicTarget : Command
        {
            static constexpr auto command = Body::SetKinematicTarget;

            Physics::Pose value;
        };

        struct GetKinematicTarget : Command
        {
            static constexpr auto command = Body::GetKinematicTarget;

            Physics::Pose value;
        };

        struct GetIsSleeping : Command
        {
            static constexpr auto command = Body::GetIsSleeping;

            bool value;
        };

        struct Sleep : Command
        {
            static constexpr auto command = Body::Sleep;
        };

        struct WakeUp : Command
        {
            static constexpr auto command = Body::WakeUp;
        };

        struct SetSleepThresholdOverride : Command
        {
            static constexpr auto command = Body::SetSleepThresholdOverride;

            float value;
        };

        struct GetSleepThresholdOverride : Command
        {
            static constexpr auto command = Body::GetSleepThresholdOverride;

            float value;
        };

        struct SetSolverIterationsOverride : Command
        {
            static constexpr auto command = Body::SetSolverIterationsOverride;

            uint32_t positionIterations;
            uint32_t velocityIterations;
        };

        struct GetSolverIterationsOverride : Command
        {
            static constexpr auto command = Body::GetSolverIterationsOverride;

            uint32_t positionIterations;
            uint32_t velocityIterations;
        };
    }
}
