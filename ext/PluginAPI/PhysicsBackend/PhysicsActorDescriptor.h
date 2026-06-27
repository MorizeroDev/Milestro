// Unity Physics Backend System Native Plugin API Data Protocol copyright © 2025 Unity Technologies ApS
//
// Licensed under the Unity Companion License for Unity - dependent projects--see[Unity Companion License](http://www.unity3d.com/legal/licenses/Unity_Companion_License).
//
// Unless expressly provided otherwise, the Software under this license is made available strictly on an “AS IS” BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.Please review the license for details on these and other terms and conditions.
//
// Please note that the following data protocol types represent the data used internally by Unity's Physics Module in order to communicate with the current SDK Integration. As such these types are subject to rapid change between different Unity versions.

#pragma once
#include "PhysicsCommonTypes.h"

namespace Physics
{
    enum class ActorType : uint16_t
    {
        Invalid,
        Static,
        Dynamic,
        Link
    };

    struct ActorDescriptor final
    {
        SDKObjectHandle actor;
        void* userData = NULL;
        ActorType type = ActorType::Invalid;

        inline bool operator== (const ActorDescriptor& other) const
        {
            return actor == other.actor
                && userData == other.userData
                && type == other.type;
        }

        bool operator!= (const ActorDescriptor& other) const
        {
            return !operator==(other);
        }
    };
}
