#include <IUnityLog.h>
#include <Milestro/game/milestro_game_interface.h>
#include <Milestro/input/ScrollPhaseMonitor.h>
#include <Milestro/log/log.h>

#include <cstdlib>

#include "milestro_game_unity_render.h"

static IUnityLog *unityLogPtr = nullptr;

extern "C" {

UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API
UnityPluginLoad(IUnityInterfaces *unityInterfacesPtr) {
    unityLogPtr = unityInterfacesPtr->Get<IUnityLog>();
    milestro::log::MilestroLogger::initWithUnity(unityLogPtr);
    milestro::game::unity_render::Load(unityInterfacesPtr);
    MILESTROLOG_DEBUG ("Milestro Launched");
}

UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API
UnityPluginUnload() {
    using milestro::input::ScrollPhasePluginUnloadDecision;

    auto decision = milestro::input::DecideScrollPhasePluginUnload(milestro::input::HasActiveScrollPhaseMonitorState(),
                                                                   milestro::input::IsScrollPhaseMonitorMainThread(),
                                                                   false,
                                                                   false);
    if (decision == ScrollPhasePluginUnloadDecision::Cleanup) {
        const auto cleanupResult = milestro::input::ShutdownScrollPhaseMonitorForPluginUnload();
        decision = milestro::input::DecideScrollPhasePluginUnload(
                milestro::input::HasActiveScrollPhaseMonitorState(),
                true,
                true,
                cleanupResult == milestro::input::ScrollPhaseMonitorResult::Succeeded);
    }
    if (decision == ScrollPhasePluginUnloadDecision::Abort) {
        MILESTROLOG_ERROR("Active scroll phase monitor prevented safe plugin unload.");
        std::abort();
    }
    milestro::game::unity_render::Unload();
    unityLogPtr = nullptr;
}

MILESTRO_API int64_t MilestroGetVersion(int32_t &major, int32_t &minor, int32_t &patch) {
    major = MILESTRO_VERSION_MAJOR;
    minor = MILESTRO_VERSION_MINOR;
    patch = MILESTRO_VERSION_PATCH;
    MILESTROLOG_DEBUG("GetVersion: major={}, minor={}, patch={}", major, minor, patch);
    return MILESTRO_VERSION_MAJOR;
}
}
