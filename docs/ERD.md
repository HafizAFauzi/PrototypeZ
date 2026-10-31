# Entity Relationship Diagram (ERD) & Skema Telemetri Data
**Penelitian**: Komparasi Efektivitas Monetisasi Rewarded Ads dan In-App Purchase (Premium Cosmetics) pada Desain Game Hybrid-Casual Berdasarkan Analisis ARPU  
**Game**: *Lucky Duelist: Cyber Roulette (1v1 Hybrid-Casual RNG Slot with Action Timing)*  
**Peneliti**: Hafiz Ahmad Fauzi / 5224600067  

---

## 1. Konsep Pemodelan Data

Struktur data prototipe dirancang untuk memenuhi dua kebutuhan utama penelitian:
1. **Model Relasional (RDBMS Normalized)**: Digunakan untuk struktur database analitik, kamus data yang terstandarisasi, dan kemudahan ekspor ke file CSV/Excel untuk uji hipotesis statistik (*One-Way ANOVA* / $t$-test).
2. **Model Dokumen NoSQL (Firebase Firestore / Supabase REST)**: Digunakan oleh Unity Client untuk mengirimkan log sesi terpadu (*unified session payload*) secara *real-time* via HTTP POST.

---

## 2. Entity Relationship Diagram (Mermaid)

```mermaid
erDiagram
    EXPERIMENTAL_GROUPS ||--o{ PLAYERS : "assigned to"
    PLAYERS ||--o{ SESSIONS : "participates in"
    PLAYERS ||--o{ IAP_TRANSACTIONS : "purchases"
    PLAYERS ||--o| SURVEY_RESPONSES : "fills out"
    SESSIONS ||--o{ MATCH_LOGS : "contains"
    SESSIONS ||--o{ AD_IMPRESSIONS : "triggers"
    COSMETIC_ITEMS ||--o{ IAP_TRANSACTIONS : "bought in"

    EXPERIMENTAL_GROUPS {
        string group_id PK "GROUP_A, GROUP_B, GROUP_C"
        string group_name "Dominan Ads / Dominan IAP / Hybrid"
        boolean ads_enabled "Status aktif tombol Rewarded Ads"
        boolean iap_enabled "Status aktif etalase IAP Cosmetics"
        float fixed_initial_budget "Saldo awal eksperimen (Rp 20.000)"
    }

    PLAYERS {
        string user_id PK "UUID unik responden / Tester ID"
        string group_id FK "Relasi ke EXPERIMENTAL_GROUPS"
        datetime created_at "Waktu pertama kali membuka game"
        float initial_wallet_balance "Saldo kuota awal (Rp 20.000)"
        float current_wallet_balance "Sisa saldo kuota eksperimen"
        int soft_currency_gold "Akumulasi koin hasil kemenangan match"
        string equipped_skin_body "ID skin avatar yang digunakan"
        string equipped_skin_gun "ID skin senjata tembak yang digunakan"
        string equipped_skin_knife "ID skin pisau parry yang digunakan"
        string equipped_skin_slot "ID skin tema mesin slot yang digunakan"
    }

    SESSIONS {
        string session_id PK "UUID unik sesi bermain"
        string user_id FK "Relasi ke PLAYERS"
        datetime start_time "Waktu mulai sesi"
        datetime end_time "Waktu selesai sesi"
        float play_duration_seconds "Durasi responden bermain (detik)"
        int matches_completed "Jumlah duel yang diselesaikan (0-3)"
        int matches_won "Jumlah kemenangan match"
        int total_ad_count "Total ad ditonton selama sesi"
        float total_ad_revenue "Total pendapatan iklan sesi (Rp)"
        float total_iap_revenue "Total belanja skin kosmetik (Rp)"
        float session_arpu "Total pendapatan ARPU user (Ad + IAP)"
        boolean completed_stage_3 "Status tamat mengalahkan Boss"
    }

    MATCH_LOGS {
        string match_id PK "UUID per duel pertarungan"
        string session_id FK "Relasi ke SESSIONS"
        int stage_level "1 (Rookie), 2 (Gambler), 3 (Cyber Baron)"
        int turns_count "Total giliran bertarung"
        int player_final_hp "Sisa HP pemain saat match berakhir"
        int bot_final_hp "Sisa HP bot saat match berakhir"
        string outcome "WIN atau LOSS"
        int aim_lockon_success_count "Jumlah tembakan lock-on berhasil"
        int dodge_success_count "Jumlah aksi Hindar (Q) berhasil"
        int perfect_parry_count "Jumlah aksi Tangkis Pisau (E) berhasil"
        int damage_deflected "Total damage balasan dari aksi parry"
        int respin_ad_used "Jumlah re-spin slot via Ads"
        boolean revive_ad_used "Apakah pemain menggunakan revive via Ads"
        boolean double_gold_ad_used "Apakah pemain melipatgandakan koin"
    }

    COSMETIC_ITEMS {
        string item_id PK "ID item kosmetik (misal: SKIN_KNIFE_DAMASCUS)"
        string item_type "WEAPON_GUN, WEAPON_KNIFE, BODY, SLOT_THEME"
        string item_name "Nama item di etalase katalog"
        float price_idr "Harga item (Rp 5.000 s/d Rp 15.000)"
        string asset_sprite_key "Kunci referensi sprite asset di Unity"
        string rarity "COMMON, RARE, EPIC"
    }

    IAP_TRANSACTIONS {
        string transaction_id PK "UUID transaksi pembelian"
        string session_id FK "Relasi ke SESSIONS"
        string user_id FK "Relasi ke PLAYERS"
        string item_id FK "Relasi ke COSMETIC_ITEMS"
        float amount_paid "Nominal transaksi terpotong (Rp)"
        datetime transaction_time "Waktu pembelian dilakukan"
    }

    AD_IMPRESSIONS {
        string ad_id PK "UUID impresi penayangan iklan"
        string session_id FK "Relasi ke SESSIONS"
        string user_id FK "Relasi ke PLAYERS"
        string ad_placement "RESPIN_SLOT, REVIVE_MATCH, DOUBLE_GOLD"
        int duration_seconds "Durasi video simulasi (10 detik)"
        float ecpm_rate_idr "Patokan eCPM per impresi (Rp 250,00)"
        float revenue_earned "Revenue tercatat (Rp 250,00)"
        datetime timestamp "Waktu iklan selesai ditonton"
    }

    SURVEY_RESPONSES {
        string survey_id PK "UUID respon evaluasi kuesioner"
        string user_id FK "Relasi ke PLAYERS"
        float pam_score_mean "Skor rerata Perceived Aggressive Monetization"
        float ux_score_mean "Skor rerata User Experience (Skala Likert 1-5)"
        string feedback_comment "Komentar subjektif responden"
        datetime submitted_at "Waktu pengisian kuesioner selesai"
    }
```

---

## 3. Kamus Data Kolom Terpilih

### A. Tabel `PLAYERS`
| Nama Kolom | Tipe Data | Keterangan & Validasi |
|---|---|---|
| `user_id` | VARCHAR(36) [PK] | UUID otomatis atau kode tester (misal: `R001-C`). |
| `group_id` | VARCHAR(10) [FK] | Referensi ke kelompok: `'GROUP_A'`, `'GROUP_B'`, `'GROUP_C'`. |
| `initial_wallet_balance`| DECIMAL(10,2) | Saldo kuota eksperimen awal: `20000.00`. |
| `current_wallet_balance`| DECIMAL(10,2) | Sisa saldo setelah responden membeli skin di toko. |
| `soft_currency_gold` | INT | Koin emas hasil memenangkan duel. |
| `equipped_skin_gun` | VARCHAR(50) | ID skin senjata tembak aktif. |
| `equipped_skin_knife` | VARCHAR(50) | ID skin pisau parry aktif. |
| `equipped_skin_body` | VARCHAR(50) | ID skin avatar duelis aktif. |
| `equipped_skin_slot` | VARCHAR(50) | ID tema bingkai mesin slot aktif. |

### B. Tabel `MATCH_LOGS` (Statistik Aksi Real-Time)
| Nama Kolom | Tipe Data | Keterangan & Validasi |
|---|---|---|
| `match_id` | VARCHAR(36) [PK] | UUID unik per ronde pertarungan. |
| `session_id` | VARCHAR(36) [FK] | Relasi ke sesi bermain terkait. |
| `stage_level` | INT | Tingkat kesulitan: `1` (Rookie), `2` (Gambler), `3` (Boss). |
| `outcome` | ENUM | `'WIN'` atau `'LOSS'`. |
| `aim_lockon_success_count`| INT | Frekuensi tembakan berhasil (Lock-On 1.0s penuh). |
| `dodge_success_count`| INT | Frekuensi tombol Q (Hindar) ditekan tepat waktu. |
| `perfect_parry_count`| INT | Frekuensi tombol E (Parry Pisau) ditekan tepat di fase kuning. |
| `damage_deflected` | INT | Total akumulasi damage serangan balik dari aksi parry. |
| `respin_ad_used` | INT | Frekuensi re-spin slot reel via Rewarded Ads. |
| `revive_ad_used` | BOOLEAN | Apakah responden menonton iklan untuk hidup kembali. |
| `double_gold_ad_used`| BOOLEAN | Apakah responden menonton iklan untuk menggandakan koin. |

### C. Tabel `COSMETIC_ITEMS`
| Nama Kolom | Tipe Data | Keterangan & Validasi |
|---|---|---|
| `item_id` | VARCHAR(50) [PK] | Kode unik item (misal: `SKIN_KNIFE_DAMASCUS`). |
| `item_type` | ENUM | `'WEAPON_GUN'`, `'WEAPON_KNIFE'`, `'BODY'`, `'SLOT_THEME'`. |
| `item_name` | VARCHAR(100) | Nama display item di katalog toko. |
| `price_idr` | DECIMAL(10,2) | Harga item: `5000.00`, `10000.00`, atau `15000.00`. |
| `asset_sprite_key` | VARCHAR(100) | Kunci string untuk memuat aset sprite di Unity. |

---

## 4. Format Payload Dokumen NoSQL (Firebase Firestore / Supabase)

Data dikirimkan di akhir sesi bermain sebagai satu dokumen terstruktur ke koleksi `telemetry_sessions`:

```json
{
  "user_id": "RES-C-021",
  "group_id": "GROUP_C",
  "device_model": "Xiaomi Redmi Note 12 / Android 13",
  "created_at": "2026-11-01T14:20:00Z",
  "session": {
    "session_id": "a3b9c8e1-7d24-4f89-912a-88f2e411b092",
    "play_duration_sec": 385.2,
    "matches_completed": 3,
    "matches_won": 3,
    "completed_stage_3": true,
    "soft_currency_gold": 500
  },
  "monetization": {
    "initial_budget": 20000.0,
    "remaining_budget": 0.0,
    "total_iap_revenue": 20000.0,
    "purchased_items": [
      {
        "item_id": "SKIN_KNIFE_DAMASCUS",
        "item_type": "WEAPON_KNIFE",
        "price": 10000.0,
        "timestamp": "2026-11-01T14:21:10Z"
      },
      {
        "item_id": "SKIN_GUN_NEON_BLASTER",
        "item_type": "WEAPON_GUN",
        "price": 10000.0,
        "timestamp": "2026-11-01T14:21:40Z"
      }
    ],
    "ad_impressions_count": 3,
    "ad_impressions_breakdown": {
      "respin_slot": 1,
      "revive_match": 1,
      "double_gold": 1
    },
    "total_ad_revenue": 750.0,
    "total_user_arpu": 20750.0
  },
  "combat_performance": {
    "total_aim_shots_fired": 9,
    "total_aim_lockons_successful": 9,
    "total_dodges_successful": 4,
    "total_perfect_parries": 3,
    "total_damage_deflected": 115
  },
  "match_details": [
    {
      "stage": 1,
      "outcome": "WIN",
      "turns": 3,
      "parry_count": 1,
      "dodge_count": 1,
      "ad_respin_count": 0,
      "ad_revive_used": false
    },
    {
      "stage": 2,
      "outcome": "WIN",
      "turns": 5,
      "parry_count": 1,
      "dodge_count": 2,
      "ad_respin_count": 1,
      "ad_revive_used": false
    },
    {
      "stage": 3,
      "outcome": "WIN",
      "turns": 7,
      "parry_count": 1,
      "dodge_count": 1,
      "ad_respin_count": 0,
      "ad_revive_used": true
    }
  ]
}
```

---

## 5. Formula Analisis Data untuk Uji Beda Statistik (ANOVA)

Data telemetri yang terkumpul dari seluruh responden akan diagregasikan berdasarkan perlakuan kelompok:

1. **Rerata ARPU Iklan ($\overline{ARPU}_{Ads}$)**:
   $$\overline{ARPU}_{Ads} = \frac{\sum_{i=1}^{N_g} \text{total\_ad\_revenue}_i}{N_g}$$

2. **Rerata ARPU Belanja Kosmetik IAP ($\overline{ARPU}_{IAP}$)**:
   $$\overline{ARPU}_{IAP} = \frac{\sum_{i=1}^{N_g} \text{total\_iap\_revenue}_i}{N_g}$$

3. **Total Rerata Pendapatan per User ($\overline{ARPU}_{Total}$)**:
   $$\overline{ARPU}_{Total} = \overline{ARPU}_{Ads} + \overline{ARPU}_{IAP}$$

4. **Uji Hipotesis**:
   Menggunakan **One-Way ANOVA** pada tingkat signifikansi $\alpha = 0,05$ untuk menguji hipotesis:
   * $H_0: \mu_{ARPU\_A} = \mu_{ARPU\_B} = \mu_{ARPU\_C}$ (Tidak terdapat perbedaan efektivitas pendapatan antar skema monetisasi).
   * $H_1$: Setidaknya terdapat satu pasang kelompok yang memiliki perbedaan nilai ARPU secara signifikan.
