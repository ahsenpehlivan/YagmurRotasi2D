using System;
using System.Collections.Generic;

namespace YagmurRotasi2D.Education2D
{
    /// <summary>Which of the three educational categories a message belongs to - drives the small type label shown above the message on the success panel.</summary>
    public enum WaterMessageType2D
    {
        WaterSavingTip,
        FunFact,
        MotivationalQuote
    }

    /// <summary>One campaign level's educational water-awareness message. Plain data, never PlayerPrefs-backed - this is static campaign content, not player progress.</summary>
    [Serializable]
    public sealed class WaterMessageEntry2D
    {
        public int levelNumber;
        public WaterMessageType2D type;
        public string message;

        public WaterMessageEntry2D(int levelNumber, WaterMessageType2D type, string message)
        {
            this.levelNumber = levelNumber;
            this.type = type;
            this.message = message;
        }
    }

    /// <summary>
    /// The single authoritative source for every campaign level's educational
    /// water-awareness message - exactly one hand-authored entry per level,
    /// 1 through 100 (TotalLevels). A plain static class (not a
    /// PlayerPrefs-backed store, not a runtime-mutable ScriptableObject
    /// asset) - this content never changes at runtime, so a compile-time
    /// list is the simplest and most robust source of truth, with no
    /// Resources.Load/missing-asset failure mode. Mirrors the same
    /// hand-authored-static-list convention LevelManager2D.BuildLevels()
    /// already uses for Levels 1-6.
    /// </summary>
    public static class WaterMessageCatalog2D
    {
        public const int TotalLevels = 100;

        /// <summary>Level 100's message is a fixed campaign requirement - never regenerated or reworded by tooling.</summary>
        public const string FinalLevelMessage = "Suyun yolunu tamamladın! Şimdi her damlayı koruma sırası sende.";

        private const string FallbackMessage = "Suyu korumak, doğaya değer vermektir.";

        private static readonly List<WaterMessageEntry2D> entries = BuildEntries();
        private static readonly Dictionary<int, WaterMessageEntry2D> byLevel = BuildLookup(entries);

        /// <summary>Every authored entry, in level-number order - used by the Editor validator/preview tools. Never mutated by callers (the backing list is never exposed directly).</summary>
        public static IReadOnlyList<WaterMessageEntry2D> AllEntries => entries;

        /// <summary>
        /// Safe lookup by level number - invalid/missing level numbers (out
        /// of the catalog's 1-100 range, or a hole if one were ever
        /// introduced) return a short, safe fallback entry instead of
        /// throwing or returning null, so a UI caller never needs its own
        /// null-check/try-catch around this.
        /// </summary>
        public static WaterMessageEntry2D GetMessageForLevel(int levelNumber)
        {
            if (byLevel.TryGetValue(levelNumber, out WaterMessageEntry2D entry))
            {
                return entry;
            }

            return new WaterMessageEntry2D(levelNumber, WaterMessageType2D.MotivationalQuote, FallbackMessage);
        }

        private static Dictionary<int, WaterMessageEntry2D> BuildLookup(List<WaterMessageEntry2D> source)
        {
            var map = new Dictionary<int, WaterMessageEntry2D>(source.Count);
            foreach (WaterMessageEntry2D entry in source)
            {
                map[entry.levelNumber] = entry;
            }
            return map;
        }

        private static List<WaterMessageEntry2D> BuildEntries()
        {
            var tip = WaterMessageType2D.WaterSavingTip;
            var fact = WaterMessageType2D.FunFact;
            var quote = WaterMessageType2D.MotivationalQuote;

            return new List<WaterMessageEntry2D>(TotalLevels)
            {
                // Levels 1-20: simple habits at home - closing taps, using only
                // the water needed, reporting leaks, washing produce in a bowl,
                // reusing leftover clean water for plants.
                new WaterMessageEntry2D(1, tip, "Musluğu ihtiyacın kadar aç; suyu boşuna akıtmadan işini bitir."),
                new WaterMessageEntry2D(2, fact, "Evindeki her musluk, uzak bir kaynaktan gelen suyu sana ulaştırır."),
                new WaterMessageEntry2D(3, quote, "Az kullanmak, aslında çok şey korumak demektir."),
                new WaterMessageEntry2D(4, tip, "Diş fırçalarken bardak kullanmak, boşa akan suyu önler."),
                new WaterMessageEntry2D(5, tip, "Musluk damlıyorsa bir büyüğüne haber ver, küçük bir damla bile değerlidir."),
                new WaterMessageEntry2D(6, fact, "Şehrimize gelen su, uzun borular ve emek dolu bir yolculuktan geçer."),
                new WaterMessageEntry2D(7, quote, "Suyu seven bir çocuk, geleceğini de sevmiş olur."),
                new WaterMessageEntry2D(8, tip, "Meyveleri ve sebzeleri yıkarken suyu bir kapta biriktirmek daha tasarruflu bir yöntemdir."),
                new WaterMessageEntry2D(9, fact, "Bulaşıkları biriktirip yıkamak, suyu sürekli açık tutmaktan daha akıllıcadır."),
                new WaterMessageEntry2D(10, tip, "Bulaşık yıkarken kalan temiz suyla bahçedeki çiçekleri de sulayabilirsin."),
                new WaterMessageEntry2D(11, quote, "Her kapatılan musluk, doğaya söylenmiş küçük bir teşekkürdür."),
                new WaterMessageEntry2D(12, tip, "Duşta gereğinden uzun kalmamak, hem sana hem doğaya iyi gelir."),
                new WaterMessageEntry2D(13, fact, "Evimizdeki suyun çoğu, aslında uzak göllerden ve nehirlerden yolculuk eder."),
                new WaterMessageEntry2D(14, tip, "Elini sabunlarken musluğu kapatmak küçük ama etkili bir alışkanlıktır."),
                new WaterMessageEntry2D(15, quote, "Su, paylaştıkça çoğalan bir hazinedir."),
                new WaterMessageEntry2D(16, fact, "Bir bardak su, sana ulaşana kadar uzun bir yolculuk yapar."),
                new WaterMessageEntry2D(17, tip, "Evde su tasarrufu, ailenle birlikte oynayabileceğin küçük bir oyun olabilir."),
                new WaterMessageEntry2D(18, quote, "Bugün korunan su, yarının umududur."),
                new WaterMessageEntry2D(19, fact, "Yağmur suyu toplama kapları, bahçe sulamak için harika bir yardımcıdır."),
                new WaterMessageEntry2D(20, quote, "Suyu koruyan çocuklar, doğaya en güzel armağanı verir."),

                // Levels 21-40: gardens and plants - watering at suitable times,
                // rainwater, soil moisture, protecting trees and green spaces.
                new WaterMessageEntry2D(21, tip, "Bitkileri sabah erken ya da akşamüzeri sulamak, suyun buharlaşmasını azaltır."),
                new WaterMessageEntry2D(22, fact, "Toprak, yağmur suyunu bir sünger gibi emerek bitkilere saklar."),
                new WaterMessageEntry2D(23, quote, "Her sulanan fidan, geleceğe atılmış küçük bir adımdır."),
                new WaterMessageEntry2D(24, tip, "Saksı topraklarının nemini kontrol et; kuru değilse sulamayı erteleyebilirsin."),
                new WaterMessageEntry2D(25, tip, "Yağmur suyunu bir kovada biriktirip bahçe sulamada kullanabilirsin."),
                new WaterMessageEntry2D(26, fact, "Ağaçların kökleri, toprağın derinliklerindeki suyu bulup yukarı taşır."),
                new WaterMessageEntry2D(27, quote, "Yeşili büyüten sabır, suyu koruyan sabırla birlikte yürür."),
                new WaterMessageEntry2D(28, tip, "Çimenleri fazla sulamak yerine, doğal yağmuru beklemek de bir seçenektir."),
                new WaterMessageEntry2D(29, fact, "Nemli toprak, bitkinin köklerini serinletir ve suyu daha uzun süre tutar."),
                new WaterMessageEntry2D(30, tip, "Damlama sulama yöntemi, suyu doğrudan köke ulaştırarak israfı azaltır."),
                new WaterMessageEntry2D(31, quote, "Bir ağacı sulamak, aslında geleceğe gölge bırakmaktır."),
                new WaterMessageEntry2D(32, fact, "Yeşil alanlar, yağmur suyunun toprağa sızmasına yardımcı olur."),
                new WaterMessageEntry2D(33, tip, "Saksı altlıklarında biriken suyu döküp tekrar bitkilere verebilirsin."),
                new WaterMessageEntry2D(34, tip, "Bahçe hortumunu açık unutmamak, günlerce fark etmeyeceğin bir israfı önler."),
                new WaterMessageEntry2D(35, fact, "Ormanlar, yağmur sularını tutarak toprağı erozyondan korur."),
                new WaterMessageEntry2D(36, quote, "Küçük bir fidanı sulayan el, büyük bir ormana katkı sunar."),
                new WaterMessageEntry2D(37, tip, "Sulama zamanını rüzgârsız saatlere denk getirmek, suyun daha verimli kullanılmasını sağlar."),
                new WaterMessageEntry2D(38, fact, "Çiçekler, ihtiyaç duydukları suyu yapraklarından ve köklerinden alır."),
                new WaterMessageEntry2D(39, quote, "Yeşili koruyan eller, suyu da korumuş olur."),
                new WaterMessageEntry2D(40, quote, "Parktaki her fidanı korumak, mahalleye su kadar değerli bir hediyedir."),

                // Levels 41-60: clouds, rain, streams, groundwater, the water
                // cycle, nature's own use of water.
                new WaterMessageEntry2D(41, fact, "Güneş, denizlerdeki suyu ısıtarak buhara dönüştürür ve gökyüzüne gönderir."),
                new WaterMessageEntry2D(42, fact, "Bulutlar, gökyüzünde toplanan su damlacıklarından oluşur."),
                new WaterMessageEntry2D(43, tip, "Yağmur yağarken dışarıdaki kovaları açık bırakmak, suyu doğal yoldan biriktirmenin bir yoludur."),
                new WaterMessageEntry2D(44, quote, "Her yağmur damlası, doğanın sabırla anlattığı bir hikâyedir."),
                new WaterMessageEntry2D(45, fact, "Yağmur, bulutlardaki su damlacıklarının ağırlaşıp yere düşmesiyle oluşur."),
                new WaterMessageEntry2D(46, fact, "Dereler, dağlardan gelen suyu ovalara ve denizlere taşır."),
                new WaterMessageEntry2D(47, quote, "Su, hiç durmadan dönen sabırlı bir yolculuktur."),
                new WaterMessageEntry2D(48, tip, "Dere kenarında çöp bırakmamak, suyun temiz kalmasına yardımcı olur."),
                new WaterMessageEntry2D(49, fact, "Yer altı suları, yağmurun toprağa sızmasıyla yavaş yavaş oluşur."),
                new WaterMessageEntry2D(50, quote, "Bulutlar, denizlerin göğe yazdığı mektuplardır."),
                new WaterMessageEntry2D(51, fact, "Kar, erimeye başladığında dereleri ve nehirleri besler."),
                new WaterMessageEntry2D(52, tip, "Doğada gördüğün su kaynaklarına saygılı davranmak, onları korumanın ilk adımıdır."),
                new WaterMessageEntry2D(53, fact, "Sular, denizden buluta, buluttan yağmura, yağmurdan tekrar denize durmadan yolculuk eder."),
                new WaterMessageEntry2D(54, quote, "Doğa, suyu asla israf etmez; sadece yeniden yolculuğa çıkarır."),
                new WaterMessageEntry2D(55, tip, "Piknik yaparken su kenarını temiz bırakmak, oradaki canlılara iyilik etmektir."),
                new WaterMessageEntry2D(56, fact, "Göller, çevrelerindeki birçok canlıya hem su hem de yaşam alanı sunar."),
                new WaterMessageEntry2D(57, quote, "Her damla, denizden gökyüzüne uzanan sabırlı bir yolcudur."),
                new WaterMessageEntry2D(58, tip, "Bahçende küçük bir su birikintisi bile kuşlara ve böceklere can suyu olabilir."),
                new WaterMessageEntry2D(59, quote, "Suyun döngüsü, doğanın hiç durmayan nefesidir."),
                new WaterMessageEntry2D(60, tip, "Doğada yürürken suyu kirletmemek, gelecekteki gezginlere de bir armağandır."),

                // Levels 61-80: schools, parks, neighbourhood responsibility,
                // shared public spaces, clean waterways, community cooperation.
                new WaterMessageEntry2D(61, tip, "Okulun su sebilini kullanırken bardağını gereğinden fazla doldurmamak iyi bir alışkanlıktır."),
                new WaterMessageEntry2D(62, fact, "Okul bahçesindeki ağaçlar, sınıf arkadaşların gibi düzenli suya ihtiyaç duyar."),
                new WaterMessageEntry2D(63, quote, "Bir okulun suyunu koruyan çocuklar, geleceğin de bekçileridir."),
                new WaterMessageEntry2D(64, tip, "Parktaki çeşmeyi kullandıktan sonra iyice kapatmak, herkes için önemlidir."),
                new WaterMessageEntry2D(65, tip, "Sınıf arkadaşlarınla su tasarrufu için küçük kurallar belirleyebilirsiniz."),
                new WaterMessageEntry2D(66, fact, "Mahalledeki parklar, yeşil kalabilmek için düzenli ve dikkatli sulamaya ihtiyaç duyar."),
                new WaterMessageEntry2D(67, quote, "Paylaşılan bir çeşme, paylaşılan bir sorumluluktur."),
                new WaterMessageEntry2D(68, tip, "Mahallede bir su kaçağı görürsen, belediyeye haber vermek büyük bir yardımdır."),
                new WaterMessageEntry2D(69, fact, "Şehirdeki su şebekesi, birçok evi aynı anda besleyen dev bir ağdır."),
                new WaterMessageEntry2D(70, quote, "Bir mahalleyi güzelleştiren şey, suyuna sahip çıkan komşulardır."),
                new WaterMessageEntry2D(71, tip, "Okul bahçesindeki musluğu kullanırken israf etmemek, herkese örnek olur."),
                new WaterMessageEntry2D(72, tip, "Toplu taşımayla gidilen bir gezide su şişeni doldurup tekrar kullanabilirsin."),
                new WaterMessageEntry2D(73, fact, "Belediyeler, temiz suyu evlere ulaştırmak için sürekli çalışan büyük bir emek harcar."),
                new WaterMessageEntry2D(74, quote, "Komşuların birlikte koruduğu su, mahallenin ortak hazinesidir."),
                new WaterMessageEntry2D(75, tip, "Parkta piknik yaparken su kenarına çöp bırakmamak küçük ama önemli bir katkıdır."),
                new WaterMessageEntry2D(76, fact, "Şehir parkları, hem insanlara hem de kuşlara serin bir su kaynağı sunar."),
                new WaterMessageEntry2D(77, quote, "Duru akan bir dere, doğanın gülümseyen yüzüdür."),
                new WaterMessageEntry2D(78, tip, "Sınıfça bir su tasarrufu panosu hazırlamak, arkadaşlarını da bilinçlendirir."),
                new WaterMessageEntry2D(79, fact, "Bir şehirdeki herkesin küçük bir dikkati, büyük bir su tasarrufuna dönüşebilir."),
                new WaterMessageEntry2D(80, quote, "Herkesin küçük çabası, suyun uzun yolculuğunu kolaylaştırır."),

                // Levels 81-99: long-term environmental responsibility, teaching
                // good habits, protecting water together, hopeful sayings.
                new WaterMessageEntry2D(81, quote, "Bugün öğrendiğin küçük bir alışkanlık, yarının büyük bir korumasıdır."),
                new WaterMessageEntry2D(82, tip, "Ailenle birlikte evde bir su tasarrufu listesi hazırlamak güzel bir başlangıçtır."),
                new WaterMessageEntry2D(83, fact, "Suyu koruma bilinci, küçük yaşta öğrenilen alışkanlıklarla güçlenir."),
                new WaterMessageEntry2D(84, quote, "Doğaya iyi bakan bir çocuk, geleceğe de iyi bakar."),
                new WaterMessageEntry2D(85, tip, "Kardeşine ya da arkadaşına su tasarrufu ipuçlarını anlatmak, bilgiyi çoğaltır."),
                new WaterMessageEntry2D(86, quote, "Ufak bir çaba, doğaya sunulan büyük bir armağandır."),
                new WaterMessageEntry2D(87, tip, "Evdeki su sayacını ailenle birlikte kontrol etmek, tasarrufu eğlenceli hâle getirir."),
                new WaterMessageEntry2D(88, fact, "Alışkanlıklar tekrar ettikçe kalıcılaşır; su tasarrufu da böyle bir alışkanlıktır."),
                new WaterMessageEntry2D(89, quote, "Suya değer veren bir el, doğaya da değer katar."),
                new WaterMessageEntry2D(90, tip, "Bir gün suyu az kullandığını fark edersen, kendini bu konuda ödüllendirebilirsin."),
                new WaterMessageEntry2D(91, tip, "Küçük bir tasarruf alışkanlığını her gün tekrarlamak, kalıcı bir alışkanlığa dönüşmesini sağlar."),
                new WaterMessageEntry2D(92, fact, "Doğaya değer veren toplumlar, suyu da uzun yıllar boyunca koruyabilir."),
                new WaterMessageEntry2D(93, tip, "Öğrendiğin su tasarrufu alışkanlıklarını büyüyünce de sürdürmeyi unutma."),
                new WaterMessageEntry2D(94, tip, "Yaz aylarında bahçe hortumunu kullanırken süreyi kısa tutmak suyu korumana yardımcı olur."),
                new WaterMessageEntry2D(95, fact, "Gelecek nesiller, bugün gösterdiğimiz özenle temiz suya kavuşabilir."),
                new WaterMessageEntry2D(96, tip, "Ailenle birlikte suyu nerelerde daha dikkatli kullanabileceğinizi konuşabilirsiniz."),
                new WaterMessageEntry2D(97, tip, "Bir gün büyüdüğünde, öğrendiğin bu alışkanlıkları başkalarına da öğretebilirsin."),
                new WaterMessageEntry2D(98, tip, "Evdeki herkesle birlikte kısa süreli bir su tasarrufu haftası düzenleyebilirsin."),
                new WaterMessageEntry2D(99, quote, "Suyu koruyan bir dünya, herkes için daha güzel bir yarındır."),

                // Level 100: special final message celebrating completion of
                // the whole campaign journey - exact required text, never
                // paraphrased.
                new WaterMessageEntry2D(100, quote, FinalLevelMessage)
            };
        }
    }
}
