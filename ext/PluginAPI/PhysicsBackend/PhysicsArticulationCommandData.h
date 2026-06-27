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
#include "PhysicsJointTypes.h"
#include "PhysicsPose.h"

namespace Physics
{
    struct FloatBuffer
    {
        const float* data;
        size_t size;
        bool sdkOwnedMemory;
    };

    enum class ArticulationMotorType
    {
        Force = 0,
        Acceleration = 1,
        Target = 2,
        Velocity = 3
    };

    struct ArticulationMotorConfiguration
    {
        float lowerLimit;
        float upperLimit;
        float stiffness;
        float damping;
        float forceLimit;
        float target;
        float targetVelocity;
        ArticulationMotorType type;

        constexpr ArticulationMotorConfiguration()
            : lowerLimit(0),
            upperLimit(0),
            stiffness(0),
            damping(0),
            forceLimit(std::numeric_limits<float>::max()),
            target(0),
            targetVelocity(0),
            type(ArticulationMotorType::Force) {}
    };

    struct ReducedSpaceCoordinateStorage
    {
        float coordinate[3];
        int dofCount;

        constexpr ReducedSpaceCoordinateStorage()
            : coordinate{ 0,0,0 }
            , dofCount(0){}
    };
}

namespace PhysicsCommands
{
    namespace ArticulationJointData
    {
        struct SetType : Command
        {
            static constexpr auto command = Articulation::SetLinkJointType;

            Physics::JointType value;
        };

        struct GetType : Command
        {
            static constexpr auto command = Articulation::GetLinkJointType;

            Physics::JointType value;
        };

        struct SetAxisLock : Command
        {
            static constexpr auto command = Articulation::SetLinkJointAxisLock;

            Physics::ConstraintAxis axis;
            Physics::JointDofLock value;
        };

        struct SetLimit : Command
        {
            static constexpr auto command = Articulation::SetLinkJointLimit;

            //when using a rotational axis the values are in radians
            Physics::ConstraintAxis axis;
            float min;
            float max;
        };

        struct SetActorLocalPose : Command
        {
            static constexpr auto command = Articulation::SetLinkJointActorLocalPose;

            Physics::PoseTarget target;
            Physics::Pose value;
        };

        struct SetAxisMotorConfiguration : Command
        {
            static constexpr auto command = Articulation::SetLinkJointAxisMotorConfiguration;

            Physics::ConstraintAxis axis;
            Physics::ArticulationMotorConfiguration value;
        };

        struct SetFiction : Command
        {
            static constexpr auto command = Articulation::SetLinkJointFriction;

            float value;
        };

        struct SetPosition : Command
        {
            static constexpr auto command = Articulation::SetLinkJointPosition;

            Physics::ReducedSpaceCoordinateStorage value;
        };

        struct GetPosition : Command
        {
            static constexpr auto command = Articulation::GetLinkJointPosition;

            Physics::ReducedSpaceCoordinateStorage value;
        };

        struct SetVelocity : Command
        {
            static constexpr auto command = Articulation::SetLinkJointVelocity;

            Physics::ReducedSpaceCoordinateStorage value;
        };

        struct GetVelocity : Command
        {
            static constexpr auto command = Articulation::GetLinkJointVelocity;

            Physics::ReducedSpaceCoordinateStorage value;
        };

        struct SetAcceleration : Command
        {
            static constexpr auto command = Articulation::SetLinkJointAcceleration;

            Physics::ReducedSpaceCoordinateStorage value;
        };

        struct GetAcceleration : Command
        {
            static constexpr auto command = Articulation::GetLinkJointAcceleration;

            Physics::ReducedSpaceCoordinateStorage value;
        };

        struct SetForce : Command
        {
            static constexpr auto command = Articulation::SetLinkJointForce;

            Physics::ReducedSpaceCoordinateStorage value;
        };

        struct GetForce : Command
        {
            static constexpr auto command = Articulation::GetLinkJointForce;

            Physics::ReducedSpaceCoordinateStorage value;
        };

        struct GetMotorForce : Command
        {
            static constexpr auto command = Articulation::GetLinkJointMotorForce;

            Physics::ReducedSpaceCoordinateStorage value;

        };

        struct GetForcesForAcceleration : Command
        {
            static constexpr auto command = Articulation::GetLinkJointForcesForAcceleration;

            Physics::ReducedSpaceCoordinateStorage acceleration;
            Physics::ReducedSpaceCoordinateStorage value;
        };

        struct SetMaxVelocity : Command
        {
            static constexpr auto command = Articulation::SetLinkJointMaxVelocity;

            float value;
        };

        struct GetMaxVelocity : Command
        {
            static constexpr auto command = Articulation::GetLinkJointMaxVelocity;

            float value;
        };

        struct GetDofCount : Command
        {
            static constexpr auto command = Articulation::GetLinkJointDofCount;

            uint32_t value;
        };
    }

    namespace ArticulationLinkData
    {
        struct GetLinkIndex : Command
        {
            static constexpr auto command = Articulation::GetLinkIndex;

            int value;
        };

        struct GetLinkJoint : Command
        {
            static constexpr auto command = Articulation::GetLinkJoint;

            Physics::SDKObjectHandle value;
        };

        struct SetFlags : Command
        {
            static constexpr auto command = Articulation::SetLinkFlags;

            Physics::BodyFlags value;
            bool enabled;
        };

        struct GetFlags : Command
        {
            static constexpr auto command = Articulation::GetLinkFlags;

            Physics::BodyFlags value;
        };

        struct SetMass : Command
        {
            static constexpr auto command = Articulation::SetLinkMass;

            float value;
        };

        struct GetMass : Command
        {
            static constexpr auto command = Articulation::GetLinkMass;

            float value;
        };

        struct SetDamping : Command
        {
            static constexpr auto command = Articulation::SetLinkDamping;

            float linear;
            float angular;
        };

        struct GetDamping : Command
        {
            static constexpr auto command = Articulation::GetLinkDamping;

            float linear;
            float angular;
        };

        struct SetPose : Command
        {
            static constexpr auto command = Articulation::SetLinkPose;

            Physics::Pose value;
            bool awake;
        };

        struct GetPose : Command
        {
            static constexpr auto command = Articulation::GetLinkPose;

            Physics::Pose value;
        };

        struct AttachShape : Command
        {
            static constexpr auto command = Articulation::AttachShapeToLink;

            Physics::SDKObjectHandle shape;
            Physics::Pose pose;
        };

        struct DetackShape : Command
        {
            static constexpr auto command = Articulation::DetachShapeFromLink;

            Physics::SDKObjectHandle shape;
        };

        struct GetShapeCount : Command
        {
            static constexpr auto command = Articulation::GetLinkShapeCount;

            size_t value;
        };

        struct GetShapesUserData : Command
        {
            static constexpr auto command = Articulation::GetLinkShapesUserData;

            void** buffer;
            size_t bufferSize;

            size_t written;
        };

        struct GetUserData : Command
        {
            static constexpr auto command = Articulation::GetLinkUserData;

            void* value;
        };

        struct SetCollisionDetectionMode : Command
        {
            static constexpr auto command = Articulation::SetLinkCollisionDetectionMode;

            Physics::CollisionDetectionMode value;
        };

        struct AddForce : Command
        {
            static constexpr auto command = Articulation::AddForceToLink;

            float value[3];
            Physics::ForceMode mode;
            bool awake;
        };

        struct AddTorque : Command
        {
            static constexpr auto command = Articulation::AddTorqueToLink;

            float value[3];
            Physics::ForceMode mode;
            bool awake;
        };

        struct SetLinearVelocity : Command
        {
            static constexpr auto command = Articulation::SetLinkLinearVelocity;

            float value[3];
            bool awake;
        };

        struct GetLinearVelocity : Command
        {
            static constexpr auto command = Articulation::GetLinkLinearVelocity;

            float value[3];
        };

        struct SetAngularVelocity : Command
        {
            static constexpr auto command = Articulation::SetLinkAngularVelocity;

            float value[3];
            bool awake;
        };

        struct GetAngularVelocity : Command
        {
            static constexpr auto command = Articulation::GetLinkAngularVelocity;

            float value[3];
        };

        struct SetMaxLinearVelocity : Command
        {
            static constexpr auto command = Articulation::SetLinkMaxLinearVelocity;

            float value;
        };

        struct GetMaxLinearVelocity : Command
        {
            static constexpr auto command = Articulation::GetLinkMaxLinearVelocity;

            float value;
        };

        struct SetMaxAngularVelocity : Command
        {
            static constexpr auto command = Articulation::SetLinkMaxAngularVelocity;

            float value;
        };

        struct GetMaxAngularVelocity : Command
        {
            static constexpr auto command = Articulation::GetLinkMaxAngularVelocity;

            float value;
        };

        struct SetMaxDepenetrationVelocity : Command
        {
            static constexpr auto command = Articulation::SetLinkMaxDepenetrationVelocity;

            float value;
        };

        struct GetMaxDepenetrationVelocity : Command
        {
            static constexpr auto command = Articulation::GetLinkMaxDepenetrationVelocity;

            float value;
        };

        struct SetInertiaTensor : Command
        {
            static constexpr auto command = Articulation::SetLinkInertiaTensor;

            float value[3];
        };

        struct GetInertiaTensor : Command
        {
            static constexpr auto command = Articulation::GetLinkInertiaTensor;

            float value[3];
        };

        struct SetInertiaTensorRotation : Command
        {
            static constexpr auto command = Articulation::SetLinkInertiaTensorRotation;

            float value[4];
        };

        struct GetInertiaTensorRotation : Command
        {
            static constexpr auto command = Articulation::GetLinkInertiaTensorRotation;

            float value[4];
        };

        struct SetLocalCenterOfMass : Command
        {
            static constexpr auto command = Articulation::SetLinkLocalCenterOfMass;

            float value[3];
        };

        struct GetLocalCenterOfMass : Command
        {
            static constexpr auto command = Articulation::GetLinkLocalCenterOfMass;

            float value[3];
        };

        struct GetWorldInertiaTensorMatrix : Command
        {
            static constexpr auto command = Articulation::GetLinkWorldInertiaTensorMatrix;

            //stored as row major
            float value[9];
        };

        struct RecomputeMassProperties : Command
        {
            static constexpr auto command = Articulation::RecomputeLinkMassProperties;

            Physics::MassPropertiesOverride overrides;
        };

        struct GetAccumulatedForce : Command
        {
            static constexpr auto command = Articulation::GetLinkAccumulatedForce;

            float deltaTime;

            float value[3];
        };

        struct GetAccumulatedTorque : Command
        {
            static constexpr auto command = Articulation::GetLinkAccumulatedTorque;

            float deltaTime;

            float value[3];
        };

        struct GetLocalPointVelocity : Command
        {
            static constexpr auto command = Articulation::GetLinkLocalPointVelocity;

            float localPoint[3];

            float value[3];
        };

        struct GetWorldPointVelocity : Command
        {
            static constexpr auto command = Articulation::GetLinkWorldPointVelocity;

            float worldPoint[3];

            float value[3];
        };

        struct GetClosestWorldPoint : Command
        {
            static constexpr auto command = Articulation::GetLinkClosestWorldPoint;

            Physics::Pose enginePose;
            float worldPosition[3];

            float outClosestWorldPosition[3];
            float outSqrDistance;
        };
    }

    namespace ArticulationData
    {
        struct CreateLink : Command
        {
            static constexpr auto command = Articulation::CreateLink;

            Physics::SDKObjectHandle parentLink;
            void* userData;
            Physics::Pose pose;
            
            Physics::SDKObjectHandle outNewLink;
        };

        struct DestroyLink : Command
        {
            static constexpr auto command = Articulation::DestroyLink;

            Physics::SDKObjectHandle value;
        };

        struct GetLinkCount : Command
        {
            static constexpr auto command = Articulation::GetLinkCount;

            size_t value;
        };

        struct GetLinksUserData : Command
        {
            static constexpr auto command = Articulation::GetLinksUserData;

            bool sortedByIndex;

            void** buffer;
            size_t bufferSize;

            size_t written;
        };

        struct GetLinkParent : Command
        {
            static constexpr auto command = Articulation::GetLinkParent;

            Physics::SDKObjectHandle link;

            Physics::SDKObjectHandle value;
        };

        struct GetLinkParentUserData : Command
        {
            static constexpr auto command = Articulation::GetLinkParentUserData;

            Physics::SDKObjectHandle link;

            void* value;
        };

        struct GetLinkChildCount : Command
        {
            static constexpr auto command = Articulation::GetLinkChildCount;

            Physics::SDKObjectHandle link;

            size_t value;
        };

        struct GetLinkChildrenUserData : Command
        {
            static constexpr auto command = Articulation::GetLinkChildrenUserData;

            Physics::SDKObjectHandle link;

            void** buffer;
            size_t bufferSize;

            size_t written;
        };

        struct SetFlags : Command
        {
            static constexpr auto command = Articulation::SetFlags;

            //temporary until we change the processing queue to pass through (obj, context/scene, commandId, commandData)
            Physics::SDKObjectHandle scene;
            Physics::ArticulationFlags value;
            bool enabled;
        };

        struct GetFlags : Command
        {
            static constexpr auto command = Articulation::GetFlags;

            Physics::ArticulationFlags value;
        };

        struct GetUserData : Command
        {
            static constexpr auto command = Articulation::GetUserData;

            void* value;
        };

        struct GetIsSleeping : Command
        {
            static constexpr auto command = Articulation::GetIsSleeping;

            bool value;
        };

        struct Sleep : Command
        {
            static constexpr auto command = Articulation::Sleep;
        };

        struct WakeUp : Command
        {
            static constexpr auto command = Articulation::WakeUp;
        };

        struct SetSleepThresholdOverride : Command
        {
            static constexpr auto command = Articulation::SetSleepThresholdOverride;

            float value;
        };

        struct GetSleepThresholdOverride : Command
        {
            static constexpr auto command = Articulation::GetSleepThresholdOverride;

            float value;
        };

        struct SetSolverIterationsOverride : Command
        {
            static constexpr auto command = Articulation::SetSolverIterationsOverride;

            uint32_t positionIterations;
            uint32_t velocityIterations;
        };

        struct GetSolverIterationsOverride : Command
        {
            static constexpr auto command = Articulation::GetSolverIterationsOverride;

            uint32_t positionIterations;
            uint32_t velocityIterations;
        };

        struct GetDenseJacobianAsBuffer : Command
        {
            static constexpr auto command = Articulation::GetDenseJacobianAsFloatBuffer;

            uint32_t rowCount;
            uint32_t colCount;
            Physics::FloatBuffer value;
        };

        struct SetJointsPositions : Command
        {
            static constexpr auto command = Articulation::SetJointsPositions;

            Physics::FloatBuffer value;
        };

        struct GetJointsPositions : Command
        {
            static constexpr auto command = Articulation::GetJointsPositions;

            Physics::FloatBuffer value;
        };

        struct SetJointsVelocities : Command
        {
            static constexpr auto command = Articulation::SetJointsVelocities;

            Physics::FloatBuffer value;
        };

        struct GetJointsVelocities : Command
        {
            static constexpr auto command = Articulation::GetJointsVelocities;

            Physics::FloatBuffer value;
        };

        struct SetJointsAccelerations : Command
        {
            static constexpr auto command = Articulation::SetJointsAccelerations;

            Physics::FloatBuffer value;
        };

        struct GetJointsAccelerations : Command
        {
            static constexpr auto command = Articulation::GetJointsAccelerations;

            Physics::FloatBuffer value;
        };

        struct SetJointsForces : Command
        {
            static constexpr auto command = Articulation::SetJointsForces;

            Physics::FloatBuffer value;
        };

        struct GetJointsForces : Command
        {
            static constexpr auto command = Articulation::GetJointsForces;

            Physics::FloatBuffer value;
        };

        struct GetJointsDriveForces : Command
        {
            static constexpr auto command = Articulation::GetJointsDriveForces;

            Physics::FloatBuffer value;
        };

        struct GetJointsGravityForces : Command
        {
            static constexpr auto command = Articulation::GetJointsGravityForces;

            Physics::FloatBuffer value;
        };

        struct GetJointsCoriolisCentrifugalForces : Command
        {
            static constexpr auto command = Articulation::GetJointsCoriolisCentrifugalForces;

            Physics::FloatBuffer value;
        };

        struct GetJointsExternalForces : Command
        {
            static constexpr auto command = Articulation::GetJointsExternalForces;

            Physics::FloatBuffer value;
            float deltaTime;
        };

        struct ReleaseDataBuffer : Command
        {
            static constexpr auto command = Articulation::ReleaseDataBuffer;

            Physics::FloatBuffer value;
        };
    }
}
