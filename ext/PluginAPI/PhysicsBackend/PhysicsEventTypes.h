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
    struct ContactPoint
    {
        float position[3];
        float separation;
        float normal[3];
        uint32_t faceIndex0;
        float impulse[3];
        uint32_t faceIndex1;
    };

    enum class BodyPairFlags : uint16_t
    {
        Invalid = 0,
        RemovedActor = 1 << 0,
        RemovedOtherActor = 1 << 1
    };

    enum class ShapePairFlags : uint16_t
    {
        Invalid = 0,
        RemovedShape = 1 << 0,
        RemovedOtherShape = 1 << 1,
        ActorPairHasFirstTouch = 1 << 2,
        ActorPairLostTouch = 1 << 3,
        InternalHasImpulses = 1 << 4,
        InternalContactAreFlipped = 1 << 5
    };

    enum class ShapePairEventFlags : uint16_t // Size is important!
    {
        None = 0,
        SolveContact = 1 << 0,
        ModifyContacts = 1 << 1,
        NotifyTouchFound = 1 << 2,
        NotifyTouchPersists = 1 << 3,
        NotifyTouchLost = 1 << 4,
        NotifyTouchCCD = 1 << 5,
        NotifyThresholdForceFound = 1 << 6,
        NotifyThresholdForcePersists = 1 << 7,
        NotifyThresholdForceLost = 1 << 8,
        NotifyContactPoints = 1 << 9,
        DetectDiscreteContact = 1 << 10,
        DetectCCDContact = 1 << 11,
        PreSolverVelocity = 1 << 12,
        PostSolvedVelocity = 1 << 13,
        ContactEventPose = 1 << 14,
        NextFree = 1 << 15,
        ContactDefault = SolveContact | DetectDiscreteContact,
        TriggerDefault = NotifyTouchFound | NotifyTouchLost | DetectDiscreteContact
    };

    inline constexpr ShapePairEventFlags operator & (ShapePairEventFlags lhs, ShapePairEventFlags rhs)
    {
        using type_t = std::underlying_type_t<ShapePairEventFlags>;
        return static_cast<ShapePairEventFlags>(static_cast<type_t>(lhs) & static_cast<type_t>(rhs));
    }

    inline constexpr ShapePairEventFlags operator | (ShapePairEventFlags lhs, ShapePairEventFlags rhs)
    {
        using type_t = std::underlying_type_t<ShapePairEventFlags>;
        return static_cast<ShapePairEventFlags>(static_cast<type_t>(lhs) | static_cast<type_t>(rhs));
    }

    inline constexpr ShapePairEventFlags& operator |= (ShapePairEventFlags& lhs, ShapePairEventFlags rhs)
    {
        lhs = lhs | rhs;
        return lhs;
    }

    inline constexpr ShapePairEventFlags operator ~ (ShapePairEventFlags rhs)
    {
        using type_t = std::underlying_type_t<ShapePairEventFlags>;
        return static_cast<ShapePairEventFlags>(~static_cast<type_t>(rhs));
    }

    inline constexpr ShapePairEventFlags& operator &= (ShapePairEventFlags& lhs, ShapePairEventFlags rhs)
    {
        lhs = lhs & rhs;
        return lhs;
    }

    struct ShapePair
    {
        EntityId thisColliderID;
        EntityId otherColliderID;
        ContactPoint* startPtr;
        uint32_t nbPoints;
        ShapePairFlags flags;
        ShapePairEventFlags events;
        float impulseSum[3];
    };

    struct BodyPair
    {
        EntityId thisBodyID;
        EntityId otherBodyID;
        ShapePair* startPtr;
        uint32_t nbPairs;
        BodyPairFlags flags;
        float relativeVelocity[3];
    };

    enum class TriggerEventType
    {
        Invalid,
        Enter,
        Exit,
    };

    struct TriggerEvent
    {
        SDKObjectHandle triggerShape;
        SDKObjectHandle colliderShape;
        EntityId trigger;
        EntityId collider;
        TriggerEventType type;

        TriggerEvent(TriggerEventType evt, SDKObjectHandle sdkTrigger, SDKObjectHandle sdkCollider, EntityId triggerID, EntityId colliderID)
            : type(evt)
            , triggerShape(sdkTrigger)
            , colliderShape(sdkCollider)
            , trigger(triggerID)
            , collider(colliderID)
        {}
    };

    struct JointBreakEvent
    {
        EntityId jointID;
        float linearMagnitude;
        float angularMagnitude;
    };
}
