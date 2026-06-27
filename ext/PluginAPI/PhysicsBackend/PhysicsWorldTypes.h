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
#include "PhysicsCommonTypes.h"

namespace Physics
{
    enum class SolverType
    {
        ProjectedGaussSeidelSolver = 0,
        TemporalGaussSeidelSolver = 1
    };

    enum class BroadphaseType
    {
        SweepAndPruneBroadphase = 0,
        MultiBoxPruning = 1,
        AutomaticBoxPruning = 2
    };

    enum class FrictionType
    {
        PatchFrictionType = 0,
        OneDirectionalFrictionType = 1,
        TwoDirectionalFrictionType = 2
    };

    enum class ContactPairsMode
    {
        DefaultContactPairs = 0,
        EnableKinematicKinematicPairs = 1 << 0,
        EnableKinematicStaticPairs = 1 << 1,
        EnableAllContactPairs = EnableKinematicKinematicPairs | EnableKinematicStaticPairs
    };

    inline constexpr ContactPairsMode operator | (ContactPairsMode lhs, ContactPairsMode rhs)
    {
        using type_t = std::underlying_type_t<ContactPairsMode>;
        return static_cast<ContactPairsMode>(static_cast<type_t>(lhs) | static_cast<type_t>(rhs));
    }

    inline constexpr ContactPairsMode& operator |= (ContactPairsMode& lhs, ContactPairsMode rhs)
    {
        lhs = lhs | rhs;
        return lhs;
    }

    inline constexpr ContactPairsMode operator ~(ContactPairsMode v)
    {
        using type_t = std::underlying_type_t<ContactPairsMode>;

        return static_cast<ContactPairsMode>(~static_cast<type_t>(v));
    }

    inline constexpr ContactPairsMode operator &(ContactPairsMode lhs, ContactPairsMode rhs)
    {
        using type_t = std::underlying_type_t<ContactPairsMode>;
        return static_cast<ContactPairsMode>(static_cast<type_t>(lhs) & static_cast<type_t>(rhs));
    }

    inline constexpr ContactPairsMode operator &=(ContactPairsMode& lhs, ContactPairsMode rhs)
    {
        lhs = lhs & rhs;
        return lhs;
    }

    using ContactModificationEventCallback = void(*)(void* context, void* sdkSpecificContactBuffer, const size_t contactCount, bool isCCDStream);

    struct WorldStats
    {
        WorldStats() = default;

        uint32_t bodies = 0;
        uint32_t articulations = 0;
        uint32_t constraints = 0;
    };

    struct SimulationStepStats
    {
        SimulationStepStats() = default;

        uint32_t dynamicBodies = 0;
        uint32_t activeDynamicBodies = 0;
        uint32_t kinematicBodies = 0;
        uint32_t activeKinematicBodies = 0;
        uint32_t staticBodies = 0;
        uint32_t articulations = 0;
        uint32_t constraints = 0;
        uint32_t broadPhaseAdds = 0;
        uint32_t broadPhaseRemoves = 0;
        uint32_t newTouches = 0;
        uint32_t lostTouches = 0;
        uint32_t contactPairs = 0;
        uint32_t CCDContactPairs = 0;
        uint32_t modifiableContactPairs = 0;
        uint32_t triggerPairs = 0;
    };
}
