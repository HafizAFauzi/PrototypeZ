# Entity Relationship Diagram (ERD) & Skema Telemetri Data
**Penelitian**: Komparasi Efektivitas Monetisasi Rewarded Ads dan In-App Purchase (Premium Cosmetics) pada Desain Game Hybrid-Casual Berdasarkan Analisis ARPU  
**Game**: *Lucky Duelist: Cyber Roulette (1v1 First-Person RNG Slot Battler)*  
**Perspektif Kamera**: *Full First-Person View (FPS)*  
**Peneliti**: Hafiz Ahmad Fauzi / 5224600067  

---

## 1. Konsep Pemodelan Data

Struktur data prototipe dirancang untuk merekam perilaku ekonomi pemain (*ARPU*, transaksi *IAP*, dan penayangan *Rewarded Ads*) berdampingan dengan performa refleks sudut pandang orang pertama (*First-Person Combat Stats*):
1. **Model Relasional (RDBMS Normalized)**: Standar integritas data untuk ekspor ke SPSS / Excel / Python guna uji beda statistik (*One-Way ANOVA* / $t$-test).
2. **Model Dokumen NoSQL (Firebase Firestore / Supabase REST)**: Pengiriman payload JSON terintegrasi di akhir sesi bermain secara *real-time*.

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
        int soft_currency_gold "Akumulasi koin hasil kemenangan duel"
        string equipped_skin_revolver "ID skin senjata revolver aktif"
        string equipped_skin_spinner "ID skin silinder mesin slot aktif"
        string equipped_skin_knife "ID skin pisau parry aktif"
    }

    SESSIONS {
        string session_id PK "UUID unik sesi bermain"
        string user_id FK "Relasi ke PLAYERS"
        datetime start_time "Waktu mulai sesi"
        datetime end_time "Waktu selesai sesi"
        float play_duration_seconds "Durasi responden bermain (detik)"
        int matches_completed "Jumlah duel yang diselesaikan (0-3)"
        int matches_won "Jumlah kemenangan duel"
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
        int dodge_red_count "Jumlah aksi Hindar [Q] peluru merah berhasil"
        int parry_yellow_count "Jumlah aksi Tangkis [E] peluru kuning berhasil"
        int unparryable_fail_count "Frekuensi salah tekan E saat peluru merah"
        int damage_deflected "Total damage balasan dari tebasan pisau parry"
        int respin_ad_used "Jumlah re-spin slot reel via Ads"
        boolean revive_ad_used "Apakah pemain menggunakan revive via Ads"
        boolean double_gold_ad_used "Apakah pemain melipatgandakan koin"
    }

    COSMETIC_ITEMS {
        string item_id PK "ID item kosmetik (misal: SKIN_REVOLVER_GOLD)"
        string item_type "WEAPON_REVOLVER, SPINNER, WEAPON_KNIFE"
        string item_name "Nama item di etalase katalog"
        float price_idr "Harga item (Rp 5.000 s/d Rp 15.000)"
        string asset_prefab_key "Kunci referensi prefab model/sprite di Unity"
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
| `equipped_skin_revolver`| VARCHAR(50) | ID skin senjata revolver di tangan kanan. |
| `equipped_skin_spinner` | VARCHAR(50) | ID skin silinder 3-reel slot amunisi. |
| `equipped_skin_knife` | VARCHAR(50) | ID skin pisau parry di tangan kiri. |

### B. Tabel `MATCH_LOGS` (Statistik Refleks FPS)
| Nama Kolom | Tipe Data | Keterangan & Validasi |
|---|---|---|
| `match_id` | VARCHAR(36) [PK] | UUID unik per ronde duel. |
| `session_id` | VARCHAR(36) [FK] | Relasi ke sesi bermain terkait. |
| `stage_level` | INT | Tingkat kesulitan: `1` (Rookie), `2` (Gambler), `3` (Boss). |
| `outcome` | ENUM | `'WIN'` atau `'LOSS'`. |
| `aim_lockon_success_count`| INT | Frekuensi tembakan auto-fire lock-on 1,0s berhasil. |
| `dodge_red_count` | INT | Frekuensi sukses menghindar [Q] saat peluru merah musuh meluncur. |
| `parry_yellow_count` | INT | Frekuensi sukses menangkis [E] saat peluru kuning musuh meluncur. |
| `unparryable_fail_count` | INT | Frekuensi salah menekan E saat serangan merah (gagal parry). |
| `damage_deflected` | INT | Total damage balasan ke bot dari aksi parry pisau. |
| `respin_ad_used` | INT | Frekuensi re-spin silinder revolver via Rewarded Ads. |
| `revive_ad_used` | BOOLEAN | Apakah responden menonton iklan untuk hidup kembali. |
| `double_gold_ad_used`| BOOLEAN | Apakah responden menonton iklan untuk menggandakan koin. |

### C. Tabel `COSMETIC_ITEMS` (Katalog Skin First-Person)
| Nama Kolom | Tipe Data | Keterangan & Validasi |
|---|---|---|
| `item_id` | VARCHAR(50) [PK] | Kode unik item (misal: `SKIN_REVOLVER_GOLD`). |
| `item_type` | ENUM | `'WEAPON_REVOLVER'`, `'SPINNER'`, `'WEAPON_KNIFE'`. |
| `item_name` | VARCHAR(100) | Nama display item di katalog toko. |
| `price_idr` | DECIMAL(10,2) | Harga item: `5000.00`, `10000.00`, atau `15000.00`. |
| `asset_prefab_key` | VARCHAR(100) | Kunci string untuk memuat aset model/material di Unity. |

---

## 4. Format Payload Dokumen NoSQL (Firebase Firestore / Supabase)

Dokumen dikirimkan pada akhir sesi ke collection `telemetry_sessions`:

```json
{
  "user_id": "RES-C-029",
  "group_id": "GROUP_C",
  "device_model": "Xiaomi Redmi Note 12 / Android 13",
  "created_at": "2026-11-01T17:10:00Z",
  "session": {
    "session_id": "c1f7b942-8e12-42da-921a-99d8e411b033",
    "play_duration_sec": 395.4,
    "matches_completed": 3,
    "matches_won": 3,
    "completed_stage_3": true,
    "soft_currency_gold": 550
  },
  "monetization": {
    "initial_budget": 20000.0,
    "remaining_budget": 0.0,
    "total_iap_revenue": 20000.0,
    "purchased_items": [
      {
        "item_id": "SKIN_REVOLVER_DAMASCUS",
        "item_type": "WEAPON_REVOLVER",
        "price": 10000.0,
        "timestamp": "2026-11-01T17:11:15Z"
      },
      {
        "item_id": "SKIN_KNIFE_TANTO",
        "item_type": "WEAPON_KNIFE",
        "price": 10000.0,
        "timestamp": "2026-11-01T17:11:40Z"
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
  "fps_combat_performance": {
    "total_aim_shots_fired": 11,
    "total_aim_lockons_successful": 11,
    "total_red_bullets_dodged": 4,
    "total_yellow_bullets_parried": 3,
    "total_unparryable_fails": 1,
    "total_damage_deflected": 120
  },
  "match_details": [
    {
      "stage": 1,
      "outcome": "WIN",
      "turns": 3,
      "parry_count": 1,
      "dodge_count": 1,
      "unparryable_fails": 0,
      "ad_respin_count": 0,
      "ad_revive_used": false
    },
    {
      "stage": 2,
      "outcome": "WIN",
      "turns": 5,
      "parry_count": 1,
      "dodge_count": 2,
      "unparryable_fails": 1,
      "ad_respin_count": 1,
      "ad_revive_used": false
    },
    {
      "stage": 3,
      "outcome": "WIN",
      "turns": 7,
      "parry_count": 1,
      "dodge_count": 1,
      "unparryable_fails": 0,
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
   Menggunakan **One-Way ANOVA** pada tingkat signifikansi $\alpha = 0,05$ untuk menguji signifikansi perbedaan antara Kelompok A, B, dan C.
