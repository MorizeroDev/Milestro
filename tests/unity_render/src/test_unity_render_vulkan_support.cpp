#include "unity_render/MilestroUnityRenderVulkanLifecycle.h"
#include "unity_render/MilestroUnityRenderVulkanMemorySupport.h"

#include <gtest/gtest.h>

#include <array>
#include <cstdint>
#include <functional>
#include <vector>

namespace {

using milestro::unity_render::vulkan::EnqueueResult;
using milestro::unity_render::vulkan::DeviceTransitionKind;
using milestro::unity_render::vulkan::DeviceTransitionSnapshot;
using milestro::unity_render::vulkan::DeviceTransitionState;
using milestro::unity_render::vulkan::EventInfo;
using milestro::unity_render::vulkan::EpochCancellationMailbox;
using milestro::unity_render::vulkan::FixedEventLifecycle;
using milestro::unity_render::vulkan::IsValidCommandCount;
using milestro::unity_render::vulkan::SubmissionPhase;
using milestro::unity_render::vulkan::ClassifyVulkanHostMemorySupport;
using milestro::unity_render::vulkan::VulkanHostMemorySupport;

constexpr uint32_t kHostVisible = 1U << 0U;
constexpr uint32_t kHostCoherent = 1U << 1U;
constexpr uint32_t kDeviceLocal = 1U << 2U;

struct FakeMemoryType {
    uint32_t propertyFlags = 0;
};

struct FakeMemoryProperties {
    uint32_t memoryTypeCount = 0;
    std::array<FakeMemoryType, 4> memoryTypes{};
};

TEST(UnityRenderVulkanMemorySupportTest, ReportsNonCoherentWhenCoherentTypeIsNotACandidate) {
    FakeMemoryProperties properties;
    properties.memoryTypeCount = 3;
    properties.memoryTypes[0].propertyFlags = kDeviceLocal;
    properties.memoryTypes[1].propertyFlags = kHostVisible;
    properties.memoryTypes[2].propertyFlags = kHostVisible | kHostCoherent;

    constexpr uint32_t candidateBits = (1U << 0U) | (1U << 1U);
    EXPECT_EQ(ClassifyVulkanHostMemorySupport(
                  properties, candidateBits, kHostVisible, kHostCoherent),
              VulkanHostMemorySupport::NonCoherent);
}

TEST(UnityRenderVulkanMemorySupportTest, ReportsUnavailableWhenCandidatesAreNotHostVisible) {
    FakeMemoryProperties properties;
    properties.memoryTypeCount = 2;
    properties.memoryTypes[0].propertyFlags = kDeviceLocal;
    properties.memoryTypes[1].propertyFlags = 0;

    constexpr uint32_t candidateBits = 1U << 0U;
    EXPECT_EQ(ClassifyVulkanHostMemorySupport(
                  properties, candidateBits, kHostVisible, kHostCoherent),
              VulkanHostMemorySupport::Unavailable);
}

struct FakePayload {
    int id = 0;
};

class FakeDeviceTransitionScheduler {
public:
    void PublishInitialize(int generation, bool service = true) {
        const uint64_t intent = transitions.BeginTransition();
        activeGate = 0;
        gpuActive = false;
        gpuActive = true;
        transitions.PublishInitialize(intent,
                                      21,
                                      generation * 2);
        if (service) {
            TryService();
        }
    }

    void PublishShutdown(bool service = true) {
        const uint64_t intent = transitions.BeginTransition();
        activeGate = 0;
        gpuActive = false;
        transitions.PublishShutdown(intent);
        if (service) {
            TryService();
        }
    }

    void LockState() {
        ASSERT_FALSE(locked);
        locked = true;
        ApplyLocked();
    }

    void UnlockAndHandoff(const std::function<void()>& unlockWindow = {}) {
        ASSERT_TRUE(locked);
        locked = false;
        if (unlockWindow) {
            unlockWindow();
        }
        TryService();
    }

    void TryService() {
        while (transitions.HasStablePendingTransition()) {
            if (locked) {
                return;
            }
            locked = true;
            ApplyLocked();
            locked = false;
        }
    }

    void ServiceWithBeforeUnlockHook(const std::function<void()>& hook) {
        ASSERT_FALSE(locked);
        ASSERT_TRUE(transitions.HasStablePendingTransition());
        locked = true;
        ApplyLocked();
        hook();
        locked = false;
        TryService();
    }

    void ApplyLocked() {
        transitions.ApplyLocked([&](const DeviceTransitionSnapshot& transition) {
            applied.push_back(transition.kind);
            if (transition.kind == DeviceTransitionKind::Initialize) {
                // Coalesced Initialize always replaces any previous device
                // state, so applying it has shutdown-before-start semantics.
                ++shutdownCount;
                if (transitions.IsCurrentIntent(transition.intent)) {
                    activeGate = transition.firstRenderEventId;
                }
            } else if (transition.kind == DeviceTransitionKind::Shutdown) {
                ++shutdownCount;
                activeGate = 0;
            }
        });
    }

    DeviceTransitionState transitions;
    bool locked = false;
    bool gpuActive = false;
    int activeGate = 0;
    int shutdownCount = 0;
    std::vector<DeviceTransitionKind> applied;
};

TEST(UnityRenderVulkanTransitionTest, LatestGenerationCoalescesWithShutdownBeforeInitialize) {
    FakeDeviceTransitionScheduler scheduler;
    scheduler.LockState();
    scheduler.PublishShutdown();
    scheduler.PublishInitialize(7);

    EXPECT_EQ(scheduler.activeGate, 0);
    EXPECT_TRUE(scheduler.applied.empty());
    scheduler.UnlockAndHandoff();

    ASSERT_EQ(scheduler.applied.size(), 1U);
    EXPECT_EQ(scheduler.applied[0], DeviceTransitionKind::Initialize);
    EXPECT_EQ(scheduler.shutdownCount, 1);
    EXPECT_EQ(scheduler.activeGate, 14);
    EXPECT_FALSE(scheduler.transitions.HasStablePendingTransition());
}

TEST(UnityRenderVulkanTransitionTest, DeviceShutdownReleasesGpuBeforeDeferredCpuHandoff) {
    FakeDeviceTransitionScheduler scheduler;
    scheduler.PublishInitialize(1);
    ASSERT_TRUE(scheduler.gpuActive);
    scheduler.LockState();

    scheduler.PublishShutdown();
    EXPECT_FALSE(scheduler.gpuActive);
    EXPECT_EQ(scheduler.activeGate, 0);
    ASSERT_EQ(scheduler.applied.size(), 1U);

    scheduler.UnlockAndHandoff();
    EXPECT_EQ(scheduler.activeGate, 0);
    ASSERT_EQ(scheduler.applied.size(), 2U);
    EXPECT_EQ(scheduler.applied.back(), DeviceTransitionKind::Shutdown);
}

TEST(UnityRenderVulkanTransitionTest, LatestShutdownCannotReactivateAnOlderInitialize) {
    FakeDeviceTransitionScheduler scheduler;
    scheduler.LockState();
    scheduler.PublishInitialize(3);
    scheduler.PublishShutdown();
    scheduler.UnlockAndHandoff();

    ASSERT_EQ(scheduler.applied.size(), 1U);
    EXPECT_EQ(scheduler.applied[0], DeviceTransitionKind::Shutdown);
    EXPECT_EQ(scheduler.activeGate, 0);
}

TEST(UnityRenderVulkanTransitionTest, UnlockWindowPublicationIsServicedByHandoff) {
    FakeDeviceTransitionScheduler scheduler;
    scheduler.LockState();
    scheduler.UnlockAndHandoff([&] { scheduler.PublishInitialize(5, false); });

    ASSERT_EQ(scheduler.applied.size(), 1U);
    EXPECT_EQ(scheduler.applied[0], DeviceTransitionKind::Initialize);
    EXPECT_EQ(scheduler.activeGate, 10);
    EXPECT_EQ(scheduler.transitions.AppliedSequence(), 2U);
}

TEST(UnityRenderVulkanTransitionTest, PublicationBeforeServiceUnlockIsObservedAfterUnlock) {
    FakeDeviceTransitionScheduler scheduler;
    scheduler.PublishInitialize(4, false);
    scheduler.ServiceWithBeforeUnlockHook([&] { scheduler.PublishShutdown(); });

    ASSERT_EQ(scheduler.applied.size(), 2U);
    EXPECT_EQ(scheduler.applied[0], DeviceTransitionKind::Initialize);
    EXPECT_EQ(scheduler.applied[1], DeviceTransitionKind::Shutdown);
    EXPECT_EQ(scheduler.activeGate, 0);
    EXPECT_FALSE(scheduler.transitions.HasStablePendingTransition());
}

TEST(UnityRenderVulkanTransitionTest, NewIntentPreventsOlderInitializeActivationBeforePayloadPublish) {
    DeviceTransitionState transitions;
    const uint64_t initializeIntent = transitions.BeginTransition();
    transitions.PublishInitialize(initializeIntent, 21, 10);

    int activeGate = 0;
    uint64_t shutdownIntent = 0;
    transitions.ApplyLocked([&](const DeviceTransitionSnapshot& transition) {
        shutdownIntent = transitions.BeginTransition();
        if (transitions.IsCurrentIntent(transition.intent)) {
            activeGate = transition.firstRenderEventId;
        }
    });

    EXPECT_EQ(activeGate, 0);
    transitions.PublishShutdown(shutdownIntent);
    transitions.ApplyLocked([&](const DeviceTransitionSnapshot& transition) {
        EXPECT_EQ(transition.kind, DeviceTransitionKind::Shutdown);
    });
    EXPECT_FALSE(transitions.HasStablePendingTransition());
}

TEST(UnityRenderVulkanTransitionTest, PostActivationIntentCheckClosesGateAgain) {
    DeviceTransitionState transitions;
    const uint64_t initializeIntent = transitions.BeginTransition();
    transitions.PublishInitialize(initializeIntent, 21, 12);

    int activeGate = 0;
    uint64_t shutdownIntent = 0;
    transitions.ApplyLocked([&](const DeviceTransitionSnapshot& transition) {
        ASSERT_TRUE(transitions.IsCurrentIntent(transition.intent));
        activeGate = transition.firstRenderEventId;
        shutdownIntent = transitions.BeginTransition();
        if (!transitions.IsCurrentIntent(transition.intent)) {
            activeGate = 0;
        }
    });

    EXPECT_EQ(activeGate, 0);
    transitions.PublishShutdown(shutdownIntent);
}

class FakeScheduler {
public:
    using Lifecycle = FixedEventLifecycle<FakePayload>;

    bool StartEpoch(int prepareId, int submitId) {
        return lifecycle.StartEpoch(prepareId, submitId, eventInfo);
    }

    bool Enqueue(int id, uint64_t& serial) {
        return lifecycle.TryEnqueue(eventInfo.epoch, FakePayload{id}, serial) == EnqueueResult::Accepted;
    }

    bool RetireAndRestart(uint64_t serial, int prepareId, int submitId) {
        FakePayload payload;
        if (!lifecycle.Retire(eventInfo.epoch, serial, payload)) {
            return false;
        }

        failed.push_back(payload.id);
        return RetireActiveAndRestart(prepareId, submitId);
    }

    bool RetireActiveAndRestart(int prepareId, int submitId) {
        lifecycle.DetachActiveEpoch();
        while (lifecycle.QueueSize() != 0) {
            failed.push_back(lifecycle.TakeAt(0).id);
        }
        return lifecycle.StartEpoch(prepareId, submitId, eventInfo);
    }

    bool Dispatch(int eventId) {
        const auto prepare = lifecycle.ConsumePrepareEvent(eventId);
        if (prepare.recognized) {
            if (!prepare.hadOutstandingEvent || !prepare.active) {
                return false;
            }
            if (lifecycle.QueueSize() == 0 || lifecycle.At(0).epoch != prepare.epoch) {
                return false;
            }
            if (lifecycle.At(0).phase == SubmissionPhase::Prepared) {
                const int replacementPrepareId = eventInfo.submitEventId + 1;
                RetireActiveAndRestart(replacementPrepareId, replacementPrepareId + 1);
                return false;
            }
            if (failNextPrepare) {
                failNextPrepare = false;
                const int replacementPrepareId = eventInfo.submitEventId + 1;
                RetireActiveAndRestart(replacementPrepareId, replacementPrepareId + 1);
                return false;
            }
            lifecycle.MarkPrepared(0);
            return true;
        }

        const auto submit = lifecycle.ConsumeSubmitEvent(eventId);
        if (!submit.recognized) {
            return false;
        }
        if (!submit.hadOutstandingEvent || !submit.active) {
            return false;
        }
        if (lifecycle.QueueSize() == 0 || lifecycle.At(0).epoch != submit.epoch) {
            return false;
        }
        if (lifecycle.At(0).phase != SubmissionPhase::Prepared) {
            const int replacementPrepareId = eventInfo.submitEventId + 1;
            RetireActiveAndRestart(replacementPrepareId, replacementPrepareId + 1);
            return false;
        }
        completed.push_back(lifecycle.At(0).payload.id);
        lifecycle.TakeAt(0);
        return true;
    }

    void Shutdown() {
        lifecycle.DetachActiveEpoch();
        while (lifecycle.QueueSize() != 0) {
            lifecycle.TakeAt(0);
        }
    }

    bool PublishCancellation(uint64_t epoch) {
        return cancellations.Publish(epoch);
    }

    void ServiceCancellations() {
        uint64_t pendingEpochs = cancellations.TakePendingMask();
        for (uint64_t epoch = 1; pendingEpochs != 0; ++epoch) {
            const bool requested = (pendingEpochs & 1U) != 0;
            pendingEpochs >>= 1U;
            if (!requested || lifecycle.ActiveEpoch() != epoch) {
                continue;
            }

            const int replacementPrepareId = eventInfo.submitEventId + 1;
            RetireActiveAndRestart(replacementPrepareId, replacementPrepareId + 1);
        }
    }

    Lifecycle lifecycle;
    EpochCancellationMailbox<32> cancellations;
    EventInfo eventInfo;
    bool failNextPrepare = false;
    std::vector<int> completed;
    std::vector<int> failed;
};

TEST(UnityRenderVulkanLifecycleTest, EnforcesSubmissionAndCommandBounds) {
    FakeScheduler scheduler;
    ASSERT_TRUE(scheduler.StartEpoch(10, 11));

    for (int i = 0; i < 32; ++i) {
        uint64_t serial = 0;
        EXPECT_TRUE(scheduler.Enqueue(i, serial));
        EXPECT_NE(serial, 0U);
    }
    uint64_t serial = 0;
    EXPECT_FALSE(scheduler.Enqueue(32, serial));
    EXPECT_EQ(scheduler.lifecycle.QueueSize(), 32U);
    EXPECT_TRUE(IsValidCommandCount(256));
    EXPECT_FALSE(IsValidCommandCount(257));
}

TEST(UnityRenderVulkanLifecycleTest, InlinePrepareAndSubmitMakesProgressWithoutSelfDeadlock) {
    FakeScheduler scheduler;
    ASSERT_TRUE(scheduler.StartEpoch(20, 21));
    uint64_t serial = 0;
    ASSERT_TRUE(scheduler.Enqueue(7, serial));

    EXPECT_TRUE(scheduler.Dispatch(20));
    EXPECT_TRUE(scheduler.Dispatch(21));
    ASSERT_EQ(scheduler.completed.size(), 1U);
    EXPECT_EQ(scheduler.completed[0], 7);
}

TEST(UnityRenderVulkanLifecycleTest, RepeatedPrepareRetiresEpochBeforeAcceptingReplacementWork) {
    FakeScheduler scheduler;
    ASSERT_TRUE(scheduler.StartEpoch(30, 31));
    uint64_t first = 0;
    uint64_t second = 0;
    uint64_t replacement = 0;
    ASSERT_TRUE(scheduler.Enqueue(1, first));
    ASSERT_TRUE(scheduler.Enqueue(2, second));

    ASSERT_TRUE(scheduler.Dispatch(30));
    EXPECT_FALSE(scheduler.Dispatch(30));
    const EventInfo replacementEpoch = scheduler.eventInfo;
    ASSERT_TRUE(scheduler.Enqueue(3, replacement));
    EXPECT_FALSE(scheduler.Dispatch(31));
    EXPECT_TRUE(scheduler.Dispatch(replacementEpoch.prepareEventId));
    EXPECT_TRUE(scheduler.Dispatch(replacementEpoch.submitEventId));
    ASSERT_EQ(scheduler.failed.size(), 2U);
    EXPECT_EQ(scheduler.failed[0], 1);
    EXPECT_EQ(scheduler.failed[1], 2);
    ASSERT_EQ(scheduler.completed.size(), 1U);
    EXPECT_EQ(scheduler.completed[0], 3);
}

TEST(UnityRenderVulkanLifecycleTest, MissingPrepareRetiresEpochBeforeAcceptingReplacementWork) {
    FakeScheduler scheduler;
    ASSERT_TRUE(scheduler.StartEpoch(40, 41));
    uint64_t first = 0;
    uint64_t second = 0;
    uint64_t replacement = 0;
    ASSERT_TRUE(scheduler.Enqueue(1, first));
    ASSERT_TRUE(scheduler.Enqueue(2, second));

    EXPECT_FALSE(scheduler.Dispatch(41));
    const EventInfo replacementEpoch = scheduler.eventInfo;
    ASSERT_TRUE(scheduler.Enqueue(3, replacement));
    EXPECT_FALSE(scheduler.Dispatch(40));
    EXPECT_TRUE(scheduler.Dispatch(replacementEpoch.prepareEventId));
    EXPECT_TRUE(scheduler.Dispatch(replacementEpoch.submitEventId));
    ASSERT_EQ(scheduler.failed.size(), 2U);
    EXPECT_EQ(scheduler.failed[0], 1);
    EXPECT_EQ(scheduler.failed[1], 2);
    ASSERT_EQ(scheduler.completed.size(), 1U);
    EXPECT_EQ(scheduler.completed[0], 3);
}

TEST(UnityRenderVulkanLifecycleTest, PrepareFailureIsolatesImmediateReplacementFromLateSubmit) {
    FakeScheduler scheduler;
    ASSERT_TRUE(scheduler.StartEpoch(50, 51));
    uint64_t failed = 0;
    uint64_t replacement = 0;
    ASSERT_TRUE(scheduler.Enqueue(1, failed));

    scheduler.failNextPrepare = true;
    EXPECT_FALSE(scheduler.Dispatch(50));
    const EventInfo replacementEpoch = scheduler.eventInfo;
    ASSERT_NE(replacementEpoch.epoch, 0U);
    ASSERT_NE(replacementEpoch.epoch, 1U);
    ASSERT_TRUE(scheduler.Enqueue(2, replacement));

    EXPECT_FALSE(scheduler.Dispatch(51));
    EXPECT_TRUE(scheduler.Dispatch(replacementEpoch.prepareEventId));
    EXPECT_TRUE(scheduler.Dispatch(replacementEpoch.submitEventId));
    ASSERT_EQ(scheduler.failed.size(), 1U);
    EXPECT_EQ(scheduler.failed[0], 1);
    ASSERT_EQ(scheduler.completed.size(), 1U);
    EXPECT_EQ(scheduler.completed[0], 2);
}

TEST(UnityRenderVulkanLifecycleTest, DeferredCancellationCompletesWithoutAnyEventCallback) {
    FakeScheduler scheduler;
    ASSERT_TRUE(scheduler.StartEpoch(60, 61));
    uint64_t canceled = 0;
    uint64_t replacement = 0;
    ASSERT_TRUE(scheduler.Enqueue(1, canceled));
    const EventInfo canceledEpoch = scheduler.eventInfo;

    ASSERT_TRUE(scheduler.PublishCancellation(canceledEpoch.epoch));
    EXPECT_EQ(scheduler.cancellations.PendingCount(), 1U);
    EXPECT_EQ(scheduler.lifecycle.QueueSize(), 1U);

    scheduler.ServiceCancellations();
    EXPECT_EQ(scheduler.cancellations.PendingCount(), 0U);
    ASSERT_EQ(scheduler.failed.size(), 1U);
    EXPECT_EQ(scheduler.failed[0], 1);
    const EventInfo replacementEpoch = scheduler.eventInfo;
    ASSERT_NE(replacementEpoch.epoch, canceledEpoch.epoch);
    ASSERT_TRUE(scheduler.Enqueue(2, replacement));

    EXPECT_FALSE(scheduler.Dispatch(canceledEpoch.prepareEventId));
    EXPECT_FALSE(scheduler.Dispatch(canceledEpoch.submitEventId));
    EXPECT_TRUE(scheduler.Dispatch(replacementEpoch.prepareEventId));
    EXPECT_TRUE(scheduler.Dispatch(replacementEpoch.submitEventId));
    scheduler.ServiceCancellations();
    ASSERT_EQ(scheduler.failed.size(), 1U);
    ASSERT_EQ(scheduler.completed.size(), 1U);
    EXPECT_EQ(scheduler.completed[0], 2);
}

TEST(UnityRenderVulkanLifecycleTest, CancelKeepsBothPossibleLateEventsTombstoned) {
    FakeScheduler scheduler;
    ASSERT_TRUE(scheduler.StartEpoch(34, 35));
    uint64_t canceled = 0;
    uint64_t next = 0;
    ASSERT_TRUE(scheduler.Enqueue(1, canceled));
    ASSERT_TRUE(scheduler.RetireAndRestart(canceled, 36, 37));
    ASSERT_TRUE(scheduler.Enqueue(2, next));
    EXPECT_EQ(scheduler.lifecycle.OutstandingEvents(), 4U);

    EXPECT_FALSE(scheduler.Dispatch(34));
    EXPECT_FALSE(scheduler.Dispatch(35));
    EXPECT_TRUE(scheduler.Dispatch(36));
    EXPECT_TRUE(scheduler.Dispatch(37));
    ASSERT_EQ(scheduler.failed.size(), 1U);
    EXPECT_EQ(scheduler.failed[0], 1);
    ASSERT_EQ(scheduler.completed.size(), 1U);
    EXPECT_EQ(scheduler.completed[0], 2);
    EXPECT_EQ(scheduler.lifecycle.OutstandingEvents(), 0U);
}

TEST(UnityRenderVulkanLifecycleTest, CancelAfterOnlyPrepareCanStartNextEpochImmediately) {
    FakeScheduler scheduler;
    ASSERT_TRUE(scheduler.StartEpoch(38, 39));
    uint64_t canceled = 0;
    uint64_t next = 0;
    ASSERT_TRUE(scheduler.Enqueue(1, canceled));

    // Unity delivered Prepare, then managed submission reported an ambiguous
    // failure before Submit was observed.
    ASSERT_TRUE(scheduler.Dispatch(38));
    ASSERT_TRUE(scheduler.RetireAndRestart(canceled, 40, 41));
    ASSERT_TRUE(scheduler.Enqueue(2, next));
    EXPECT_FALSE(scheduler.Dispatch(39));
    EXPECT_TRUE(scheduler.Dispatch(40));
    EXPECT_TRUE(scheduler.Dispatch(41));
    ASSERT_EQ(scheduler.completed.size(), 1U);
    EXPECT_EQ(scheduler.completed[0], 2);
    EXPECT_EQ(scheduler.lifecycle.OutstandingEvents(), 0U);
}

TEST(UnityRenderVulkanLifecycleTest, MissingOldSubmitDoesNotBlockReplacementEpoch) {
    FakeScheduler scheduler;
    ASSERT_TRUE(scheduler.StartEpoch(42, 43));
    uint64_t canceled = 0;
    uint64_t next = 0;
    ASSERT_TRUE(scheduler.Enqueue(1, canceled));
    ASSERT_TRUE(scheduler.RetireAndRestart(canceled, 44, 45));
    ASSERT_TRUE(scheduler.Enqueue(2, next));

    // Only the old Prepare callback arrives. Its unmatched Submit credit stays
    // bounded as an old-epoch tombstone while the replacement epoch advances.
    EXPECT_FALSE(scheduler.Dispatch(42));
    EXPECT_TRUE(scheduler.Dispatch(44));
    EXPECT_TRUE(scheduler.Dispatch(45));
    ASSERT_EQ(scheduler.completed.size(), 1U);
    EXPECT_EQ(scheduler.completed[0], 2);
    EXPECT_EQ(scheduler.lifecycle.OutstandingEvents(), 1U);
}

TEST(UnityRenderVulkanLifecycleTest, LateCallbacksAfterShutdownAreNoOp) {
    FakeScheduler scheduler;
    ASSERT_TRUE(scheduler.StartEpoch(40, 41));
    uint64_t serial = 0;
    ASSERT_TRUE(scheduler.Enqueue(9, serial));
    const EventInfo old = scheduler.eventInfo;
    scheduler.Shutdown();

    EXPECT_FALSE(scheduler.Dispatch(old.prepareEventId));
    EXPECT_FALSE(scheduler.Dispatch(old.submitEventId));
    EXPECT_TRUE(scheduler.completed.empty());

    ASSERT_TRUE(scheduler.StartEpoch(42, 43));
    ASSERT_TRUE(scheduler.Enqueue(10, serial));
    EXPECT_TRUE(scheduler.Dispatch(42));
    EXPECT_TRUE(scheduler.Dispatch(43));
    ASSERT_EQ(scheduler.completed.size(), 1U);
    EXPECT_EQ(scheduler.completed[0], 10);
}

TEST(UnityRenderVulkanLifecycleTest, EpochIdsAreNeverReusedAndReloadsRemainBounded) {
    FakeScheduler scheduler;
    for (int i = 0; i < 4; ++i) {
        const int prepareId = 100 + i * 2;
        const int submitId = prepareId + 1;
        ASSERT_TRUE(scheduler.StartEpoch(prepareId, submitId));
        const EventInfo old = scheduler.eventInfo;
        scheduler.Shutdown();
        EXPECT_FALSE(scheduler.lifecycle.StartEpoch(old.prepareEventId, old.submitEventId, scheduler.eventInfo));
    }
    EXPECT_EQ(scheduler.lifecycle.EpochCount(), 4U);
    EXPECT_FALSE(scheduler.lifecycle.StartEpoch(100, 101, scheduler.eventInfo));
}

TEST(UnityRenderVulkanLifecycleTest, UnpublishedEpochRollbackDoesNotConsumeCapacity) {
    FakeScheduler scheduler;
    for (int i = 0; i < 64; ++i) {
        ASSERT_TRUE(scheduler.StartEpoch(600, 601));
        ASSERT_TRUE(scheduler.lifecycle.RollbackActiveEpoch(scheduler.eventInfo.epoch));
        EXPECT_EQ(scheduler.lifecycle.EpochCount(), 0U);
        EXPECT_EQ(scheduler.lifecycle.ActiveEpoch(), 0U);
        EXPECT_FALSE(scheduler.lifecycle.Disabled());
    }

    ASSERT_TRUE(scheduler.StartEpoch(600, 601));
    EXPECT_EQ(scheduler.eventInfo.epoch, 1U);
}

TEST(UnityRenderVulkanLifecycleTest, ThreeReloadsKeepLateEventsOutOfCurrentEpoch) {
    FakeScheduler scheduler;
    std::vector<EventInfo> retired;
    uint64_t serial = 0;

    for (int generation = 0; generation < 3; ++generation) {
        const int prepareId = 500 + generation * 2;
        ASSERT_TRUE(scheduler.StartEpoch(prepareId, prepareId + 1));
        ASSERT_TRUE(scheduler.Enqueue(generation, serial));
        retired.push_back(scheduler.eventInfo);
        scheduler.Shutdown();
    }

    ASSERT_TRUE(scheduler.StartEpoch(506, 507));
    ASSERT_TRUE(scheduler.Enqueue(99, serial));
    for (const EventInfo& old : retired) {
        EXPECT_FALSE(scheduler.Dispatch(old.prepareEventId));
        EXPECT_FALSE(scheduler.Dispatch(old.submitEventId));
    }
    EXPECT_TRUE(scheduler.Dispatch(506));
    EXPECT_TRUE(scheduler.Dispatch(507));
    ASSERT_EQ(scheduler.completed.size(), 1U);
    EXPECT_EQ(scheduler.completed[0], 99);
    EXPECT_EQ(scheduler.lifecycle.EpochCount(), 4U);
}

TEST(UnityRenderVulkanLifecycleTest, EventBudgetExhaustionDisablesProcess) {
    FakeScheduler scheduler;
    ASSERT_TRUE(scheduler.StartEpoch(200, 201));
    uint64_t serial = 0;
    for (int i = 0; i < 32; ++i) {
        ASSERT_TRUE(scheduler.Enqueue(i, serial));
    }
    scheduler.Shutdown();
    ASSERT_TRUE(scheduler.StartEpoch(202, 203));
    EXPECT_FALSE(scheduler.Enqueue(33, serial));
    EXPECT_TRUE(scheduler.lifecycle.Disabled());
}

TEST(UnityRenderVulkanLifecycleTest, FakeHostOwnsReturnToUnityFact) {
    enum class HostState { Idle, InCallback, Returned };
    struct FakeHost {
        HostState state = HostState::Idle;
        bool callbackReturned = false;

        bool CanUnload() const {
            return callbackReturned && state == HostState::Returned;
        }

        void Dispatch(const std::function<void()>& callback) {
            state = HostState::InCallback;
            callbackReturned = false;
            callback();
            state = HostState::Returned;
            callbackReturned = true;
        }
    } host;

    FakeScheduler scheduler;
    ASSERT_TRUE(scheduler.StartEpoch(300, 301));
    uint64_t serial = 0;
    ASSERT_TRUE(scheduler.Enqueue(1, serial));

    host.Dispatch([&] {
        EXPECT_FALSE(host.CanUnload());
        EXPECT_TRUE(scheduler.Dispatch(300));
        EXPECT_FALSE(host.CanUnload());
    });
    EXPECT_TRUE(host.CanUnload());
    host.Dispatch([&] { EXPECT_TRUE(scheduler.Dispatch(301)); });
    EXPECT_TRUE(host.CanUnload());
}

TEST(UnityRenderVulkanLifecycleTest, DeferredSameThreadAndAsyncReloadUseHostReturnBoundary) {
    enum class HostState { Idle, InCallback, Returned };
    struct FakeHost {
        HostState state = HostState::Idle;
        bool callbackReturned = false;
        std::vector<std::function<void()>> deferred;

        bool CanUnload() const {
            return callbackReturned && state == HostState::Returned && deferred.empty();
        }

        void Queue(std::function<void()> callback) {
            deferred.push_back(std::move(callback));
            callbackReturned = false;
        }

        void RunOne() {
            ASSERT_FALSE(deferred.empty());
            auto callback = std::move(deferred.front());
            deferred.erase(deferred.begin());
            state = HostState::InCallback;
            callback();
            state = HostState::Returned;
            callbackReturned = true;
        }
    } host;

    FakeScheduler scheduler;
    ASSERT_TRUE(scheduler.StartEpoch(400, 401));
    uint64_t serial = 0;
    ASSERT_TRUE(scheduler.Enqueue(1, serial));
    const EventInfo old = scheduler.eventInfo;

    // Deferred callbacks are queued by the fake host, then device shutdown
    // detaches the epoch without waiting for them.
    host.Queue([&] { EXPECT_FALSE(scheduler.Dispatch(old.prepareEventId)); });
    host.Queue([&] { EXPECT_FALSE(scheduler.Dispatch(old.submitEventId)); });
    EXPECT_FALSE(host.CanUnload());
    scheduler.Shutdown();
    ASSERT_TRUE(scheduler.StartEpoch(402, 403));
    ASSERT_TRUE(scheduler.Enqueue(2, serial));
    host.RunOne();
    EXPECT_FALSE(host.CanUnload());
    EXPECT_TRUE(scheduler.Dispatch(402));
    EXPECT_TRUE(scheduler.Dispatch(403));
    host.RunOne();
    EXPECT_TRUE(host.CanUnload());

    // Device recovery does not wait for old callbacks to return. Host return
    // remains a separate prerequisite for module unload only.
    ASSERT_EQ(scheduler.completed.size(), 1U);
    EXPECT_EQ(scheduler.completed[0], 2);
}

} // namespace
