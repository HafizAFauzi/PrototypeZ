using System;
using System.Text;
using UnityEngine;

namespace LuckyDuelist.Telemetry
{
    /// <summary>
    /// Target backend provider for telemetry synchronization.
    /// </summary>
    public enum CloudBackendType
    {
        /// <summary>
        /// Supabase PostgREST API (POST https://[PROJECT_ID].supabase.co/rest/v1/[TABLE]).
        /// Headers: apikey, Authorization: Bearer, Content-Type: application/json.
        /// </summary>
        SupabaseRest = 0,

        /// <summary>
        /// Firebase Firestore REST API (POST https://firestore.googleapis.com/v1/projects/[PROJECT_ID]/databases/(default)/documents/[COLLECTION]).
        /// Payload formatted as Firestore Document with typed fields.
        /// </summary>
        FirestoreRest = 1,

        /// <summary>
        /// Generic HTTP POST JSON REST API / Webhook (e.g. Google Apps Script, Node.js server, Webhook.site).
        /// Raw JSON payload with standard application/json header.
        /// </summary>
        GenericWebhook = 2
    }

    /// <summary>
    /// Formats payload into provider-specific REST format if necessary.
    /// </summary>
    public static class TelemetryPayloadAdapter
    {
        /// <summary>
        /// Prepares the request body string based on the selected cloud backend provider.
        /// </summary>
        public static string FormatPayload(TelemetrySessionPayload payload, CloudBackendType backendType)
        {
            if (payload == null) return "{}";

            switch (backendType)
            {
                case CloudBackendType.FirestoreRest:
                    return BuildFirestoreDocumentJson(payload);

                case CloudBackendType.SupabaseRest:
                case CloudBackendType.GenericWebhook:
                default:
                    return payload.ToJson(false);
            }
        }

        /// <summary>
        /// Formats standard TelemetrySessionPayload into Google Cloud Firestore REST API Document schema.
        /// Ref: https://cloud.google.com/firestore/docs/reference/rest/v1/projects.databases.documents#Document
        /// </summary>
        public static string BuildFirestoreDocumentJson(TelemetrySessionPayload p)
        {
            var sb = new StringBuilder();
            sb.Append("{\"fields\":{");

            // Top-level fields
            sb.Append($"\"user_id\":{{\"stringValue\":\"{Escape(p.user_id)}\"}},");
            sb.Append($"\"group_id\":{{\"stringValue\":\"{Escape(p.group_id)}\"}},");
            sb.Append($"\"device_model\":{{\"stringValue\":\"{Escape(p.device_model)}\"}},");
            sb.Append($"\"created_at\":{{\"stringValue\":\"{Escape(p.created_at)}\"}},");

            // Session map
            sb.Append("\"session\":{\"mapValue\":{\"fields\":{");
            sb.Append($"\"session_id\":{{\"stringValue\":\"{Escape(p.session.session_id)}\"}},");
            sb.Append($"\"play_duration_sec\":{{\"doubleValue\":{p.session.play_duration_sec:F1}}},");
            sb.Append($"\"matches_completed\":{{\"integerValue\":\"{p.session.matches_completed}\"}},");
            sb.Append($"\"matches_won\":{{\"integerValue\":\"{p.session.matches_won}\"}},");
            sb.Append($"\"completed_stage_3\":{{\"booleanValue\":{p.session.completed_stage_3.ToString().ToLowerInvariant()}}},");
            sb.Append($"\"soft_currency_gold\":{{\"integerValue\":\"{p.session.soft_currency_gold}\"}}");
            sb.Append("}}},");

            // Monetization map
            sb.Append("\"monetization\":{\"mapValue\":{\"fields\":{");
            sb.Append($"\"initial_budget\":{{\"doubleValue\":{p.monetization.initial_budget:F2}}},");
            sb.Append($"\"remaining_budget\":{{\"doubleValue\":{p.monetization.remaining_budget:F2}}},");
            sb.Append($"\"total_iap_revenue\":{{\"doubleValue\":{p.monetization.total_iap_revenue:F2}}},");
            sb.Append($"\"ad_impressions_count\":{{\"integerValue\":\"{p.monetization.ad_impressions_count}\"}},");
            sb.Append($"\"total_ad_revenue\":{{\"doubleValue\":{p.monetization.total_ad_revenue:F2}}},");
            sb.Append($"\"total_user_arpu\":{{\"doubleValue\":{p.monetization.total_user_arpu:F2}}},");

            // Ad breakdown map
            sb.Append("\"ad_impressions_breakdown\":{\"mapValue\":{\"fields\":{");
            sb.Append($"\"respin_slot\":{{\"integerValue\":\"{p.monetization.ad_impressions_breakdown.respin_slot}\"}},");
            sb.Append($"\"revive_match\":{{\"integerValue\":\"{p.monetization.ad_impressions_breakdown.revive_match}\"}},");
            sb.Append($"\"double_gold\":{{\"integerValue\":\"{p.monetization.ad_impressions_breakdown.double_gold}\"}}");
            sb.Append("}}},");

            // Purchased items array
            sb.Append("\"purchased_items\":{\"arrayValue\":{\"values\":[");
            if (p.monetization.purchased_items != null && p.monetization.purchased_items.Count > 0)
            {
                for (int i = 0; i < p.monetization.purchased_items.Count; i++)
                {
                    var item = p.monetization.purchased_items[i];
                    sb.Append("{\"mapValue\":{\"fields\":{");
                    sb.Append($"\"item_id\":{{\"stringValue\":\"{Escape(item.item_id)}\"}},");
                    sb.Append($"\"item_type\":{{\"stringValue\":\"{Escape(item.item_type)}\"}},");
                    sb.Append($"\"price\":{{\"doubleValue\":{item.price:F2}}},");
                    sb.Append($"\"timestamp\":{{\"stringValue\":\"{Escape(item.timestamp)}\"}}");
                    sb.Append("}}}");
                    if (i < p.monetization.purchased_items.Count - 1) sb.Append(",");
                }
            }
            sb.Append("]}}");
            sb.Append("}}},");

            // FPS Combat Performance map
            sb.Append("\"fps_combat_performance\":{\"mapValue\":{\"fields\":{");
            sb.Append($"\"total_aim_shots_fired\":{{\"integerValue\":\"{p.fps_combat_performance.total_aim_shots_fired}\"}},");
            sb.Append($"\"total_aim_lockons_successful\":{{\"integerValue\":\"{p.fps_combat_performance.total_aim_lockons_successful}\"}},");
            sb.Append($"\"total_red_bullets_dodged\":{{\"integerValue\":\"{p.fps_combat_performance.total_red_bullets_dodged}\"}},");
            sb.Append($"\"total_yellow_bullets_parried\":{{\"integerValue\":\"{p.fps_combat_performance.total_yellow_bullets_parried}\"}},");
            sb.Append($"\"total_unparryable_fails\":{{\"integerValue\":\"{p.fps_combat_performance.total_unparryable_fails}\"}},");
            sb.Append($"\"total_damage_deflected\":{{\"integerValue\":\"{p.fps_combat_performance.total_damage_deflected}\"}}");
            sb.Append("}}},");

            // Match details array
            sb.Append("\"match_details\":{\"arrayValue\":{\"values\":[");
            if (p.match_details != null && p.match_details.Count > 0)
            {
                for (int i = 0; i < p.match_details.Count; i++)
                {
                    var m = p.match_details[i];
                    sb.Append("{\"mapValue\":{\"fields\":{");
                    sb.Append($"\"stage\":{{\"integerValue\":\"{m.stage}\"}},");
                    sb.Append($"\"outcome\":{{\"stringValue\":\"{Escape(m.outcome)}\"}},");
                    sb.Append($"\"turns\":{{\"integerValue\":\"{m.turns}\"}},");
                    sb.Append($"\"parry_count\":{{\"integerValue\":\"{m.parry_count}\"}},");
                    sb.Append($"\"dodge_count\":{{\"integerValue\":\"{m.dodge_count}\"}},");
                    sb.Append($"\"unparryable_fails\":{{\"integerValue\":\"{m.unparryable_fails}\"}},");
                    sb.Append($"\"ad_respin_count\":{{\"integerValue\":\"{m.ad_respin_count}\"}},");
                    sb.Append($"\"ad_revive_used\":{{\"booleanValue\":{m.ad_revive_used.ToString().ToLowerInvariant()}}}");
                    sb.Append("}}}");
                    if (i < p.match_details.Count - 1) sb.Append(",");
                }
            }
            sb.Append("]}}");

            sb.Append("}}");
            return sb.ToString();
        }

        private static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }
}
