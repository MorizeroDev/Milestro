#import "UnityAppController.h"

#include "Unity/UnityInterface.h"

extern "C" void UnityPluginLoad(struct IUnityInterfaces* unityInterfaces);
extern "C" void UnityPluginUnload(void);

@interface MilestroUnityAppController : UnityAppController
@end

@implementation MilestroUnityAppController

- (void)shouldAttachRenderDelegate
{
    [super shouldAttachRenderDelegate];
    UnityRegisterPlugin(&UnityPluginLoad, &UnityPluginUnload);
}

@end

IMPL_APP_CONTROLLER_SUBCLASS(MilestroUnityAppController)
