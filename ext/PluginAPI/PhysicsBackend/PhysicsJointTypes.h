// Unity Physics Backend System Native Plugin API Data Protocol copyright © 2025 Unity Technologies ApS
//
// Licensed under the Unity Companion License for Unity - dependent projects--see[Unity Companion License](http://www.unity3d.com/legal/licenses/Unity_Companion_License).
//
// Unless expressly provided otherwise, the Software under this license is made available strictly on an “AS IS” BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.Please review the license for details on these and other terms and conditions.
//
// Please note that the following data protocol types represent the data used internally by Unity's Physics Module in order to communicate with the current SDK Integration. As such these types are subject to rapid change between different Unity versions.

#pragma once

namespace Physics
{
    enum class JointFlags
    {
        None,
        Preprocessing = 1 << 0,
        Projection = 1 << 1,
        CollisionReporting = 1 << 2,
        DriveLimitsAreForces = 1 << 3,
        LegacyLimitRanges = 1 << 4
    };

    inline constexpr JointFlags operator | (JointFlags lhs, JointFlags rhs)
    {
        using type_t = std::underlying_type_t<JointFlags>;
        return static_cast<JointFlags>(static_cast<type_t>(lhs) | static_cast<type_t>(rhs));
    }

    inline constexpr JointFlags& operator |= (JointFlags& lhs, JointFlags rhs)
    {
        lhs = lhs | rhs;
        return lhs;
    }

    inline constexpr JointFlags operator &(JointFlags lhs, JointFlags rhs)
    {
        using type_t = std::underlying_type_t<JointFlags>;
        return static_cast<JointFlags>(static_cast<type_t>(lhs) & static_cast<type_t>(rhs));
    }

    enum class ConstraintAxis
    {
        AngularX,
        AngularY,
        AngularZ,
        AngularYZ,
        LinearX,
        LinearY,
        LinearZ,
        Distance,
        Slerp
    };

    //invalid is in the specific value here as we currently bind this enum to C# for articulations
    //and the order there was done as such
    enum class JointType
    {
        Invalid = 4,
        Fixed = 0,
        Prismatic = 1,
        Hinge = 2,
        BallAndSocket = 3,
        Distance = 5,
        Dof6 = 6
    };

    enum class JointDofLock
    {
        Locked,
        Limited,
        Free
    };

    enum class PoseTarget
    {
        Parent = 0,
        Child = 1
    };
}
