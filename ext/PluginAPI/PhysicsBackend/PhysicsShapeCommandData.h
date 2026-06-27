// Unity Physics Backend System Native Plugin API Data Protocol copyright © 2025 Unity Technologies ApS
//
// Licensed under the Unity Companion License for Unity - dependent projects--see[Unity Companion License](http://www.unity3d.com/legal/licenses/Unity_Companion_License).
//
// Unless expressly provided otherwise, the Software under this license is made available strictly on an “AS IS” BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.Please review the license for details on these and other terms and conditions.
//
// Please note that the following data protocol types represent the data used internally by Unity's Physics Module in order to communicate with the current SDK Integration. As such these types are subject to rapid change between different Unity versions.

#pragma once
#include "PhysicsCommands.h"
#include "PhysicsPose.h"
#include "PhysicsActorDescriptor.h"
#include "PhysicsShapeTypes.h"
#include "PhysicsFilterData.h"
//temporary until we add the query abstraction
#include "PhysicsQueryTypes.h"

namespace PhysicsCommands
{
    namespace ShapeData
    {
        struct GetActorDescriptor : Command
        {
            static constexpr auto command = Shape::GetActorDescriptor;
            Physics::ActorDescriptor value;
        };

        struct GetUserData : Command
        {
            static constexpr auto command = Shape::GetUserData;
            void* value;
        };

        struct GetFlags : Command
        {
            static constexpr auto command = Shape::GetFlags;

            Physics::ShapeFlags flags;
        };

        struct SetFlags : Command
        {
            static constexpr auto command = Shape::SetFlags;

            Physics::ShapeFlags flags;
        };

        struct GetFilterData : Command
        {
            static constexpr auto command = Shape::GetFilterData;

            Physics::FilterData data;
        };

        struct SetFilterData : Command
        {
            static constexpr auto command = Shape::SetFilterData;

            Physics::FilterData data;
        };

        struct SetModifiableContacts : Command
        {
            static constexpr auto command = Shape::SetModifiableContacts;
            bool modifiable;
        };        

        struct SetSupportedMessages : Command
        {
            static constexpr auto command = Shape::SetSupportedMessages;
            Physics::ShapePairEventFlags flags;
            int layer;
        };

        struct SetMaterial : Command
        {
            static constexpr auto command = Shape::SetMaterial;
            void* value;
        };

        struct GetLocalPose : Command
        {
            static constexpr auto command = Shape::GetLocalPose;
            Physics::Pose value;
        };

        struct GetPose : Command
        {
            static constexpr auto command = Shape::GetPose;
            Physics::Pose value;
        };

        struct SetPose : Command
        {
            static constexpr auto command = Shape::SetPose;
            Physics::Pose value;
        };

        struct SetContactOffset : Command
        {
            static constexpr auto command = Shape::SetContactOffset;
            float value;
        };
        
        struct GetGeometryType : Command
        {
            static constexpr auto command = Shape::GetGeometryType;
            Physics::GeometryType value;
        };

        struct ExtractCollisionMeshData : Command
        {
            static constexpr auto command = Shape::ExtractCollisionMeshData;

            void* ctx;
            Physics::CollisionMeshDataExtractionCallback callback;
        };

        struct CreateShape : Command
        {
            static constexpr auto command = Shape::CreateShape;
            Physics::ShapeGeometry geom;
            Physics::SDKObjectHandle materialPtr;
            void* userData;
            Physics::SDKObjectHandle value;
            Physics::ShapeFlags flags;
        };

        struct DestroyShape : Command
        {
            static constexpr auto command = Shape::DestroyShape;
        };

        struct SetGeometry : Command
        {
            static constexpr auto command = Shape::SetGeometry;
            Physics::ShapeGeometry value;
        };

        struct GetGeometry : Command
        {
            static constexpr auto command = Shape::GetGeometry;
            Physics::ShapeGeometry value;
        };

        struct RemapTriangleIndexToSourceIndex : Command
        {
            static constexpr auto command = Shape::RemapTriangleIndexToSourceIndex;

            uint32_t physicsMeshTriangleIndex;
            uint32_t outSourceMeshTriangleIndex;
        };

        struct GetWorldBounds : Command
        {
            static constexpr auto command = Shape::GetWorldBounds;
            float extents[3];
            float center[3];
        };

        struct GetBoundsAtPose : Command
        {
            static constexpr auto command = Shape::GetBoundsAtPose;
            Physics::Pose pose;
            float extents[3];
            float center[3];
        };

        struct ShouldIgnoreCollision : Command
        {
            static constexpr auto command = Shape::ShouldIgnoreCollision;
            uint16_t ignoreId0;
            uint16_t ignoreId1;
            bool value;
        };
       
        struct IgnoreCollision : Command
        {
            static constexpr auto command = Shape::IgnoreCollision;
            void* userData0;
            void* userData1;
            uint16_t ignoreId0;
            uint16_t ignoreId1;
            bool value;
            
        };

        struct CleanupIgnoredColliders : Command
        {
            static constexpr auto command = Shape::CleanupIgnoredColliders;
            const void* colliderPtr;
        };

        //cooking
        struct CookCollisionMesh : Command
        {
            static constexpr auto command = Shape::CookCollisionMesh;

            Physics::CollisionMeshCookingDescriptor descriptor;

            void* dataContext;
            Physics::CollisionMeshReportingCallback onMeshReadyCallback;
        };

        struct CookCollisionMeshStream : Command
        {
            static constexpr auto command = Shape::CookCollisionMeshStream;

            Physics::CollisionMeshCookingDescriptor descriptor;

            void* dataContext;
            Physics::CollisionMeshStreamReportingCallback onStreamReadyCallback;
        };

        struct CreateCollisionMeshFromStream : Command
        {
            static constexpr auto command = Shape::CreateCollisionMeshFromStream;

            uint8_t* stream;
            size_t streamSize;
            bool convex;

            Physics::SDKObjectHandle outCollisionMesh;
        };

        struct CreateHeightField : Command
        {
            static constexpr auto command = Shape::CreateHeightField;

            Physics::HeightFieldDescriptor descriptor;
            void* heightField = nullptr;
        };

        struct UpdateHeightFieldRegion : Command
        {
            static constexpr auto command = Shape::UpdateHeightFieldRegion;

            Physics::HeightFieldDescriptor descriptor;
            void* heightField;
            int xBase;
            int yBase;
            int width;
            int height;
        };

        struct DestroyHeightField : Command
        {
            static constexpr auto command = Shape::DestroyHeightField;

            void* heightField;
        };

        struct DestroyCollisionMesh : Command
        {
            static constexpr auto command = Shape::DestroyCollisionMesh;

            Physics::SDKObjectHandle collisionMesh;
            bool convex;
        };
    }
}
