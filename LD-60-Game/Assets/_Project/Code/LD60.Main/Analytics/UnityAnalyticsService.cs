using System;
using Cysharp.Threading.Tasks;
using LD60.Core;
using Unity.Services.Analytics;
using Unity.Services.Core;
using UnityEngine;

namespace LD60.Main.Analytics
{
    public sealed class UnityAnalyticsService : IGameAnalytics
    {
        private bool _isInitialized;

        public async UniTask InitializeAsync()
        {
            if (_isInitialized)
                return;

            try
            {
                await UnityServices.InitializeAsync();
                AnalyticsService.Instance.StartDataCollection();
                _isInitialized = true;
                Debug.Log("[Analytics] Unity Gaming Services Analytics initialized.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Analytics] Failed to initialize Unity Analytics: {ex.Message}");
            }
        }

        public void TrackGameStart()
        {
            if (!_isInitialized)
            {
                Debug.Log("[Analytics] TrackGameStart ignored (not initialized).");
                return;
            }

            try
            {
                var customEvent = new CustomEvent("game_start");
                AnalyticsService.Instance.RecordEvent(customEvent);
                AnalyticsService.Instance.Flush();
                Debug.Log("[Analytics] Event recorded: game_start");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Analytics] Error recording game_start: {ex.Message}");
            }
        }

        public void TrackEnding(string endingId, string chapterName)
        {
            if (!_isInitialized)
            {
                Debug.Log($"[Analytics] TrackEnding ignored (not initialized): ending={endingId}, chapter={chapterName}");
                return;
            }

            try
            {
                var customEvent = new CustomEvent("game_ended")
                {
                    { "ending_id", endingId ?? "unknown" },
                    { "chapter", chapterName ?? "unknown" }
                };

                AnalyticsService.Instance.RecordEvent(customEvent);
                AnalyticsService.Instance.Flush();
                Debug.Log($"[Analytics] Event recorded: game_ended (ending={endingId}, chapter={chapterName})");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Analytics] Error recording game_ended: {ex.Message}");
            }
        }
    }
}
