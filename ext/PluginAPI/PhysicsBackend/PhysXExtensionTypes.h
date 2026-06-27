// Unity Physics Backend System Native Plugin API Data Protocol copyright © 2025 Unity Technologies ApS
//
// Licensed under the Unity Companion License for Unity - dependent projects--see[Unity Companion License](http://www.unity3d.com/legal/licenses/Unity_Companion_License).
//
// Unless expressly provided otherwise, the Software under this license is made available strictly on an “AS IS” BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.Please review the license for details on these and other terms and conditions.
//
// Please note that the following data protocol types represent the data used internally by Unity's Physics Module in order to communicate with the current SDK Integration. As such these types are subject to rapid change between different Unity versions.
#pragma once
#include <cstdint>

namespace Physics
{
    namespace PhysXExt
    {
        //matches PX_MAX_NB_WHEELS (20) defined in PxVehicleSdk.h 
        constexpr uint32_t kMaxVehicleWheelsCount = 20;

        //default number of vehicles to be allocated in a scene
        constexpr uint32_t kDefaultVehicleStorage = 256;

        //invalid wheel x vehicle ids
        constexpr uint32_t kInvalidVehicleId = 0xffffffff;
        constexpr uint32_t kInvalidWheelId = 0xffffffff;

        // physx::PxGeometryHolder blob data, the blob data members are provided in such as way so that the geometry type of the holder is the only non-opaque piece of data inside
        // the memory layout matches PxConvexMeshGeometry, ensuring we can fit all smaller types inside the holder blob
        //
        // PxTriangleMeshLayout (64bit):
        // [00...03] -- PxGeometryType
        // [04...31] -- PxMeshScale
        // [32...39] -- PxConvexMesh ptr
        // [40...43] -- PxConvexMeshGeometryFlag + 3 byte padding
        // [44...47] -- 4 byte padding
        struct GeometryHolder
        {
            constexpr static int kInvalidType = -1;
            int data[12];

            GeometryHolder() { data[0] = kInvalidType; };
        };

        struct ImmediateTransform { char blob[28]; };     // physx::PxTransform
        struct ImmediateContact { char blob[64]; };     // Gu::ContactPoint

        class WheelFrictionCurve
        {
        private:
            static constexpr float kDefaultForwardCurveExtremumSlip = 0.4f;
            static constexpr float kDefaultForwardCurveExtremumValue = 1.0f;
            static constexpr float kDefaultForwardCurveAsymptoteSlip = 0.8f;
            static constexpr float kDefaultForwardCurveAsymptoteValue = 0.5f;
            static constexpr float kDefaultForwardCurveStiffness = 1.0f;

            static constexpr float kDefaultSidewaysCurveExtremumSlip = 0.2f;
            static constexpr float kDefaultSidewaysCurveExtremumValue = 1.0f;
            static constexpr float kDefaultSidewaysCurveAsymptoteSlip = 0.5f;
            static constexpr float kDefaultSidewaysCurveAsymptoteValue = 0.75f;
            static constexpr float kDefaultSidewaysCurveStiffness = 1.0f;

        public:

            WheelFrictionCurve() : WheelFrictionCurve(true) {}

            //Wheel friction curve direction if 'isForward' is false then the curve is sideways
            WheelFrictionCurve(bool isForward)
            {
                if(isForward)
                {
                    m_ExtremumSlip = kDefaultForwardCurveExtremumSlip;
                    m_ExtremumValue = kDefaultForwardCurveExtremumValue;
                    m_AsymptoteSlip = kDefaultForwardCurveAsymptoteSlip;
                    m_AsymptoteValue = kDefaultForwardCurveAsymptoteValue;
                    m_Stiffness = kDefaultForwardCurveStiffness;
                }
                else
                {
                    m_ExtremumSlip = kDefaultSidewaysCurveExtremumSlip;
                    m_ExtremumValue = kDefaultSidewaysCurveExtremumValue;
                    m_AsymptoteSlip = kDefaultSidewaysCurveAsymptoteSlip;
                    m_AsymptoteValue = kDefaultSidewaysCurveAsymptoteValue;
                    m_Stiffness = kDefaultSidewaysCurveStiffness;
                }
            }

            inline bool IsValid() const
            {
                return m_ExtremumSlip > 0.0f
                    && m_ExtremumSlip < m_AsymptoteSlip
                    && m_ExtremumValue > 0.0f
                    && m_AsymptoteValue > 0.0f
                    && m_Stiffness > 0.0f;
            }

            inline bool operator!=(const WheelFrictionCurve& other) const
            {
                return m_ExtremumSlip != other.m_ExtremumSlip
                    || m_ExtremumValue != other.m_ExtremumValue
                    || m_AsymptoteSlip != other.m_AsymptoteSlip
                    || m_AsymptoteValue != other.m_AsymptoteValue
                    || m_Stiffness != other.m_Stiffness;
            }

            float m_ExtremumSlip; ///<Extremum Slip. range { 0.001, infinity }
            float m_ExtremumValue; ///<Extremum Value. range { 0.001, infinity }
            float m_AsymptoteSlip; ///<Asymptote Slip. range { 0.001, infinity }
            float m_AsymptoteValue; ///<Asymptote Value. range { 0.001, infinity }
            float m_Stiffness;      ///<Stiffness Factor. range { 0, infinity }
        };

        struct WheelQueryResult
        {
            float point[3];
            float normal[3];
            float forwardDir[3];
            float sidewaysDir[3];
            float force;
            float forwardSlip;
            float sidewaysSlip;
            float suspensionCompression;
            float steerAngle;
            void* userData;
        };

        struct WheelSuspensionData
        {
            float springStrength;
            float springDamperRate;
            float maxCompression;
            float maxDroop;
            float sprungMass;
        };

        struct WheelData
        {
            float radius;
            float mass;
            float momentOfInertia;
            float dampingRate;
        };
        
        struct RecordedControllerColliderHit
        {
            void*            collider;
            float            point[3];
            float            normal[3];
            float            motionDirection[3];
            float            motionLength;
        };
    }
}
