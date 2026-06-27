// Unity Physics Backend System Native Plugin API Data Protocol copyright © 2025 Unity Technologies ApS
//
// Licensed under the Unity Companion License for Unity - dependent projects--see[Unity Companion License](http://www.unity3d.com/legal/licenses/Unity_Companion_License).
//
// Unless expressly provided otherwise, the Software under this license is made available strictly on an “AS IS” BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.Please review the license for details on these and other terms and conditions.
//
// Please note that the following data protocol types represent the data used internally by Unity's Physics Module in order to communicate with the current SDK Integration. As such these types are subject to rapid change between different Unity versions.
#pragma once
#include "PhysicsCommands.h"

namespace PhysicsCommands
{
    namespace PhysXExt
    {
        constexpr uint32_t kPhysXExtensionMask = kInternalReservedMask0;

        enum class PhysXExtension : uint32_t
        {
            Invalid,

            // Scene
            RebuildSceneMBPRegions,

            // Character controller
            SetCharacterControllerMgrOverlapRecovery,
            CreateCharacterController,
            DestroyCharacterController,
            SetCharacterControllerExtents,
            SetCharacterControllerStepOffset,
            SetCharacterControllerSlopeLimit,
            SetCharacterControllerContactOffset,
            MoveCharacterController,
            SetCharacterControllerKinematicTarget,
            GetCharacterControllerKinematicTarget,
            GetAndClearCharacterControllerCollisions,

            // Vehicle
            CreateVehicle,
            DestroyVehicle,
            SetVehicleWheelEnabled,
            GetFirstDisabledWheelFromVehicle,
            SetVehicleHasImplicitSprungMasses,
            GetVehicleHasImplicitSprungMasses,
            GetVehicleWheelQueryResult,
            SetVehicleWheelFilterData,
            GetVehicleWheelFilterData,
            GetVehicleWheelCount,
            GetVehicleWheelsUserData,
            ResetVehicleQueryResultsForShape,
            SetVehicleLongitudinalSpeedSubSteps,
            SetVehicleWheelComOffset,
            GetVehicleWheelComOffset,
            SetVehicleSuspensionExpansionLimitEnabled,
            GetVehicleSuspensionExpansionLimitEnabled,
            SetVehicleWheelSuspensionData,
            GetVehicleWheelSuspensionData,
            SetVehicleWheelData,
            GetVehicleWheelData,
            GetVehicleWheelLocalPose,
            SetVehicleWheelRotationSpeed,
            GetVehicleWheelRotationSpeed,
            SetVehicleWheelMotorTorque,
            GetVehicleWheelMotorTorque,
            SetVehicleWheelBrakeTorque,
            GetVehicleWheelBrakeTorque,
            SetVehicleWheelSteerAngle,
            GetVehicleWheelSteerAngle,
            CalculateVehicleSprungMasses,

            // Immediate mode x Contact modification
            GetShapeGeometryHolder,
            GetShapeUserData,
            GetRigidActorUserData,
            GetRigidActorLinearVelocity,
            GetRigidActorAngularVelocity,
            SimulateImmediateGenerateContacts,
            Count
        };

        constexpr uint32_t ETP(PhysXExtension f) { return static_cast<uint32_t>(f) | kPhysXExtensionMask; }
    }
}
