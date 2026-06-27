// Unity Physics Backend System Native Plugin API Data Protocol copyright © 2025 Unity Technologies ApS
//
// Licensed under the Unity Companion License for Unity - dependent projects--see[Unity Companion License](http://www.unity3d.com/legal/licenses/Unity_Companion_License).
//
// Unless expressly provided otherwise, the Software under this license is made available strictly on an “AS IS” BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.Please review the license for details on these and other terms and conditions.
//
// Please note that the following data protocol types represent the data used internally by Unity's Physics Module in order to communicate with the current SDK Integration. As such these types are subject to rapid change between different Unity versions.

#pragma once
#include <cstdint>
#include "PhysicsCommonTypes.h"
#include "PhysicsShapeTypes.h"

namespace Physics
{
    struct QueryHit;
    using QueryHitResultCollectorCallback = bool (*)(void* context, QueryHit* hits, const size_t count);
    using QueryShapesResultCollectorCallback = bool (*)(void* context, void** shapeUserDatas, const size_t count);
    using BroadPhaseQueryShapeCollectorCallback = bool (*)(void* context, SDKObjectHandle shape, SDKObjectHandle body, void* shapeUserDataPtr, GeometryType shapeType);

    struct QueryHit
    {
        float point[3];
        float normal[3];
        uint32_t faceID;
        float distance;
        float uv[2];
        EngineObjectId userData;

        static constexpr QueryHit Invalid() { return { {0,0,0}, {0,0,0}, 0, 0.0f,{0.0f, 0.0f}, kEngineObjectId_None }; }
    };

    enum class QueryFilterFlags
    {
        None = 0,
        UseDynamicBodies = 1 << 0,
        UseStaticBodies = 1 << 1,
        UseTriggerShapes = 1 << 2,
        AlwaysReportTerrainMeshHoleHits = 1 << 3,
        AllowTriangleMeshBackfaceHits = 1 << 4,
        AllowMultipleTriangleMeshHits = 1 << 5,
        AllowEarlyOutOnFirstHit = 1 << 6
    };

    inline constexpr QueryFilterFlags operator | (QueryFilterFlags lhs, QueryFilterFlags rhs)
    {
        using type_t = std::underlying_type_t<QueryFilterFlags>;
        return static_cast<QueryFilterFlags>(static_cast<type_t>(lhs) | static_cast<type_t>(rhs));
    }

    inline constexpr QueryFilterFlags& operator |= (QueryFilterFlags& lhs, QueryFilterFlags rhs)
    {
        lhs = lhs | rhs;
        return lhs;
    }

    inline constexpr QueryFilterFlags operator ~(QueryFilterFlags v)
    {
        using type_t = std::underlying_type_t<QueryFilterFlags>;

        return static_cast<QueryFilterFlags>(~static_cast<type_t>(v));
    }

    inline constexpr QueryFilterFlags operator &(QueryFilterFlags lhs, QueryFilterFlags rhs)
    {
        using type_t = std::underlying_type_t<QueryFilterFlags>;
        return static_cast<QueryFilterFlags>(static_cast<type_t>(lhs) & static_cast<type_t>(rhs));
    }

    inline constexpr QueryFilterFlags operator &=(QueryFilterFlags& lhs, QueryFilterFlags rhs)
    {
        lhs = lhs & rhs;
        return lhs;
    }

    struct QueryFilterData
    {
        QueryFilterFlags options;
        int mask;
    };
}
