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
#include "PhysicsVecTypes.h"
#include "PhysicsCommonTypes.h"

namespace Physics
{
    enum class ShapeFlags
    {
        None = 0,
        SceneQuery = 1 << 0,
        Trigger = 1 << 1
    };

    inline constexpr ShapeFlags operator | (ShapeFlags lhs, ShapeFlags rhs)
    {
        using type_t = std::underlying_type_t<ShapeFlags>;
        return static_cast<ShapeFlags>(static_cast<type_t>(lhs) | static_cast<type_t>(rhs));
    }

    inline constexpr ShapeFlags& operator |= (ShapeFlags& lhs, ShapeFlags rhs)
    {
        lhs = lhs | rhs;
        return lhs;
    }

    inline constexpr ShapeFlags operator & (ShapeFlags lhs, ShapeFlags rhs)
    {
        using type_t = std::underlying_type_t<ShapeFlags>;
        return static_cast<ShapeFlags>(static_cast<type_t>(lhs) & static_cast<type_t>(rhs));
    }

    inline constexpr ShapeFlags& operator &= (ShapeFlags& lhs, ShapeFlags rhs)
    {
        lhs = lhs & rhs;
        return lhs;
    }

    inline constexpr ShapeFlags operator ~ (ShapeFlags rhs)
    {
        using type_t = std::underlying_type_t<ShapeFlags>;
        return static_cast<ShapeFlags>(~static_cast<type_t>(rhs));
    }

    enum class GeometryType : uint16_t
    {
        Invalid = 0,
        Sphere = 1,
        Capsule = 3,
        Box = 4,
        ConvexMesh = 5,
        TriangleMesh = 6,
        Terrain = 7
    };

    struct BoxGeometry
    {
        inline const bool operator==(const BoxGeometry& other) const
        {
            return halfExtents == other.halfExtents;
        }

        inline const bool operator!=(const BoxGeometry& other) const
        {
            return halfExtents != other.halfExtents;
        }

        Vec3 halfExtents;
    };

    struct SphereGeometry
    {
        inline const bool operator==(const SphereGeometry& other) const
        {
            return radius == other.radius;
        }

        inline const bool operator!=(const SphereGeometry& other) const
        {
            return radius != other.radius;
        }

        float radius;
    };

    struct CapsuleGeometry
    {
        inline const bool operator==(const CapsuleGeometry& other) const
        {
            return radius == other.radius && height == other.height;
        }

        inline const bool operator!=(const CapsuleGeometry& other) const
        {
            return radius != other.radius || height != other.height;
        }

        float radius;
        float height;
    };

    struct MeshGeometry
    {
        inline const bool operator==(const MeshGeometry& other) const
        {
            return mesh == other.mesh && scale == other.scale;
        }

        inline const bool operator!=(const MeshGeometry& other) const
        {
            return mesh != other.mesh || scale != other.scale;
        }

        void* mesh;
        Vec3 scale;
    };

    struct HeightFieldGeometry
    {
        inline const bool operator==(const HeightFieldGeometry& other) const
        {
            return heightField == other.heightField && scale == other.scale;
        }


        inline const bool operator!=(const HeightFieldGeometry& other) const
        {
            return heightField != other.heightField || scale != other.scale;
        }

        void* heightField;
        Vec3 scale;
    };

    //container to hold the max size to hold every geometry type (24 bytes)
    struct ShapeGeometry
    {
        inline const bool operator==(const ShapeGeometry& other) const
        {
            if (type != other.type)
                return false;

            switch(type)
            {
            case GeometryType::Box:
                return boxGeometry == other.boxGeometry;
            case GeometryType::Capsule:
                return capsuleGeometry == other.capsuleGeometry;
            case GeometryType::ConvexMesh:
            case GeometryType::TriangleMesh:
                return meshGeometry == other.meshGeometry;
            case GeometryType::Sphere:
                return sphereGeometry == other.sphereGeometry;
            case GeometryType::Terrain:
                return heightFieldGeometry == other.heightFieldGeometry;
            default:
                return false;
            }
        }

        inline const bool operator!=(const ShapeGeometry& other) const
        {
            return !operator==(other);
        }

        union
        {
            BoxGeometry boxGeometry;
            SphereGeometry sphereGeometry;
            CapsuleGeometry capsuleGeometry;
            MeshGeometry meshGeometry;
            HeightFieldGeometry heightFieldGeometry;
        };

        GeometryType type;
    };

    struct HeightFieldDescriptor
    {
        const int16_t* heights = nullptr;
        const uint8_t* holes = nullptr;
        int resolution = 0;
        int holesCount = 0;
        int heightsCount = 0;
    };

    enum class CollisionMeshProcessingOptions : uint16_t
    {
        None = 0,
        UseLegacyCookingSystem = 1 << 0,
        OptimizeForRuntimePerformance = 1 << 1,
        CleanupInputMesh = 1 << 2,
        WeldVertices = 1 << 3,
    };

    inline constexpr CollisionMeshProcessingOptions operator | (CollisionMeshProcessingOptions lhs, CollisionMeshProcessingOptions rhs)
    {
        using type_t = std::underlying_type_t<CollisionMeshProcessingOptions>;
        return static_cast<CollisionMeshProcessingOptions>(static_cast<type_t>(lhs) | static_cast<type_t>(rhs));
    }

    inline constexpr CollisionMeshProcessingOptions& operator |= (CollisionMeshProcessingOptions& lhs, CollisionMeshProcessingOptions rhs)
    {
        lhs = lhs | rhs;
        return lhs;
    }

    inline constexpr CollisionMeshProcessingOptions operator & (CollisionMeshProcessingOptions lhs, CollisionMeshProcessingOptions rhs)
    {
        using type_t = std::underlying_type_t<CollisionMeshProcessingOptions>;
        return static_cast<CollisionMeshProcessingOptions>(static_cast<type_t>(lhs) & static_cast<type_t>(rhs));
    }

    struct CollisionMeshCookingDescriptor
    {
        void* vertexStream = NULL;
        void* indexStream = NULL;
        uint32_t vertexElementCount = 0;
        uint32_t indexElementCount = 0;

        Physics::CollisionMeshProcessingOptions options = CollisionMeshProcessingOptions::None;
        uint16_t vertexElementSize = 0;
        uint16_t indexElementSize = 0;
        bool convex = false;
        bool flippedNormals = false;
    };

    enum class CookingError
    {
        Success = 0,
        Failed = 1 << 0,
        Warning = 1 << 1,
    };

    inline constexpr CookingError operator | (CookingError lhs, CookingError rhs)
    {
        using type_t = std::underlying_type_t<CookingError>;
        return static_cast<CookingError>(static_cast<type_t>(lhs) | static_cast<type_t>(rhs));
    }

    inline constexpr CookingError operator & (CookingError lhs, CookingError rhs)
    {
        using type_t = std::underlying_type_t<CookingError>;
        return static_cast<CookingError>(static_cast<type_t>(lhs) & static_cast<type_t>(rhs));
    }

    using CollisionMeshStreamReportingCallback = void (*)(CookingError result, const char* msg, void* context, void* stream, const size_t streamSizeinBytes);
    using CollisionMeshReportingCallback = void (*)(CookingError result, const char* msg, void* context, Physics::SDKObjectHandle mesh);

    struct SDKCollisionMeshData
    {
        const float* vertices;
        size_t vertexCount;
        size_t vertexStride;

        const void* indices;
        size_t indexCount;
        size_t indexStride;
    };

    using CollisionMeshDataExtractionCallback = void (*)(void* context, SDKCollisionMeshData meshData);
}
