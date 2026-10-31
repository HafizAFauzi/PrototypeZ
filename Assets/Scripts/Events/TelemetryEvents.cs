using System;

namespace LuckyDuelist.Events
{
    /// <summary>
    /// Dispatched when TelemetryService begins sending a session payload to the cloud backend.
    /// </summary>
    public struct TelemetrySyncStartedEvent
    {
        public string SessionId;
        public string EndpointUrl;
        public bool IsRetryQueue;

        public TelemetrySyncStartedEvent(string sessionId, string endpointUrl, bool isRetryQueue)
        {
            SessionId = sessionId;
            EndpointUrl = endpointUrl;
            IsRetryQueue = isRetryQueue;
        }
    }

    /// <summary>
    /// Dispatched when a cloud telemetry synchronization attempt completes (either success or offline fallback).
    /// </summary>
    public struct TelemetrySyncCompletedEvent
    {
        public string SessionId;
        public bool IsSuccess;
        public bool IsSavedToLocalFallback;
        public long ResponseCode;
        public string Message;
        public string PayloadJson;

        public TelemetrySyncCompletedEvent(
            string sessionId,
            bool isSuccess,
            bool isSavedToLocalFallback,
            long responseCode,
            string message,
            string payloadJson)
        {
            SessionId = sessionId;
            IsSuccess = isSuccess;
            IsSavedToLocalFallback = isSavedToLocalFallback;
            ResponseCode = responseCode;
            Message = message;
            PayloadJson = payloadJson;
        }
    }
}
