// Unity Physics Backend System Native Plugin API Data Protocol copyright © 2025 Unity Technologies ApS
//
// Licensed under the Unity Companion License for Unity - dependent projects--see[Unity Companion License](http://www.unity3d.com/legal/licenses/Unity_Companion_License).
//
// Unless expressly provided otherwise, the Software under this license is made available strictly on an “AS IS” BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.Please review the license for details on these and other terms and conditions.
//
// Please note that the following data protocol types represent the data used internally by Unity's Physics Module in order to communicate with the current SDK Integration. As such these types are subject to rapid change between different Unity versions.

#pragma once
#include "PhysXPhysicsExtensionCommands.h"

namespace PhysicsCommands
{
    namespace PhysXExt
    {
        namespace CharacterControllerData
        {
            struct SetCharacterControllerMgrOverlapRecovery : Command
            {
                static constexpr auto command = PhysXExtension::SetCharacterControllerMgrOverlapRecovery;
                bool value;
            };

            struct CreateCharacterController : Command
            {
                static constexpr auto command = PhysXExtension::CreateCharacterController;

                Physics::SDKObjectHandle material;
                void* userData;

                float initialPosition[3];
                float slopeLimit;
                float contactOffset;
                float stepOffset;
                float height;
                float radius;

                Physics::SDKObjectHandle outController;
                Physics::SDKObjectHandle outBackingBody;
                Physics::SDKObjectHandle outBackingShape;
            };


            struct DestroyCharacterController : Command
            {
                static constexpr auto command = PhysXExtension::DestroyCharacterController;

                Physics::SDKObjectHandle value;
            };

            struct SetCharacterControllerExtents : Command
            {
                static constexpr auto command = PhysXExtension::SetCharacterControllerExtents;

                float height;
                float radius;
            };

            struct SetCharacterControllerStepOffset : Command
            {
                static constexpr auto command = PhysXExtension::SetCharacterControllerStepOffset;

                float value;
            };

            struct SetCharacterControllerSlopeLimit : Command
            {
                static constexpr auto command = PhysXExtension::SetCharacterControllerSlopeLimit;

                float value;
            };

            struct SetCharacterControllerContactOffset : Command
            {
                static constexpr auto command = PhysXExtension::SetCharacterControllerContactOffset;

                float value;
            };

            struct MoveCharacterController : Command
            {
                static constexpr auto command = PhysXExtension::MoveCharacterController;

                float moveDir[3];
                float minMoveDistance;
                float deltaTime;

                uint32_t outTouching;
            };


            struct SetCharacterControllerKinematicTarget : Command
            {
                static constexpr auto command = PhysXExtension::SetCharacterControllerKinematicTarget;

                float position[3];
            };

            struct GetCharacterControllerKinematicTarget : Command
            {
                static constexpr auto command = PhysXExtension::GetCharacterControllerKinematicTarget;

                float outPosition[3];
            };

            using OnFetchCharacterControllerCollisions = void(*)(void* ctx, void* hitsVector);

            struct GetAndClearCharacterControllerCollisions : Command
            {
                static constexpr auto command = PhysXExtension::GetAndClearCharacterControllerCollisions;

                OnFetchCharacterControllerCollisions callback;
                void* ctx;
            };
        }
    }
}
