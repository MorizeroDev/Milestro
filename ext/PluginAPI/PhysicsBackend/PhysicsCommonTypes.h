// Unity Physics Backend System Native Plugin API Data Protocol copyright © 2025 Unity Technologies ApS
//
// Licensed under the Unity Companion License for Unity - dependent projects--see[Unity Companion License](http://www.unity3d.com/legal/licenses/Unity_Companion_License).
//
// Unless expressly provided otherwise, the Software under this license is made available strictly on an “AS IS” BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.Please review the license for details on these and other terms and conditions.
//
// Please note that the following data protocol types represent the data used internally by Unity's Physics Module in order to communicate with the current SDK Integration. As such these types are subject to rapid change between different Unity versions.

#pragma  once
#include <cstdint>
#include <type_traits>

namespace Physics
{
    using SDKObjectHandle = void*;

    //Mirror to UnityEngine's EntityId internal type
    struct EngineObjectId
    {
        constexpr EngineObjectId() = default;

        uint32_t data = 0;

        template<typename T>
        inline T& Reinterpret()
        {
            static_assert(sizeof(T) == sizeof(EngineObjectId), "Target type needs to have the same size (4 byte) as the EngineObjectId type");
            return *reinterpret_cast<T*>(this);
        }

        inline EngineObjectId FromSDKObjectUserData(void* obj, size_t IdOffsetInObject)
        {
            return *reinterpret_cast<EngineObjectId*>(static_cast<uint8_t*>(obj) + IdOffsetInObject);
        }
    };

    inline constexpr EngineObjectId kEngineObjectId_None{};

    enum class FeatureSupport
    {
        None = 0,
        FallbackBit = 1 << 0, // Do not set this bit, it is reserved for Unity's internal fallback integration
        DynamicsSupport = 1 << 1, // Support for rigidbody, colliders, joints, queries, mesh cooking -- mandatory
        SDKVisualDebuggerSupport = 1 << 2, // Support for PhysicsDebugger tool -- optional
        ArticulationSupport = 1 << 3, //support for ArticulationBody component -- optional 
        PhysXImmediateModeSupport = 1 << 4, //support for ImmediateMode and GeometryHolder APIs which use PhysX's binary data. -- optional
        PhysXVehicleSupport = 1 << 5, //support for WheelCollider component, which uses PhysX vehicle SDK. -- optional
        PhysXCharacterControllerSupport = 1 << 6 //support for CharacterController component, which uses PxCapsuleCharacterController from the PhysX SDK. -- optional
    };
  
    inline constexpr FeatureSupport operator | (FeatureSupport lhs, FeatureSupport rhs)
    {
        using type_t = std::underlying_type_t<FeatureSupport>;
        return static_cast<FeatureSupport>(static_cast<type_t>(lhs) | static_cast<type_t>(rhs));
    }

    inline constexpr FeatureSupport& operator |= (FeatureSupport& lhs, FeatureSupport rhs)
    {
        lhs = lhs | rhs;
        return lhs;
    }

    inline constexpr FeatureSupport operator ~(FeatureSupport v)
    {
        using type_t = std::underlying_type_t<FeatureSupport>;

        return static_cast<FeatureSupport>(~static_cast<type_t>(v));
    }

    inline constexpr FeatureSupport operator &(FeatureSupport lhs, FeatureSupport rhs)
    {
        using type_t = std::underlying_type_t<FeatureSupport>;
        return static_cast<FeatureSupport>(static_cast<type_t>(lhs) & static_cast<type_t>(rhs));
    }

    inline constexpr FeatureSupport operator &=(FeatureSupport& lhs, FeatureSupport rhs)
    {
        lhs = lhs & rhs;
        return lhs;
    }

    struct IntegrationInfo
    {
        static constexpr uint32_t invalidID = 0;

        uint32_t id = invalidID;
        uint16_t version[3] = { 0,0,0 };
        uint16_t sdkVersion[3] = { 0,0,0 };
        FeatureSupport features = FeatureSupport::None;
        char name[16] = { "Invalid" };
        char desc[220] = { "Invalid" };
    };

    enum class ErrorVerbosityLevel
    {
        Disabled, // All errors and warnings are disabled.
        Minimal, // Critical and invalid operation errors are reported, warnings are not reported.
        Default, // Critical, invalid operation and argument errors are reported, warnings are not reported.
        Verbose, // Critical, invalid operation and argument errors are reported, warnings are also reported.
        Debug  // All errors and warnings including debug info messages are reported.
    };
}
