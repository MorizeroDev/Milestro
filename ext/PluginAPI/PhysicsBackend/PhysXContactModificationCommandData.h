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

namespace PhysicsCommands
{
    namespace PhysXExt
    {
        namespace ContactModification
        {
            struct GetShapeUserData : Command
            {
                static constexpr auto command = PhysXExtension::GetShapeUserData;

                void* outUserData;
            };

            struct GetRigidActorUserData : Command
            {
                static constexpr auto command = PhysXExtension::GetRigidActorUserData;

                void* outUserData;
            };

            struct GetRigidActorAngularVelocity : Command
            {
                static constexpr auto command = PhysXExtension::GetRigidActorAngularVelocity;

                float outAngVel[3];
            };

            struct GetRigidActorLinearVelocity : Command
            {
                static constexpr auto command = PhysXExtension::GetRigidActorLinearVelocity;

                float outLinVel[3];
            };
        }
    }
}
