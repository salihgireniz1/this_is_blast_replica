# This Is Blast Replica — case study planı

## Context

Bu bir **portfolyo/case study projesi**: teslimatın kendisi kod kalitesi. Oyunun
oynanabilir olması gerekli ama yeterli değil — mimari, ölçülmüş performans ve testler
asıl ürün.

Kaynaklar:
- `this-is-blast-clone` — Voodoo *This is Blast!*'ın çalışan bir prototipi (Unity 6,
  12 gameplay script). Art'ı iyi (renkler orijinalden alınmış), kodu prototip kalitesinde.
  Mekaniği ve görsel dili buradan çıkarıldı; **koddan hiçbir şey kopyalanmayacak**.
- `this_is_blast_replica` — boş URP 17.5 projesi. Kurulu: R3, UniTask (+DOTween köprüsü),
  LeanTouch/LeanTouch+, Toony Colors Pro 2, KinoBloom, NuGetForUnity, VInspector.

**Hedefler:**
- 120 FPS üst seviye cihaz → **8.33 ms frame bütçesi**, gameplay'de **0 B/frame GC alloc**.
- Worst case sahne: 360 küp + 16 cannon + aktif mermi/particle, hepsi aynı anda.
- Katmanlı mimari, asmdef ile derleme zamanında zorlanan bağımlılık yönü.
- MVVM UI, VContainer DI, UniTask async, LeanTouch+ input.
- EditMode + PlayMode test suite.

**Teslimat:** oynanabilir core loop + mimari README/diyagram + önce/sonra profiler kanıtı
+ gameplay videosu. (Blocker'lar, booster'lar, meta sonraki fazlar.)

---

# BÖLÜM 0 — Çalışma yöntemi (bu plandaki en önemli bölüm)

Bu proje **öğrenme amaçlı**. Salih kodu ship etmeyecek; yazılan her satıra hâkim olacak.
Bu, çalışma biçimini teknik kararlar kadar bağlayıcı kılıyor.

## 0.1 İş parçacığı boyutu

**Bir iş parçacığı = bir dosya, ya da bir dosyadaki tek bir davranış.** Asla daha fazlası.
Bir fazın tamamı tek seferde yazılmayacak; faz, 5-15 parçacığa bölünecek.

Örnek: "Faz 1 — Domain + Board" tek bir iş değil, şu parçacıklar:
```
1.1  Cell struct + testleri
1.2  CubeColor enum + PaletteData SO
1.3  BoardModel — sadece constructor + Get/Set + testleri
1.4  BoardModel — FrontRow(col) + testleri
1.5  BoardModel — Remove + sütun kayması + testleri
1.6  BoardModel — AnyOfColorInFront + testleri
1.7  LevelDefinition SO + doğrulama testleri
1.8  LevelDefinition custom inspector (grid boyama)
1.9  ICubeFactory + CubePool + testleri
1.10 CubeView (humble object) + BoardPresenter + testleri
```

## 0.2 Her parçacığın akışı

Sırayla, istisnasız:

```
1. NE YAPACAĞIZ    Bir paragraf: bu parçacık neyi çözüyor, nereye oturuyor.
2. TEST ÖNCE       Testi yazıyorum. Test kırmızı — çünkü kod yok.  (TDD)
                   Testi okuyoruz: bu davranış tam olarak ne demek?
3. KOD             En küçük geçen implementasyon. XML comment'lerle.
4. YEŞİL           Testi koşuyorum, çıktıyı gösteriyorum.
5. AÇIKLAMA        - Neden bu şekilde yazdım
                   - Hangi alternatifleri eledim ve neden
                   - Hangi pattern/prensip burada devrede
                   - Nerede kırılabilir
6. ONAY            Salih "anladım, devam" demeden bir sonraki parçacığa GEÇİLMEZ.
```

Test önce yazılıyor çünkü test, davranışın **spesifikasyonu**. Testi okuyunca ne yapmak
istediğimiz belli oluyor; implementasyon ondan sonra sıradan bir iş haline geliyor.
`superpowers:test-driven-development` skill'i bu akış için kullanılacak.

## 0.3 Soru hakkı

Herhangi bir adımda "burayı anlamadım", "neden interface", "bu satır ne yapıyor",
"başka nasıl yazılırdı" soruları akışı durdurur ve cevaplanır. Anlaşılmayan kod
projede kalmaz — ya açıklanır ya basitleştirilir.

## 0.4 Kod standardı

**Her `public`/`protected` üye XML doc comment alır.** İstisnasız.

```csharp
/// <summary>
/// Verilen sütunun en öndeki (en küçük Z) dolu hücresinin satır indeksini döner.
/// </summary>
/// <param name="column">Sütun indeksi, 0 tabanlı, soldan sağa.</param>
/// <returns>
/// Satır indeksi; sütun tamamen boşsa <see cref="EmptyColumn"/> (-1).
/// </returns>
/// <remarks>
/// O(1) — ön satır indeksi <see cref="Remove"/> içinde artımlı güncellenir,
/// her çağrıda taranmaz. Hot path'te olduğu için bu önemli.
/// </remarks>
public int FrontRow(int column)
```

**Her dosyanın başında** ne olduğunu ve hangi katmana ait olduğunu anlatan bir blok:

```csharp
// BoardModel — oyun tahtasının saf veri modeli.
// Katman: Domain. UnityEngine referansı YOK, olamaz (asmdef zorluyor).
// Sorumluluğu: küplerin nerede olduğu ve bir küp yok olunca ne olduğu.
// Sorumluluğu DEĞİL: küplerin nasıl göründüğü, ne zaman öldüğü, kimin vurduğu.
```

**Method içi comment'ler** *ne* yaptığını değil, *neden* öyle yaptığını anlatır:
```csharp
// Geriden öne değil, önden geriye tarıyoruz: ön satır zaten cache'li,
// ilk dolu hücreyi bulunca durabiliyoruz. Tersi tüm sütunu taramak olurdu.
```

Ek kurallar:
- `private` alanlar `_camelCase`, `public` üyeler `PascalCase`, interface'ler `I` önekli.
- Sihirli sayı yok — hepsi ya `const` ya SO alanı, ismi ne olduğunu söylüyor.
- Bir metot ekranı aşıyorsa bölünür.
- `#region` kullanılmaz (kod gizlemek, düzenlemek değildir).

## 0.5 Test kapsamı: HER ŞEY

"Domain %90, gerisi serbest" yok. **Yazılan her sınıfın testi olacak.** MonoBehaviour'lar
dahil — onlar için yöntem aşağıda (Bölüm F).

Bir sınıf test edilemiyorsa **sınıf yanlış tasarlanmıştır**, test değil. O zaman sınıfı
bölüyoruz. Testin zorlaştığı yer, tasarımın bozulduğu yerdir — bu projede bu kuralı
canlı canlı göreceğiz.

---

# BÖLÜM A — Mekanik analizi

## A1. Core loop (clone kodundan doğrulanmış)

```
1. Board: 10 sütun × N satır × 1-3 katman renkli küp, uzakta bir "Gate"in önünde.
2. Board'un önünde DOCK sırası: 2-5 slot (z = -0.90), soldan sağa doldurulur.
3. Dock'ların arkasında QUEUE'lar: 1-4 kuyruk, her biri -Z'ye uzanan bir kolon.
   Her kuyrukta 1-9 cannon; her cannon'ın bir RENGİ ve bir MERMİ SAYISI var.

--- oyuncunun tek input'u ---
4. Oyuncu bir kuyruğun ÖNDEKİ cannon'ına dokunur.  (öndeki değilse hiçbir şey olmaz)
5. Boş dock var mı? Yoksa hiçbir şey olmaz.        ← oyunun tüm gerilimi burada
6. Cannon en soldaki boş dock'a zıplar (0.25 s), dock rezerve edilir.
7. Kuyruk bir adım öne kayar; yeni öndeki cannon aktifleşir
   (surprise cannon ise rengi tam burada açılır).
--- oyuncu tekrar devreye girmez ---

8. Dock'a inince MERGE kontrolü: dock'larda aynı renkten 3 cannon varsa soldan sağa
   üçerli gruplanır, yan ikisi ortadakine uçar, mermiler toplanır, iki dock boşalır.
9. Cannon otomatik ateşe geçer:
   a. Board'un ÖN SATIRINDAN kendi rengindeki küpleri toplar (max 5, x artan / y azalan),
      onları rezerve eder.
   b. Her hedefe döner → mermi (0.1 s aralık) → mermi uçar → küp yok olur.
   c. Küp ölünce o sütunun arkasındakiler bir slot ÖNE KAYAR.
   d. Her atışta ammo -1. Ammo 0 → cannon en yakın yan çıkışa zıplar, dock boşalır.
   e. Kendi renginde ön-satır hedefi yoksa bekler — ama DOCK'U İŞGAL ETMEYE DEVAM EDER.
10. Tüm küpler bitince level complete.
```

**Oyunun tek kararı:** *hangi cannon'ı, hangi sırayla dock'a göndereceğim.* Cannon dock'a
girdikten sonra ammo'su bitene kadar orayı bırakmaz. Dock sayısı = paralel iş sayısı;
yanlış cannon slot'u kilitler. Tüm zorluk buradan.

## A2. Sabitler (rebuild için tam değerler)

| Öğe | Değer |
|---|---|
| Küp scale | `(0.43, 0.45, 0.45)` |
| Grid X | 10 sütun, adım **0.43**, başlangıç `-1.92` (sütunlar tam bitişik) |
| Grid Z | satır adımı **0.45**, başlangıç **1.339** |
| Grid Y | katman adımı **0.40**, `0.225 / 0.625 / 1.025` |
| Dock | 5 slot, `x = 0.94*(i-2)`, `y = 0`, `z = -0.90` |
| Queue slot | `x = 0.94*(q-1)`, `z = [-2.4, -3.6, -4.68, -5.68]` (clone elle dizmiş, düzensiz) |
| Çıkış | Left `(-4.5, 0, -0.92)`, Right `(4.5, 0, -0.92)` |
| Gate | `z = 6.0`, scale 29 |
| Backdrop sprite | `z = 4.47`, tint `#CEEEFF` |
| Mermi | speed 10, Linear, trail time 0.1 / minVertexDistance 0.2 |

**Bizim düzeltmemiz:** queue Z merdiveni düzenli **1.15** adım olacak, clone'un düzensiz
elle dizilmiş değerleri taklit edilmeyecek.

## A3. Level ekonomisi — clone'un en net tasarım kararı

**Toplam mermi = toplam küp, renk renk birebir.** 9 level'ın 9'unda da. Sıfır tolerans.

| Level | küp | grid | renkler | cannon × ammo | dock | queue |
|---|---|---|---|---|---|---|
| 1 | 120 | 10×12×1 | Y60 R60 | 2 × 60 | 2 | 2×1 |
| 2 | 120 | 10×12×1 | Y40 R40 B40 | 6 × 20 | 5 | 3×2 |
| 3 | 135 | 10×15×1 ragged | Y45 R45 G45 | 9 × 15 | 5 | 3×3 |
| 4 | 180 | 10×19×1 ragged | Y60 R60 B60 | 9 × 20 | **3** | **1×9** |
| 5 | 240 | 10×12×**2** | Y120 R120 | 12 × 20 | 5 | 3×4 |
| 6 | 360 | 10×12×**3** | Y120 R120 O120 | 12 × 30 | 5 | 3×4 |
| 7 | 240 | 10×12×2 | Y80 R80 B80 | 12 × 20 | 5 | 3×4 |
| 8 | 180 | 10×21×1 sparse | Y60 R60 B60 | 9 × 20 | 5 | 3×3 |
| 9 | 320 | 10×32×1 | Y120 B120 R80 | 16 × 20 | 5 | 4×4 |

Level 1 saf tutorial (`YYYYY|RRRRR`, 12 satır aynı). Green sadece L3'te, Orange sadece
L6'da. **L6 = 360 küp** → performans worst case'imiz bu.

Surprise cannon (rengi gizli, gri materyal) sadece L8/L9'da, **her zaman kuyruğun
2. sırasında** — öne geçtiği anda rengi açılıyor.

## A4. Blokların öne kayması

Düşüş yok, **öne kayma** var. Ön küp ölünce arkadaki her küp bir slot ilerler. Clone
kayıtlı `InitialZPositions` listesini kullanıyor — float drift birikmiyor, mimari olarak
doğru yaptığı tek şey bu. Bizde zaten index tabanlı olacağı için sorun yok.

**Clone bug'ı:** ölen küp ön küp değilse hiçbir şey kaymıyor. Pratikte tetiklenmiyor
çünkü sadece ön satır hedefleniyor. Bizim modelde bu ayrım olmayacak — her boşluk kapanır.

## A5. `pos.y < 0.3f || pos.z < 5.4` gizemi

Gameplay kuralı değil, **art clipping hack'i**: z≈5.4'ten sonra yığılmış küpler 61° eğik
ortho kamerada Gate art'ının içinden çıkıyor. Gizli küpler yine de sayılıyor ve
hedeflenebiliyor — yani tamamen kozmetik. **Bizde hiç olmayacak**; gate konumu ve kamera
frustum'u ile çözülecek.

## A6. Fail condition — clone'da YOK, gerçek oyunda VAR

Clone'da hedefi olmayan cannon dock'ta sonsuza kadar 0.1 s'de bir polling yapar. Board
kilitlenirse oyun donar. Gerçek oyunun stratejik çekirdeği tam olarak bu:

> **Deadlock = kayıp.** Boş dock yok (veya boş dock var ama yerleştirilebilir queue-front
> yok) **VE** dock'taki hiçbir cannon ön satırda kendi renginde hedef bulamıyor.

Bu **Faz 3'te** gelecek. Olmadan level tasarımının anlamı yok.

## A7. Gerçek oyunda olup clone'da olmayanlar

Ses dosyası isimleri + store metni kanıtlı. **Oynayarak doğrulanmalı:**

| Eksik | Kanıt | Faz |
|---|---|---|
| **Deadlock/fail** | tasarımın merkezi | **3** |
| Ice / Stone / Metal blocker | `SFX_Interact_Ice_1`, `SFX_Match_Stone_1/3`, `SFX_Interact_Metal_1` | 7 |
| Surprise **küp** (clone'da sadece cannon'da) | enum'da tanımlı, hiç kullanılmamış | 7 |
| Bomb / ColorBomb booster | `SFX_Booster_Bomb_Combo_1`, `SFX_Booster_ColorBomb_Combo_1` | 8 |
| Kalp/can + fail'de satış | Voodoo standardı, 10 farklı IAP paketi | 8 |
| Level select / harita, rewarded ad | store görselleri | 8 |

---

# BÖLÜM B — Art bible

## B1. Shader: TCP2 Hybrid Shader 2 — birebir taşınabilir ✅

Clone Built-in RP + Gamma; biz URP 17.5 + Linear. Sorun değil:

**TCP2 "Hybrid Shader 2" hem URP hem Built-in destekliyor.** `TCP2 Hybrid Shader
2.tcp2shader` içinde önce `"RenderPipeline" = "UniversalPipeline"` SubShader'ı
(`UniversalForward` + `ShadowCaster` + `DepthOnly` + `DepthNormals` + `Meta`), sonra
Built-in SubShader'ı geliyor.

Shader GUID iki projede de aynı: `edd7abf643fa4bc4e8561d4c280c97cf`, sub-shader
`fileID: -6465566751694194690`. Sizin `RoundedCubeRed.mat` zaten onu kullanıyor.

**Ayrıca (performans için kritik, doğrulandı):**
- `TCP2 Hybrid 2 Include.cginc:75` → `CBUFFER_START(UnityPerMaterial)` → **SRP Batcher uyumlu**
- Tüm pass'lerde `#pragma multi_compile_instancing` → **GPU instancing destekli**

Yani iki hızlandırma yolu da açık. Hangisinin kazandığını **ölçeceğiz** (Bölüm D2).

**Renk çevirisi:** B2'deki hex'leri Unity color picker'a **hex olarak** girin — Unity hex
girişini sRGB kabul edip linear'a çevirir. Float RGBA alanına yazarsanız iki kat parlak
çıkar. Sonra directional light intensity ve `_RampThreshold` göz kararı eşitlenir
(Linear'da toon terminatörü biraz daha keskin görünür; `_RampSmoothing` 1 → ~1.2 gerekebilir).

## B2. Palet — tam değerler

| Renk | RGBA float (gamma) | Hex | `_RampThreshold` | `_SpecularRoughness` |
|---|---|---|---|---|
| Yellow | `1, 0.701, 0` | **`#FFB300`** | 0.743 | 0.15 |
| Red | `0.816, 0.192576, 0.192576` | **`#D03131`** | 0.743 | 0.174 |
| Blue | `0.078941, 0.405471, 0.915` | **`#1467E9`** | 0.743 | 0.084 |
| Green | `0.179174, 0.686275, 0.039118` | **`#2EAF0A`** | 0.783 | 0.13 |
| Orange | `0.972549, 0.505882, 0.137255` | **`#F88123`** | 0.783 | 0.13 |
| Surprise | `0.436524, 0.438699, 0.525` | **`#6F7086`** | 0.649 | 0.052 |

Ortak: `_RampSmoothing 1` (Surprise 0.803), `_SColor ≈ #333333`, `_HColor` beyaz
(Orange/Surprise `#CCCCCC`), `_Metallic 0`, `_UseOutline 0`, `_UseMobileMode 1`,
`_ReceiveShadowsOff 1`. **Blue'nun `_SpecularColor` = `2,2,2`** — kasıtlı over-bright,
mavi küpleri parlatıyor, aynısını yapın.

| Sahne öğesi | Hex |
|---|---|
| Kamera clear / gökyüzü | **`#93CFFF`** |
| Zemin | **`#A3CDEC`** (`_RampSmoothing 0` → sert toon terminatörü) |
| Board backdrop sprite tint | **`#CEEEFF`** |
| Gate | **`#7EA3F0`** |
| Dock platform | **`#8EB5D0`** |
| Idle (queue) platform | `#8EB5D0` @ alpha 0.73 |
| Mermi | `#BCBCBC`, `_SpecularColor 2,2,2` |
| Level 9 gece teması | **`#351841`** |

Tema tint formülü: `Lerp(base, white * theme, strength)` — zemin sprite 0.3, level-özel
sprite 0.85, zemin materyali 1.0.

## B3. Kamera ve ışık

```
Main Camera   (0, 10, -4.5), euler (61, 0, 0), ORTHOGRAPHIC, size 5.53
              near/far 0.3/1000, clear Solid #93CFFF, HDR açık
Directional   (0, 3, 0), euler (45, -25, 0), beyaz, intensity 1
              Soft shadows, strength 0.25, bias 0.3, normalBias 0.577, realtime
Ambient       Skybox modu, Unity default skybox, intensity 1. Fog kapalı.
Başka ışık YOK.
```

Portrait 9:16'da ortho 5.53 → yatay yarı-genişlik 3.11; board 3.87 geniş → ekranın %62'si.

## B4. Post-process

Clone'da **hiç yok**. Bizde `SampleSceneProfile`'da Bloom (threshold 1, intensity 0.25,
scatter 0.5, HQ filtering) + Vignette 0.2 var — clone'un look'unu bozmayacak kadar hafif.
**Bloom 0.2, Vignette 0.15, ColorAdjustments saturation +5.** Clone'un temiz hissini
kaybetmeyin; Voodoo oyunu da post-heavy değil.

## B5. Proje ayarı farkları

| Ayar | Clone | Replica şu an | Yapılacak |
|---|---|---|---|
| Color space | Gamma | Linear | Linear kal |
| Orientation | Portrait | **AutoRotation** | **Portrait** |
| Render scale | — | **0.8** | **1.0** |
| MSAA | kapalı | kapalı | **2x** (toon kenarlar için) |
| Soft shadows | açık (0.25) | **kapalı** | aç |
| Shadow distance | 30 | 50 | **30** |
| Product/company | — | DefaultCompany | isim ver |

---

# BÖLÜM C — Animasyon bible (tamamı DOTween)

**Projede tek bir `.anim` veya `.controller` yok.** Sıfır Animator, sıfır Timeline.
Hareketin %100'ü DOTween; tek istisna mermi çarpma particle'ı. Clone'un "pahalı" hissi
tamamen bu katmandan — en değerli kısmı bu.

Clone `DOTweenSettings`: `defaultEase OutQuad`, `defaultAutoKill 1`,
`defaultRecyclable 0`, `useSafeMode **0**`. **Bizde `useSafeMode = 1` ve
`recycleAllByDefault = true`** (Bölüm D3).

## Küp

| Olay | Tween | Süre | Ease |
|---|---|---|---|
| Görünme | `DOScale(scale, 0.1)` | 0.10 | OutQuad |
| **Ölüm** | `DOScale(zero, 0.15)` → pool'a dön | 0.15 | OutQuad |
| **Öne kayma** | `DOMoveZ(newZ, 0.15).SetDelay(0.15)` | 0.15 | **OutSine** |
| ↳ kayma sonrası | `DOPunchPosition(-fwd*0.1, 0.15, vibrato 2, elasticity 0.5)` | 0.15 | **OutBounce** |
| Çarpma tepkisi | `DOPunchPosition(hitDir*0.05, 0.15, v1, e0.4)` + `DOPunchRotation((0,±15,0), …)` | 0.15 | OutQuad |

Kayma delay'i tam olarak ölüm süresine eşit (0.15) — küp yok olurken arkadaki kaymaya
başlamıyor. Çarpma tepkisi clone'da comment'li; **bizde açılacak**, ucuz ve iyi hissettiriyor.

## Cannon hareketi

| Olay | Tween | Süre | Ease |
|---|---|---|---|
| **Dock'a zıplama** | `DOJump(dock, jumpPower 0.3, jumps 1, 0.25)` | 0.25 | OutQuad |
| **Kuyruk kayması** | `DOJump(newPos, 0.3, 1, 0.25).SetDelay(0.2)` | 0.25 | OutQuad |
| **Çıkış** (ammo 0) | `DORotateQuaternion(lookAt, 0.3)` | 0.30 | OutQuad |
| | → `DOJump(exit, jumpPower **1**, jumps **3**, 0.75)` | 0.75 | OutQuad |
| | join `DOPunchRotation((0, Rnd(-0.2,0.2), 0), 0.75, v3, e0.6)` | 0.75 | — |

Çıkış zıplaması girişten **3× yüksek ve 3 sekmeli** — "işim bitti, gidiyorum" hissi
bundan geliyor. Kaçırmayın.

## Cannon ateş

| Olay | Tween | Süre | Ease |
|---|---|---|---|
| Hedefe dönüş | `DORotateQuaternion(lookAt, 0.2)` | 0.20 | OutQuad |
| **Geri tepme** | `DOPunchPosition(-fwd*0.02, **0.035**, vibrato 5, elasticity 0.6)` | 0.035 | OutQuad |
| Boşta rotasyon sıfırlama | `DORotateQuaternion(identity, **3.0**)` | 3.00 | OutQuad |
| Atış aralığı | 0.1 s → 5'li seri = 0.5 s | — | — |

Recoil 0.035 s + vibrato 5 = makineli tüfek hissi. Rotasyon sıfırlaması 3 s ise kasten
yavaş: hedefsiz cannon ağır ağır ortaya dönüyor, "arıyor" gibi duruyor.

## Merge (toplam 1.1 s)

```
t=0.1   üç cannon birden DOMoveY(+0.5, 0.2) OutSine        ← havalanma
t=0.5   sol+sağ DOMove(center, 0.4) InExpo + merge sesi    ← çekilme
t=0.9   ammo toplanır, sol+sağ pool'a, dock'lar boşalır
        orta DOMoveY(0, 0.2) OutSine                       ← iniş
```
**InExpo** kritik: yavaş başlayıp son anda hızlanıyor → çarpışma hissi.

## Level complete (~4.5 s)

```
t=0.0   win sesi; HUD gizlenir; mainPanel DOFade(0→1, 0.3) OutQuad
t=0.0   victoryText scale 0→1 (0.3, OutBack) → DOAnchorPos (0,233)→hedef (0.3, OutQuad)
t=0.4   nextFeaturePanel: -Screen.height'tan DOAnchorPos(hedef, 1.5) OutCirc
t=1.0   progressFill DOFillAmount(0→0.25, 1.0) OutCubic + % sayacı aynı eğride
t=1.5   coin'ler: her coin i için bekle i*0.1 → DOScale(0→1, 0.35) OutBack
          join DOLocalMoveY(+Rnd(10..20), 0.4) ×2 Yoyo InOutSine
          bekle 0.5 → DOMove(hudTarget, 0.8) InBack → DOScale(0, 0.08) InBack
t=2.0   "You Win" + coin sayacı; butonlar DOScale(1, 0.35) OutBack
t=3.5   HUD sayacı DOTween.To(start→end, 0.5) OutQuad + coin sesi (0.08 s cooldown)
        hudTarget DOPunchScale(one*0.3, 0.1, v1, e0.6) ×coinsAdded OutBack
```

Uzun ama sıkıcı değil çünkü **her aşama farklı ekran bölgesinde**: metin üstte → panel
alttan → bar ortada → coin'ler ortadan HUD'a. Kopyalanacak asıl fikir bu mekânsal dağılım.

---

# BÖLÜM D — Mimari

## D1. Katmanlar (asmdef ile derleme zamanında zorlanır)

Bağımlılık yönü **tek yönlü**, ve bunu yorum satırı değil `.asmdef` referansları garanti
eder. README'de gösterilecek asıl artefakt bu.

```
Blast.Domain            saf C#, UnityEngine referansı YOK
                        BoardModel, Cell, CubeColor, LevelDefinition,
                        DeadlockRule, MergeRule, TargetingRule
                          ↑
Blast.Application       use case / orchestration. UniTask'a bağlı.
                        GameSession, ShootingService, MergeService,
                        PlaceCannonCommand, IInputSource, ILevelRepository
                          ↑                    ↑                ↑
Blast.Presentation   Blast.UI          Blast.Infrastructure
MonoBehaviour view,  MVVM (R3 binding) LeanTouch adapter, PlayerPrefs,
DOTween, pooling,    ViewModel+View    audio, addressable/resource yükleme
VFX, kamera
                          ↑
Blast.Bootstrap         VContainer LifetimeScope — composition root.
                        Tek yer: kim kimi enjekte ediyor.

Blast.Tests.EditMode → Domain, Application, UI (ViewModel)
Blast.Tests.PlayMode → hepsi
```

**Domain'in UnityEngine referansı olmaması** bu mimarinin kanıtı: board mantığı, deadlock
tespiti, merge kuralı ve solver Unity açmadan test edilebiliyor, milisaniyeler içinde.

## D2. Pattern'ler — her biri gerekçesiyle

Süsleme yok. Bir pattern gerekçesini yazamıyorsam koymuyorum; README'de her satırın
gerekçesi var.

| Pattern | Nerede | Gerçek gerekçe |
|---|---|---|
| **State Machine** | `GameSession` (Boot→Loading→Playing→Won/Lost), `Cannon` (Queued→Moving→Docked→Firing→Merging→Retiring) | Clone'da enum + property setter içinde yan etki var; geçiş kuralı hiçbir yerde yazılı değil. Explicit state → geçersiz geçiş derleme/assert zamanında yakalanır |
| **Command** | `PlaceCannonCommand : IGameCommand` | Oyunda tek input tipi var ama komut nesnesi 3 şey kazandırıyor: deterministik replay (test), solver'ın aynı API'yi kullanması, ileride undo. Solver zaten planda — command olmadan solver ayrı bir kod yolu yazmak zorunda kalır |
| **Object Pool** | küp, mermi, particle, floating text | `UnityEngine.Pool.ObjectPool<T>` — **stdlib**, kendi pool sınıfımızı yazmıyoruz |
| **Observer (R3)** | `ReadOnlyReactiveProperty<int> RemainingCubes`, `Observable<CubeDestroyed>` | UI'ın modeli poll etmemesi; MVVM'in bağlayıcısı |
| **Adapter** | `LeanTouchInputAdapter : IInputSource` | LeanTouch'ı Domain/Application'dan izole eder. Testte `FakeInputSource` — LeanTouch olmadan tüm oyun akışı test edilebilir |
| **Repository** | `ILevelRepository` | Level kaynağı (SO / JSON / remote) değişebilir; Application bilmez |
| **Factory** | `ICubeFactory`, `ICannonFactory` | Pool + VContainer'ı birleştirir; view'ların container'a erişmesi gerekmez |
| **DI (VContainer)** | `GameLifetimeScope` | Tek composition root. Sıfır `FindObjectOfType`, sıfır singleton |

## D2b. Interface politikası

Kural: **bir bağımlılık, sahibinin dışından geliyorsa interface arkasındadır.**
Yani bir sınıf `new`'lemediği ve kendi içinde üretmediği her şeyi interface olarak alır.

Bu proje için bu kural çok geniş uygulanıyor, ve gerekçesi net: **her şeyin testi
yazılacak** (Bölüm 0.5). Bir sınıfı izole test edebilmek, bağımlılıklarının yerine test
double koyabilmek demek. Yani her interface'in **en az iki implementasyonu** var:
gerçek olan ve testteki sahte olan. Tek implementasyonlu interface yazmıyoruz — çünkü
tam kapsam testte hiçbiri tek implementasyonlu kalmıyor.

```
IBoardModel          → BoardModel            + FakeBoard (test)
IInputSource         → LeanTouchInputAdapter + FakeInputSource (test)
ILevelRepository     → AddressableLevelRepo  + InMemoryLevelRepo (test)
ICubeFactory         → PooledCubeFactory     + FakeCubeFactory (test)
IShootingService     → ShootingService       + StubShootingService (test)
IDeadlockRule        → DeadlockRule          + AlwaysDeadlockedRule (test)
IAudioService        → AudioService          + SilentAudioService (test + build)
IGameClock           → UnityGameClock        + ManualClock (test — zamanı elle ilerlet)
ISaveStore           → PlayerPrefsSaveStore  + InMemorySaveStore (test)
```

**`IGameClock` özellikle önemli:** zamanı interface'in arkasına almak, "3 saniye bekle"
içeren bir davranışı testte 3 saniye beklemeden doğrulamayı sağlıyor. `ManualClock.Advance(3f)`
→ test milisaniyede koşuyor. Bu, test edilebilirliğin tasarımı nasıl iyileştirdiğinin
en temiz örneği; README'de anlatılacak.

**Yine de eklemediklerim** (README'de ADR olarak, gerekçesiyle):
- **Event bus / message broker:** R3 observable'ları zaten bu işi yapıyor. Üstüne ayrı bir
  mesaj katmanı, aynı şeyin ikinci kez soyutlanması olur.
- **Custom pool sınıfı:** `UnityEngine.Pool.ObjectPool<T>` stdlib'de var, aynısını
  yazmanın kazancı yok. `ICubeFactory` arkasında olduğu için değiştirmek zaten serbest.
- **Kendi DI container'ımız:** VContainer var.

Ayrım şu: **interface'i bağımlılık yönünü çevirmek ve test etmek için koyuyoruz (DIP),
gelecekte belki lazım olur diye değil.** Yukarıdaki üç madde ikinci kategoriydi.

## D3. UI — MVVM

**Neden MVVM, MVC değil:** bu UI ağırlıklı olarak *veri gösterimi* (progress, coin, level,
ammo) ve neredeyse hiç navigasyon yok. Binding, controller boilerplate'ini tamamen siliyor.
MVC burada her ekran için bir controller sınıfı demek olurdu — hiçbir şey kazandırmadan.

```
Model       Blast.Domain / Application  (BoardModel, ScoreService)  — Unity yok
   ↓
ViewModel   saf C#, R3 çıktıları:
              ReadOnlyReactiveProperty<float>  Progress
              ReadOnlyReactiveProperty<int>    Coins
              ReadOnlyReactiveProperty<string> LevelLabel
              Observable<Unit>                 LevelCompleted
            → EditMode'da sahne açmadan test edilir
   ↓
View        MonoBehaviour + [SerializeField] UGUI referansları.
            Start()'ta bind:  _vm.Progress.Subscribe(v => _slider.value = v).AddTo(this)
            View'da MANTIK YOK — sadece bağlama ve tween tetikleme.
```

`AddTo(this)` ile abonelikler GameObject ömrüne bağlanır → manuel dispose yok, sızıntı yok.

## D4. Input — LeanTouch+

```
LeanTouchInputAdapter : IInputSource        (Blast.Infrastructure)
  LeanTouch.OnFingerTap → tek raycast → IsCannonTapped(cannonId)
  → Observable<CannonTapped> yayınlar
GameSession bu observable'ı dinler → PlaceCannonCommand üretir → doğrular → uygular
```

Clone'da **her cannon kendi `Update()`'inde raycast atıyor** (16 cannon = 16 raycast/frame).
Bizde: tap anında **tek** raycast, `Update()` yok.

## D5. Async — UniTask

Coroutine yok, `Invoke` yok, `OnComplete` zinciri yok.

```csharp
// Faz 0'da kurulacak: UNITASK_DOTWEEN_SUPPORT define'ı + DOTween paketi
await transform.DOJump(dock, 0.3f, 1, 0.25f).SetEase(Ease.OutQuad)
               .ToUniTask(cancellationToken: _ct);
```

Her cannon/küp `GetCancellationTokenOnDestroy()` taşır → `OnDestroy`'da `DOKill` unutma
riski yapısal olarak biter. Clone'un her `Shoot`'ta `new WaitForSeconds(shootRate)`
allocate etmesi de böylece ortadan kalkar (Bölüm E).

## D6. Klasör iskeleti

```
Assets/00_GAME/
  Scripts/
    Domain/          Blast.Domain.asmdef          (noEngineReferences: true)
    Application/     Blast.Application.asmdef
    Presentation/    Blast.Presentation.asmdef
    UI/              Blast.UI.asmdef
    Infrastructure/  Blast.Infrastructure.asmdef
    Bootstrap/       Blast.Bootstrap.asmdef
    Tests/EditMode/  Blast.Tests.EditMode.asmdef
    Tests/PlayMode/  Blast.Tests.PlayMode.asmdef
    ~Utils/          RoundedBox.cs (mevcut)
  Data/              LevelDefinition SO'ları, PaletteData, JuiceConfig
  Materials/ Meshes/ Prefabs/ Scenes/
```

`Blast.Domain.asmdef` içinde **`"noEngineReferences": true`** — Domain'e yanlışlıkla
`using UnityEngine` eklenirse derleme patlar. Mimarinin kendini koruması bu.

## D7. Addressables

`com.unity.addressables` UPM ile eklenecek. UniTask köprüsü zaten hazır bekliyor:
`UniTask.Addressables.asmdef` içinde `versionDefines` var, paket eklenince
`UNITASK_ADDRESSABLE_SUPPORT` **kendiliğinden** tanımlanıyor — elle define eklemeye
gerek yok. (Aynı mekanizma DOTween'de de var: `com.demigiant.dotween` **UPM olarak**
kurulursa `UNITASK_DOTWEEN_SUPPORT` otomatik gelir. Asset Store `.unitypackage`'ı ile
kurarsanız gelmez ve elle eklemek gerekir — **UPM tercih edilecek**.)

**Ne Addressable olacak:**

| Grup | İçerik | Yükleme anı |
|---|---|---|
| `Levels` | `LevelDefinition` SO'ları | level yüklenirken, tek tek |
| `Gameplay` | küp prefab, cannon prefab, mermi prefab | oyun açılışında bir kez |
| `Audio` | SFX ve müzik klipleri | oyun açılışında, müzik lazy |
| `Themes` | tema materyalleri / sprite'lar | temalı level'a girerken |

**Arayüz:** Application katmanı Addressables'ı **bilmez**. `ILevelRepository` görür:

```csharp
public interface ILevelRepository
{
    /// <summary>İstenen level tanımını yükler. İptal edilebilir.</summary>
    UniTask<LevelDefinition> LoadAsync(int levelIndex, CancellationToken ct);

    /// <summary>Yüklenmiş level kaynaklarını serbest bırakır.</summary>
    void Release(int levelIndex);
}
```
- `AddressableLevelRepository` — gerçek implementasyon, `Addressables.LoadAssetAsync`
  + `.ToUniTask(ct)`, handle'ları takip eder ve `Release`'te bırakır.
- `InMemoryLevelRepository` — testler için, Addressables'a hiç dokunmaz.

**Kritik detay:** her `LoadAssetAsync` handle'ı saklanacak ve level değişince
`Addressables.Release` çağrılacak. Bırakılmayan handle en yaygın Addressables
sızıntısı — bunun için ayrı bir test var (`LevelLoad_ReleasesPreviousAddressables`,
Bölüm F6).

**Neden Addressables** (README'ye ADR): 30+ level'lık bir kampanyada tüm level SO'larını
build'e gömmek gereksiz bellek. Ayrıca ileride uzaktan level güncellemesi (Voodoo'nun
"regular updates" modeli) bunu gerektiriyor. Bu proje ölçeğinde `Resources.Load` da
çalışırdı — Addressables tercihi ölçeklenebilirlik ve async yükleme akışını
göstermek için bilinçli.

---

# BÖLÜM E — Performans

## E1. Bütçe ve worst case

| Metrik | Hedef | Worst case senaryo |
|---|---|---|
| Frame time | **< 8.33 ms** (120 FPS) | Level 6: 360 küp + 12 cannon, 5 cannon aynı anda ateşte |
| Main thread CPU | < 4 ms | |
| **GC alloc / frame** | **0 B** (gameplay sırasında) | ölçüm: `ProfilerRecorder("GC.Alloc")` |
| SetPass calls | < 15 | |
| Draw calls | < 25 | |
| Aktif ParticleSystem | 1 (paylaşımlı) | 50 emisyon/sn |

## E2. Batching — ölçülecek fork

TCP2 Hybrid 2 hem SRP Batcher uyumlu hem GPU instancing destekli. İkisi **birlikte
çalışmaz** (MaterialPropertyBlock SRP Batcher'ı devre dışı bırakır). İki yolu da kurup
ölçeceğiz — case study'nin en iyi konuşma noktası bu:

**A. SRP Batcher:** 6 paylaşımlı materyal (renk başına bir tane), aynı shader varyantı.
`sharedMaterial` ataması. Beklenti: ~6 SetPass, tüm küpler tek batch zincirinde.

**B. GPU Instancing:** tek materyal + `MaterialPropertyBlock` ile per-instance
`_BaseColor`. Beklenti: 1 draw call / 360 küp, ama SRP Batcher kapalı.

Ölçüp kazananı seçeceğiz. Sonuç README'de tablo olarak.

> ⚠️ **Clone'un yaptığı hata:** `meshRenderer.material = ...` — her küp ve her cannon için
> materyal *instance*'ı yaratıyor. 360 küp = 360 materyal instance = sıfır batching +
> sızıntı. `sharedMaterial` kullanılacak. Bu, önce/sonra profiler'da göstereceğimiz en
> net kazanç.

## E3. Hot path'te sıfır allocation

| Clone'un yaptığı | Bizim yapacağımız |
|---|---|
| `GetFrontCubes`: her volede `SelectMany`+`Where`+`OrderBy`+`ThenByDescending`+`ToList` | Sütun başına önceden hesaplanmış `int[] _frontRowIndex`, küp ölünce O(1) güncelleme. **Sıfır LINQ, sıfır alloc** |
| `new WaitForSeconds(shootRate)` her atışta | `UniTask.Delay` — struct tabanlı, alloc yok |
| `ammoText.SetText(ammoCount.ToString())` her atışta bir string | `ammoText.SetText("{0}", ammo)` — TMP'nin no-alloc overload'u |
| `allColumns.FirstOrDefault(col => col.cubes.Contains(cube))` küp ölümünde | Küp kendi `(col, row, layer)`'ını taşır → doğrudan index |
| 360 küpte `Update()` + 16 cannon'da raycast | **Sıfır `Update()`**; her şey event/UniTask ile |
| `Destroy(gameObject)` her küpte | `ObjectPool<CubeView>` |
| Hit başına yeni ParticleSystem | Tek paylaşımlı PS + `Emit(EmitParams)` dünya pozisyonuna |
| `FindFirstObjectByType<MergeManager>()` her `Player.Awake`'te | VContainer inject |

Board temsili: `CubeColor[]` düz dizi (`enum CubeColor : byte`),
`index = layer*rows*cols + row*cols + col`. `readonly struct Cell`. Sıfır boxing.

## E4. DOTween ayarları

```csharp
DOTween.Init(recycleAllByDefault: true, useSafeMode: true, LogBehaviour.ErrorsOnly);
DOTween.SetTweensCapacity(tweenersCapacity: 1500, sequencesCapacity: 100);
```
`recycleAllByDefault` → tween nesneleri havuzlanır, oyun ortasında allocation yok.
Kapasite önden ayrılır → yeniden boyutlandırma spike'ı yok.
Clone `useSafeMode: 0` ile çalışıyor; bu, yok edilmiş objeye tween uygulama hatasını
**gizliyor** sadece. Bizde açık.

## E5. Ölçüm harness'i (teslimat parçası)

`Blast.Tests.PlayMode/PerfHarness.cs`:
- Worst case level'ı (360 küp, 12 cannon) yükler, scripted bir çözümle otomatik oynatır.
- `ProfilerRecorder` ile 600 frame boyunca kaydeder:
  `Main Thread`, `GC.Alloc`, `SetPass Calls Count`, `Draw Calls Count`, `Batches Count`,
  `Triangles Count`.
- CSV döker: `ProfilerCaptures/perf-<etiket>.csv`. (Klasör projede zaten var.)
- README'ye "önce (naif implementasyon) / sonra (optimize)" tablosu bundan çıkar.

**Önce/sonra kanıtı için:** Faz 1-2'de kasten naif versiyonu (LINQ, `material`, `Destroy`,
`Update`) bir git branch'inde bırakıp ölçün, sonra optimize edip tekrar ölçün. "Optimize
ettim" demek yerine sayı göstermek — case study'nin en güçlü parçası.

---

# BÖLÜM F — Test stratejisi: her şey

Kural (Bölüm 0.5): **yazılan her sınıfın testi var.** Aşağıda katman katman *nasıl*.

## F1. Domain — düz unit test, kolay kısım

Unity API yok, sahne yok, milisaniyeler. Test yazmayı engelleyen hiçbir şey yok.

- `CellTests` — index ↔ (col,row,layer) dönüşümü, sınırlar, eşitlik.
- `BoardModelTests` — Get/Set; ön satır sorgusu; küp silme → sütun kayması; sınır
  durumları (boş sütun, tek katman, ragged board, tek küp, son küp).
- `DeadlockRuleTests` — doğruluk tablosu: boş dock var/yok × yerleştirilebilir
  queue-front var/yok × dock'ta hedefi olan cannon var/yok = **8 satır, 8 test**.
- `MergeRuleTests` — dock'ta 3/4/5/6 aynı renk gruplama; soldan sağa sıra; ammo toplamı;
  6 aynı renk = iki ayrı merge.
- `TargetingRuleTests` — max 5, ön satır, x artan / y azalan sıra; rezerve edilmiş küp
  atlanır; hedef yoksa boş liste.
- `LevelValidationTests` — her `LevelDefinition` için: toplam ammo == toplam küp
  **renk renk**; grid boyutu tutarlı; en az bir çözüm var (solver ile).
- `SolverTests` — elle kurulmuş çözülebilir/çözülemez level'lar; bilinen minimum hamle.

## F2. Application — sahte bağımlılıklarla

Her bağımlılık interface arkasında (D2b), yani hepsi izole test edilebiliyor.

- `GameSessionTests` — state machine geçiş tablosu; geçersiz geçiş atılıyor mu;
  `Won`/`Lost` doğru tetikleniyor mu. Bağımlılıklar: `FakeBoard`, `ManualClock`,
  `StubShootingService`.
- `PlaceCannonCommandTests` — doğrulama: öndeki cannon değilse reddet, boş dock yoksa
  reddet, geçerliyse uygula. Komut nesnesi olduğu için doğrudan test edilebiliyor.
- `ShootingServiceTests` — `ManualClock` ile zaman elle ilerletilerek: 5'li vole,
  0.1 s aralık, ammo 0'da çıkış. **Gerçek zamanda beklemeden.**
- `MergeServiceTests` — merge tetikleniyor mu, ammo toplanıyor mu, dock'lar boşalıyor mu.

## F3. Presentation / UI — Humble Object pattern

MonoBehaviour'ları test etmek zor. Çözüm onları **aptallaştırmak**: içlerinde test
edilecek mantık bırakmamak.

```
CubeView (MonoBehaviour)          ← aptal: sadece transform/renderer'a dokunur
   ↑ ICubeView interface'ini uygular
BoardPresenter (saf C#)           ← akıllı: hangi küp ne zaman ne yapacak
   ↑ testte FakeCubeView listesiyle çalışır
```

`BoardPresenter` **saf C#** — küpün öldüğünde `ICubeView.PlayDeath()` çağrıldığını,
kayan küplerde `ICubeView.MoveTo(z)` doğru sırayla çağrıldığını EditMode'da test ederiz.
`CubeView`'ın kendisinde test edilecek karar kalmaz (üç satır atama).

Aynısı UI'da:
- `HudViewModelTests` — R3 stream'leri: model değişince ViewModel doğru yayıyor mu.
- `LevelCompleteViewModelTests` — coin hesabı, level etiketi, tamamlanma sinyali.
- `HudView` — sadece binding; `FakeHudViewModel` ile bir PlayMode smoke test.

**Bu, testin tasarımı iyileştirdiği yer:** "MonoBehaviour test edilemiyor" diye şikâyet
etmek yerine mantığı MonoBehaviour'dan çıkarıyoruz, ve kod hem test edilebilir hem daha
iyi ayrılmış oluyor. README'de anlatılacak.

## F4. Infrastructure

- `LeanTouchInputAdapterTests` — sahte tap olayı → doğru `CannonTapped` yayını.
  LeanTouch'ın kendisi test edilmiyor (üçüncü parti), adaptörün çevirisi test ediliyor.
- `AddressableLevelRepositoryTests` — PlayMode; adres bulunamazsa ne oluyor,
  yükleme iptal edilirse ne oluyor.
- `PlayerPrefsSaveStoreTests` — yaz/oku/sil; `InMemorySaveStore` ile aynı sözleşmeyi
  geçtiğini doğrulayan **paylaşımlı sözleşme testi** (`ISaveStoreContractTests` — iki
  implementasyon aynı test setinden geçer).

## F5. Mimari testleri

- `ArchitectureTests.Domain_HasNoUnityEngineReference` —
  `typeof(BoardModel).Assembly.GetReferencedAssemblies()` içinde `UnityEngine` yok.
- `ArchitectureTests.Domain_HasNoUpwardReference` — Domain, Application'a bakmıyor.
- `ArchitectureTests.NoFindObjectOfType` — kaynak taraması: `FindObjectOfType` /
  `FindFirstObjectByType` / `GameObject.Find` hiçbir yerde geçmiyor (VContainer var).
- `ArchitectureTests.NoRendererMaterialAccess` — kaynak taraması: `.material` (instance
  yaratan) kullanımı yok, sadece `.sharedMaterial`. Bölüm E2'deki performans kuralının
  testi.

Mimari kuralı yorum satırında değil, testte tutmak — kural o zaman gerçekten kural olur.

## F6. Entegrasyon (PlayMode)

- `Level1_CompletesWithScriptedSolution` — `FakeInputSource` ile Level 1 baştan sona;
  `Won` state'i, 120 küpün 120'si ölmüş, iki cannon'ın mermisi tam bitmiş.
- `Deadlock_ProducesLostState` — kasıtlı kilitli level → `Lost`.
- `LevelLoad_ReleasesPreviousAddressables` — level değişince önceki handle'lar
  release ediliyor mu (sızıntı testi).

## F7. Performans (test değil, raporlama)

`PerfHarness` — Bölüm E5. CI'da değil, elle koşulur, CSV üretir.

**Not:** Coverage sayısı hedef değil, sonuç. Hedef "her sınıfın testi var" — coverage
raporu bunu doğrulamak için okunur, kovalanmaz.

---

# BÖLÜM G — Build fazları

Her faz **iş parçacıklarına bölünerek** yürütülür (Bölüm 0). Aşağıdaki listeler fazın
kapsamı; parçacık listesi her faza başlarken birlikte çıkarılır ve onaylanır.
Süreler kaba tahmin — parçacık başına onay beklendiği için gerçek süre sohbet hızına bağlı.
Faz N+1'e ancak N'in bitti kriteri doğrulanınca geçilir.

### Faz 0 — Altyapı *(1-2 gün)*
- **DOTween kur — UPM olarak** (`com.demigiant.dotween`). Böylece
  `UNITASK_DOTWEEN_SUPPORT` `versionDefines` ile otomatik gelir; Asset Store
  `.unitypackage`'ı ile kurulursa elle define eklemek gerekir.
- **Addressables kur** (`com.unity.addressables`) → `UNITASK_ADDRESSABLE_SUPPORT`
  otomatik tanımlanır. Grupları oluştur (D7 tablosu).
- **VContainer kur** (UPM).
- 8 asmdef'i oluştur, bağımlılık yönünü bağla, `Blast.Domain`'e
  `"noEngineReferences": true`.
- İlk testler: `ArchitectureTests` (F5) — mimari kuralları daha kod yazılmadan yeşile al.
- ProjectSettings: Portrait, product/company adı, render scale 1.0, MSAA 2x, soft
  shadows açık, shadow distance 30.
- `PaletteData` SO (B2 tablosu) + `JuiceConfig` SO iskeleti (C bölümündeki tüm
  süre/ease alanları — koda sabit gömülmeyecek).
- TCP2 Hybrid 2 materyalleri: 6 küp + ground + gate + dock + idle + projectile + trail.
  Clone'un `.mat` dosyaları kopyalanabilir (shader GUID aynı), **renkler hex olarak
  yeniden girilecek**.
- Sahne: kamera, tek directional light, Global Volume.
- `GameLifetimeScope` iskeleti.
- **Bitti:** boş zemin + tek test küpü; ekran görüntüsü clone'unkiyle yan yana konunca
  renk/açı farkı yok. `ArchitectureTests` yeşil.

### Faz 1 — Domain + Board *(3-4 gün)*
Parçacık listesi Bölüm 0.1'de örnek olarak verildi (1.1 → 1.10). Kapsam:
- `Cell`, `CubeColor : byte`, `IBoardModel` + `BoardModel` (düz `CubeColor[]`).
- `BoardModel`: `Get/Set`, `FrontRow(col)` (artımlı güncellenen index), `Remove` → sütun
  kayması, `AnyOfColorInFront(color)`.
- `LevelDefinition` SO + `ILevelRepository` (`InMemoryLevelRepository` önce,
  `AddressableLevelRepository` sonra) + custom inspector (grid renkli buton matrisi).
- `ICubeView` / `CubeView` (humble object) + `ICubeFactory` + `ObjectPool<CubeView>`
  + `BoardPresenter` (saf C#).
- **Blocker'lara yer:** `BoardModel`'e `byte hp` + `bool hidden` alanları **şimdi**
  ayrılacak (Faz 7'de Ice/Stone/Metal/Surprise için). Kullanılmıyor ama veri modelinde yeri var.
- **Bitti:** F1'in tamamı + `BoardPresenterTests` yeşil; editörde küp silince sütun düzgün
  kayıyor; 360 küplük board'da GC alloc 0.

### Faz 2 — Cannon / dock / queue *(4-5 gün)*
- `IGameClock` + `UnityGameClock` / `ManualClock` — **önce bu**, sonrası bunun üstüne kurulu.
- `DockRow`, `CannonQueue`, `Cannon` state machine (Queued→Moving→Docked→Firing→
  Merging→Retiring), geçiş tablosu testli.
- `IInputSource` + `LeanTouchInputAdapter` (tek raycast) + `FakeInputSource`.
- `PlaceCannonCommand` + doğrulama (öndeki mi, boş dock var mı).
- `IShootingService` + `ShootingService` (UniTask): hedef topla (max 5, ön satır) → dön →
  ateşle (0.1 s) → mermi (pool, speed 10) → küp öl. `ManualClock` ile testli.
- `MergeService`: dock'ta 3 aynı renk → soldan üçerli grup → birleş.
- Ammo 0 → çıkışa zıpla, dock boşalt.
- **Bitti:** F2'nin tamamı yeşil. Level 1 (10×12, Y60/R60, 2 cannon × 60, 2 dock) baştan
  sona oynanıyor; `Level1_CompletesWithScriptedSolution` yeşil.

### Faz 3 — Oyun akışı + fail *(2 gün)*
- `GameSession` state machine: Boot → Loading → Playing → Won / Lost.
- **`IDeadlockRule` + `DeadlockRule`** — her board/dock değişiminde (A6'daki kural).
- `RemainingCubes` `ReactiveProperty` → progress.
- `AddressableLevelRepository` — gerçek yükleme + handle release.
- **Bitti:** `DeadlockRuleTests` (8 satır), `GameSessionTests`,
  `Deadlock_ProducesLostState`, `LevelLoad_ReleasesPreviousAddressables` yeşil.

### Faz 4 — UI (MVVM) *(2-3 gün)*
- `HudViewModel`, `LevelCompleteViewModel` (saf C#, R3).
- `HudView`, `LevelCompleteView` (MonoBehaviour, sadece binding — humble object).
- Level complete zaman çizelgesi (Bölüm C).
- **Bitti:** F3'ün tamamı yeşil, ViewModel testleri sahne açmadan koşuyor.
  UI'da tek satır iş mantığı yok — `ArchitectureTests` bunu da doğruluyor.

### Faz 5 — Performans pass *(2 gün)*
- `PerfHarness` + `ProfilerRecorder` + CSV çıktısı.
- E2'deki batching fork'unu ölç (SRP Batcher vs GPU instancing), kazananı seç.
- E3'teki alloc kaynaklarını tek tek kapat, her adımda ölç.
- **Bitti:** Level 6 (360 küp) worst case'inde frame < 8.33 ms, gameplay'de GC alloc
  0 B/frame, SetPass < 15. CSV'ler `ProfilerCaptures/`'ta.

### Faz 6 — Art + juice *(2.5 gün)*
- Gate, backdrop, dock/idle platform meshleri — **`RoundedBox` ile prosedürel üret**,
  clone'un `.obj`'lerini almayın.
- Post-process (B4), tema sistemi (`LevelDefinition.themeColor`).
- Bölüm C'deki animasyon bible'ı satır satır uygulanır, öncelik sırası:
  1. küp ölüm + öne kayma (ritmi bu belirliyor)
  2. cannon çıkış zıplaması (3 sekme — karakter bundan)
  3. recoil + mermi trail
  4. merge sekansı
  5. level complete zaman çizelgesi
  6. ses (pop 0.08 s cooldown'lı spam koruması)
- **Bitti:** yan yana ekran görüntüsü ±3/255 içinde; 30 sn kayıt clone'unkiyle "aynı his".

### Faz 7-8 — sonraki teslimat
Blocker'lar (Ice/Stone/Metal/Surprise küp — `BoardModel`'e `byte hp` + `bool hidden`
alanları **Faz 1'de şimdiden ayrılacak**), sonra booster'lar (`ITargetSelector` burada
çıkarılır), meta (kalp, level select, ekonomi).

### Faz 9 — Case study teslimatı *(2 gün)*
- `README.md`: gameplay GIF (en başta), katman diyagramı (Mermaid), her pattern'in
  gerekçesi, **interface politikası** (D2b — neden bu kadar çok interface var: her
  birinin testte ikinci implementasyonu var), ADR listesi (**neyi neden yapmadığımız
  dahil**).
- Test raporu: kaç test, hangi katmanda, `IGameClock` ve Humble Object örnekleriyle
  "test edilebilirlik tasarımı nasıl iyileştirdi" anlatımı.
- Profiler önce/sonra tablosu + CSV'ler + capture dosyaları.
- **Bitti:** README'yi hiç Unity açmadan okuyan biri mimariyi anlıyor; Salih projedeki
  her dosyayı açıp ne yaptığını ve neden öyle yapıldığını anlatabiliyor.

---

# BÖLÜM H — Kampanya planı (ilk 30 bölüm)

`Puzzle_Ideation/method/campaign-architecture.md`'deki **Block Out 10'luk döngüsü** ev
standardı: `…0` rahatlama (%23 zaman), `…4` ilk zirve (%64), `…7` yarım zirve (%55),
`…9` ana zirve (%73).

## H1. Bu oyunda zorluk kolları

| Kol | Etkisi |
|---|---|
| **Dock sayısı** (5→4→3) | **En güçlü kol.** Paralel iş sayısı = hata toleransı |
| **Queue derinliği** | Kaç adım ileri planlayabildiğin (1×9 = sıfır seçim, 4×4 = geniş) |
| **Renk çeşitliliği** | `campaign-architecture` bulgusu: en güçlü zaman sürücüsü (β +0.37) |
| **Renk dağılımı** (blok mu, serpme mi) | Blok = kolay, serpme = ön satır hep karışık |
| **Katman sayısı** (1→2→3) | Öndeki küpü öldürmek arkadakini açar |
| **Ammo bolluğu** | Clone hep tam eşit. %5-10 fazla vermek yeni bir kol |
| ~~Board büyüklüğü~~ | **Kol değil** — sadece süreyi uzatır (metodolojinin net bulgusu) |

**Ana kolumuz: dock sayısı × renk dağılımı.** Board büyütmeyin.

## H2. İlk 30 bölüm

Kural: yeni mekanik `…1`/`…2`'de tanıtılır, `…3`-`…6`'da tekrarlanır, `…9`'da zirvede
kullanılır. Tanıtım level'ında mekanik tek başına, ucuz ve fail imkânsız.

| # | Yeni şey | Board | Renk | Dock | Queue | Rol |
|---|---|---|---|---|---|---|
| 1 | tap → dock → otomatik ateş | 10×8 | 2 blok | 2 | 2×1 | tutorial, fail imkânsız |
| 2 | 2. kuyruk, sıralama seçimi | 10×8 | 2 | 3 | 2×2 | |
| 3 | renk serpiştirme | 10×10 | 2 | 3 | 3×2 | |
| **4** | **3. renk** | 10×10 | 3 | 3 | 3×2 | **ilk zirve** |
| 5 | rahatlama | 10×8 | 3 blok | 4 | 3×2 | |
| 6 | queue derinliği 3 | 10×10 | 3 | 4 | 3×3 | |
| **7** | **merge** | 10×12 | 3 | 5 | 3×3 | **yarım zirve** — merge burada öğretilir |
| 8 | merge drill | 10×12 | 3 | 5 | 3×3 | |
| **9** | **dock 3'e düşer** | 10×12 | 3 | **3** | 3×3 | **ana zirve** |
| **10** | rahatlama | 10×8 | 2 | 5 | 3×2 | **%23 nefes** |
| 11 | **2. katman** | 10×10×2 | 3 | 5 | 3×3 | tanıtım |
| 12-13 | katman drill | | 3 | 5 | 3×3 | |
| **14** | katman + 4 renk | 10×12×2 | **4** | 4 | 3×4 | zirve |
| 15-16 | rotate | | 3-4 | 4-5 | | |
| **17** | **surprise cannon** | 10×12×2 | 3 | 4 | 3×4 | yarım zirve |
| 18 | surprise drill | | 4 | 4 | | |
| **19** | surprise + dock 3 | 10×12×2 | 4 | **3** | 3×4 | ana zirve |
| **20** | rahatlama + **ilk tema değişimi** | 10×8 | 2 | 5 | 3×2 | görsel ödül |
| 21 | **3. katman** | 10×10×3 | 3 | 5 | 3×4 | tanıtım |
| 22-23 | drill | | 3-4 | 5 | | |
| **24** | 3 katman + 4 renk | 10×12×3 | 4 | 4 | 3×4 | zirve |
| 25-26 | rotate | | 4 | 4 | 4×4 | |
| **27** | **tek kuyruk, sıfır seçim** | 10×12×2 | 4 | 4 | **1×12** | yarım zirve |
| 28 | rotate | | 4 | 4 | 3×4 | |
| **29** | 3 katman, 4 renk, 3 dock | 10×12×3 | 4 | **3** | 3×4 | **bölüm finali** |
| **30** | rahatlama + tema | 10×8 | 2 | 5 | 3×2 | |

**Zirve keskinliği:** bir zirve, önceki level'ın **1.6-2.3 katı** süre almalı. Ölçülmeden
"zor" etiketi konmaz.

**Ammo bolluğu eğrisi:** 1-10 arası **+%10**, 11-20 arası **+%5**, 21+ **tam eşleşme**.
Clone'un baştan tam-eşleşmesi acımasız.

## H3. Level formatı ve solver

`LevelDefinition` SO içinde grid ASCII olarak (git-diff'lenebilir, elle yazılabilir):

```
name: 004
docks: 3
grid: |
  YYYRRRBBBB
  YYYRRRBBBB
queues:
  - [Y:20, R:20, B:20]
  - [R:20, B:20, Y:20]
```

**Solver zorunlu.** Level'ın çözülebilir olduğunu ve kaç farklı sırayla çözüldüğünü
bilmeden zorluk ayarlanamaz. `Blast.Domain` üzerine kurulacak BFS/brute-force:
durum = (board, dock içerikleri, queue başları); hamle = `PlaceCannonCommand`. Çıktı:
çözüm var mı, minimum hamle, ortalama seçim genişliği. `Blast.Domain`'in Unity'den
bağımsız olması bunu mümkün kılıyor — solver bir konsol testinde koşar.
Mevcut `/level-audit` metodolojiniz buraya birebir oturur.

---

# Doğrulama

**Her iş parçacığında** (Bölüm 0.2, adım 4):
- `mcp__unity-editor-mcp__run_tests` → o parçacığın testi kırmızıdan yeşile geçti mi,
  çıktı gösterilir.
- `get_console_logs` → hata/uyarı sıfır.
- Onay alınmadan sonraki parçacığa geçilmez.

**Her fazın sonunda:**
1. **Unity MCP**: `editor_play` → `capture_game_view` → clone'un aynı level'ının
   görüntüsüyle karşılaştır.
2. `run_tests` — **tüm** EditMode + PlayMode suite'i, tek bir kırmızı yok.
3. `get_console_logs` → hata/uyarı sıfır.
4. **Faz 2:** Level 1 baştan sona; 120 küpün 120'si ölmüş, iki cannon'ın mermisi tam bitmiş.
5. **Faz 3:** kasıtlı deadlock level'ı → `Lost`.
6. **Faz 5:** `PerfHarness` CSV'si — Level 6'da frame < 8.33 ms, GC alloc 0 B/frame,
   SetPass < 15. Önce/sonra tablosu README'ye.
7. **Faz 6:** `capture_game_view` + clone ekran görüntüsü piksel karşılaştırması, ±3/255.
