# Technical Specification Document (TSD)
**Penelitian**: Komparasi Efektivitas Monetisasi Rewarded Ads dan In-App Purchase (Premium Cosmetics) pada Desain Game Hybrid-Casual Berdasarkan Analisis ARPU  
**Game**: *Lucky Duelist: Cyber Roulette (1v1 First-Person RNG Slot Battler)*  
**Target Engine**: Unity (C#)  
**Perspektif Kamera**: *Full First-Person View (FPS)*  
**Peneliti**: Hafiz Ahmad Fauzi / 5224600067  

---

## 1. Arsitektur Perangkat Lunak & Kamera First-Person

Sistem aplikasi dibangun dengan struktur hierarki modular yang menghubungkan kamera sudut pandang orang pertama (*FPS Camera Rig*), kontroler senjata silinder (*Slot Revolver*), modul pertahanan refleks (*Dodge/Parry QTE*), serta mesin telemetri cloud.

```
┌────────────────────────────────────────────────────────────────────────────┐
│                    FIRST-PERSON PRESENTATION & UI LAYER                    │
│  ┌────────────────────────┐  ┌───────────────────────┐  ┌────────────────┐ │
│  │    FPS Camera Rig      │  │  Right Hand Weapon    │  │ Left Hand Knife│ │
│  │ (Camera Sway on Q/Recoi│  │ (Revolver + 3-Spinner)│  │ (Parry on E)   │ │
│  └───────────▲────────────┘  └──────────▲────────────┘  └────────▲───────┘ │
│              │                          │                        │         │
│  ┌───────────┴──────────────────────────┴────────────────────────┴───────┐ │
│  │                 Canvas UI (Bottom-Left Minimalist HUD)                │ │
│  │  - Health Bar & Status                                                │ │
│  │  - 2 Hold Slots & Spin / Re-spin Ad Button                            │ │
│  │  - Aim Lock-On Meter & Target Frame Overlay                           │ │
│  └──────────────────────────────────────▲────────────────────────────────┘ │
└─────────────────────────────────────────┼──────────────────────────────────┘
                                          │
┌─────────────────────────────────────────┴──────────────────────────────────┐
│                        CORE APPLICATION MANAGERS                           │
│  ┌───────────────────────┐  ┌───────────────────────┐  ┌────────────────┐  │
│  │      GameManager      │  │   ExperimentManager   │  │  DummyAdPlayer │  │
│  │ (Master State Machine)│  │ (A/B Feature-Gating)  │  │(Countdown 10s) │  │
│  └───────────▲───────────┘  └──────────▲────────────┘  └────────▲───────┘  │
│              │                         │                        │          │
│  ┌───────────▼─────────────────────────┴────────────────────────▼────────┐ │
│  │                            CombatManager                              │ │
│  │  - SlotRevolverController (Putaran 3-Reel Spinner di Silinder Senjata)│ │
│  │  - AimStabilizerController (Perlin Drift & 1s Lock-On Auto-Fire)      │ │
│  │  - DefenseQTEController (Peluru Merah Unparryable vs Kuning Parryable)│ │
│  │  - BotGunnerAI (Pola Tembakan Pistol Musuh: Stage 1 s/d Stage 3)      │ │
│  └─────────────────────────────────────▲─────────────────────────────────┘ │
└────────────────────────────────────────┼───────────────────────────────────┘
                                         │
┌────────────────────────────────────────┴───────────────────────────────────┐
│                    TELEMETRY & NETWORKING LAYER                            │
│  ┌──────────────────────────────────────────────────────────────────────┐  │
│  │                  TelemetryService (UnityWebRequest)                  │  │
│  │  - Serialisasi JSON (Session, Ad Impressions, IAP, FPS Combat Stats) │  │
│  │  - Fallback Lokal: Application.persistentDataPath/pending_logs.json  │  │
│  └─────────────────────────────────────┬────────────────────────────────┘  │
└────────────────────────────────────────┼───────────────────────────────────┘
                                         ▼
┌────────────────────────────────────────────────────────────────────────────┐
│           CLOUD BACKEND (Firebase Firestore / Supabase REST API)           │
└────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Rincian Modul & Kelas Teknis C#

### 2.1. `FPSCameraController` & Tangan Karakter
* **Tanggung Jawab**: Mengatur kamera pandangan pemain, efek goncangan (*camera shake*) saat menembak, dan efek *camera sway* saat melakukan aksi Hindar (Q).
* **Mekanisme Gerakan Kamera**:
  * **Idle Breathing**: Osilasi sinusoidal halus pada posisi kamera.
  * **Dodge Sway (Tombol Q)**: Ketika tombol Q ditekan, kamera bergerak cepat miring ke kiri/kanan sebesar $-15^\circ$ (rotasi roll) dan bergeser secara lateral sejauh 1,2 unit selama 0,4 detik sebelum kembali ke posisi semula.
  * **Fire Recoil**: Kamera menghentak ke atas sebesar $+3^\circ$ dan perlahan kembali (*smooth damp*).

---

### 2.2. `SlotRevolverController` (Silinder Mesin Slot Diegetik)
* **Tanggung Jawab**: Mengendalikan model 3D/2.5D senjata Revolver di tangan kanan pemain serta memutar animasi 3 gulungan silinder amunisi.
* **Tabel Probabilitas Simbol**:
  ```csharp
  public enum SlotSymbol {
      ATTACK,           // 35% Bobot -> Mengisi peluru aktif
      MARK,             // 20% Bobot -> Debuff 2x Critical Damage pada musuh
      HEAL,             // 20% Bobot -> Injeksi +20 HP
      COUNTER_SHARPEN,  // 15% Bobot -> Audio cue kokangan & ekstra parry damage
      WILD_JOKER        // 10% Bobot -> Pilihan bebas pemain
  }
  ```
* **Mekanisme Alur Silinder**:
  1. *Spin Animation*: Saat tombol "Putar Silinder" ditekan, silinder revolver berputar dengan efek suara mekanis (*click-clack-clack*).
  2. *Re-spin 1 Reel via Ad*: Jika pemain berada di Group A atau C, pemain dapat memicu *DummyAdPlayer* (10 detik) untuk memutar ulang salah satu reel yang tidak diinginkan.
  3. *Hold Slots Management*: 2 kotak cadangan di HUD pojok kiri bawah menyimpan simbol untuk turn berikutnya.

---

### 2.3. `AimStabilizerController` (Mini-Game Bidik & Auto-Fire)
* **Tanggung Jawab**: Mengatur kotak target di atas musuh, mengkalkulasi gaya goyangan (*drift*), memproses *counter-drag* pemain, dan menembak otomatis saat *Lock-On* 100%.
* **Kalkulasi Vektor Bidikan**:
  1. **Drift Vektor**:
     $$\vec{v}_{\text{drift}} = \text{Vector2}\big(\text{Mathf.PerlinNoise}(t \cdot f, 0) - 0.5f, \; \text{Mathf.PerlinNoise}(0, t \cdot f) - 0.5f\big) \times \text{Speed}$$
  2. **Vektor Input Pemain (*Counter-Drag*)**:
     $$\vec{v}_{\text{player}} = -\text{dragInputVector} \times \text{Sensitivity}$$
  3. **Posisi Reticle Total**:
     $$\vec{P}_{\text{reticle}}(t+\Delta t) = \vec{P}_{\text{reticle}}(t) + (\vec{v}_{\text{drift}} + \vec{v}_{\text{player}}) \times \Delta t$$
  4. **Akumulasi Lock-On**:
     * Jika $\|\vec{P}_{\text{reticle}} - \vec{P}_{\text{vital\_circle}}\| \le R_{\text{vital}}$:
       $$\text{lockOnTimer} += \Delta t$$
     * Begitu $\text{lockOnTimer} \ge 1.0\text{s}$:
       Senjata meletus secara otomatis!
       * Jika musuh memiliki status `[MARK]`: Menghasilkan $2 \times \text{Base Damage}$ (30 DMG) + efek partikel ledakan crit.
       * Jika kombo 2x atau 3x *Attack*: Indikator langsung mereset dan melanjutkan pengisian tembakan berikutnya secara beruntun (*chaining*).

---

### 2.4. `DefenseQTEController` (Pistol Musuh: Merah Unparryable vs Kuning Parryable)
* **Tanggung Jawab**: Mengatur tembakan pistol musuh, membedakan jenis peluru merah vs kuning, memutar audio peringatan, dan memproses tangkisan pisau tangan kiri atau hindar kamera.
* **Struktur Tipe Serangan**:
  ```csharp
  public enum EnemyAttackType {
      UNPARRYABLE_RED,    // Moncong Merah -> Peluru Merah -> WAJIB HINDAR [Q]
      PARRYABLE_YELLOW    // Moncong Kuning -> Peluru Kuning -> BISA PARRY [E]
  }
  ```
* **Logika Resolusi Pertahanan**:
  ```mermaid
  sequenceDiagram
      autonumber
      participant Bot as Musuh (Pistol)
      participant UI as Visual Moncong & Peluru
      participant Player as Input (Q / E)
      participant Cam as FPS Camera / Knife
      participant Combat as CombatManager

      Bot->>UI: Membidik Kamera Pemain
      alt Ada Buff COUNTER/SHARPEN
          UI->>Player: Mainkan Audio Cue Peringatan ("Ching-Ching / Kokangan")
      end

      alt Serangan Merah (Unparryable)
          UI->>UI: Moncong Menyala Cahaya Merah & Peluru Merah Meluncur
          alt Pemain Tekan "Q" (Hindar)
              Player->>Cam: Kamera Tersentak Miring ke Samping
              Cam->>Combat: Peluru Lewat di Samping Kamera -> Damage = 0
          else Pemain Tekan "E" (Salah Aksi) atau Terlambat
              Combat->>Player: Pisau Gagal Menahan! Kena Damage Penuh (-25 HP)
          end
      else Serangan Kuning (Parryable)
          UI->>UI: Moncong Menyala Kilatan Kuning & Peluru Kuning Meluncur
          alt Pemain Tekan "E" Presisi (~0.2-0.3s)
              Player->>Cam: Tangan Kiri Mengayunkan Pisau ke Tengah Layar
              Cam->>Combat: SFX 'CLANG!' + Percikan Bunga Api Neon
              Combat->>Bot: Peluru Berbalik! Kena Critical Deflection Slash (35-45 Damage)
          else Pemain Tekan "Q" (Hindar Aman)
              Player->>Cam: Kamera Miring Menghindar -> Damage = 0
          else Terlambat / Tidak Tekan
              Combat->>Player: Kena Tembakan (-25 HP)
          end
      end
  ```

---

### 2.5. `MonetizationManager`
* **Sub-komponen `DummyAdPlayer`**:
  * Menampilkan kanvas overlay video simulasi fullscreen berdurasi **10 detik**.
  * Memberikan kompensasi nilai kontribusi iklan terstandarisasi:
    $$\text{Ad Revenue} = \text{Rp } 250,00$$
* **Sub-komponen `VirtualIAPStore`**:
  * Mengelola saldo kuota eksperimen sebesar **Rp 20.000,00**.
  * Menyimpan status kepemilikan katalog kosmetik:
    * `SKIN_REVOLVER` (Cyber Damascus Revolver, Golden Revolver).
    * `SKIN_SPINNER` (Neon Casino Reels, Retro 1984 Spinner).
    * `SKIN_KNIFE` (Cyber Tantō Knife, Viper Dagger).
  * Menghubungkan aset visual 3D/sprite pada *FPS Weapon Rig* melalui modul `CosmeticApplier`.

---

### 2.6. `TelemetryService` (Perekam & Sinkronisasi Cloud)
* **Tanggung Jawab**: Mencatat setiap ronde duel, impresi iklan, transaksi kosmetik, serta statistik keterampilan FPS pemain ke dalam dokumen JSON dan mengirimkannya ke REST API Firestore / Supabase.
* **Metrik Spesifik FPS yang Dicatat**:
  * `aim_shots_fired`: Total tembakan meletus.
  * `aim_lockons_successful`: Frekuensi lock-on 1,0s penuh.
  * `red_bullets_dodged`: Frekuensi sukses menghindar peluru merah dengan tombol Q.
  * `yellow_bullets_parried`: Frekuensi sukses menangkis peluru kuning dengan tombol E.
  * `unparryable_fails`: Frekuensi pemain salah menekan E saat serangan merah.
  * `total_damage_deflected`: Akumulasi damage yang dikembalikan ke bot via parry pisau.

---

## 3. Tata Letak UI & Konfigurasi Canvas (HUD Pojok Kiri Bawah)

* **Anchor Setting**: `Bottom-Left` (`Min(0,0)`, `Max(0,0)`).
* **Komponen HUD**:
  * `HealthBarSlider`: Nilai 0 s/d 100 dengan teks dinamik `"HP: 80/100"`.
  * `HoldSlotContainer`: Menampung 2 ikon tombol kartu cadangan.
  * `SpinButton`: Tombol utama pemicu putaran silinder revolver.
  * `RespinAdButton`: Tombol bertanda ikon video clapper/ad untuk re-spin 1 reel (hanya tampil pada Group A dan C).
  * `DefensePromptContainer`: Indikator visual tombol `[Q: HINDAR]` dan `[E: PARRY]` yang memandu pemain baru.
