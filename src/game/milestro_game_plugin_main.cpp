#include <IUnityLog.h>
#include <Milestro/game/milestro_game_interface.h>
#include <Milestro/log/log.h>

static IUnityLog *unityLogPtr = nullptr;

extern "C" {

UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API
UnityPluginLoad(IUnityInterfaces *unityInterfacesPtr) {
    unityLogPtr = unityInterfacesPtr->Get<IUnityLog>();
    milestro::log::MilestroLogger::initWithUnity(unityLogPtr);
    MILESTROLOG_DEBUG ("Milestro Launched");
}

UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API
UnityPluginUnload() {
    unityLogPtr = nullptr;
}

MILESTRO_API int64_t MilestroGetVersion(int32_t & major, int32_t & minor, int32_t & patch) {
    major = MILESTRO_VERSION_MAJOR;
    minor = MILESTRO_VERSION_MINOR;
    patch = MILESTRO_VERSION_PATCH;
    MILESTROLOG_DEBUG("GetVersion: major={}, minor={}, patch={}", major, minor, patch);
    return MILESTRO_VERSION_MAJOR;
}
}
