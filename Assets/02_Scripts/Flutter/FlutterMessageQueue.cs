using System;
using System.Collections.Generic;
using UnityEngine;

namespace FlutterIntegration
{
    /// <summary>
    /// 전송 실패한 메시지를 저장하고 재시도하는 큐
    /// </summary>
    public class FlutterMessageQueue
    {
        private List<PendingMessage> pendingMessages = new List<PendingMessage>();
        private float[] retryDelays;

        public FlutterMessageQueue(float[] delays = null)
        {
            retryDelays = delays ?? new float[] { 3f, 5f, 10f, 20f, 30f };
        }

        /// <summary>
        /// 메시지를 큐에 추가
        /// </summary>
        public void Enqueue(string json, Func<bool> sendAction)
        {
            pendingMessages.Add(new PendingMessage
            {
                Json = json,
                SendAction = sendAction,
                RetryCount = 0,
                NextRetryDelay = retryDelays[0],
                NextRetryTime = Time.time + retryDelays[0]
            });

            Debug.Log($"[FlutterMessageQueue] 메시지 큐에 추가됨. 대기 중: {pendingMessages.Count}개");
        }

        /// <summary>
        /// 대기 중인 메시지가 있는지 확인
        /// </summary>
        public bool HasPendingMessages()
        {
            return pendingMessages.Count > 0;
        }

        /// <summary>
        /// 재시도할 준비가 된 다음 메시지 반환
        /// </summary>
        public PendingMessage GetNextReadyMessage()
        {
            float currentTime = Time.time;

            for (int i = 0; i < pendingMessages.Count; i++)
            {
                if (pendingMessages[i].NextRetryTime <= currentTime)
                {
                    return pendingMessages[i];
                }
            }

            return null;
        }

        /// <summary>
        /// 메시지 전송 성공 시 큐에서 제거
        /// </summary>
        public void MarkComplete(PendingMessage message)
        {
            pendingMessages.Remove(message);
            Debug.Log($"[FlutterMessageQueue] 메시지 전송 성공. 남은 대기: {pendingMessages.Count}개");
        }

        /// <summary>
        /// 재시도 스케줄링
        /// </summary>
        public void ScheduleRetry(PendingMessage message)
        {
            message.RetryCount++;
            int delayIndex = Math.Min(message.RetryCount, retryDelays.Length - 1);
            message.NextRetryDelay = retryDelays[delayIndex];
            message.NextRetryTime = Time.time + message.NextRetryDelay;

            Debug.Log($"[FlutterMessageQueue] 재시도 예약: {message.NextRetryDelay}초 후 (시도 {message.RetryCount + 1}회)");
        }

        /// <summary>
        /// 특정 타입의 메시지 모두 제거 (예: 새 SYNC_INTIMACY가 오면 이전 것 제거)
        /// </summary>
        public void RemoveMessagesByType(string messageType)
        {
            pendingMessages.RemoveAll(m => m.Json.Contains($"\"type\":\"{messageType}\""));
        }

        /// <summary>
        /// 모든 대기 메시지 클리어
        /// </summary>
        public void Clear()
        {
            pendingMessages.Clear();
        }
    }

    /// <summary>
    /// 대기 중인 메시지 데이터
    /// </summary>
    public class PendingMessage
    {
        public string Json;
        public Func<bool> SendAction;
        public int RetryCount;
        public float NextRetryDelay;
        public float NextRetryTime;
    }
}
