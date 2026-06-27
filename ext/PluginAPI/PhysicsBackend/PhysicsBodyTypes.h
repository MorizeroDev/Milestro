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

namespace Physics
{
    enum class BodyFlags
    {
        None = 0,
        Gravity = 1 << 0,
        Kinematic = 1 << 1,
        Simulated = 1 << 2
    };

    inline constexpr BodyFlags operator | (BodyFlags lhs, BodyFlags rhs)
    {
        using type_t = std::underlying_type_t<BodyFlags>;
        return static_cast<BodyFlags>(static_cast<type_t>(lhs) | static_cast<type_t>(rhs));
    }

    inline constexpr BodyFlags& operator |= (BodyFlags& lhs, BodyFlags rhs)
    {
        lhs = lhs | rhs;
        return lhs;
    }

    inline constexpr BodyFlags operator ~(BodyFlags v)
    {
        using type_t = std::underlying_type_t<BodyFlags>;

        return static_cast<BodyFlags>(~static_cast<type_t>(v));
    }

    inline constexpr BodyFlags operator &(BodyFlags lhs, BodyFlags rhs)
    {
        using type_t = std::underlying_type_t<BodyFlags>;
        return static_cast<BodyFlags>(static_cast<type_t>(lhs) & static_cast<type_t>(rhs));
    }

    inline constexpr BodyFlags operator &=(BodyFlags& lhs, BodyFlags rhs)
    {
        lhs = lhs & rhs;
        return lhs;
    }

    enum class ArticulationFlags
    {
        None = 0,
        DriveLimitsAsForces = 1 << 0,
        ImmovableRoot = 1 << 1,
        Simulated = 1 << 2
    };

    inline constexpr ArticulationFlags operator | (ArticulationFlags lhs, ArticulationFlags rhs)
    {
        using type_t = std::underlying_type_t<ArticulationFlags>;
        return static_cast<ArticulationFlags>(static_cast<type_t>(lhs) | static_cast<type_t>(rhs));
    }

    inline constexpr ArticulationFlags& operator |= (ArticulationFlags& lhs, ArticulationFlags rhs)
    {
        lhs = lhs | rhs;
        return lhs;
    }

    inline constexpr ArticulationFlags operator &(ArticulationFlags lhs, ArticulationFlags rhs)
    {
        using type_t = std::underlying_type_t<ArticulationFlags>;
        return static_cast<ArticulationFlags>(static_cast<type_t>(lhs) & static_cast<type_t>(rhs));
    }

    enum class CollisionDetectionMode
    {
        Discrete = 0,
        Continuous,
        ContinuousDynamic,
        ContinuousSpeculative
    };

    enum class ForceMode
    {
        Force = 0,
        Impulse,
        //VelocityChange is only present in order to allow us to bind this enum type to managed. This value should not be used at integration level for integration always convert to impulse.
        VelocityChange,
        //Acceleration is only present in order to allow us to bind this enum type to managed code. This value should not be used at integration level for integration always convert to force.
        Acceleration = 5
    };

    struct MassPropertiesOverride
    {
        MassPropertiesOverride()
            : inertiaTensorRotation{0,0,0,1}
            , inertiaTensor{1,1,1}
            , centerOfMass{0,0,0}
            , overrideCenterOfMass(false)
            , overrideInertiaTensor(false)
        {}
        float inertiaTensorRotation[4];
        float inertiaTensor[3];
        float centerOfMass[3];
        bool overrideCenterOfMass;
        bool overrideInertiaTensor;
    };
}
