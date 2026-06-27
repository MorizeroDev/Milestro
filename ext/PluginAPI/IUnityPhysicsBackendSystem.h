// Unity Physics Backend System Native Plugin API copyright © 2025 Unity Technologies ApS
//
// Licensed under the Unity Companion License for Unity - dependent projects--see[Unity Companion License](http://www.unity3d.com/legal/licenses/Unity_Companion_License).
//
// Unless expressly provided otherwise, the Software under this license is made available strictly on an “AS IS” BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.Please review the license for details on these and other terms and conditions.

#pragma once
#include "IUnityInterface.h"

namespace PhysicsCommands
{
    typedef struct Context UnityPhysicsContext;
    typedef struct Command UnityPhysicsCommand;
}

typedef void (*BackendEntryPoint)(void* sdkObject, const PhysicsCommands::Context& ctx, const uint32_t cmdFunc, PhysicsCommands::Command& c);

UNITY_DECLARE_INTERFACE(IUnityPhysicsBackendSystem)
{
    bool(UNITY_INTERFACE_API * Register)(BackendEntryPoint entryPoint);
};
UNITY_REGISTER_INTERFACE_GUID(0xED2B16A043DE39F8ULL, 0x924E3DAB294216EAULL, IUnityPhysicsBackendSystem)
