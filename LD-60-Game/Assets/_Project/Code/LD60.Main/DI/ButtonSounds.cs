using LD60.UI;
using UnityEngine.SceneManagement;
using VContainer;

namespace LD60.Main.DI
{
    public static class ButtonSounds
    {
        public static void Inject(Scene scene, IObjectResolver resolver)
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var sounds = roots[i].GetComponentsInChildren<UiButtonSound>(true);
                for (var s = 0; s < sounds.Length; s++)
                    resolver.Inject(sounds[s]);
            }
        }
    }
}
