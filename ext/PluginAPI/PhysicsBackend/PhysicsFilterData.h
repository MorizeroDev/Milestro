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
#include <limits.h>
#include "PhysicsEventTypes.h"
#include "PhysicsBodyTypes.h"
#include "PhysicsShapeTypes.h"

namespace Physics
{
    struct FilterData
    {
        FilterData()
            : belongsTo(0)
            , collidesWith(0)
            , pairFlags(ShapePairEventFlags::None)
            , ccdMode(static_cast<uint16_t>(CollisionDetectionMode::Discrete))
            , geomType(GeometryType::Invalid)
            , layerOverridePriority(defaultLayerPrio)
            , hasOverride(0)
            , hasCollisionDisabled(0)
            , isTrigger(0)
            , vehicleId(invalidVehicleID)
            , ignorePairID(invalidIgnorePairID)
        {}

        inline const bool operator==(const FilterData& other) const
        {
            return belongsTo == other.belongsTo
                && collidesWith == other.collidesWith
                && pairFlags == other.pairFlags
                && ccdMode == other.ccdMode
                && geomType == other.geomType
                && layerOverridePriority == other.layerOverridePriority
                && hasOverride == other.hasOverride
                && hasCollisionDisabled == other.hasCollisionDisabled
                && isTrigger == other.isTrigger
                && vehicleId == other.vehicleId
                && ignorePairID == other.ignorePairID;
        }

        static constexpr uint16_t defaultLayerPrio = 64; //expected priority range is -64,64 but we remap it to 0,128
        static constexpr uint16_t invalidIgnorePairID = 0;
        // Offset value to move priority value into positive range of priority 0 to 128
        static constexpr uint16_t layerPriorityOffset = 64;
        static constexpr uint16_t invalidVehicleID = 0xffff;

        // word0  A bit mask describing which layers this object can collide with
        uint32_t collidesWith;

        //word1  A bit mask describing which layers this object belongs to
        uint32_t belongsTo;

        // word2 Combination multiple value flags combined in a 32 bit word size
        ShapePairEventFlags pairFlags;
        uint16_t ccdMode : 2;
        GeometryType geomType : 3;
        uint16_t layerOverridePriority : 7;
        uint16_t hasOverride : 1;
        uint16_t hasCollisionDisabled : 1;
        uint16_t isTrigger : 1;
        uint16_t padding : 1;

        // word3 Stores vehicle ID and ignore pair ID for ignore collider pairs
        // We need to store the vehicleId until we abstract/decide what we will do with wheel collider/vehicle
        uint16_t vehicleId;
        uint16_t ignorePairID;
    };
    static_assert(sizeof(FilterData) == 16, "Physics::FilterData must not exceed 16 byte");
}
