using System;
using System.Collections.Generic;
using UnityEngine;

namespace LuckyDuelist.Telemetry
{
    /// <summary>
    /// Root document payload for a participant's complete game session telemetry.
    /// Serialized directly to JSON matching docs/ERD.md Section 4 for Firebase Firestore / Supabase REST API.
    /// </summary>
    [Serializable]
    public class TelemetrySessionPayload
    {
        public string user_id;
        public string group_id;
        public string device_model;
        public string created_at;
        public SessionMetrics session = new SessionMetrics();
        public MonetizationMetrics monetization = new MonetizationMetrics();
        public FPSCombatMetrics fps_combat_performance = new FPSCombatMetrics();
        public List<MatchDetailRecord> match_details = new List<MatchDetailRecord>();

        public TelemetrySessionPayload()
        {
            user_id = string.Empty;
            group_id = string.Empty;
            device_model = SystemInfo.deviceModel ?? "PC / Editor";
            created_at = DateTime.UtcNow.ToString("o");
        }

        /// <summary>
        /// Serializes this telemetry session payload to an indented JSON string.
        /// </summary>
        public string ToJson(bool prettyPrint = true)
        {
            return JsonUtility.ToJson(this, prettyPrint);
        }

        /// <summary>
        /// Deserializes a telemetry session payload from a JSON string.
        /// </summary>
        public static TelemetrySessionPayload FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            return JsonUtility.FromJson<TelemetrySessionPayload>(json);
        }
    }

    /// <summary>
    /// Session duration and progression metrics (SESSIONS table in ERD.md).
    /// </summary>
    [Serializable]
    public class SessionMetrics
    {
        public string session_id;
        public float play_duration_sec;
        public int matches_completed;
        public int matches_won;
        public bool completed_stage_3;
        public int soft_currency_gold;

        public SessionMetrics()
        {
            session_id = Guid.NewGuid().ToString();
            play_duration_sec = 0f;
            matches_completed = 0;
            matches_won = 0;
            completed_stage_3 = false;
            soft_currency_gold = 0;
        }
    }

    /// <summary>
    /// Economic and monetization performance metrics (AD_IMPRESSIONS and IAP_TRANSACTIONS in ERD.md).
    /// </summary>
    [Serializable]
    public class MonetizationMetrics
    {
        public float initial_budget = 20000.0f;
        public float remaining_budget = 20000.0f;
        public float total_iap_revenue = 0.0f;
        public List<PurchasedItemRecord> purchased_items = new List<PurchasedItemRecord>();
        public int ad_impressions_count = 0;
        public AdImpressionsBreakdown ad_impressions_breakdown = new AdImpressionsBreakdown();
        public float total_ad_revenue = 0.0f;
        public float total_user_arpu = 0.0f;

        public void RecalculateTotals()
        {
            float iapSum = 0f;
            if (purchased_items != null)
            {
                for (int i = 0; i < purchased_items.Count; i++)
                {
                    iapSum += purchased_items[i].price;
                }
            }
            total_iap_revenue = iapSum;
            total_user_arpu = total_ad_revenue + total_iap_revenue;
        }
    }

    /// <summary>
    /// Breakdown of Rewarded Ad impression counts by placement type.
    /// </summary>
    [Serializable]
    public class AdImpressionsBreakdown
    {
        public int respin_slot = 0;
        public int revive_match = 0;
        public int double_gold = 0;
    }

    /// <summary>
    /// Record of a purchased cosmetic item from the Virtual IAP Store.
    /// </summary>
    [Serializable]
    public class PurchasedItemRecord
    {
        public string item_id;
        public string item_type;
        public float price;
        public string timestamp;

        public PurchasedItemRecord()
        {
            item_id = string.Empty;
            item_type = string.Empty;
            price = 0f;
            timestamp = DateTime.UtcNow.ToString("o");
        }

        public PurchasedItemRecord(string itemId, string itemType, float priceIdr, DateTime? time = null)
        {
            item_id = itemId;
            item_type = itemType;
            price = priceIdr;
            timestamp = (time ?? DateTime.UtcNow).ToString("o");
        }
    }

    /// <summary>
    /// First-person reflex and timing combat statistics (MATCH_LOGS aggregate in ERD.md).
    /// </summary>
    [Serializable]
    public class FPSCombatMetrics
    {
        public int total_aim_shots_fired = 0;
        public int total_aim_lockons_successful = 0;
        public int total_red_bullets_dodged = 0;
        public int total_yellow_bullets_parried = 0;
        public int total_unparryable_fails = 0;
        public int total_damage_deflected = 0;
    }

    /// <summary>
    /// Detailed record for each duel encounter / stage played (MATCH_LOGS in ERD.md).
    /// </summary>
    [Serializable]
    public class MatchDetailRecord
    {
        public int stage;
        public string outcome; // "WIN" or "LOSS"
        public int turns;
        public int parry_count;
        public int dodge_count;
        public int unparryable_fails;
        public int ad_respin_count;
        public bool ad_revive_used;

        public MatchDetailRecord()
        {
            stage = 1;
            outcome = "LOSS";
            turns = 0;
            parry_count = 0;
            dodge_count = 0;
            unparryable_fails = 0;
            ad_respin_count = 0;
            ad_revive_used = false;
        }

        public MatchDetailRecord(int stageNumber)
        {
            stage = stageNumber;
            outcome = "IN_PROGRESS";
            turns = 0;
            parry_count = 0;
            dodge_count = 0;
            unparryable_fails = 0;
            ad_respin_count = 0;
            ad_revive_used = false;
        }
    }

    /// <summary>
    /// Wrapper for serializing offline queue of pending telemetry payloads to disk.
    /// Stored at Application.persistentDataPath/pending_logs.json.
    /// </summary>
    [Serializable]
    public class PendingTelemetryQueue
    {
        public List<TelemetrySessionPayload> pendingSessions = new List<TelemetrySessionPayload>();

        public string ToJson(bool prettyPrint = true)
        {
            return JsonUtility.ToJson(this, prettyPrint);
        }

        public static PendingTelemetryQueue FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return new PendingTelemetryQueue();
            try
            {
                var queue = JsonUtility.FromJson<PendingTelemetryQueue>(json);
                return queue ?? new PendingTelemetryQueue();
            }
            catch
            {
                return new PendingTelemetryQueue();
            }
        }
    }
}
