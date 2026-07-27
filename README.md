# Yağmur Rotası

Tepebaşı Belediyesi için geliştirilen, yağmur suyu ve su tasarrufu
farkındalığına odaklanan WebGL tabanlı eğitici boru bulmaca oyunu.

## İçindekiler

1. [Proje Hakkında](#1-proje-hakkında)
2. [Oynanış](#2-oynanış)
3. [Temel Özellikler](#3-temel-özellikler)
4. [Ekran Görüntüleri](#4-ekran-görüntüleri)
5. [Kullanılan Teknolojiler](#5-kullanılan-teknolojiler)
6. [Sahne Akışı](#6-sahne-akışı)
7. [Proje Yapısı](#7-proje-yapısı)
8. [Gereksinimler](#8-gereksinimler)
9. [Projeyi Açma](#9-projeyi-açma)
10. [WebGL Build Alma](#10-webgl-build-alma)
11. [Web Sitesine Yükleme](#11-web-sitesine-yükleme)
12. [Kayıt Sistemi](#12-kayıt-sistemi)
13. [Kontroller](#13-kontroller)
14. [Geliştirici Araçları](#14-geliştirici-araçları)
15. [Yeni Bölüm Ekleme / Geliştirme](#15-yeni-bölüm-ekleme--geliştirme)
16. [Önemli Teknik Notlar](#16-önemli-teknik-notlar)
17. [Test Kontrol Listesi](#17-test-kontrol-listesi)
18. [Bilinen Sınırlamalar](#18-bilinen-sınırlamalar)
19. [Katkı ve Geliştirme](#19-katkı-ve-geliştirme)
20. [Proje Durumu](#20-proje-durumu)
21. [Lisans ve İletişim](#21-lisans-ve-i̇letişim)

---

## 1. Proje Hakkında

Yağmur Rotası, oyuncunun boru parçalarını döndürerek toplanan yağmur
suyunu kaynaktan hedefe ulaştırdığı eğitici bir bulmaca oyunudur. Oyun;
su tasarrufu, sürdürülebilir şehir altyapısı ve sel önleme konularında
farkındalık kazandırmayı amaçlar. Tarayıcı üzerinden oynanabilecek şekilde
WebGL platformu için tasarlanmış ve geliştirilmiştir.

## 2. Oynanış

- Boru parçalarına tıklayarak/dokunarak 90 derece döndürün.
- Kaynaktan hedefe kadar sızıntısız, kesintisiz bir su yolu oluşturun.
- **Suyu Başlat** butonuna basın.
- Yol geçerliyse su animasyonu oynar ve hamle sayınıza göre 1-3 yıldız
  kazanırsınız (daha az hamle, daha yüksek yıldız).
- Başarılı tamamlamanın ardından su tasarrufuyla ilgili bir bilgi/ipucu
  mesajı gösterilir.
- Bölüm tamamlandığında bir sonraki bölümün kilidi açılır.

## 3. Temel Özellikler

- 100 kampanya bölümü (5x5'ten 10x10'a kadar artan zorlukta).
- Sıralı, kilitli ilerleme sistemi (bir bölümü tamamlamadan sonrakine
  geçilemez).
- Bölüm başına en iyi yıldız kaydı.
- Oynarken canlı 3→2→1 yıldız önizlemesi (kalan hamle hakkına göre).
- Tarayıcı-lokal (PlayerPrefs tabanlı) ilerleme kaydı.
- İlerlemeyi sıfırlama seçeneği.
- Duyarlı (responsive) Web/masaüstü yerleşimi.
- Tam ekran desteği (gerçek tarayıcı `requestFullscreen` köprüsü ile).
- Müzik ve ses efektleri, ayrı ses seviyesi kaydırıcılarıyla.
- Yağmur, bulut, su akışı, çiçek açması ve ördek efektleri.
- 100 adet su-farkındalığı eğitim mesajı.
- Tamamen Türkçe arayüz.

## 4. Ekran Görüntüleri

> Ekran görüntüleri `Documentation/Screenshots/` klasörüne eklenebilir.

## 5. Kullanılan Teknolojiler

- **Unity Editor**: `6000.3.11f1` (bkz. `ProjectSettings/ProjectVersion.txt`)
- **C#** (Unity script backend)
- **Unity WebGL** build platformu
- **Unity UI (uGUI)** ve **TextMeshPro** (kart/ayarlar metinleri için)
- **Unity Input System** paketi (`com.unity.inputsystem`)
- **Universal Render Pipeline** (`com.unity.render-pipelines.universal`)
- **JavaScript / `.jslib` köprüleri**: tam ekran (`WebFullscreen2D.jslib`)
  ve WebGL `PlayerPrefs` → IndexedDB senkronizasyonu
  (`GameProgressSync2D.jslib`)
- **PlayerPrefs / IndexedDB tabanlı tarayıcı-lokal ilerleme kaydı**

## 6. Sahne Akışı

```
MainMenuScene2D  →  LevelSelectScene2D  →  GameScene2D
```

- **MainMenuScene2D**: Oyuna Başla, Ayarlar (müzik/ses efekti aç-kapa ve
  seviye kaydırıcıları) ve İlerlemeyi Sıfırla seçeneklerini içerir.
- **LevelSelectScene2D**: Kampanyadaki 100 bölümü kart olarak listeler;
  kilitli/açık durumu ve kazanılan yıldızları `GameProgress2D`'den okuyarak
  gösterir.
- **GameScene2D**: Asıl bulmaca oynanışı, hamle/yıldız göstergesi, üst bar
  (Geri, Tam Ekran, Ayarlar) ve başarı paneli burada bulunur.

## 7. Proje Yapısı

```
Assets/
├── Art2D/              # Görsel varlıklar (FinalSprites, Placeholder, PipesTileset)
├── Audio/               # Müzik ve SFX dosyaları
├── Editor/               # Editor-only builder/validator scriptleri ([MenuItem])
├── Fonts/                # TMP font asset'leri (SHPinscher SDF)
├── Plugins/WebGL/        # .jslib WebGL köprüleri
├── Prefabs2D/            # Boru, UI ve efekt prefab'ları
├── Resources/            # CampaignLevelCatalog2D ve bölüm verileri
├── SHPinscher-Regular11/ # Kaynak font dosyası
├── Scenes/               # MainMenuScene2D, LevelSelectScene2D, GameScene2D
├── Scripts/              # Oyun/UI/ses/görsel runtime scriptleri
├── Settings/              # URP render pipeline ayarları
├── TextMesh Pro/          # TMP Essential Resources
└── WebGLTemplates/         # Özel WebGL şablonu (YagmurRotasiWeb)

Packages/
ProjectSettings/
Documentation/
```

## 8. Gereksinimler

- **Unity Editor `6000.3.11f1`** (tam sürüm eşleşmesi önerilir).
- **WebGL Build Support** modülü (Unity Hub üzerinden eklenmelidir).
- Güncel bir masaüstü web tarayıcısı (Chrome, Edge, Firefox vb.).
- WebGL build'i test etmek için yerel/uzak bir HTTP sunucu ortamı
  (bkz. [WebGL Build Alma](#10-webgl-build-alma)).

## 9. Projeyi Açma

1. Depoyu klonlayın veya indirin.
2. Unity Hub'ı açın.
3. `6000.3.11f1` sürümünün kurulu olduğundan emin olun (değilse Unity
   Hub üzerinden ekleyin).
4. Unity Hub'da **Add** ile proje klasörünü seçin ve projeyi açın.
5. Unity'nin paket/script derlemesini tamamlamasını bekleyin.
6. Proje penceresinden `Assets/Scenes/MainMenuScene2D.unity` sahnesini
   açın.
7. Play Mode'a girerek oyunu test edin.

## 10. WebGL Build Alma

Sahne sırası (`Build Settings` içinde doğrulanmıştır):

```
0 — MainMenuScene2D
1 — LevelSelectScene2D
2 — GameScene2D
```

Adımlar:

1. **File > Build Settings** açın, platformun **WebGL** olarak seçili
   olduğundan emin olun.
2. Test için **Build And Run** kullanın (Unity, sonucu otomatik olarak
   yerel bir sunucu üzerinden açar).
3. Üretilen `index.html` dosyasını doğrudan `file://` protokolüyle
   açmayın — WebGL build'leri modern tarayıcı güvenlik kısıtlamaları
   nedeniyle bir HTTP(S) sunucusu gerektirir.
4. Build çıktısı klasörünün **tamamını** (ör. `Build/`, `TemplateData/`,
   varsa `StreamingAssets/` ile birlikte `index.html`) dağıtın; klasör
   ilişkilerini bozmayın.

## 11. Web Sitesine Yükleme

- `index.html`, `Build/`, `TemplateData/` ve (varsa) `StreamingAssets/`
  klasörlerinin **hepsini** aynı klasör yapısını koruyarak sunucuya
  yükleyin.
- Yalnızca `index.html` dosyasını yüklemek yeterli değildir.
- Oyunu mutlaka HTTP veya HTTPS üzerinden servis edin.
- Güncellemeler arasında tarayıcı-lokal ilerleme kaydının korunması için
  oyunu **aynı origin (alan adı + yol)** üzerinden servis etmeye devam
  edin.
- Tarayıcı depolaması cihaza/tarayıcıya özeldir; cihazlar arasında
  otomatik senkronize olmaz.

## 12. Kayıt Sistemi

- Başlangıçta yalnızca Bölüm 1 açıktır.
- Bir bölüm tamamlandığında bir sonraki bölümün kilidi açılır.
- Her bölüm için elde edilen en iyi yıldız sayısı saklanır.
- İlerleme, tarayıcının yerel depolamasında (`PlayerPrefs`, WebGL'de
  IndexedDB'ye senkronize edilir) tutulur.
- Tarayıcı/site verilerinin temizlenmesi yerel ilerlemeyi siler.
- İlerleme bulut senkronizasyonu **yapmaz** — cihaza/tarayıcıya özeldir.
- **İlerlemeyi Sıfırla**, ses (müzik/SFX) tercihlerini silmez; yalnızca
  bölüm ilerlemesini sıfırlar.

## 13. Kontroller

| Eylem | Açıklama |
|---|---|
| Boruya tıkla/dokun | Boruyu 90 derece döndürür |
| **Suyu Başlat** | Mevcut yerleşimi test eder, geçerliyse su akışını başlatır |
| **Sıfırla** | Bölümü başa döndürür |
| **Geri** | Bölüm Seçimi ekranına döner |
| **Ayarlar** | Müzik/SFX aç-kapa ve ses seviyesi kaydırıcılarını açar |
| **Tam Ekran** | Tarayıcı tam ekran modunu açar/kapatır |

## 14. Geliştirici Araçları

Aşağıdaki komutlar, projenin **güncel ve güvenli** Editor araçlarıdır
(`Unity Editor` menü çubuğunda `YagmurRotasi2D` altında bulunur). Çok
sayıda eski "Phase" adlı builder script projede tarihsel nedenlerle
korunmuştur; bunlar günlük geliştirme akışının bir parçası değildir ve
burada listelenmemiştir (bkz. [Önemli Teknik Notlar](#16-önemli-teknik-notlar)).

| Komut | Amaç |
|---|---|
| `YagmurRotasi2D > Build Phase 9A Level Select Scene` | `LevelSelectScene2D`'nin güncel Web/landscape yerleşimini idempotent şekilde inşa eder/onarır (arka plan, kartlar, tam ekran butonu dahil). |
| `YagmurRotasi2D > Build Phase 7E8 Main Menu` | `MainMenuScene2D`'nin güncel Web yerleşimini inşa eder/onarır. İsmindeki eski faz numarasına rağmen bu, Ana Menü için **hâlâ güncel ve tek** sahne builder'ıdır. |
| `YagmurRotasi2D > Audio > Build Audio System` | Üç ana sahnede `GameAudioManager2D`'yi kurar, tüm butonlara `UIButtonSound2D` ekler, ayarlar panellerine ses seviyesi kaydırıcılarını (Slider + `AudioVolumeSlider2D`) ekler/onarır. |
| `YagmurRotasi2D > UI > Repair Settings Layout` | Ana Menü ve oyun-içi Ayarlar panellerinin dikey yerleşimini (`VerticalLayoutGroup` tabanlı) tek noktadan onarır. **`Build Audio System` çalıştırıldıktan sonra** çalıştırılmalıdır (ses kaydırıcı satırlarının önce var olması gerekir). |
| `YagmurRotasi2D > Web > Repair Fullscreen Buttons` | Ana Menü ve `GameScene2D`'deki Tam Ekran butonlarının çalışma zamanı bağlantısını doğrular/onarır (Level Select'in tam ekran onarımı doğrudan Phase 9A builder'ının içindedir). |
| `YagmurRotasi2D > Build Level Button Prefab` | `LevelButton2D.prefab`'ı (TMP metinleri, ses, kilit/yıldız görselleri) inşa eder/onarır. |
| `YagmurRotasi2D > Build In-Game Menu Prefab` | `InGameMenu2D.prefab`'ı (duraklatma/ayarlar menüsü) inşa eder/onarır. |
| `YagmurRotasi2D > Build Dedicated Success Panel Prefab` | Başarı panelini inşa eder/onarır. |
| `YagmurRotasi2D > Phase 9 > Build All Web Layouts` | Üç sahnenin Web yerleşimini tek seferde inşa eder. |
| `YagmurRotasi2D > Phase 9 > Validate Web Layouts` | Web yerleşimlerini doğrular (salt okunur). |
| `YagmurRotasi2D > Progress > Audit Star Difficulty` | Bölümlerin yıldız zorluk eşiklerini denetler (salt okunur). |
| `YagmurRotasi2D > Education > Validate Water Messages` | 100 eğitim mesajının bütünlüğünü doğrular (salt okunur). |
| `YagmurRotasi2D > Phase 8 > Validate All Campaign Levels` | 1-100 arası tüm kampanya bölümlerini doğrular (salt okunur). |
| `YagmurRotasi2D > Phase 8 > Validate Unique Solutions` | Her bölümün tek çözümlü olduğunu doğrular (salt okunur). |
| `YagmurRotasi2D > Run Phase 7F Branching Solver Tests` | Akış çözücüsü (T/Cross boru desteği dahil) için regresyon testleri çalıştırır. |

## 15. Yeni Bölüm Ekleme / Geliştirme

- Bölüm verisi `CampaignLevelDefinition2D` (ScriptableObject) ile
  tanımlanır ve `CampaignLevelCatalog2D` içinde (`Assets/Resources/`)
  sırayla indekslenir; `LevelManager2D.ProductionLevelCount` bu katalogdan
  türetilir, hiçbir yerde sabit sayı olarak kodlanmaz.
- Her boru için hem `startRotationIndex` (oyuncunun karşılaştığı
  karıştırılmış başlangıç durumu) hem de `solvedRotationIndex` (doğru/çözülü
  durum) ayrı ayrı tanımlanır.
- Yeni/üretilen bölümler Editor-only araçlarla (`Assets/Editor/Campaign/`)
  oluşturulur ve gerçek `FlowSolver2D` ile doğrulanır; çalışma zamanı asla
  bölüm üretmez veya çözüm aramaz.
- Yeni bir bölüm eklendikten sonra `YagmurRotasi2D > Progress > Audit Star
  Difficulty` ile zorluk/yıldız eşikleri kontrol edilmelidir.
- Eğitim mesajı eklerken `WaterMessageCatalog2D`'ye ekleyin ve
  `YagmurRotasi2D > Education > Validate Water Messages` ile doğrulayın.
- Yeni bölümler, `GameProgress2D`'nin var olan kayıt anahtarlarıyla
  otomatik uyumludur (bölüm sayısı katalogdan türetildiği için mevcut
  oyuncu ilerlemesi bozulmaz).

## 16. Önemli Teknik Notlar

- **`GameProgress2D`**, kampanya ilerlemesi (en yüksek açık bölüm, bölüm
  başına en iyi yıldız) için tek otoritedir.
- **`ScoreManager2D`**, hamle sayısı ve canlı yıldız durumu için tek
  otoritedir.
- **`BoardManager2D.VisualPackingScale`**, tahta paketleme oranı için
  paylaşılan tek kaynaktır; grid aralığıyla senkron tutulmalıdır.
- **`IndependentWorldFXRoot`**, `BoardFitContainer`'ın dışında kalmalıdır
  — `CloudAndRain`/`SuccessFXZone` asla tekrar `BoardRoot` altına
  taşınmamalıdır.
- **`WebFullscreen2D.jslib`** ve **`GameProgressSync2D.jslib`**, WebGL
  build'i için gereklidir (sırasıyla tam ekran ve ilerleme
  senkronizasyonu).
- Unity varlıkları ve ilgili `.meta` dosyaları her zaman birlikte
  tutulmalıdır.

## 17. Test Kontrol Listesi

- [ ] Sahne geçişleri: Ana Menü → Bölüm Seçimi → Oyun → Geri
- [ ] Bölüm kilidi açma (bir bölümü bitirince bir sonraki açılıyor mu?)
- [ ] En iyi yıldız kaydı doğru gösteriliyor mu?
- [ ] Sayfa yenilendiğinde ilerleme korunuyor mu?
- [ ] İlerlemeyi Sıfırla doğru çalışıyor mu?
- [ ] Müzik/SFX aç-kapa ve ses seviyesi kaydırıcıları çalışıyor mu?
- [ ] Tam ekran açılıp kapanıyor mu (ve Esc ile çıkışta buton durumu
      bozulmuyor mu)?
- [ ] 1920×1080, 1366×768 ve 960×540 çözünürlüklerinde yerleşim bozulmuyor
      mu?
- [ ] Başarılı bölüm sonunda eğitim mesajı görünüyor mu?
- [ ] Tarayıcı konsolunda hata/uyarı yok mu?

## 18. Bilinen Sınırlamalar

- İlerleme kaydı tamamen tarayıcı-lokaldir; bulut senkronizasyonu yoktur.
- Tarayıcı/site verilerinin silinmesi ilerlemeyi tamamen sıfırlar.
- Tarayıcı tam ekran API'si, kullanıcı etkileşimi (tıklama) gerektirir;
  programatik/otomatik tam ekran açılışı desteklenmez.
- Birincil hedef deneyim, masaüstü/landscape Web kullanımıdır.

## 19. Katkı ve Geliştirme

- Değişiklik yapmadan önce ayrı bir dal (branch) oluşturun.
- Değişiklikleri odaklı ve tek amaçlı tutun.
- Değişikliklerinizi üç ana sahnede (`MainMenuScene2D`,
  `LevelSelectScene2D`, `GameScene2D`) test edin.
- Mümkünse bir WebGL build alıp tarayıcıda test edin.
- Unity `.meta` dosyalarını varlıklarıyla birlikte taşıyın/commit edin;
  bir varlığı silmeden `.meta` dosyasını silmeyin (veya tersini
  yapmayın).

## 20. Proje Durumu

- Ana oyun döngüsü tamamlanmıştır.
- 100 kampanya bölümü mevcuttur.
- Bu devir noktasında bildirilen bilinen hatalar kapatılmıştır.
- Her yeni sürüm/dağıtım öncesinde WebGL build'inin gerçek bir tarayıcı
  ortamında doğrulanması önerilir.

## 21. Lisans ve İletişim

Bu proje için henüz açık kaynak lisansı tanımlanmamıştır. Kullanım ve
dağıtım izinleri için proje sahibiyle iletişime geçilmelidir.
