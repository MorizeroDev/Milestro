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
#include "PhysicsCommonTypes.h"

namespace PhysicsCommands
{
    namespace SdkData
    {
        struct InitializeSdk : Command
        {
            static constexpr auto command = SDK::InitializeSdk;

            uint16_t engineObjectIdOffset = 0;

            bool vehicleExtensionModulePresent = false;

            bool result = false;
        };

        struct ShutdownSdk : Command
        {
            static constexpr auto command = SDK::ShutdownSdk;
        };

        struct GetSdkIntegrationInfo : Command
        {
            static constexpr auto command = SDK::GetIntegrationInfo;
            Physics::IntegrationInfo value;
        };

        struct SetErrorVerbosityLevel : Command
        {
            static constexpr auto command = SDK::SetErrorVerbosityLevel;
            Physics::ErrorVerbosityLevel value;
        };

        struct ConnectVisualDebugger : Command
        {
            static constexpr auto command = SDK::ConnectVisualDebugger;

            const char* addr = NULL;
            int port = -1;
            int timeoutInMs = 15;

            bool result = false;
        };

        struct DisconnectVisualDebugger : Command
        {
            static constexpr auto command = SDK::DisconnectVisualDebugger;
        };

        struct GetIsVisualDebuggerConnected : Command
        {
            static constexpr auto command = SDK::GetIsVisualDebuggerConnected;

            bool value;
        };

        struct SetVisualDebuggerViewport : Command
        {
            static constexpr auto command = SDK::SetVisualDebuggerViewport;

            float position[3];
            float up[3];
            float target[3];
        };
    }
}
