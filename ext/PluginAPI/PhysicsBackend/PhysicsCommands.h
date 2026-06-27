// Unity Physics Backend System Native Plugin API Data Protocol copyright © 2025 Unity Technologies ApS
//
// Licensed under the Unity Companion License for Unity - dependent projects--see[Unity Companion License](http://www.unity3d.com/legal/licenses/Unity_Companion_License).
//
// Unless expressly provided otherwise, the Software under this license is made available strictly on an “AS IS” BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.Please review the license for details on these and other terms and conditions.
//
// Please note that the following data protocol types represent the data used internally by Unity's Physics Module in order to communicate with the current SDK Integration. As such these types are subject to rapid change between different Unity versions.

#pragma once
#include <cstdint>
#include <cstddef>
#include "PhysicsCommonTypes.h"

namespace PhysicsCommands
{
    constexpr uint32_t kMaskOffsetSize = 26;
    constexpr uint32_t kSDKFunctionMask = 0x1 << kMaskOffsetSize;
    constexpr uint32_t kWorldFunctionMask = 0x2 << kMaskOffsetSize;
    constexpr uint32_t kBodyFunctionMask = 0x3 << kMaskOffsetSize;
    constexpr uint32_t kShapeFunctionMask = 0x4 << kMaskOffsetSize;
    constexpr uint32_t kQueryFunctionMask = 0x5 << kMaskOffsetSize;
    constexpr uint32_t kJointFunctionMask = 0x6 << kMaskOffsetSize;
    constexpr uint32_t kArticulationFunctionMask = 0x7 << kMaskOffsetSize;
    constexpr uint32_t kInternalReservedMask0 = 0x8 << kMaskOffsetSize;
    constexpr uint32_t kFullMask = 0xF << kMaskOffsetSize;
    constexpr uint32_t kReverseFullMask = ~kFullMask;

    enum class SDK : uint32_t
    {
        Invalid = 0,
        //integration
        InitializeSdk,
        ShutdownSdk,
        GetIntegrationInfo,
        SetErrorVerbosityLevel,

        //visual debugger
        ConnectVisualDebugger,
        DisconnectVisualDebugger,
        GetIsVisualDebuggerConnected,
        SetVisualDebuggerViewport,

        //material
        CreateMaterial,
        ReleaseMaterial,
        GetDefaultMaterial,
        SetImprovedPatchFrictionEnabledGlobal,
        SetMaterialConfiguration,
        GetMaterialConfiguration,
        SetMaterialCombineModes,
        GetMaterialCombineModes,
        Count
    };

    enum class World : uint32_t
    {
        Invalid = 0,
        CreateWorld,
        DestroyWorld,
        SetGravity,
        GetGravity,
        SetBounceThreshold,
        GetBounceThreshold,
        GetBasicStats,
        SetScratchBufferChunkCount,
        Simulate,
        GetLastSimulationStepResults,
        ReleaseLastSimulationStepData,
        GetLastSimulationStats,
        GetUserData,
        CreateBody,
        AddActorsToScene,
        DestroyBody,        
        CreateArticulation,
        DestroyArticulation,
        CreateJoint,
        DestroyJoint,
        GetArticulationCount,
        GetArticulations,
        GetAllShapesByLayerMask,
        Count
    };

    enum class Body : uint32_t
    {
        Invalid = 0,
        SetFlags,
        GetFlags,
        SetMass,
        GetMass,
        SetDamping,
        GetDamping,
        SetPose,
        GetPose,
        AttachShape,
        DetachShape,
        GetShapeCount,
        GetShapes,
        GetShapesUserData,
        GetUserData,
        SetCollisionDetectionMode,
        AddForce,
        AddTorque,
        SetLinearVelocity,
        GetLinearVelocity,
        SetAngularVelocity,
        GetAngularVelocity,
        SetMaxLinearVelocity,
        GetMaxLinearVelocity,
        SetMaxAngularVelocity,
        GetMaxAngularVelocity,
        SetMaxDepenetrationVelocity,
        GetMaxDepenetrationVelocity,
        SetInertiaTensor,
        GetInertiaTensor,
        SetInertiaTensorRotation,
        GetInertiaTensorRotation,
        SetLocalCenterOfMass,
        GetLocalCenterOfMass,
        GetWorldInertiaTensorMatrix,
        RecomputeMassProperties,
        GetAccumulatedForce,
        GetAccumulatedTorque,
        GetLocalPointVelocity,
        GetWorldPointVelocity,
        GetClosestWorldPointOnBounds,
        SetKinematicTarget,
        GetKinematicTarget,
        GetIsSleeping,
        Sleep,
        WakeUp,
        SetSleepThresholdOverride,
        GetSleepThresholdOverride,
        SetSolverIterationsOverride,
        GetSolverIterationsOverride,
        Count
    };

    enum class Shape : uint32_t
    {
        //shape
        Invalid = 0,
        CreateShape,
        DestroyShape,
        GetActorDescriptor,
        GetUserData,
        GetFlags,
        SetFlags,
        GetFilterData,
        SetFilterData,
        SetModifiableContacts,
        SetSupportedMessages,
        SetMaterial,
        GetLocalPose,
        GetPose,
        SetPose,
        SetContactOffset,
        GetWorldBounds,
        GetBoundsAtPose,
        ShouldIgnoreCollision,
        IgnoreCollision,
        CleanupIgnoredColliders,

        //mesh creation
        CookCollisionMesh,
        CookCollisionMeshStream,
        CreateCollisionMeshFromStream,
        CreateHeightField,
        UpdateHeightFieldRegion,
        DestroyHeightField,
        DestroyCollisionMesh,
        ExtractCollisionMeshData,
            
        //geometry
        GetGeometryType,
        SetGeometry,
        GetGeometry,
        RemapTriangleIndexToSourceIndex,
        Count
    };

    enum class Query : uint32_t
    {
        Invalid = 0,
        ComputeShapePenetration,
        ClosestPointOnShape,
        RayCast,
        RayCastAgainstShape,
        RayCastWithCollector,
        Overlap,
        OverlapWithCollector,
        OverlapWithBroadphaseCollector,
        ShapeCast,
        ShapeCastAgainstShape,
        ShapeCastWithCollector,
        BodyCast,
        BodyCastWithCollector,
        Count
    };

    enum class Joint : uint32_t
    {
        Invalid = 0,
        SetFlags,
        GetFlags,
        GetUserData,
        SetActorLocalPose,
        GetActorLocalPose,
        SetBreakForce,
        GetIsConstraintValid,
        GetForces,
        SetInvMassAndInertiaScale,
        SetActors,
        GetActors,
        WakeUpActors,
        Set6DofLimit,
        Get6DofLimit,
        Set6DofSpringLimit,
        Set6DofAxisLock,
        Set6DofAxisMotorConfiguration,
        Set6DofMotorTargetPosition,
        Set6DofMotorTargetRotation,
        Set6DofMotorTargetLinearVelocity,
        Set6DofMotorTargetAngularVelocity,
        Set6DofProjectionTolerance,
        SetDistanceLimit,
        SetDistanceSpringLimit,
        SetDistanceErrorTolerance,
        SetHingeMotorConfiguration,
        SetHingeMotorTargetVelocity,
        SetHingeLimit,
        SetHingeLimitEnabled,
        Count
    };

    enum class Articulation : uint32_t
    {
        Invalid = 0,

        //articulation link
        GetLinkIndex,
        GetLinkJoint,
        SetLinkFlags,
        GetLinkFlags,
        SetLinkMass,
        GetLinkMass,
        SetLinkDamping,
        GetLinkDamping,
        SetLinkPose,
        GetLinkPose,
        AttachShapeToLink,
        DetachShapeFromLink,
        GetLinkShapeCount,
        GetLinkShapesUserData,
        GetLinkUserData,
        SetLinkCollisionDetectionMode,
        AddForceToLink,
        AddTorqueToLink,
        SetLinkLinearVelocity,
        GetLinkLinearVelocity,
        SetLinkAngularVelocity,
        GetLinkAngularVelocity,
        SetLinkMaxLinearVelocity,
        GetLinkMaxLinearVelocity,
        SetLinkMaxAngularVelocity,
        GetLinkMaxAngularVelocity,
        SetLinkMaxDepenetrationVelocity,
        GetLinkMaxDepenetrationVelocity,
        SetLinkInertiaTensor,
        GetLinkInertiaTensor,
        SetLinkInertiaTensorRotation,
        GetLinkInertiaTensorRotation,
        SetLinkLocalCenterOfMass,
        GetLinkLocalCenterOfMass,
        GetLinkWorldInertiaTensorMatrix,
        RecomputeLinkMassProperties,
        GetLinkAccumulatedForce,
        GetLinkAccumulatedTorque,
        GetLinkLocalPointVelocity,
        GetLinkWorldPointVelocity,
        GetLinkClosestWorldPoint,

        //articulation joint
        SetLinkJointType,
        GetLinkJointType,
        SetLinkJointAxisLock,
        SetLinkJointLimit,
        SetLinkJointActorLocalPose,
        SetLinkJointAxisMotorConfiguration,
        SetLinkJointFriction,
        SetLinkJointPosition,
        GetLinkJointPosition,
        SetLinkJointVelocity,
        GetLinkJointVelocity,
        SetLinkJointAcceleration,
        GetLinkJointAcceleration,
        SetLinkJointForce,
        GetLinkJointForce,
        GetLinkJointMotorForce,
        GetLinkJointForcesForAcceleration,
        SetLinkJointMaxVelocity,
        GetLinkJointMaxVelocity,
        GetLinkJointDofCount,

        //articulation
        CreateLink,
        DestroyLink,
        GetLinkCount,
        GetLinks,
        GetLinksUserData,
        GetLinkParent,
        GetLinkParentUserData,
        GetLinkChildCount,
        GetLinkChildrenUserData,
        SetFlags,
        GetFlags,
        GetUserData,
        GetIsSleeping,
        Sleep,
        WakeUp,
        SetSleepThresholdOverride,
        GetSleepThresholdOverride,
        SetSolverIterationsOverride,
        GetSolverIterationsOverride,
        GetDenseJacobianAsFloatBuffer,
        SetJointsPositions,
        GetJointsPositions,
        SetJointsVelocities,
        GetJointsVelocities,
        SetJointsAccelerations,
        GetJointsAccelerations,
        SetJointsForces,
        GetJointsForces,
        GetJointsDriveForces,
        GetJointsGravityForces,
        GetJointsCoriolisCentrifugalForces,
        GetJointsExternalForces,
        ReleaseDataBuffer,
        Count
    };

    constexpr uint32_t ETP(SDK f) { return static_cast<uint32_t>(f) | kSDKFunctionMask; }
    constexpr uint32_t ETP(World f) { return static_cast<uint32_t>(f) | kWorldFunctionMask; }
    constexpr uint32_t ETP(Body f) { return static_cast<uint32_t>(f) | kBodyFunctionMask; }
    constexpr uint32_t ETP(Shape f) { return static_cast<uint32_t>(f) | kShapeFunctionMask; }
    constexpr uint32_t ETP(Query f) { return static_cast<uint32_t>(f) | kQueryFunctionMask; }
    constexpr uint32_t ETP(Joint f) { return static_cast<uint32_t>(f) | kJointFunctionMask; }
    constexpr uint32_t ETP(Articulation f) { return static_cast<uint32_t>(f) | kArticulationFunctionMask; }

    struct Command {};

    struct Context
    {
        constexpr Context() = default;
        Physics::SDKObjectHandle world = NULL;
        Physics::SDKObjectHandle body = NULL;
    };

    using ProcessCommandFunc = void (*)(Physics::SDKObjectHandle sdkObject, const Context& ctx, const uint32_t cmdFunc, Command& c);
    using FunctionBinding = void (*)(Physics::SDKObjectHandle sdkObject, const Context& ctx, Command& c);
}
