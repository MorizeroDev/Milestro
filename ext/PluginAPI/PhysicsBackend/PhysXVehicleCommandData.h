// Unity Physics Backend System Native Plugin API Data Protocol copyright © 2025 Unity Technologies ApS
//
// Licensed under the Unity Companion License for Unity - dependent projects--see[Unity Companion License](http://www.unity3d.com/legal/licenses/Unity_Companion_License).
//
// Unless expressly provided otherwise, the Software under this license is made available strictly on an “AS IS” BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.Please review the license for details on these and other terms and conditions.
//
// Please note that the following data protocol types represent the data used internally by Unity's Physics Module in order to communicate with the current SDK Integration. As such these types are subject to rapid change between different Unity versions.

#pragma once
#include "PhysXPhysicsExtensionCommands.h"
#include "PhysXExtensionTypes.h"

#include "Modules/Physics/CommandLayer/PhysicsFilterData.h"
#include "Modules/Physics/CommandLayer/PhysicsPose.h"

namespace PhysicsCommands
{
    namespace PhysXExt
    {
        namespace VehicleData
        {
            struct CreateVehicle : Command
            {
                static constexpr auto command = PhysXExtension::CreateVehicle;

                // vehicle to copy tire data from if provided
                uint32_t sourceVehicleId;
                uint32_t wheelCount;

                // userData object in need of update after the vehicle got recreated
                void** wheelUserDatasBuffer;

                uint32_t outWrittenUserDatas;
                uint32_t outVehicleId;
            };

            struct DestroyVehicle : Command
            {
                static constexpr auto command = PhysXExtension::DestroyVehicle;

                uint32_t vehicleId;
            };

            struct SetVehicleWheelEnabled : Command
            {
                static constexpr auto command = PhysXExtension::SetVehicleWheelEnabled;

                Physics::PhysXExt::WheelFrictionCurve* wheelFrictionData;
                void* wheelUserData;
                uint32_t wheelId;
                bool enabled;
                bool outAllWheelsDisabled;
            };

            struct GetFirstDisabledWheelFromVehicle : Command
            {
                static constexpr auto command = PhysXExtension::GetFirstDisabledWheelFromVehicle;

                uint32_t outWheelIndex;
                uint32_t outWheelCount;
            };

            struct SetVehicleHasImplicitSprungMasses : Command
            {
                static constexpr auto command = PhysXExtension::SetVehicleHasImplicitSprungMasses;

                bool hasSprungMasses;
            };

            struct GetVehicleHasImplicitSprungMasses : Command
            {
                static constexpr auto command = PhysXExtension::GetVehicleHasImplicitSprungMasses;

                bool outHasSprungMasses;
            };

            struct GetVehicleWheelQueryResult : Command
            {
                static constexpr auto command = PhysXExtension::GetVehicleWheelQueryResult;

                uint32_t wheelId;

                Physics::PhysXExt::WheelQueryResult outHit;
            };

            struct SetVehicleWheelFilterData : Command
            {
                static constexpr auto command = PhysXExtension::SetVehicleWheelFilterData;

                uint32_t wheelId;

                Physics::FilterData filterData;
            };

            struct GetVehicleWheelFilterData : Command
            {
                static constexpr auto command = PhysXExtension::GetVehicleWheelFilterData;

                uint32_t wheelId;

                Physics::FilterData outFilterData;
            };

            struct GetVehicleWheelCount : Command
            {
                static constexpr auto command = PhysXExtension::GetVehicleWheelCount;

                uint32_t outCount;
            };

            struct GetVehicleWheelsUserData : Command
            {
                static constexpr auto command = PhysXExtension::GetVehicleWheelsUserData;

                void** buffer;
                uint32_t bufferSize;
                uint32_t outWritten;
            };

            struct ResetVehicleQueryResultsForShape : Command
            {
                static constexpr auto command = PhysXExtension::ResetVehicleQueryResultsForShape;

                Physics::SDKObjectHandle shape;
            };
            
            struct SetVehicleLongitudinalSpeedSubSteps : Command
            {
                static constexpr auto command = PhysXExtension::SetVehicleLongitudinalSpeedSubSteps;

                float longitudinalSpeed;
                uint32_t belowSpeedSubsteps;
                uint32_t aboveSpeedSubsteps;
            };

            struct SetVehicleWheelComOffset : Command
            {
                static constexpr auto command = PhysXExtension::SetVehicleWheelComOffset;

                float wheelCentreOffset[3];
                float forceAppPointOffset[3];
                
                uint32_t wheelId;
            };

            struct GetVehicleWheelComOffset : Command
            {
                static constexpr auto command = PhysXExtension::GetVehicleWheelComOffset;

                float outWheelCentreOffset[3];
                float outForceAppPointOffset[3];
                
                uint32_t wheelId;
            };

            struct SetVehicleSuspensionExpansionLimitEnabled : Command
            {
                static constexpr auto command = PhysXExtension::SetVehicleSuspensionExpansionLimitEnabled;

                bool enabled;
            };

            struct GetVehicleSuspensionExpansionLimitEnabled : Command
            {
                static constexpr auto command = PhysXExtension::GetVehicleSuspensionExpansionLimitEnabled;

                bool outEnabled;
            };

            struct SetVehicleWheelSuspensionData : Command
            {
                static constexpr auto command = PhysXExtension::SetVehicleWheelSuspensionData;

                Physics::PhysXExt::WheelSuspensionData suspension;
                uint32_t wheelId;
            };

            struct GetVehicleWheelSuspensionData : Command
            {
                static constexpr auto command = PhysXExtension::GetVehicleWheelSuspensionData;

                Physics::PhysXExt::WheelSuspensionData outSuspension;
                uint32_t wheelId;
            };

            struct SetVehicleWheelData : Command
            {
                static constexpr auto command = PhysXExtension::SetVehicleWheelData;

                Physics::PhysXExt::WheelData wheelData;
                uint32_t wheelId;
            };

            struct GetVehicleWheelData : Command
            {
                static constexpr auto command = PhysXExtension::GetVehicleWheelData;

                Physics::PhysXExt::WheelData outWheelData;
                uint32_t wheelId;
            };

            struct GetVehicleWheelLocalPose : Command
            {
                static constexpr auto command = PhysXExtension::GetVehicleWheelLocalPose;

                Physics::Pose pose;
                uint32_t wheelId;
            };

            struct SetVehicleWheelRotationSpeed : Command
            {
                static constexpr auto command = PhysXExtension::SetVehicleWheelRotationSpeed;

                uint32_t wheelId;
                float speed;
            };

            struct GetVehicleWheelRotationSpeed : Command
            {
                static constexpr auto command = PhysXExtension::GetVehicleWheelRotationSpeed;

                uint32_t wheelId;
                float outSpeed;
            };

            struct SetVehicleWheelMotorTorque : Command
            {
                static constexpr auto command = PhysXExtension::SetVehicleWheelMotorTorque;

                uint32_t wheelId;
                float torque;
            };

            struct GetVehicleWheelMotorTorque : Command
            {
                static constexpr auto command = PhysXExtension::GetVehicleWheelMotorTorque;

                uint32_t wheelId;
                float outTorque;
            };


            struct SetVehicleWheelBrakeTorque : Command
            {
                static constexpr auto command = PhysXExtension::SetVehicleWheelBrakeTorque;

                uint32_t wheelId;
                float brkTorque;
            };

            struct GetVehicleWheelBrakeTorque : Command
            {
                static constexpr auto command = PhysXExtension::GetVehicleWheelBrakeTorque;

                uint32_t wheelId;
                float outBrkTorque;
            };

            struct SetVehicleWheelSteerAngle : Command
            {
                static constexpr auto command = PhysXExtension::SetVehicleWheelSteerAngle;

                uint32_t wheelId;
                float angle;
            };

            struct GetVehicleWheelSteerAngle : Command
            {
                static constexpr auto command = PhysXExtension::GetVehicleWheelSteerAngle;

                uint32_t wheelId;
                float outAngle;
            };

            struct CalculateVehicleSprungMasses : Command
            {
                static constexpr auto command = PhysXExtension::CalculateVehicleSprungMasses;

                uint32_t* wheelIds;
                //array of vector3 provided as float stream
                float* wheelOffsets;
                uint32_t wheelCount;
            };
        }
    }
}
