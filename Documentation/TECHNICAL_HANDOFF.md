# Teknik Devir Notları (Technical Handoff)

Bu dosya, Yağmur Rotası projesinin mimarisi hakkında gelecekteki
geliştiriciler için doğrulanmış, güncel teknik gerçekleri özetler.
Kronolojik geliştirme geçmişi için `Documentation/DEVELOPMENT_NOTES.md`
dosyasına, orijinal plan için `Documentation/ROADMAP_2D.md` dosyasına
bakınız.

## Genel Mimari

- Proje tamamen 2D'dir: `SpriteRenderer`, `BoxCollider2D`, `Physics2D`,
  ortografik kamera, XY oyun düzlemi, sadece Z ekseninde döndürme.
- Üç sahne akışı: `MainMenuScene2D` → `LevelSelectScene2D` → `GameScene2D`
  (Build Settings sırası tam olarak bu şekilde: 0/1/2).
- Kampanya 100 bölüm içerir (`CampaignLevelCatalog2D`,
  `LevelManager2D.ProductionLevelCount` bu katalogdan türetilir, asla sabit
  kodlanmaz).

## Otorite Sınıflar (Single Source of Truth)

- **`GameProgress2D`** — kampanya ilerlemesi için tek otorite: en yüksek
  açık bölüm, bölüm başına en iyi yıldız sayısı. Tarayıcı-lokal
  `PlayerPrefs` üzerinden saklanır.
- **`ScoreManager2D`** — hamle sayısı ve canlı (3→2→1) yıldız önizlemesi
  için tek otorite.
- **`BoardManager2D.VisualPackingScale`** — grid hücreleri, borular ve
  Source/Target görsellerinin paylaştığı tek paketleme ölçeği. Bu değer,
  `GridToWorld` hesaplamasındaki hücre aralığıyla senkron tutulmalıdır;
  biri değiştirilmeden diğeri değiştirilmemelidir.

## Kritik Hiyerarşi Kuralları

- **`IndependentWorldFXRoot`** sahne seviyesinde `BoardFitContainer`'ın bir
  kardeşidir (child'ı değil). `CloudAndRain` ve `SuccessFXZone` bu root'un
  altında yaşar ve **asla tekrar `BoardRoot` altına taşınmamalıdır** —
  aksi halde `BoardFitContainer`'ın çalışma zamanı ölçek/pozisyon
  değişikliklerini yanlışlıkla miras alırlar.
- `DuckFXRoot`/`FlowerFXRoot`, `SuccessFXZone`'un altında sabit kalır.
- Unity varlıkları ve `.meta` dosyaları her zaman birlikte tutulmalıdır.

## WebGL Gereksinimleri

- `Assets/Plugins/WebGL/WebFullscreen2D.jslib` — gerçek tarayıcı
  `requestFullscreen()`/`exitFullscreen()` köprüsü.
  `WebFullscreenController2D`/`WebFullscreenButtonForwarder2D` tarafından
  kullanılır; kaldırılırsa tam ekran çalışmaz.
- `Assets/Plugins/WebGL/GameProgressSync2D.jslib` — WebGL'de
  `PlayerPrefs`'in IndexedDB'ye zamanında yazılmasını garanti eden
  senkronizasyon köprüsü (`GameProgress2D.Save()` içinden çağrılır).
- `Assets/WebGLTemplates/YagmurRotasiWeb/` — özel WebGL şablonu
  (`#unity-container`/`#unity-canvas`).

## Buton/Listener Deseni (Önemli)

Bu projede tekrar tekrar karşılaşılan bir hata sınıfı: **Editor
tooling'inden (bir Builder script içinde) yapılan
`button.onClick.AddListener(...)` çağrısı hiçbir zaman sahneye kalıcı
olarak kaydedilmez** (`Button.m_OnClick.m_PersistentCalls`'a yazılmaz).
Bu yüzden bu projedeki tüm buton dinleyicileri, ilgili bileşenin kendi
`Awake()`/`OnEnable()` metodunda, idempotent bir `listenerRegistered`
koruması ile çalışma zamanında kaydedilir (örnekler:
`GameSceneBackButtonForwarder2D`, `WebFullscreenButtonForwarder2D`,
`UIButtonSound2D`, `AudioVolumeSlider2D`). Yeni bir buton eklerken bu
deseni takip edin.

## Ayarlar Paneli Layout Mimarisi

`MainMenuScene2D`'nin `SettingsCard`'ı ve `InGameMenu2D.prefab`'ın
`SettingsPage`'i, kendi `SettingsContent` alt objelerinde gerçek bir
`VerticalLayoutGroup` kullanır. Pozisyonlar asla başka bir kontrolün o
anki pozisyonundan hesaplanmaz — bu, önceden tekrarlanan builder
çalıştırmalarının kontrolleri üst üste bindirdiği bir hataya yol açmıştı.
Bu panellerin nihai layout sorumluluğu `SettingsLayoutBuilder2D`
("YagmurRotasi2D > UI > Repair Settings Layout") içindedir;
`GameAudioSystemBuilder2D` sadece ses kontrollerini (slider,
`AudioVolumeSlider2D`) oluşturur/bağlar, konumlandırma yapmaz.

## GUID/Referans Bütünlüğü

- Kök dizindeki (Assets/ dışındaki) gevşek görsel dosyalar ve üçüncü
  taraf `UIs/` paketi, temizlik sırasında kaldırılmıştır — bunlar Unity
  projesinin bir parçası değildi (GUID'leri yoktu) ve zaten
  `Assets/Art2D/FinalSprites/...` altına düzgün şekilde içe aktarılmış
  eşdeğerleri mevcuttu.
- `ProjectSettings/EditorBuildSettings.asset` içinde var olmayan bir
  sahneye (`SampleScene.unity`) işaret eden devre dışı bir kayıt
  temizlendi; sahne sırası artık tam olarak 0=MainMenu, 1=LevelSelect,
  2=GameScene.
