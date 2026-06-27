#ifndef MILESTRO_GAME_UNITY_RENDER_H
#define MILESTRO_GAME_UNITY_RENDER_H

#include <IUnityInterface.h>

namespace milestro::game::unity_render {

void Load(IUnityInterfaces *unityInterfaces);
void Unload();

} // namespace milestro::game::unity_render

#endif // MILESTRO_GAME_UNITY_RENDER_H
