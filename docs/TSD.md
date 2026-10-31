# Technical Specification Document (TSD)
**Judul Game**: *Lucky Duelist: Cyber Roulette*  
**Engine & Tools**: Unity 2022.3.48f1 LTS, C#, TextMeshPro, Universal Render Pipeline (URP)  
**Arsitektur Desain**: *Modular Event-Driven Architecture (EventBus)* & *State Pattern*  
**Target Platform**: Android (Vulkan / OpenGL ES 3.0) & PC Standalone  

---

## 1. Arsitektur Sistem Tingkat Tinggi (System Architecture)

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
│  │  - Health Bar & Status (TextMeshPro)                                  │ │
│  │  - 3-Reel Cylinder Cards & Action Buttons                             │ │
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
* **Tanggung Jawab**: Mengendalikan model senjata Revolver di tangan kanan pemain serta memutar animasi 3 gulungan silinder amunisi.
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
  3. *Direct Action Resolution*: Seluruh 3 reel silinder langsung dieksekusi secara berurutan pada giliran aktif (tanpa sistem hold).

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

---

### 2.4. `DefenseQTEController` (Parry Pisau & Hindar Proyektil)
* **Tanggung Jawab**: Mendeteksi waktu input pertahanan pemain saat proyektil musuh mendekati kamera.
* **Window Waktu Respon**:
  * **Tombol Q (Dodge)**: Efektif pada rentang 0,3 detik sebelum proyektil merah mencapai titik temu. Memicu animasi *camera sway* dan flag *Invulnerable* selama 0,4 detik.
  * **Tombol E (Parry)**: Efektif pada rentang ketat (0,15 detik) sebelum proyektil kuning menabrak. Memicu animasi tebas pisau tangan kiri, efek suara 'CLANG!', dan partikel pendaran percikan api.

---

### 2.5. `ExperimentManager` (Feature-Gating & Persistensi Riset)
* **Tanggung Jawab**: Mengatur penugasan ID responden deterministik, aktivasi fitur berdasarkan kelompok eksperimen (Group A, B, C), dan penyimpanan lokal via `PlayerPrefs`.
* **State Matriks Eksperimen**:
  ```csharp
  public enum ExperimentGroup {
      GroupA_DominantAds,   // Ads On (Respin, Revive, Double Gold), Store Off
      GroupB_DominantIAP,   // Ads Off, Store On (Initial Wallet Rp 20.000)
      GroupC_Hybrid         // Ads On, Store On (Initial Wallet Rp 20.000)
  }
  ```

---

### 2.6. `TelemetryService` & Cloud Logging
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
  * `ReelsContainer`: Menampung 3 kartu reel silinder.
  * `SpinButton`: Tombol utama pemicu putaran silinder revolver.
  * `RespinAdButton`: Tombol bertanda ikon video clapper/ad untuk re-spin 1 reel (hanya tampil pada Group A dan C).
  * `DefensePromptContainer`: Indikator visual tombol `[Q: HINDAR]` dan `[E: PARRY]` yang memandu pemain baru.
