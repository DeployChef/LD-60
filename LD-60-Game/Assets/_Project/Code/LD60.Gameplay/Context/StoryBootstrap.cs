using LD60.Core.Bus;
using LD60.Gameplay.Story;
using UnityEngine;
using VContainer;

namespace LD60.Gameplay.Context
{
    public sealed class StoryBootstrap : MonoBehaviour
    {
        [SerializeField] private ChapterDefinition[] chapters = System.Array.Empty<ChapterDefinition>();
        [SerializeField] private DayStartPreset debugStart;
        [SerializeField] private bool debugKeys = true;

        private StoryDriver _driver;

        public StoryContext Context { get; private set; }
        public bool DebugKeys => debugKeys;

        [Inject]
        public void Construct(IMessageBus bus, IGameSession session)
        {
            Context = new StoryContext();
            var desk = new Desk(bus);
            _driver = new StoryDriver(bus, Context, desk, chapters, debugStart, session);
        }

        private void Start()
        {
            _driver.Begin();
        }

        private void Update()
        {
            _driver.Tick();
        }

        private void OnDestroy()
        {
            _driver?.Dispose();
        }

        public void AddFill(float amount)
        {
            _driver.AddFill(amount);
        }

        public void Stamp()
        {
            _driver.Stamp();
        }

        public void CompleteCurrent()
        {
            _driver.CompleteCurrent();
        }

        public void LogStatus()
        {
            Debug.Log($"[Story] {_driver.Describe()}");
        }
    }
}
