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
    struct Vec3
    {
        //Default ctor does not initialize data
        Vec3() = default;

        constexpr Vec3(float v)
            : x(v)
            , y(v)
            , z(v)
        {}

        constexpr Vec3(float vx, float vy, float vz)
            : x(vx)
            , y(vy)
            , z(vz)
        {}

        static constexpr Vec3 One() { return Vec3(1.0f, 1.0f, 1.0f); }
        static constexpr Vec3 Zero() { return Vec3(0.0f, 0.0f, 0.0f); }

        template<typename float3Type>
        static inline Vec3 Load(const float3Type& p)
        {
            static_assert(sizeof(float3Type) == sizeof(Vec3), "Type size mismatch.");
            return reinterpret_cast<const Vec3&>(p);
        }

        template<typename float3Type>
        inline void Store(float3Type& p) const
        {
            static_assert(sizeof(float3Type) == sizeof(Vec3), "Type size mismatch.");
            reinterpret_cast<Vec3&>(p) = *this;
        }

        template<typename float3Type>
        inline float3Type& AsRef()
        {
            static_assert(sizeof(float3Type) == sizeof(Vec3), "Type size mismatch.");
            return reinterpret_cast<float3Type&>(*this);
        }

        template<typename float3Type>
        inline const float3Type& AsRef() const
        {
            static_assert(sizeof(float3Type) == sizeof(Vec3), "Type size mismatch.");
            return reinterpret_cast<const float3Type&>(*this);
        }

        float* Data() { return &x; }
        const float* Data() const { return &x; }

        inline bool operator!=(const Vec3& other) const { return x != other.x || y != other.y || z != other.z; }
        inline bool operator ==(const Vec3& other) const { return !operator!=(other); }

        float x;
        float y;
        float z;
    };

    struct Vec4
    {
        //Default ctor does not initialize data
        Vec4() = default;

        constexpr Vec4(float v)
            : x(v)
            , y(v)
            , z(v)
            , w(v)
        {}

        constexpr Vec4(float vx, float vy, float vz, float vw)
            : x(vx)
            , y(vy)
            , z(vz)
            , w(vw)
        {}

        static constexpr Vec4 QuatIdentity() { return Vec4(0.0f, 0.0f, 0.0f, 1.0f); }
        static constexpr Vec4 One() { return Vec4(1.0f, 1.0f, 1.0f, 1.0f); }
        static constexpr Vec4 Zero() { return Vec4(0.0f, 0.0f, 0.0f, 0.0f); }

        //Expects XYZW data layout for the other type 
        template<typename float4Type>
        inline static Vec4 Load(const float4Type& q)
        {
            static_assert(sizeof(float4Type) == sizeof(Vec4), "Type size mismatch.");
            return reinterpret_cast<const Vec4&>(q);
        }


        //Expects XYZW data layout for the other type
        template<typename float4Type>
        inline void Store(float4Type& q) const
        {
            static_assert(sizeof(float4Type) == sizeof(Vec4), "Type size mismatch.");
            reinterpret_cast<Vec4&>(q) = *this;
        }

        template<typename float4Type>
        inline float4Type& AsRef()
        {
            static_assert(sizeof(float4Type) == sizeof(Vec4), "Type size mismatch.");
            return reinterpret_cast<float4Type&>(*this);
        }

        template<typename float4Type>
        inline const float4Type& AsRef() const
        {
            static_assert(sizeof(float4Type) == sizeof(Vec4), "Type size mismatch.");
            return reinterpret_cast<const float4Type&>(*this);
        }

        float* Data() { return &x; }
        const float* Data() const { return &x; }

        inline bool operator!=(const Vec4& other) const { return x != other.x || y != other.y || z != other.z || w != other.w; }
        inline bool operator ==(const Vec4& other) const { return !operator!=(other); }

        float x;
        float y;
        float z;
        float w;
    };
}
