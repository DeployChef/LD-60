namespace LD60.Core
{
    public interface IGameAnalytics
    {
        void TrackGameStart();
        void TrackEnding(string endingId, string chapterName);
    }
}
