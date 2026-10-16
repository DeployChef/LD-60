using LD60.UI;
using UnityEngine.SceneManagement;
using VContainer;

namespace LD60.Main.DI
{
    public static class VolumeInjection
    {
        public static void Inject(Scene scene, IObjectResolver resolver)
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var views = roots[i].GetComponentsInChildren<VolumeController>(true);
                for (var v = 0; v < views.Length; v++)
                    resolver.Inject(views[v]);
            }
        }
    }
}
