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
#include <limits>

#include "PhysicsCommands.h"
#include "PhysicsBodyTypes.h"
#include "PhysicsJointTypes.h"
#include "PhysicsWorldTypes.h"
#include "PhysicsEventTypes.h"
#include "PhysicsPose.h"

namespace Physics
{
    using ColliderBufferCallback = void(*)(void** buffer, size_t size);
}

namespace PhysicsCommands
{
    namespace WorldData
    {
        struct CreateWorld : Command
        {
            static constexpr auto command = World::CreateWorld;

            Physics::SDKObjectHandle outWorld = nullptr;
            Physics::ContactModificationEventCallback onModifyContactsCallback;
            void* userData = nullptr;

            Physics::SolverType solverType = Physics::SolverType::ProjectedGaussSeidelSolver;
            Physics::BroadphaseType broadphaseType = Physics::BroadphaseType::AutomaticBoxPruning;
            Physics::FrictionType frictionType = Physics::FrictionType::PatchFrictionType;
            Physics::ContactPairsMode contactPairsMode = Physics::ContactPairsMode::DefaultContactPairs;

            float gravity[3] = { 0.0f, -9.81f, 0.0f };
            float bounceThreshold = 1.0f;
            float fastMotionThreshold = std::numeric_limits<float>::max();
            uint32_t jobWorkerCount = 0;

            float worldMin[3] = { -256.0f, -256.0f, -256.0f };
            float worldMax[3] = { 256.0f, 256.0f, 256.0f };
            uint32_t worldSubdivisions = 8;

            bool enableIncrementalStaticBroadphase = false;
            bool enableAdaptiveForce = false;
            bool enableEnhancedDeterminism = false;
        };

        struct DestroyWorld : Command
        {
            static constexpr auto command = World::DestroyWorld;
        };

        struct SetGravity : Command
        {
            static constexpr auto command = World::SetGravity;

            float value[3];

        };

        struct GetGravity : Command
        {
            static constexpr auto command = World::GetGravity;

            float value[3];
        };

        struct SetBounceThreshold : Command
        {
            static constexpr auto command = World::SetBounceThreshold;

            float value;
        };

        struct GetBounceThreshold : Command
        {
            static constexpr auto command = World::GetBounceThreshold;

            float value;
        };

        struct GetBasicStats : Command
        {
            static constexpr auto command = World::GetBasicStats;

            Physics::WorldStats value;
        };

        struct SetScratchBufferChunkCount : Command
        {
            static constexpr auto command = World::SetScratchBufferChunkCount;

            uint32_t value = 0;
        };

        struct Simulate : Command
        {
            static constexpr auto command = World::Simulate;

            uint64_t jobGroupdId = 0;
            float deltaTime = 0.0f;
            float expectedMaxDeltaTime = std::numeric_limits<float>::max();
            bool ignoreEmptyScene = false;
            bool ranSimulation = true;
        };

        struct GetLastSimulationStepResults : Command
        {
            static constexpr auto command = World::GetLastSimulationStepResults;

            Physics::BodyPair* bodyContactPairs;
            size_t bodyContactPairsCount;

            Physics::TriggerEvent* triggerEvents;
            size_t triggerEventsCount;

            Physics::JointBreakEvent* jointBreakEvents;
            size_t jointBreakEventsCount;
        };

        struct ReleaseLastSimulationStepData : Command
        {
            static constexpr auto command = World::ReleaseLastSimulationStepData;
        };

        struct GetLastSimulationStepStats : Command
        {
            static constexpr auto command = World::GetLastSimulationStats;

            Physics::SimulationStepStats value;
        };

        struct GetUserData : Command
        {
            static constexpr auto command = World::GetUserData;

            void* userData;
        };

        struct CreateBody : Command
        {
            static constexpr auto command = World::CreateBody;

            Physics::Pose pose;
            void* userData;
            float mass;
            float linearDamping;
            float angularDamping;
            Physics::BodyFlags flags;
            bool isStatic;
            uint8_t padding[7];

            Physics::SDKObjectHandle outBody;
        };

        struct AddActorsToScene : Command
        {
            static constexpr auto command = World::AddActorsToScene;
            Physics::SDKObjectHandle actors;
            int bufferSize;            
        };

        struct DestroyBody : Command
        {
            static constexpr auto command = World::DestroyBody;

            Physics::SDKObjectHandle value;
        };

        struct CreateArticulation : Command
        {
            static constexpr auto command = World::CreateArticulation;

            Physics::ArticulationFlags flags;
            void* userData;

            Physics::SDKObjectHandle outArticulation;
        };

        struct DestoryArticulation : Command
        {
            static constexpr auto command = World::DestroyArticulation;

            Physics::SDKObjectHandle value;
        };

        struct CreateJoint : Command
        {
            static constexpr auto command = World::CreateJoint;

            Physics::SDKObjectHandle body0;
            Physics::SDKObjectHandle body1;
            void* userData;
            Physics::JointType type;
            Physics::JointFlags flags;

            Physics::SDKObjectHandle outJoint;
        };

        struct DestroyJoint : Command
        {
            static constexpr auto command = World::DestroyJoint;

            Physics::SDKObjectHandle value;
        };

        struct GetArticulationCount : Command
        {
            static constexpr auto command = World::GetArticulationCount;

            uint32_t value;
        };

        struct GetArticulations : Command
        {
            static constexpr auto command = World::GetArticulations;

            Physics::SDKObjectHandle* buffer;
            uint32_t bufferSize;
            uint32_t written;
        };

        struct GetAllShapesByLayerMask : Command
        {
            static constexpr auto command = World::GetAllShapesByLayerMask;

            uint32_t layerMask;
            Physics::ColliderBufferCallback callback;
        };
    }
}
