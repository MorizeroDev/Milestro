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

#include "PhysicsCommands.h"
#include "PhysicsJointTypes.h"
#include "PhysicsActorDescriptor.h"
#include "PhysicsPose.h"

namespace PhysicsCommands
{
    namespace JointData
    {
        struct SetFlags : Command
        {
            static constexpr auto command = Joint::SetFlags;

            Physics::JointFlags value;
            bool enabled;
        };

        struct GetFlags : Command
        {
            static constexpr auto command = Joint::GetFlags;

            Physics::JointFlags value;
        };

        struct GetUserData : Command
        {
            static constexpr auto command = Joint::GetUserData;

            void* value;
        };

        struct SetActorLocalPose : Command
        {
            static constexpr auto command = Joint::SetActorLocalPose;

            Physics::PoseTarget target;
            Physics::Pose value;
        };

        struct GetActorLocalPose : Command
        {
            static constexpr auto command = Joint::GetActorLocalPose;

            Physics::PoseTarget target;
            Physics::Pose value;
        };

        struct SetBreakForce : Command
        {
            static constexpr auto command = Joint::SetBreakForce;

            float force;
            float torque;
        };

        struct GetIsConstraintValid : Command
        {
            static constexpr auto command = Joint::GetIsConstraintValid;

            bool value;
        };

        struct GetForces : Command
        {
            static constexpr auto command = Joint::GetForces;

            float linear[3];
            float angular[3];
        };

        struct SetInvMassAndInertiaScale : Command
        {
            static constexpr auto command = Joint::SetInvMassAndInertiaScale;

            Physics::PoseTarget target;
            float value;
        };

        struct SetActors : Command
        {
            static constexpr auto command = Joint::SetActors;

            Physics::ActorDescriptor actors[2];
        };

        struct GetActors : Command
        {
            static constexpr auto command = Joint::GetActors;

            Physics::ActorDescriptor actors[2];
        };

        struct WakeUpActors : Command
        {
            static constexpr auto command = Joint::WakeUpActors;
        };

        struct Set6DofSpringLimit : Command
        {
            static constexpr auto command = Joint::Set6DofSpringLimit;

            Physics::ConstraintAxis axis;
            float spring;
            float damping;
        };

        struct Set6DofLimit : Command
        {
            static constexpr auto command = Joint::Set6DofLimit;

            Physics::ConstraintAxis axis;
            float min;
            float max;
            float bounciness;
            float bounceMinVelocity;
            float contactDistance;
        };

        struct Get6DofLimit : Command
        {
            static constexpr auto command = Joint::Get6DofLimit;

            Physics::ConstraintAxis axis;
            float min;
            float max;
            float bounciness;
            float bounceMinVelocity;
            float contactDistance;
        };

        struct Set6DofAxisLock : Command
        {
            static constexpr auto command = Joint::Set6DofAxisLock;

            Physics::ConstraintAxis axis;
            Physics::JointDofLock value;
        };

        struct Set6DofAxisMotorConfiguration : Command
        {
            static constexpr auto command = Joint::Set6DofAxisMotorConfiguration;

            Physics::ConstraintAxis axis;
            float positionSpring;
            float positionDamper;
            float maximumForce;
            int usesAcceleration;
        };

        struct Set6DofMotorTargetPosition : Command
        {
            static constexpr auto command = Joint::Set6DofMotorTargetPosition;

            float value[3];
        };

        struct Set6DofMotorTargetRotation : Command
        {
            static constexpr auto command = Joint::Set6DofMotorTargetRotation;

            float value[4];
        };

        struct Set6DofMotorTargetLinearVelocity : Command
        {
            static constexpr auto command = Joint::Set6DofMotorTargetLinearVelocity;

            float value[3];
        };

        struct Set6DofMotorTargetAngularVelocity : Command
        {
            static constexpr auto command = Joint::Set6DofMotorTargetAngularVelocity;

            float value[3];
        };

        struct Set6DofProjectionTolerance : Command
        {
            static constexpr auto command = Joint::Set6DofProjectionTolerance;

            float linear;
            float angular;
        };

        struct SetDistanceLimit : Command
        {
            static constexpr auto command = Joint::SetDistanceLimit;

            float min;
            float max;
        };

        struct SetDistanceSpringLimit : Command
        {
            static constexpr auto command = Joint::SetDistanceSpringLimit;

            float spring;
            float damping;
        };

        struct SetDistanceErrorTolerance : Command
        {
            static constexpr auto command = Joint::SetDistanceErrorTolerance;

            float value;
        };

        struct SetHingeLimitEnabled : Command
        {
            static constexpr auto command = Joint::SetHingeLimitEnabled;
            bool value;
        };

        struct SetHingeMotorConfiguration : Command
        {
            static constexpr auto command = Joint::SetHingeMotorConfiguration;

            float maxForce;
            // Instructs the motor to not add any force if the current velocity is greater than the target velocity
            bool freeSpin; 
        };

        struct SetHingeMotorTargetVelocity : Command
        {
            static constexpr auto command = Joint::SetHingeMotorTargetVelocity;

            float value;
        };

        struct SetHingeLimit : Command
        {
            static constexpr auto command = Joint::SetHingeLimit;

            float min;
            float max;
            float bounciness;
            float bounceMinVelocity;
            float contactDistance;
        };
    }
}
