// Unity Physics Backend System Native Plugin API Data Protocol copyright © 2025 Unity Technologies ApS
//
// Licensed under the Unity Companion License for Unity - dependent projects--see[Unity Companion License](http://www.unity3d.com/legal/licenses/Unity_Companion_License).
//
// Unless expressly provided otherwise, the Software under this license is made available strictly on an “AS IS” BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.Please review the license for details on these and other terms and conditions.
//
// Please note that the following data protocol types represent the data used internally by Unity's Physics Module in order to communicate with the current SDK Integration. As such these types are subject to rapid change between different Unity versions.

#pragma once
#include "PhysicsCommands.h"

namespace Physics
{
    enum class MaterialCombineMode
    {
        Average = 0,
        Multiply,
        Minimum,
        Maximum,
    };
}

namespace PhysicsCommands
{
    namespace MaterialData
    {
        struct CreateMaterial : Command
        {
            static constexpr auto command = SDK::CreateMaterial;
            void* userData;
            void* outMaterial;
        };

        struct ReleaseMaterial : Command
        {
            static constexpr auto command = SDK::ReleaseMaterial;
        };

        struct GetDefaultMaterial : Command
        {
            static constexpr auto command = SDK::GetDefaultMaterial;
            void* value;
        };

        struct SetImprovedPatchFrictionEnabledGlobal : Command
        {
            static constexpr auto command = SDK::SetImprovedPatchFrictionEnabledGlobal;
            bool value;
        };

        struct SetMaterialConfiguration : Command
        {
            static constexpr auto command = SDK::SetMaterialConfiguration;

            float dynamicFriction;
            float staticFriction;
            float bounciness;
        };

        struct GetMaterialConfiguration : Command
        {
            static constexpr auto command = SDK::GetMaterialConfiguration;

            float dynamicFriction;
            float staticFriction;
            float bounciness;
        };

        struct SetMaterialCombineModes : Command
        {
            static constexpr auto command = SDK::SetMaterialCombineModes;
            Physics::MaterialCombineMode friction;
            Physics::MaterialCombineMode bounciness;
        };

        struct GetMaterialCombineModes : Command
        {
            static constexpr auto command = SDK::GetMaterialCombineModes;
            Physics::MaterialCombineMode friction;
            Physics::MaterialCombineMode bounciness;
        };
    }
}
