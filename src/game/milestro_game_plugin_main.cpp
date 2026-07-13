#include <IUnityLog.h>
#include <Milestro/game/milestro_game_interface.h>
#include <Milestro/input/ScrollPhaseMonitor.h>
#include <Milestro/log/log.h>

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
    const auto scrollMonitorResult = milestro::input::ShutdownScrollPhaseMonitorForPluginUnload();
    if (scrollMonitorResult != milestro::input::ScrollPhaseMonitorResult::Succeeded) {
        MILESTROLOG_ERROR("Scroll phase monitor remained active during plugin unload; result={}.",
                          static_cast<int32_t>(scrollMonitorResult));
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
