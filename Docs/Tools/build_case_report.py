"""Build the Turkish case report PDF (Docs/This_Is_Blast_Vaka_Raporu.pdf) from the README's
content, the README captures and the performance ledger's numbers. Run from the repo root:

    python Docs/Tools/build_case_report.py
"""
import os, sys
from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (Image, KeepTogether, PageBreak, Paragraph, SimpleDocTemplate,
                                Spacer, Table, TableStyle, XPreformatted)
from reportlab.graphics.shapes import Drawing, String, Line
from reportlab.graphics.charts.barcharts import VerticalBarChart

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SHOTS = os.path.join(ROOT, "Docs", "Screenshots", "readme")
OUT = os.path.join(ROOT, "Docs", "This_Is_Blast_Vaka_Raporu.pdf")

FONTS = "C:/Windows/Fonts"
pdfmetrics.registerFont(TTFont("Body", f"{FONTS}/calibri.ttf"))
pdfmetrics.registerFont(TTFont("BodyB", f"{FONTS}/calibrib.ttf"))
pdfmetrics.registerFont(TTFont("BodyI", f"{FONTS}/calibrii.ttf"))
pdfmetrics.registerFont(TTFont("Mono", f"{FONTS}/consola.ttf"))
pdfmetrics.registerFontFamily("Body", normal="Body", bold="BodyB", italic="BodyI", boldItalic="BodyB")

INK, MUTED, ACCENT, RULE = colors.HexColor("#1f2933"), colors.HexColor("#616e7c"), colors.HexColor("#c2410c"), colors.HexColor("#d9dde3")
S = {
    "title": ParagraphStyle("title", fontName="BodyB", fontSize=26, leading=30, textColor=INK, spaceAfter=4),
    "sub": ParagraphStyle("sub", fontName="Body", fontSize=12, leading=16, textColor=MUTED, spaceAfter=14),
    "h1": ParagraphStyle("h1", fontName="BodyB", fontSize=16, leading=20, textColor=INK, spaceBefore=10, spaceAfter=6),
    "h2": ParagraphStyle("h2", fontName="BodyB", fontSize=11.5, leading=14, textColor=ACCENT, spaceBefore=8, spaceAfter=3),
    "p": ParagraphStyle("p", fontName="Body", fontSize=10.5, leading=14.5, textColor=INK, spaceAfter=6),
    "li": ParagraphStyle("li", fontName="Body", fontSize=10.5, leading=14.5, textColor=INK, leftIndent=12, bulletIndent=2, spaceAfter=3),
    "cap": ParagraphStyle("cap", fontName="BodyI", fontSize=9, leading=12, textColor=MUTED, alignment=TA_CENTER, spaceAfter=8),
    "mono": ParagraphStyle("mono", fontName="Mono", fontSize=8.6, leading=11.5, textColor=INK, backColor=colors.HexColor("#f3f4f6"), borderPadding=(6, 8, 6, 8), spaceBefore=4, spaceAfter=10),
    "cell": ParagraphStyle("cell", fontName="Body", fontSize=9.5, leading=12.5, textColor=INK),
    "cellb": ParagraphStyle("cellb", fontName="BodyB", fontSize=9.5, leading=12.5, textColor=INK),
}


def P(text, style="p"):
    return Paragraph(text, S[style])


def bullets(items):
    return [Paragraph(t, S["li"], bulletText="\u2022") for t in items]


def table(rows, widths, header=True):
    data = [[Paragraph(c, S["cellb"] if header and i == 0 else S["cell"]) for c in row] for i, row in enumerate(rows)]
    t = Table(data, colWidths=widths, repeatRows=1 if header else 0)
    style = [("VALIGN", (0, 0), (-1, -1), "TOP"), ("LINEBELOW", (0, 0), (-1, -1), 0.4, RULE),
             ("TOPPADDING", (0, 0), (-1, -1), 4), ("BOTTOMPADDING", (0, 0), (-1, -1), 4), ("LEFTPADDING", (0, 0), (-1, -1), 4)]
    if header:
        style += [("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#f3f4f6")), ("LINEBELOW", (0, 0), (-1, 0), 0.8, INK)]
    t.setStyle(TableStyle(style))
    return t


def shot(name, height_mm):
    path = os.path.join(SHOTS, name) if not os.path.isabs(name) else name
    im = Image(path)
    ratio = im.imageWidth / im.imageHeight
    im.drawHeight, im.drawWidth = height_mm * mm, height_mm * mm * ratio
    return im


def shots_row(names, height_mm, captions):
    imgs = [shot(n, height_mm) for n in names]
    caps = [Paragraph(c, S["cap"]) for c in captions]
    t = Table([imgs, caps], colWidths=[im.drawWidth + 8 * mm for im in imgs], hAlign="CENTER")
    t.setStyle(TableStyle([("ALIGN", (0, 0), (-1, -1), "CENTER"), ("VALIGN", (0, 0), (-1, 0), "BOTTOM")]))
    return t


def frame_chart():
    steps = ["Bulunduğu hal\n(Android 30 fps)", "targetFrameRate 60", "Sert gölge,\npost yok, HDR yok", "Opak küp shader'ı\n(60 Hz cap)", "Telefon 90 Hz,\nyumuşak Low"]
    l01 = [33.3, 27.5, 16.7, 16.7, 11.1]
    l03 = [36.3, 36.3, 23.3, 16.7, 14.4]
    d = Drawing(460, 224)
    bc = VerticalBarChart()
    bc.x, bc.y, bc.width, bc.height = 40, 56, 400, 150
    bc.data = [l01, l03]
    bc.groupSpacing, bc.barSpacing, bc.barWidth = 14, 3, 20
    bc.valueAxis.valueMin, bc.valueAxis.valueMax, bc.valueAxis.valueStep = 0, 40, 10
    bc.valueAxis.labels.fontName, bc.valueAxis.labels.fontSize = "Body", 8
    bc.valueAxis.strokeColor = RULE
    bc.valueAxis.gridStrokeColor, bc.valueAxis.visibleGrid = RULE, 1
    bc.categoryAxis.categoryNames = [""] * len(steps)
    bc.categoryAxis.strokeColor = RULE
    bc.bars[0].fillColor, bc.bars[1].fillColor = colors.HexColor("#e76f51"), colors.HexColor("#457b9d")
    bc.bars.strokeColor = None
    bc.barLabelFormat, bc.barLabels.fontName, bc.barLabels.fontSize, bc.barLabels.nudge = "%.1f", "Body", 7.5, 6
    d.add(bc)
    refs = ((8.33, colors.HexColor("#2a9d8f"), "8.33 ms = 120 fps hedefi"), (16.67, colors.HexColor("#999999"), "16.67 ms = 60 fps"))
    for v, col, _ in refs:
        y = bc.y + bc.height * v / 40
        d.add(Line(bc.x, y, bc.x + bc.width, y, strokeColor=col, strokeDashArray=[4, 3], strokeWidth=0.8))
    gw = bc.width / len(steps)
    for i, name in enumerate(steps):
        for k, line in enumerate(name.split("\n")):
            d.add(String(bc.x + gw * i + gw / 2, 42 - k * 10, line, fontName="Body", fontSize=7.5, fillColor=INK, textAnchor="middle"))
    d.add(String(40, 16, "Level_01, vaka leveli (100 küp)", fontName="Body", fontSize=8, fillColor=colors.HexColor("#e76f51")))
    d.add(String(240, 16, "Level_03, stres leveli (900 küp, 100 shooter)", fontName="Body", fontSize=8, fillColor=colors.HexColor("#457b9d")))
    for x, (v, col, label) in zip((40, 240), refs):
        d.add(Line(x, 6, x + 18, 6, strokeColor=col, strokeDashArray=[4, 3], strokeWidth=0.8))
        d.add(String(x + 22, 3, label, fontName="Body", fontSize=8, fillColor=col))
    return d


def build():
    doc = SimpleDocTemplate(OUT, pagesize=A4, leftMargin=18 * mm, rightMargin=18 * mm, topMargin=16 * mm, bottomMargin=16 * mm,
                            title="This Is Blast - Vaka Raporu", author="Salih Gireniz")
    W = A4[0] - 36 * mm
    story = []

    # Cover
    story += [Spacer(1, 10 * mm), P("This Is Blast", "title"),
              P("Apps Game Developer Case · Salih Gireniz · 1-7 Eylül 2026", "sub"),
              P("Voodoo'nun <i>This is Blast!</i> çekirdek döngüsünün sıfırdan yapımı: 10x10 küp ızgarası, beş slot, "
                "ön sırası tıklanabilir bir shooter kuyruğu, gizli shooter'lar, WIN / LOST ekranları ve aynı leveli yeniden "
                "başlatan buton, JSON'dan okunan bir örnek level, merge yok. Unity 6000.0.68f1, URP 17. "
                "Sahne açılıp Play'e basıldığında level doğrudan başlar; menü yok."),
              Spacer(1, 4 * mm)]
    story.append(shots_row(["level_01_idle.png", "level_01_burst_frame.png", "level_01_win.png"], 92,
                           ["Açılış: örnek level, iki gizli shooter", "Üç shooter ateşte: mermi, iz ve sıçrama shooter'ın renginde", "WIN ekranı ve aynı leveli yeniden başlatan buton"]))
    story += [PageBreak()]

    # 1 Brief -> implementation
    story += [P("1. Brief'te istenenler ve karşılıkları", "h1"),
              table([
                  ["Brief", "Projede"],
                  ["10x10 ızgara, 5 slot, level'ın belirlediği shooter kolonları, seçilebilir sıranın arkasında 2 görünür sıra", "<font name='Mono'>LevelSpawner</font>; her yerleşim sayısı serileştirilmiş bir struct"],
                  ["Ön sıradaki shooter'a dokun; boş slota koşar, kendi renginin ön küplerine ateş eder", "<font name='Mono'>GameDirector</font> → <font name='Mono'>GameLoop.TrySelect / TryShoot</font>"],
                  ["Yıkılan küpün arkasındakiler shooter'a doğru akar", "<font name='Mono'>BoardModel</font> ön indeksi + <font name='Mono'>CollapseTweens</font>"],
                  ["Mühimmat bitince sahneden çıkar, slotu boşaltır; mühimmat var hedef yoksa yerinde kalır", "<font name='Mono'>SlotRow</font>: doluluk mühimmatın kendisidir"],
                  ["Gizli shooter: rengi seçilebilir sıraya gelince görünür", "<font name='Mono'>ShooterQueue.IsRevealed</font>, <font name='Mono'>LevelSpawner</font>"],
                  ["WIN / LOST ekranı ve aynı leveli yeniden başlatan buton", "<font name='Mono'>LevelEndViewModel</font> (R3) + <font name='Mono'>LevelEndView</font>, <font name='Mono'>SceneRestarter</font>"],
                  ["JSON'da tek örnek level: beş renk, gizli shooter, çözülebilir", "<font name='Mono'>Level_01.json</font>; <font name='Mono'>LevelFileTests</font> her level dosyasında renk başına mühimmat ≥ küp kuralını doğrular"],
                  ["Merge yok", "Yapılmadı; kancası da yok"],
              ], [W * 0.5, W * 0.5]),
              Spacer(1, 3 * mm),
              P("Oyun hissi (juice), orijinal APK'dan kare kare ölçülüp elle ayarlandı: shooter'ın koşusu, iniş ezilmesi ve sayaç vuruşu, "
                "hedefe dönüş, shooter renginde izli mermi, küpün üst yüzünde sıçrama, çizilen ölüm eğrisi, kamera sarsıntısı, "
                "iki sesli ve perde titreşimli atış sesi.")]

    # 2 Architecture
    story += [P("2. Mimari", "h1"),
              P("Altı assembly. Bağımlılık yönü asmdef referanslarıyla zorlanır, gelenekle değil; <font name='Mono'>ArchitectureTests</font> "
                "asmdef dosyalarını diskten okur ve bir katman üstündekine referans verirse kırmızıya döner."),
              XPreformatted(
                "Blast.Domain          saf C#: BoardModel, ShooterQueue, SlotRow, GameRules (noEngineReferences)\n"
                "  ^\n"
                "Blast.Application     GameLoop: TrySelect / TryShoot, karar, kolon durumları\n"
                "  ^            ^              ^\n"
                "Presentation   UI (MVVM, R3)  Infrastructure (LevelParser, PaletteData)\n"
                "  ^\n"
                "Blast.Bootstrap       GameLifetimeScope: VContainer kompozisyon kökü\n"
                "\n"
                "Blast.Diagnostics     PerfProbe / PerfSweep; yalnız development build, oyun katmanına referans yok", S["mono"])]
    story += bullets([
        "<b>Domain</b> Unity'yi bilmez ve içinde hiçbir şey hareket etmez: kart yazıldığı renkleri tutar, kolon başına bir ön indeks üzerinde yürür; kuyruk aynısını yapar. "
        "Bir slot, shooter'ın mühimmatı olduğu sürece doludur; \"boş slot\" ile \"mühimmat bitti\" birbiriyle çelişemez. <font name='Mono'>GameRules</font> üç saf fonksiyondur: en soldaki eşleşen ön küp, kazanıldı mı, kaybedildi mi.",
        "<b>Application</b> tek kullanım senaryosudur: <font name='Mono'>GameLoop</font>. Bir çağrı bir atışı bir küple değiştirir ve kararı aynı çağrıda yeniden okur; ekran hiçbir zaman bir hamle geç kalmaz. "
        "Önce <i>kazanıldı</i>, sonra <i>kaybedildi</i> sorulur, çünkü boşalmış kart ve dolu slot sırası ikisini de sağlar. Zaman kavramı yoktur; tempoyu Presentation verir.",
        "<b>Presentation</b> döngünün cevaplarını giydirir. <font name='Mono'>GameDirector</font> dokunuşu <font name='Mono'>TrySelect</font>'e çevirir ve her oturan shooter'ın atış döngüsünü UniTask ile yürütür; "
        "<font name='Mono'>LevelSpawner</font> dünya konumlarının tek sahibidir; view'lar itaatkârdır, karar vermez. Kolon durumu el sıkışması "
        "(<font name='Mono'>LockColumn / MarkSettling / MarkSettled</font>), domain küpü vurulduğu anda silerken merminin ekranda hâlâ havada olmasından doğar.",
        "<b>UI</b> R3 akışları üzerinde tek bir MVVM çifti. <b>Infrastructure</b> güven sınırıdır: parser her bozuk dosyayı yerini söyleyen bir <font name='Mono'>FormatException</font> ile reddeder.",
        "<b>Bootstrap</b> parse edilmiş modelleri, <font name='Mono'>GameLoop</font>'u, palet adaptörünü ve sahne bileşenlerini kaydeder; VContainer grafiği kurar ve her bileşenin <font name='Mono'>[Inject] Construct</font>'ını çağırır.",
    ])

    # 3 Tests
    story += [P("3. Testler", "h1"),
              table([["Takım", "Sayı", "Çalıştırma"],
                     ["Unity EditMode, assembly <font name='Mono'>Blast.Tests</font>", "92", "Test Runner penceresi"],
                     ["Level editörü, <font name='Mono'>level-editor/logic.js</font>", "41", "<font name='Mono'>node --test level-editor/logic.test.js</font>"]],
                    [W * 0.45, W * 0.1, W * 0.45]),
              Spacer(1, 2 * mm),
              P("Oyunun her kuralının testi var; MonoBehaviour'lar Humble Object deseniyle test edilir. Sessizce kırılan iki şeyi iki test korur: "
                "asmdef bağımlılık yönü ve her level dosyasında renk başına mühimmat ≥ küp kuralı (brief'in uyardığı \"kırmızı küp var, kırmızı shooter yok\" leveli).")]

    # 4 Levels and the original
    story += [P("4. Level verisi ve orijinal oyunun okunması", "h1"),
              P("Level bir metin dosyasıdır: sıralar ön sıradan başlayarak kolon başına bir harf (<font name='Mono'>Y R B G O</font>), her shooter kolonu önden arkaya, "
                "<font name='Mono'>hidden</font> rengi ön sıraya kadar gizler. <font name='Mono'>level-editor/index.html</font> bağımlılıksız bir HTML editördür: kartı boyar, kuyruğu otomatik doldurur, testlerle aynı kontrolleri yapar."),
              P('{ "boardLayers": [ { "rows": ["RRRRRBBBBB", "..."] } ], "slotCount": 5,<br/>'
                '  "shooterColumns": [ { "shooters": [ { "color": "R", "ammo": 10, "hidden": false } ] } ] }', "mono"),
              P("Orijinal APK okundu, kopyalanmadı", "h2"),
              P("Yayınlanan build UnityPy ile açıldı; kural: <b>sayı oku, asset alma.</b> Görünümün nelerden oluştuğu ölçüldü (Linear renk uzayı, küp tonları, TCP2 ramp değerleri, gölge bayrakları) ve "
                "2447 levelin nasıl saklandığı çıkarıldı: kart ve deste sütun-major, kartın son sırası shooter'lara bakar, gizli shooter bir <font name='Mono'>SecretItem</font> bayrağı, mühimmat her shooter için sabit 20. "
                "Levellerin 6 A/B kohortuna ayrılması (onboarding, 7. gün, erken churn) orijinalin canlı-operasyon yüzeyini de gösterdi."),
              P("<font name='Mono'>Docs/Tools/convert_original_level.py</font> bu şemayı bizimkine çevirir ve sonucu bizim kurallarla ispatlar: oyuncunun seçimleri üzerinde bir arama (DFS) kazanan bir hat bulmalı, "
                "rastgele atış sırası altında yeniden planlayan oyuncu kazanmalı. Orijinalin 4. leveli olduğu gibi girdi (<font name='Mono'>Level_Original_04.json</font>; 10x12, kendi renkleri, "
                "çalışan APK ile küp küp karşılaştırıldı) ve çözücü onu gerçek oyunda 26 dokunuşta kazandı.")]
    story.append(KeepTogether([shots_row(["level_original_04.png"], 78, ["Orijinalin 4. leveli bu projede: ön sıra YYBBYYRRYY, orijinalle aynı"])]))
    story += [P("Yoğun leveller neden yok", "h2"),
              P("Orijinal her shooter'a 20 mühimmat verir ve slotları merge ile açar; bu vakada merge yasak. Bizde mühimmat tam-uyumludur (renk başına küp sayısı o rengin shooter'larına bölünür), "
                "yoksa rengi biten bir shooter slotunu sonsuza dek tutar. Bu kurallar altında orijinalin yoğun levelleri ya çözülemez (18, 650) ya da yola bağımlıdır: 868 simülatörde %100 kazanılırken "
                "gerçek oyunda 12. dokunuşta takıldı, çünkü aynı renkten hangi shooter'ın ateş edeceğini kolon kilitleri ve koşu süresi belirler. Ölçüm üç kez aynı sonucu verdi; "
                "o yüzden örnek level elle yazılmış <font name='Mono'>Level_01</font> olarak kaldı. Brief'in \"tutarlı level\" kuralının somut hali budur: başka kurallar için yazılmış bir level, gerçek oyunda ölçülmeden tutarlı sayılmaz. Hiçbir mesh, doku, ses veya kod alınmadı.")]

    # 5 Level editor
    story += [P("5. Level editörü", "h1"),
              P("Levelleri elle JSON yazarak değil, bir editörle yapıyoruz. Bu editör Unity'nin içinde değil; deponun kökünde, tarayıcıda çalışan bir HTML sayfası. "
                "Kartı fırçayla boyar, shooter kuyruğunu kolon kolon düzenler ve JSON'u doğrudan Unity'nin okuduğu klasöre yazar. Kullanıcıları tasarımcılar olduğu için arayüzü Türkçe."),
              P("Nerede", "h2"),
              P("Depo kökünde <font name='Mono'>level-editor/</font> klasörü, üç dosya: <font name='Mono'>index.html</font> (arayüz), <font name='Mono'>logic.js</font> (level mantığı: okuma, yazma, doğrulama, "
                "otomatik doldurma, simülasyon) ve <font name='Mono'>logic.test.js</font> (41 test). <font name='Mono'>Assets/</font> dışındadır, Unity onu hiç görmez ve derlemez; oyuna tek dokunuşu, "
                "<font name='Mono'>Assets/00_GAME/Levels/</font> klasörüne yazdığı JSON dosyalarıdır. Oyun o dosyaları kendi parser'ından geçirir; editör ne yazmış olsa da son söz oyunun."),
              P("Neden HTML", "h2"),
              P("Alternatif bir Unity Editor penceresiydi: C# editör script'i, derleme, domain reload, ve level yapmak isteyen herkesin Unity'yi açıp projeyi yüklemesi. HTML'in maliyeti sıfır: kurulum yok, "
                "paket yok, sunucu yok, internet gerekmez; dosya çift tıkla açılır ve Unity kapalıyken de level yapılır. Level formatı zaten düz JSON olduğu için editörün Unity'den bir şey bilmesi gerekmiyor; "
                "aynı kuralları (renk başına mühimmat, kolon sayısı, boş kolon) JavaScript'te uygular ve bunlar <font name='Mono'>node --test</font> ile ayrı bir test takımında tutulur. "
                "Tek gerçek bedel, tarayıcının klasöre yazma izni: o da aşağıda."),
              P("Nasıl açılır", "h2"),
              P("Windows Gezgini'nde <font name='Mono'>level-editor/index.html</font> dosyasına çift tıklayın; varsayılan tarayıcı Chrome veya Edge olmalı (klasöre yazma yalnız onlarda var). "
                "Ya da dosyayı açık bir tarayıcı sekmesine sürükleyin; adres çubuğunda <font name='Mono'>file:///…/level-editor/index.html</font> görünür. Sayfa açıldığında kırmızı bir uyarı "
                "hangi klasörün seçileceğini söyler; ilk iş o. Kafa karıştırabilecek üç noktayı sırayla yazıyoruz: klasör seçimi, kaydetme ve Unity'nin dosyayı görmesi."),
              P("1. Klasör seçimi", "h2"),
              P("Editör dosyaları tarayıcının klasör erişimi (File System Access API) ile yazar; bu yalnız Chrome ve Edge'de vardır. Açılışta üstte kırmızı bir uyarı, seçilmesi gereken klasörün tam yolunu gösterir "
                "(<font name='Mono'>…\\Assets\\00_GAME\\Levels</font>) ve yanında <b>Yolu kopyala</b> düğmesi vardır. <b>Levels klasörünü seç…</b> düğmesine basınca tarayıcının klasör penceresi açılır; "
                "yolu üstteki adres alanına yapıştırıp Enter'a basın, klasörün <i>içine</i> girin ve <b>Select Folder</b> deyin. Tarayıcı bir kez izin ister, sonraki açılışlarda klasörü hatırlar. "
                "Dikkat: Edge kullanıcı klasörünün kendisini (Masaüstü, Belgeler) reddeder; seçilen klasör Levels'ın kendisi olmalı. Klasör seçildikten sonra üstteki liste klasördeki levelleri gösterir; "
                "<b>Aç</b> ile ya da çift tıkla yüklenir. Klasör erişimi olmayan bir tarayıcıda editör yine çalışır: <b>Aç</b> bir dosya penceresi açar, <b>İndir</b> JSON'u indirir, dosyayı Levels klasörüne elle atarsınız."),
              P("2. Yeni level ve var olanı güncelleme", "h2"),
              P("<b>Yeni</b>, listede boş olan ilk adla (Level_07.json gibi) 10x10, tek katman, beş slot, beş kolonluk boş bir level açar. Sağdaki panelden genişlik, yükseklik, katman ve kolon sayısı değişir; "
                "fırçadan renk seçip hücrelere tıklayarak ya da <b>Tahtayı fırçayla doldur</b> ile kart boyanır. Her kolonun altındaki <b>+ ekle</b> shooter ekler; her shooter'ın rengi, mermisi, "
                "gizli (?) kutusu ve yukarı/aşağı oklarıyla kuyruk sırası vardır. <b>Shooter'ları otomatik doldur</b> karttaki renk sayımına göre kuyruğu tam-uyumlu mühimmatla dağıtır; sağdaki Küp / Mermi tablosu "
                "renk başına farkı (Δ) anında gösterir, kırmızı bir Δ kazanılamaz bir level demektir. Kontroller bölümü Unity testleriyle aynı kuralları uygular ve her birini yerini söyleyerek yazar: boş tahta, boş kolon, mermisiz shooter, küpünden az mermisi olan renk (\"level kazanılamaz\"), beşten fazla kolon (dock'a sığmaz). Üstüne bir de açgözlü bir simülasyon oynar: hep en soldaki hedefe ateş eden basit bir oyuncu leveli bitiriyorsa \"Hata yok\" der, takılıyorsa kaç küp kala takıldığını uyarı olarak gösterir; dikkatli bir oyuncu yine kazanabilir, ama kuyruk sırasına bakmak gerekir."),
              P("<b>Kaydet</b> açık dosyanın üstüne yazar; <b>Farklı Kaydet</b> soldaki ad alanındaki adla yazar ve var olan bir dosyanın üstüne yazmadan önce sorar. Var olan bir leveli güncellemek için listeden açın, "
                "değiştirin, Kaydet. Kaydedilmemiş değişiklik varken başka dosya açmak sorar. Editörün gösteremediği bir şey içeren dosya (farklı renkli katmanlar gibi) açılırsa üstüne yazmaz, başka ad ister."),
              P("3. Unity'nin dosyayı görmesi", "h2"),
              P("Editör JSON'u diske yazar; Unity ise değişen bir dosyayı kendi penceresi odak aldığında içe aktarır. Çoğu zaman bu yeterlidir: editörden kaydedip Unity'ye geçin, Play'e basın. Ama Unity'nin "
                "otomatik yenilemesi kapalıysa ya da odak değişimini yakalamazsa eski level oynanır ve \"kaydettim ama değişmedi\" hissi doğar. O durumda iki yol var: Unity penceresinde <b>Ctrl+R</b> "
                "(Assets → Refresh), ya da Project panelinde <font name='Mono'>Level_XX.json</font> dosyasına sağ tık → <b>Reimport</b>. Level_01 Play'de doğrudan açıldığı için onu düzenledikten sonra Play'i durdurup "
                "yeniden başlatmak gerekir; başka bir leveli oynamak için sahnedeki <font name='Mono'>GameLifetimeScope</font> bileşeninin <font name='Mono'>_level</font> alanına o dosyayı sürükleyin."),
              ]
    story.append(KeepTogether([shots_row(["editor_empty.png"], 100, ["Açılış: kırmızı uyarı seçilecek klasörün yolunu gösterir, Yolu kopyala düğmesi yanında; sağda kart ayarları, fırça, Küp / Mermi tablosu ve kontroller"])]))
    story.append(KeepTogether([shots_row(["editor_loaded.png"], 104, ["Level_01 açık: kart boyalı, beş kolonda shooter'lar (renk, mermi, gizli kutusu, sıra), Küp / Mermi tablosunda her Δ sıfır, Kontroller yeşil"])]))

    # 6 Performance
    story += [P("6. Performans", "h1"),
              P("Hedef: 120 FPS ve oyun sırasında kare başına 0 B GC. Her ölçüm Samsung Galaxy A16 üzerinde, adb ile yüklenen development build'de, saniyede bir satır yazan bir çalışma zamanı probuyla alındı "
                "(fps, kare süresi, GC bayt, batch, SetPass, draw, gölge kaynağı, üçgen). Editör sayıları render ve GC için kullanılmadı; yalnız yanıltırlar. "
                "Yöntem hep aynıydı: önce ölç, tek bir şeyi değiştir, tekrar ölç, sonucu deftere yaz. Defter <font name='Mono'>Docs/PERFORMANCE.md</font>, alınmayanlar da sayısıyla orada."),
              frame_chart(),
              P("Kare süresi, ms (düşük iyi). Son iki çubuk ekranın tazeleme sınırında; kapağın altındaki gerçek iş adb üzerinden okundu: Level_01'de 4.24 ms.", "cap"),
              P("Nereye gitti, ne yapıldı", "h2"),
              P("İlk okuma varsayımı tersine çevirdi: darboğaz draw sayısı değil, gölge <i>örnekleme</i>siydi. Yumuşak gölge High kalitede piksel başına 16 doku okuması yapıyor ve küpler ekranı kaplıyor; "
                "sert gölge tek başına iki levelde de 8 ms geri verdi. İkinci büyük kalem küp shader'ıydı: TCP2'nin ürettiği <font name='Mono'>CustomShader</font> TransparentCutout'tu ve her pikselde <font name='Mono'>clip</font> çağırıyordu; "
                "oyunda alfası olan tek doku yok, ve Mali GPU'da bir fragment clip early-Z'yi öldürür. Opak yapılıp clip'ler silindi: 900 küplük levelde 7 ms. Post-process (vignette) ve HDR bir ara doku ve blit demekti, "
                "ölçüldü ve 2-3 ms için kaldırıldı. <font name='Mono'>targetFrameRate</font> Android'in varsayılan 30'undan 120'ye alındı; telefon ekranı 90 Hz olduğu için gerçek tavan 11.1 ms."),
              P("CPU tarafında hedef sıfır tahsisti. Kaynaklar tek tek kapatıldı: hareket eden her view için her hamlede yeni bir DOTween kısayolu yerine bir kez kurulan ve yeniden hedeflenen tek tween "
                "(<font name='Mono'>SetAutoKill(false)</font> + <font name='Mono'>ChangeEndValue</font>), küp ölümleri için paylaşılan bir çökme tween havuzu, mermi ve sıçrama havuzları, iki sesli sabit bir ses kanalı halkası, "
                "her yerde <font name='Mono'>sharedMaterial</font> (renk değişimi materyal örneği değil, paletin hazır materyaline geçiş). Boşta 16 B/kare bulunan tek tahsis Easy Save'in bir coroutine'iydi; eklenti silindi. "
                "Sonuç: boşta 0 B, ateş ederken salvo başına ~11 B/kare oyun tahsisi, o da <font name='Mono'>CancellationToken.Register</font>'ın restart güvenliği için bilerek bırakılan 48 baytları."),
              table([["", "Bulunduğu hal", "Teslim edilen"],
                     ["Level_01 kare süresi", "33.3 ms (30 fps)", "11.1 ms, telefonun 90 Hz sınırı; altında 4.24 ms gerçek iş"],
                     ["Level_03 (900 küp) kare süresi", "36.3 ms", "14.4 ms"],
                     ["SetPass call", "11-14, her sahnede", "11-14, değişmedi"],
                     ["GC, boşta", "16 B/kare", "0 B"],
                     ["GC, ateş ederken", "78 B/kare", "0 B; bir salvo içinde ~11 B/kare oyun tahsisi"]],
                    [W * 0.3, W * 0.25, W * 0.45]),
              Spacer(1, 3 * mm),
              P("Neden SRP Batcher'a güvenildi", "h2"),
              P("İlk ölçümde SetPass sayısı küp sayısından bağımsız 11'de duruyordu: 100 küp de 900 küp de aynı. Bu, URP'nin SRP Batcher'ının işini yaptığının kanıtıdır. Batcher, aynı shader varyantını kullanan "
                "her renderer'ı materyali farklı olsa bile tek bir batch'te toplar ve materyal verilerini GPU'da kalıcı tutar; küplerin hepsi aynı TCP2 shader'ının aynı varyantı olduğu için beş renk beş SetPass etmiyor. "
                "Mermi de küplerin palet materyalini giyer, o yüzden atış sırasında draw sayısı artmaz; renkli parçacık da beyaz parçacıkla aynı draw'dur. Buna güvenmenin şartı, batcher'ı kıran şeyleri yapmamaktı: "
                "MaterialPropertyBlock yok, çalışma zamanında materyal örneği yok, per-renderer keyword yok."),
              P("Neden GPU instancing yapılmadı", "h2"),
              P("Instancing ile SRP Batcher birbirini dışlar; instancing'e geçmek batcher'dan vazgeçmek demektir. Karar tahminle değil ölçümle verildi: draw sayısının maliyetini görmek için 390 küp gölge geçişinden çıkarıldı, "
                "390 batch gitti ve kare süresi 0 ms değişti. Draw sayısı hiçbir zaman darboğaz olmadı; SetPass zaten 11-14'te. Instancing'in kazandıracağı şeyi batcher zaten kazandırıyordu, "
                "üstüne bir de kendi shader desteğini ve per-instance veri yönetimini getirecekti. Yapılmadı, gerekçesi sayısıyla defterde."),
              P("Neden küpler havuzlanmadı", "h2"),
              P("Havuz, dönen (churn eden) nesneler için vardır: mermi ve sıçrama saniyede kırk kez doğup ölür, bu yüzden havuzlandılar. Bir küp level yüklenirken bir kez yaratılır, ölürken bir kez yok edilir; "
                "dönme yok. Havuz maliyeti yükleme anından yükleme anına taşır ve yönetilecek bir ömür ekler; tahsis avında ölüm yolu ölçülebilir hiçbir şey katmıyordu (salvoda toplam 11 B/kare). "
                "Aynı şekilde ekran dışı küpleri kapatmak da ölçüldü: 400 küpün 130'u frustum içindeydi ve Unity zaten 378 batch çiziyordu, yani frustum culling işi yapıyordu; elle kapatmak sadece "
                "Unity'nin bedava yaptığı testi C#'a taşımak olurdu ve ışığa göre kesilen gölge kaynaklarını yanlışlıkla düşürürdü. İkisi de alınmadı."),
              P("Ölçülüp alınmayan diğerleri: 512'lik gölge haritası (0 ms), MSAA'yı kapatmak (0 ms), render scale 0.75 (2.5 ms ama bulanık), Optimized Frame Pacing (+2 ms, 90 Hz'e geçirmedi). "
                "Kapağın altındaki gerçek iş adb üzerinden profiler ile okundu: Level_01'de kare başına 4.24 ms, tamamı render; listede hiçbir gameplay script'i yok. 11.1 ms'lik bütçeye karşı 2.6 kat pay; oyun ekrana bağlı, hesaba değil, ve iş burada bitti.")]

    # 7 Process
    story += [P("7. Süreç ve git", "h1"),
              P("1-6 Eylül arasında 150'den fazla commit; her yeşil parça bir commit, her davranış önce testiyle. Değerlendirme sırası (bug-free &gt; oyun hissi &gt; mimari &gt; performans &gt; git) her çelişkide "
                "karar vericiydi: bir bug riski taşıyan juice eklenmedi, teslimden günler önce orkestratörü yeniden yazmak reddedildi. Depoda üç kayıt dosyası vardır: "
                "<font name='Mono'>Docs/PLAN.md</font> (tasarım), <font name='Mono'>Docs/PERFORMANCE.md</font> (performans defteri), <font name='Mono'>Docs/ORIGINAL_GAME_ANALYSIS.md</font> (orijinalin okunması); "
                "parça parça yapım kaydı <font name='Mono'>.claude/notes/case-status-log.md</font>'dir.")]

    doc.build(story)
    print("wrote", OUT)


if __name__ == "__main__":
    build()
