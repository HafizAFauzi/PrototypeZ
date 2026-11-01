# Game Design Document (GDD)
**Judul Game**: *Lucky Duelist: Cyber Roulette*  
**Genre**: *Hybrid-Casual 1v1 First-Person RNG Slot Battler with Action Timing*  
**Perspektif Kamera**: *Full First-Person View (FPS)*  
**Platform**: Android (Mobile) & PC  
**Peneliti / Desainer**: Hafiz Ahmad Fauzi / 5224600067  
**Fokus Penelitian**: Komparasi Efektivitas Monetisasi Rewarded Ads vs In-App Purchase (Premium Cosmetics) Berdasarkan Analisis ARPU  

---

## 1. Visi Game & Perspektif First-Person (High Concept)

### 1.1. Ringkasan Eksekutif
*Lucky Duelist: Cyber Roulette* adalah game aksi duel 1v1 berkecepatan tinggi dari **sudut pandang orang pertama (First-Person / FPS)**. Layar adalah mata pemain:
* **Tangan Kanan**: Memegang **Revolver Taktis** di mana silinder amunisinya berupa **Mesin Slot 3-Reel (*Diegetic Slot Spinner*)** yang berputar setiap giliran.
* **Tangan Kiri**: Memegang **Pisau Tempur (*Parry Knife*)** yang siap diangkat ke tengah layar untuk menangkis peluru lawan.
* **Musuh di Depan**: Berdiri di seberang ruangan/arena kasino cyberpunk, membidikkan pistol langsung ke arah kamera pemain.

Game ini memadukan sensasi undian kasino kasual (*slot machine roll & re-spin*) dengan refleks mikro *first-person*:
1. **Fase Menyerang**: Membidik kotak target melayang di atas tubuh musuh, menahan goyangan reticle (*counter-drag*) selama 1 detik hingga *Lock-On* 100%, lalu melepaskan tembakan otomatis.
2. **Fase Bertahan**: Mengantisipasi tembakan pistol musuh. Menghindari peluru merah tak tertangkis dengan **[Q] Hindar** (kamera tersentak/miring ke samping) atau menebas peluru kuning dengan **[E] Tangkis Pisau** (percikan 'CLANG!' yang membalikkan proyektil ke musuh).

```
┌────────────────────────────────────────────────────────────────────────┐
│                        CORE LOOP (FIRST-PERSON)                        │
│                                                                        │
│    ┌──────────────┐     ┌──────────────┐     ┌──────────────────┐      │
│    │  Kunjungi    │ ──► │  Pilih Stage │ ──► │ Putar Silinder   │      │
│    │  Toko Skin   │     │  (1 s/d 3)   │     │ (Slot Revolver)  │      │
│    └──────────────┘     └──────────────┘     └────────┬─────────┘      │
│           ▲                                           │                │
│           │                                           ▼                │
│    ┌──────┴───────┐     ┌──────────────┐     ┌──────────────────┐      │
│    │ Koin Menang  │ ◄── │ Fase Bertahan│ ◄── │  Fase Menyerang  │      │
│    │(Double via Ad│     │ (Dodge/Parry)│     │(FPS Aim Lock-On) │      │
│    └──────────────┘     └──────────────┘     └──────────────────┘      │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Mekanika Gameplay Inti (Core Mechanics)

### 2.1. Parameter Pertarungan
* **Player HP**: 100 / 100.
* **Kondisi Menang**: Menghabisi HP musuh di hadapan pemain hingga 0.
* **Kondisi Kalah**: HP pemain mencapai 0 (memicu penawaran *Second-Chance Revive via Rewarded Ad* pada Group A dan C).

---

### 2.2. Komposisi 5 Simbol Silinder Revolver (Slot Deck)
Setiap giliran, silinder revolver berputar mengacak 3 simbol aksi:

| Simbol | Ikon | Peluang | Efek & Peran Mekanis |
|---|---|---|---|
| **ATTACK** | ⚔ Peluru Blaster | 35% | Mengisi peluru aktif; memicu fase bidik FPS (*Aim Stabilizer*). |
| **MARK** | 🎯 Target Reticle | 20% | Menandai tubuh musuh; memperbesar lingkaran vital dan melipatgandakan *Critical Damage* tembakan (2x lipat). |
| **HEAL** | 💉 Nano-Medkit | 20% | Pemulihan instan +20 HP ke pemain. |
| **COUNTER / SHARPEN** | 🗡 Asah Pisau | 15% | Mengasah pisau parry; memunculkan *Audio Cue Warning* ("Ching-Ching / Kokangan Senjata") sebelum musuh menembak dan menambah *True Damage* saat parry sukses. |
| **WILD / JOKER** | ⭐ Bintang Kasino | 10% | Kartu langka (Rare); pemain bebas memilih simbol pengganti apa pun. |

---

### 2.3. Sistem Eksekusi 3-Reel Langsung (Direct Action Resolution)
Terletak pada HUD minimalis di pojok kiri bawah:
1. Setiap giliran, pemain menekan tombol **"PUTAR SILINDER"** untuk mengacak ketiga reel silinder revolver.
2. Pemain dapat memanfaatkan fitur **Full Re-spin 3-Reel via Rewarded Ads** (1x per giliran, aktif pada Group A & C) jika ingin memutar ulang ketiga reel silinder sekaligus untuk mendapatkan kombinasi simbol yang lebih menguntungkan (*second-chance re-roll*).
3. Seluruh 3 simbol yang terkunci pada silinder langsung dieksekusi secara berurutan saat pemain menekan tombol **"EKSEKUSI GILIRAN"** (*Direct 3-Reel Action Resolution*).

---

### 2.4. Fase Menyerang: FPS Aim Stabilizer & Auto-Fire Lock-On
Saat simbol `[ATTACK]` aktif, kamera memfokuskan bidikan ke musuh:

```
┌─────────────────────────────────────────────────────────────┐
│ [PAUSE]                                                     │
│                                                             │
│             ┌──────────────────────┐                        │
│             │   [KOTAK TARGET]     │                        │
│             │   ┌──────────────┐   │                        │
│             │   │(Lingkaran O) │   │                        │
│             │   └──────────────┘   │                        │
│             └──────────────────────┘                        │
│                                                             │
│                   (+) Reticle [Drift]                       │
│                        │ [Counter-Drag]                     │
│                        ▼                                    │
│                                                             │
│                                      [REVOLVER DENGAN]      │
│ [HUD KIRI BAWAH]                     [3-REEL SPINNER ]      │
│ HP: [████████░░] 80/100              [DI SILINDER SENJATA]  │
│ LOCK-ON: [██████████] 100% (AUTO-FIRE!)                     │
└─────────────────────────────────────────────────────────────┘
```

1. **Kotak Target & Lingkaran Vital**:
   * Sebuah kotak target mengambang di atas tubuh musuh (seperti referensi luk4dev).
   * Di dalam kotak, terdapat **Lingkaran Vital (*Sweet Spot*)** di area kepala atau jantung.
2. **Kontrol *Counter-Drag***:
   * Reticle bidik bergerak meluncur liar secara acak (*Perlin noise drift*).
   * Pemain menggeser/drag mouse atau layar sentuh ke arah berlawanan untuk mengarahkan reticle ke dalam lingkaran vital.
3. **Mekanisme *Auto-Fire Lock-On (1,0 Detik)***:
   * Menahan reticle di dalam lingkaran vital mengisi meteran *Lock-On* (0% $\rightarrow$ 100% dalam 1,0 detik).
   * Begitu mencapai 100%, revolver **otomatis meletuskan tembakan** dengan akurasi sempurna!
   * Jika pemain memiliki kombo 2x/3x *Attack*, tembakan pertama langsung disusul pengisian *lock-on* berikutnya secara beruntun (*rapid burst*).
   * Jika musuh terkena status `[MARK]`, tembakan yang meletus menghasilkan *Critical Damage* masif.

---

### 2.5. Fase Bertahan: Dual-Defense QTE
Setelah giliran menyerang pemain selesai, giliran musuh membalas tembakan. Pemain bereaksi berdasarkan warna proyektil musuh:

1. **Peluru Merah (*Unparryable Bullet*)**:
   * **Ciri**: Berpendar merah menyala dengan jalur laser tebal.
   * **Respon Benar**: Tekan tombol **[Q] HINDAR** tepat saat peluru ditembakkan.
   * **Visual FX**: Kamera pemain tersentak miring (*sway tilt*) ke kiri/kanan, peluru musuh meleset melewati bahu pemain (*MISS!*).
   * **Kesalahan Fatal**: Jika menekan [E] Parry pada peluru merah, pertahanan ditembus dan pemain menerima *Full Damage*.

2. **Peluru Kuning (*Parryable Bullet*)**:
   * **Ciri**: Berpendar kuning neon terang dengan lingkaran telegraf di sekelilingnya.
   * **Respon Benar**: Tekan tombol **[E] TANGKIS PISAU** saat peluru berjarak dekat.
   * **Visual & Audio FX**: Tangan kiri mengayunkan pisau ke tengah layar, percikan bunga api emas masif (*SPARK!*), suara denting baja (*CLANG!*), dan proyektil memantul kembali ke tubuh musuh (*Deflected Damage*).
   * **Sinergi Simbol `COUNTER_SHARPEN`**: Menghasilkan suara kokangan senjata sebelum peluru lepas dan menggandakan damage pantulan ke musuh.

---

## 3. Desain Musuh (Bot Gunner AI) & Progresi Stage

Pemain menghadapi 3 Stage duel berturut-turut dengan eskalasi pola tembakan:

| Stage | Nama Musuh | HP Musuh | Pola Serangan | Telegraf Jeda |
|---|---|---|---|---|
| **Stage 1** | *Rust Raider* | 100 HP | 1 tembakan tunggal per giliran (70% Kuning, 30% Merah). | 1,5 Detik |
| **Stage 2** | *Neon Enforcer* | 150 HP | Serangan ganda beruntun: Peluru Merah disusul Peluru Kuning. | 1,0 Detik |
| **Stage 3** | *Cyber Boss: "The Dealer"* | 220 HP | Tembakan acak 2 s/d 3 peluru bertempo cepat + goyangan drift reticle musuh lebih kencang. | 0,7 Detik |

---

## 4. Matriks Eksperimen A/B/C (Monetisasi & ARPU)

Responden dibagi secara acak dan deterministik menjadi 3 kelompok riset:

| Fitur / Parameter | Group A (Ads Dominant) | Group B (IAP Dominant) | Group C (Hybrid Model) |
|---|---|---|---|
| **Akses Toko Skin IAP** | **DITUTUP** (Menu Hidden) | **DIBUKA** (Saldo Rp 20.000) | **DIBUKA** (Saldo Rp 20.000) |
| **Katalog Skin** | - | Skin Revolver, Spinner, Pisau | Skin Revolver, Spinner, Pisau |
| **Tombol Re-spin Ads** | **AKTIF** | **NONAKTIF** (Hidden) | **AKTIF** |
| **Tombol Revive Ads** | **AKTIF** | **NONAKTIF** (Langsung Kalah) | **AKTIF** |
| **Tombol 2x Gold Ads** | **AKTIF** | **NONAKTIF** (Klaim Normal) | **AKTIF** |
| **Perhitungan ARPU** | $ARPU = \sum \text{Ad Revenue}$ | $ARPU = \sum \text{IAP Spent}$ | $ARPU = \text{Ad Rev} + \text{IAP Spent}$ |

---

## 5. Tata Letak Antarmuka (HUD Layout)

Sesuai preferensi terpilih, layar First-Person menggunakan tata letak **HUD Minimalis di Pojok Bawah Kiri**:

```
┌─────────────────────────────────────────────────────────────┐
│ [PAUSE]                                                     │
│                                                             │
│                   ┌──────────────────┐                      │
│                   │   KOTAK TARGET   │                      │
│                   │      [ 🎯 ]      │                      │
│                   │  (MUSUH ROBOT)   │                      │
│                   └──────────────────┘                      │
│                                                             │
│                                           TANGAN KANAN:     │
│                                          ┌────────────────┐ │
│                                          │ REVOLVER DENGAN│ │
│                                          │ 3-REEL SPINNER │ │
│                                          │ PADA SILINDER  │ │
│                                          └────────────────┘ │
│                                                             │
│ [HUD POJOK KIRI BAWAH]                                      │
│ HP PEMAIN: [██████████░░] 80/100                            │
│ ┌──────────────────────────────────────────────┐            │
│ │ [ ⚔ ATTACK ]  [ 🎯 MARK ]  [ 💉 HEAL ]       │            │
│ └──────────────────────────────────────────────┘            │
│ [ PUTAR SILINDER ]       [ 🎬 RE-SPIN VIA ADS ]             │
│                                                             │
│ [KONTROL DEFENSE]: [Q: HINDAR]   |   [E: PARRY PISAU]       │
└─────────────────────────────────────────────────────────────┘
```

---

## 6. Alur Pasca-Sesi & Kuesioner Evaluasi
1. Sesi berakhir setelah responden menyelesaikan Stage 3 atau gugur total.
2. Data telemetri (ARPU, impresi iklan, transaksi belanja skin, akurasi lock-on, dan statistik tangkisan parry/dodge) otomatis terkirim ke cloud database.
3. Tombol *"Lanjut ke Kuesioner Evaluasi"* membuka browser menuju Google Form dengan User ID terisi otomatis.
