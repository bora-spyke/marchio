# NeonLoop — Unity Geçiş Dosyası (Prototip v0.20 → Unity)

**Kaynak:** `neonloop_prototype_20.html` (v0.20, kanıt-konsept/referans — üretim asla bu dosyada devam etmeyecek)
**Amaç:** Bu dosya, HTML5 Canvas prototipindeki TÜM oyun mantığını, sabitleri ve formülleri Unity'de birebir yeniden üretebilecek bir dev'e (kod bilgisi gerektirmeyen okuyucular için de) aktarmak için hazırlandı. **Hiçbir sayı tahminle yazılmadı — hepsi prototip kodundan birebir kopyalandı.** JAM-STATE.md'deki "Next action" kararı buydu: *"Unity projesine geçiş — CONFIG değerlerini birebir taşı (yeniden icat etme)"*.

Bu doküman kod içermiyor (C# yazmıyoruz) — her sistemin **ne yaptığını, hangi sayılarla, hangi sırayla** yaptığını anlatıyor. Unity'de bunu MonoBehaviour/ScriptableObject'e döken kişi, JS dosyasını satır satır okumak zorunda kalmasın diye.

---

## 1. Genel Mimari Eşleştirme

Prototip tek dosyada ama işlevsel olarak modüler (JS'teki yorum başlıkları bunu işaret ediyor). Unity'de bu modüller ayrı script'ler/component'ler olmalı:

| Prototip modülü (JS) | Unity karşılığı (önerilen) |
|---|---|
| `CONFIG` sabitler objesi | `GameConfigSO` (ScriptableObject) — tek kaynak, Inspector'dan ayarlanabilir |
| `S` (global state objesi) | `GameManager` (Singleton MonoBehaviour) — `S.mode`, `S.wave`, `S.upgrades` vb. alanları taşır |
| `resetGame()` | `GameManager.ResetGame()` |
| PlayerController (hareket, input) | `PlayerController.cs` (Rigidbody2D veya kinematik hareket — aşağıda detay) |
| TrailDrawer/LoopDetector | `LoopTrail.cs` — `LineRenderer` + nokta listesi |
| PolygonUtility (`pointInPolygon`, `segmentsIntersect`, vb.) | `PolygonMath.cs` (statik yardımcı sınıf) |
| LoopAttack (`closeLoop`, `applyLoopDamage`) | `LoopTrail.cs` içinde ya da `LoopDamageResolver.cs` |
| EnemyBase + Spawner | `EnemyBase.cs` (chaser/fast/ranged için ortak) + `WaveSpawner.cs` |
| Boss FSM | `BossController.cs` — Unity Animator State Machine ile 1:1 örtüşüyor, aşağıda önerilir |
| ProjectileSystem | `Projectile.cs` (oyuncu ve düşman mermileri için tek prefab, `fromBoss`/`homing` bool flag'leri) |
| UpgradeManager | `UpgradeManager.cs` + `UpgradeDataSO` listesi |
| WaveManager | `WaveManager.cs` |
| GameManager (render/HUD) | Unity'de bu kısım tamamen **Canvas UI + Sprite + Spine** ile yeniden yapılacak, mantık tarafı taşınmayacak (bkz. Bölüm 8) |

**Kamera:** Prototipte "sınırsız harita", kamera her zaman oyuncuyu merkezde tutuyor (`S.camera.x = S.player.x`). Unity'de bu **Cinemachine Virtual Camera → Follow = Player**, ölü bölge (dead zone) sıfır, yani tam kilitli takip. Ekstra kod gerekmez.

---

## 2. Sabitler Tablosu (`GameConfigSO`)

Aşağıdaki TÜM değerler `CONFIG` objesinden birebir alındı. Unity'de bunları bir tek ScriptableObject'e (`GameConfig.asset`) alanlar olarak koyup Inspector'dan ayarlanabilir yapın — playtest ayarları kod değişmeden yapılabilsin.

### 2.1 Oyuncu (PlayerController)
| Alan | Değer | Not |
|---|---|---|
| playerRadius | 15 | çarpışma yarıçapı (px) |
| playerMaxHP | 100 | |
| playerSpeed | 320 | px/s, hedef hız |
| playerAccel | 2300 | px/s², girdi VARKEN bu ivmeyle hedef hıza ulaşır |
| playerDecel | 2900 | px/s², girdi YOKKEN bu ivmeyle durur |
| playerInvulnMs | 500 | hasar sonrası dokunulmazlık |
| playerContactDamage | 10 | düşman temasının varsayılan hasarı (boss kendi contactDamage'ını taşır) |

**Hareket modeli — ÖNEMLİ:** Anlık hız değişimi YOK. Her frame `velocity = MoveToward(velocity, targetVelocity, accel*dt)` — `targetVelocity = inputVector * playerSpeed`. Girdi varsa `accel`, girdi bırakılınca `decel` kullanılır. Bu "yumuşak" his prototipte özellikle playtest'le ayarlandı, Unity'de birebir aynı formülle yapılmalı (Rigidbody2D.velocity üzerinde `Vector2.MoveTowards` ile, her FixedUpdate'te).

### 2.2 İz/İlmek (LoopTrail)
| Alan | Değer | Not |
|---|---|---|
| trailMinDist | 7 | bu kadar hareket etmeden yeni nokta EKLENMEZ (LineRenderer nokta sayısını sınırlar) |
| minLoopLengthMult | 3 | min ilmek uzunluğu = bu × oyuncu genişliği (30px) = **90px** |
| closeRadiusMult | 0.5 | kapanma yarıçapı = bu × oyuncu genişliği = **15px** |
| loopFlashMs | 380 | kapanan poligonun ekranda "flash" süresi |
| maxLoopLengthMult | 18.4 | başlangıç izin sınırı = bu × 30px = **~552px** |
| loopGrowthXPStep | 3 | bu kadar XP toplanınca limit bir kademe büyür |
| loopGrowthPct | 0.06 | her kademede limit +%6 büyür |
| **maxLoopLengthGrowthCapMult** | **2.0** | **(v0.20 buğ düzeltmesi)** taban uzunluk EN FAZLA başlangıcının 2 katına kadar büyür, sonra kilitlenir — sonsuza kadar büyümenin önüne geçildi |
| loopGrowthXPChaser / Fast / Ranged | 1 / 1 / 2 | düşman tipine göre öldürünce kazanılan XP |
| comboLoopLengthStep | 0.10 | kombo başına GEÇİCİ +%10 iz uzunluğu |
| comboLoopLengthCapBonus | 1.5 | kombo bonusu tavanı +%150 |

**Mutlak tavan:** Kalıcı büyüme (2.0×) × kombo bonusu (2.5×) = izin uzunluğu en fazla **başlangıcının 5 katına** kadar çıkabilir (geçici olarak, kombo sıfırlanınca kalıcı tavana düşer).

### 2.3 Bariyer (kapanan ilmeğin geçici duvarı)
| Alan | Değer |
|---|---|
| barrierDurationMs | 3000 (3 sn geçilmez duvar) |
| barrierDps | 30 (üzerinde/içinde duran düşmana saniyede) |
| deadTrailMs | 2500 (parmak bırakılınca kesilen AMA kapanmamış izin, salt görsel kalıntı ömrü — hasar/engel YOK) |

### 2.4 Dokunmatik Girdi (Dinamik Joystick)
| Alan | Değer |
|---|---|
| touchAutoResumeMs | 1000 (parmak kalkınca iz ANINDA kesilir, bu süre sonra parmak basılmasa BİLE iz kendiliğinden yeniden başlar) |
| joystickRadius | 60 (parmağın ilk değdiği nokta = joystick merkezi; buradan bu kadar uzaklaşınca TAM hız) |
| joystickDeadzone | 4 (bundan az hareket = titreme, yön uygulanmaz) |

**Unity Input System notu:** Bu tam olarak "dinamik/sürükleme joystick" (Archero tarzı) — merkez sabit değil, ilk dokunulan noktadır. Unity'de hazır bir "on-screen joystick" paketi kullanılacaksa mutlaka bu davranışı destekleyeni seçin (sabit-merkezli joystick DEĞİL). Parmak kalkınca hareketin **son yönde devam etmesi** de bilinçli bir tasarım kararı (v0.15) — playtest onaylı, kaldırılmamalı.

### 2.5 LoopAttack (alan hasarı)
| Alan | Değer | Not |
|---|---|---|
| baseLoopDamage | 25 | taban hasar |
| areaSmallMaxPx2 | 9000 | bu alanın altı = küçük (×1.0) |
| areaMediumMaxPx2 | 25000 | bu alanın altı = orta (×1.5), üstü = büyük (×2.0) |
| multiKillThreshold1/Bonus1 | 3 / +%25 | ilmek içinde 3+ düşman → +%25 hasar |
| multiKillThreshold2/Bonus2 | 5 / +%50 | 5+ düşman → +%50 hasar (üstteki YERİNE geçer, üst üste binmez) |
| electricBorderRadius | 42 | Electric Border seviye 1 menzili (px) |
| electricBorderRadiusStep | 15 | her ek seviye menzile +15px ekler (seviye 2=57, seviye 3=72...) |
| electricBorderDamageMult | 0.5 | border-only isabetler ana hasarın yarısını alır (seviye arttıkça SADECE menzil büyür, bu çarpan sabit) |

**Hasar formülü (`closeLoop`):**
```
alan = polygonArea(kapanan_poligon)
areaMult = alan < 9000 ? 1.0 : alan < 25000 ? 1.5 : (2.0 + biggerMultiplier_seviyesi × 0.5)
insideCount = poligon içindeki düşman sayısı
multiKillBonus = insideCount>=5 ? 0.5 : insideCount>=3 ? 0.25 : 0
fillDamageMult = 1 + fillDamage_seviyesi × 0.30
totalDamage = baseLoopDamage × areaMult × (1+multiKillBonus) × fillDamageMult
```
Bu hasar önce POLİGON İÇİNDEKİ her düşmana tam uygulanır, sonra (Electric Border varsa) sınırın `electricBorderRadius()` px dışındaki düşmanlara `totalDamage × 0.5` uygulanır.

**Kapanan her ilmek** (içi boş olsa dahi) **her zaman** bir bariyere dönüşür — bu tuzak kurma amaçlı bilinçli bir kullanım, sadece hasar amaçlı değil.

### 2.6 Auto Attack (oyuncunun zayıf/ikincil ateşi)
| Alan | Değer |
|---|---|
| autoAttackCooldownMs | 450 |
| autoAttackDamage | 13 |
| autoAttackProjectileSpeed | 340 |

Sürekli çalışır (hareketsizlik şartı yok), her zaman EN YAKIN düşmanı hedefler. İlmek asıl hasar kaynağı — bu bilerek zayıf tutuldu, Unity'de dengeyi bozacak şekilde büyütülmemeli.

### 2.7 Düşmanlar
| | Chaser | Fast | Ranged |
|---|---|---|---|
| HP | 50 | 30 | 60 |
| Speed (px/s) | 90 | 150 | 60 |
| Radius | 14 | 12 | 15 |
| Ateş aralığı (ms) | 1900 | 1700 | 1400 |
| Mermi hızı | 130 | 150 | 150 |
| Mermi hasarı | 5 | 5 | 8 |
| Min ateş mesafesi | 70 (bundan yakınsa ateş etmez, zaten temas ediyordur) | 70 | — (ranged için `rangedPreferredDist`=190 kullanılır) |

Diğer ortak düşman değerleri:
- `waveHpScalePerWave`: 0.06 → her dalga canları ×(1 + 0.06×(dalga sayısı)) ölçeklenir (`waveHpMult`)
- `enemySpawnStaggerMs`: 160 → bir dalgadaki düşmanlar bu aralıkla sahneye girer
- `enemySteerJitterRad`: 0.40 rad (~±23°) → her düşman doğduğunda SABİT kişisel bir sapma açısı çeker, oyuncuya giden vektör bu açıyla döndürülür (hepsi aynı düz çizgide üst üste gelmesin diye)
- `enemySpeedVariance`: 0.20 → ±%20 kişisel hız çarpanı
- `rangedPreferredDistJitter`: 40px → ranged'lar hepsi aynı yarıçapta durmasın
- `enemySeparationForce`: 220 px/s → çakışan düşmanları O(n²) boid-separation ile hafifçe iter

**Ranged davranışı:** `preferredDist=190px`'den uzaksa yaklaşır, `preferredDist×0.7`'den yakınsa uzaklaşır, arada ise durur ve ateş eder.

**Chaser/Fast davranışı:** Sürekli oyuncuya koşar VE yaklaşırken de (min mesafe dışındaysa) zayıf mermi atar — yakına gelince mermi atmayı bırakır, artık temas hasarı devrede.

### 2.8 Dalga Bileşimi (`waveComposition(n)`)
```
dalga 1: chaser 7, fast 0, ranged 0
dalga 2: chaser 8, fast 3, ranged 0
dalga 3: chaser 7, fast 4, ranged 3   (+ dalga sonunda BOSS)
dalga 4+: extra = n-3
  chaser = 7 + floor(extra × 1.5)
  fast   = 4 + floor(extra × 1.0)
  ranged = 3 + floor(extra × 0.8)
```
`upgradeEveryNWaves = 3` → her 3 dalgada bir (3, 6, 9...) yükseltme ekranı açılır.

### 2.9 BOSS (dalga 3 sonu) — v0.20 güncel değerler
| Alan | Değer | Not |
|---|---|---|
| bossHP | 1300 | `waveHpMult` ile ölçeklenir (diğer düşmanlarla aynı mantık) |
| bossR | 34 | chaser'ın (14) ~2.4 katı |
| bossSpeed | 70 | chase fazında yavaş (kaçılabilsin) |
| bossContactDamage | 18 | normal temas |
| bossDashContactDamage | 30 | dash sırasında temas |
| bossAttackCooldownMs | 2200 | chase fazı süresi (saldırılar arası) |
| bossArenaRadius | 820 | boss tetiklenince oyuncunun O ANKİ konumu merkez alınarak kurulan çember-arena yarıçapı (çap ~1640px, oyuncu hızının ~5 katı) |
| **Dash saldırısı** | telegraph 550ms, hız 480px/s, süre 450ms | playerSpeed'den (320) hızlı — düz kaçış yetmez, konumlanma gerekir |
| **Burst saldırısı** | telegraph 450ms, 12 mermi (360° eşit), mermi hızı 170, mermi hasarı 16, recover 300ms | |
| **Homing saldırısı** | telegraph 500ms, mermi hızı 210, dönüş hızı 2.6 rad/s, hasar 16, ömür 2 sn, recover 350ms | |

**Boss FSM (durum makinesi) — Unity Animator State Machine'e 1:1 taşınabilir:**

```
[chase] --(attackTimer<=0)--> [telegraph]
[telegraph] --(telegraphTimer<=0, nextAttack='dash')--> [dash] --(dashTimer<=0)--> [chase]
[telegraph] --(telegraphTimer<=0, nextAttack='burst')--> [recover] --(recoverTimer<=0)--> [chase]
[telegraph] --(telegraphTimer<=0, nextAttack='homing')--> [recover] --(recoverTimer<=0)--> [chase]
```

Kurallar:
1. **chase**: oyuncuya `bossSpeed` ile düz yaklaşır. `attackTimer` (başlangıç: `bossAttackCooldownMs`) sıfırlanınca → telegraph. Bu anda `targetX/Y = oyuncunun O ANKİ konumu` **kilitlenir** — telegraph boyunca güncellenmez (oyuncu kaçabilsin diye, "fair" tasarım).
2. **telegraph**: saldırı tipine göre süre (`bossTelegraphMsFor`) sayılır — ekranda **büyüyen kırmızı halka** olarak gösterilmeli (görsel telegraph, oyuncuya "kaç" sinyali). Süre dolunca fiili saldırı tetiklenir.
3. **dash**: kilitli `targetX/Y`'ye doğru `bossDashSpeed` ile düz gider, `bossDashDurationMs` sürer, bu sırada `contactDamage = bossDashContactDamage` (yükseltilmiş). Süre dolunca → chase, `contactDamage` normale döner, **`nextAttack` yeniden seçilir**.
4. **burst/homing**: telegraph bitince mermi(ler) hemen fırlatılır, boss `recoverMs` kadar hareketsiz kalır (saldırısız), süre dolunca → chase, `nextAttack` yeniden seçilir.
5. **`pickNextAttack(prev)`**: üç saldırı ('dash','burst','homing') arasından, ÖNCEKİYLE AYNI OLMAYACAK şekilde rastgele seçer — aynı saldırı art arda iki kez gelmez.
6. **Arena kısıtı**: hem oyuncu hem boss, `bossArena` merkezinden `bossArenaRadius` (820px) dışına çıkamaz (basit "clamp to circle" — mesafe yarıçapı aşarsa konumu dairenin sınırına projekte et).
7. **Bariyer muafiyeti (v0.20, kritik!):** Boss'un GÖVDESİ yerdeki bariyerlerden (kapanan ilmeklerden) hiç etkilenmez — çarpışmıyor, yavaşlamıyor, hasar almıyor. Boss'un KENDİ mermileri de (dash/burst/homing'den gelen) bariyeri delip geçer. **Normal düşmanlar ve onların mermileri bariyerden hâlâ etkileniyor — sadece boss istisna.** Bu "daha korkunç olsun" isteğiyle bilinçli eklendi, Unity'de collision layer/mask ile (boss kendi layer'ında, bariyer trigger'ı bossu görmezden gelecek şekilde) uygulanmalı.
8. Boss ölünce: arena kalkar, normal dalga-bitiş akışı (yükseltme ekranı öncesi 1000ms bekleme) devam eder. Boss'a özel XP/iz-büyümesi YOK — tek seferlik bir karşılaşma.
9. Boss, `applyLoopDamage`/`applyBarrierDamage`/`autoAttack`/yanma/donma dahil TÜM normal hasar sistemlerine (bariyer HARİÇ) açık — özel bir hasar mekaniği yok, sadece daha çok HP'si var.

### 2.10 Yükseltmeler (`UPGRADE_POOL`)
| id | Ad | Etki |
|---|---|---|
| fillDamage | Fill Damage | +%30 ilmek hasarı / seviye (`1 + seviye×0.30` çarpanı) |
| burningFill | Burning Fill | İlmek isabetleri 3 sn yanma verir: `dps = 5 × seviye` |
| freezeFill | Freeze Fill | İlmek isabetleri 2 sn %50 yavaşlatır (seviye tekrar alınsa süre/oran DEĞİŞMEZ, sadece "aktif" olur) |
| electricBorder | Electric Border | Bkz. 2.5 — her seviye menzili +15px büyütür |
| healFill | Heal Fill | **(v0.19 buğ düzeltmesi)** İlmek İÇİNDE en az 1 düşman GERÇEKTEN öldürülünce +5×seviye can. Eski buğ: "içinde 3+ düşman VARSA" kontrolü yapıyordu (öldürüp öldürmediğine bakmadan) — artık gerçek ölüm sayılıyor. |
| biggerMultiplier | Bigger Multiplier | Büyük alan çarpanına (2.0) `+seviye×0.5` ekler |

Yükseltme ekranı her 3 dalgada bir, havuzdan RASTGELE 3 tanesi karıştırılıp gösterilir, seçilen yükseltmenin seviyesi +1 olur (üst üste alınabilir, tavan yok).

---

## 3. Kritik Algoritmalar (pseudo-kod)

Bu üç algoritma prototipin kalbi — Unity'de matematiksel olarak BİREBİR aynı sonucu vermeli, "yaklaşık" implementasyon oyunun hissini değiştirir.

### 3.1 İzin kendine değme testi (`findSelfTouchIndex`)
İz, kendi geçmişindeki bir noktaya değdi mi diye EN YENİ noktadan GERİYE doğru taranır (en küçük/en yeni kapanışı bulmak için — "kapandığı kadarı" kapanır, tüm iz değil):
```
for i = son_nokta_indeksi downto 0:
    eğer (toplam_uzunluk - points[i].uzunluk) < minLoopLength: devam et  // kendi kuyruğuna yapışmasın
    eğer oyuncu_pozisyonu, points[i]'ye closeRadius (15px) içindeyse: return i
return -1 (kapanma yok)
```
Bulunan `i`'den güncel konuma kadar olan kısım poligon olur, ÖNCESİ atılır.

### 3.2 Point-in-polygon (ray casting)
Standart "ray casting" algoritması — Unity'nin kendi `Physics2D.OverlapPoint` / `PolygonCollider2D` API'leri ile de yapılabilir ama prototipteki custom implementasyon deterministik ve hızlı, aynen taşınabilir.

### 3.3 Mermi-bariyer çarpışması (segment kesişimi, tünelleme önleme)
Mermi her frame'de "önceki konum → yeni konum" ARASINDAKİ TÜM HATTI bariyerin her kenarına karşı test eder (sadece nokta mesafesine bakmak hızlı mermilerin ince duvarı "atlamasını" — tunneling — engellemez). Unity'de bu, `Physics2D.Linecast` (önceki pozisyon → yeni pozisyon) ile bariyerin `EdgeCollider2D`'lerine karşı doğal olarak çözülebilir — custom segment-intersection kodu yazmaya gerek kalmayabilir.

### 3.4 Homing mermi dönüşü (açısal hız sınırlı steering)
```
desiredAngle = atan2(oyuncuY - mermiY, oyuncuX - mermiX)
curAngle = atan2(mermi.vy, mermi.vx)
diff = wrapToPi(desiredAngle - curAngle)   // [-π, π] aralığına sar
turn = clamp(diff, -turnRate×dt, +turnRate×dt)   // turnRate = 2.6 rad/s
newAngle = curAngle + turn
mermi.vx = cos(newAngle) × hız; mermi.vy = sin(newAngle) × hız
```
**ÖNEMLİ:** Bu ANLIK hedef kilitlenmesi DEĞİL — açısal hız sınırlı, yani oyuncu yön değiştirip kaçabilir. Unity'de `Vector2.SignedAngle` + `Mathf.MoveTowardsAngle` ile birebir karşılığı var, custom wrap-to-pi yazmaya gerek yok.

---

## 4. Girdi Kaynakları ve Öncelik Sırası

Prototip üç girdi kaynağını (klavye/dokunmatik/fare) TEK bir `moveVec + draw` durumuna indiriyor, oyun mantığı kaynağı bilmiyor. Unity'de **New Input System** ile de aynı soyutlama önerilir (bir "PlayerInputActions" → tek `Move` + `Draw` action, farklı binding'ler farklı cihazlardan gelsin). Öncelik: **klavye > dokunmatik > fare** (biri aktifken diğeri araya girmesin).

- **Draw tetikleyicileri:** Klavye=Space basılı, Fare=SAĞ TIK basılı (sol tık sadece menü/UI tıklaması), Dokunmatik=parmak ekranda olduğu HER an otomatik çizer.
- **Parmak bırakınca:** iz ANINDA kesilir (`cancelLoop('release')`), ama karakter hareketi SON YÖNDE devam eder (durmaz). `touchAutoResumeMs` (1000ms) sonra parmak basılmasa bile iz kendiliğinden yeniden başlar.
- **Hasar alınca çizim iptal olur** (`cancelLoop('hit')`) — risk/ödül mekaniği, mobilde de aynen korunmalı.

---

## 5. Kamera ve Dünya

- Sabit arena/sınır YOK — dünya sonsuz, kamera her frame oyuncuyu merkezde tutar.
- **TEK istisna:** Boss karşılaşması sırasında (bkz. 2.9) hem oyuncu hem boss `bossArena` çemberinin dışına çıkamaz.
- Mermi/nesne "despawn" sınırı KAMERAYA göre hesaplanmalı (dünya sabit 0..W/0..H değil!) — prototipte bu tam olarak v0.14'te düzeltilen kritik bir buğdu ("ateş kesmişiz" hissi), Unity'de kamera-relative bir sınır (ya da basitçe `Camera.main` viewport'unun biraz dışı) kullanılmalı.

---

## 6. Spine2D / Sanat Entegrasyonu İçin Notlar

Bu bölüm özellikle animasyon tarafı için — prototipte SADECE geometrik şekiller var, gerçek sanat/Spine rig'leri Unity'de eklenecek. Her karakter/düşman için gereken minimum animasyon state'leri:

**Oyuncu:**
- Idle / Move (4-8 yönlü blend ya da tek "run" + flip, tasarıma bağlı)
- Hit (playerInvulnMs=500ms boyunca görünür flaş/yanıp-sönme — Spine'da ayrı bir "hit" state şart değil, tint/alpha animasyonuyla da çözülebilir)
- Death (hp<=0)
- (Opsiyonel) Draw/Channeling — iz çizerken farklı bir duruş

**Chaser / Fast / Ranged (3 düşman tipi):**
- Idle/Move (chaser & fast sürekli koşar; ranged yaklaşma/uzaklaşma/durma arası geçiş yapar)
- Fire (mermi atarken kısa bir "attack" state — chaserFireIntervalMs/fastFireIntervalMs/rangedFireIntervalMs'e göre tetiklenir)
- Hit flash (`en.hitFlash`, isabet sonrası ~0.1-0.15sn)
- Death

**Boss — EN ÖNEMLİ rig, 6 farklı görsel durum gerekiyor:**
1. Chase (yavaş takip)
2. Telegraph — **görsel olarak en kritik**: oyuncuya "kaç" sinyali veren, büyüyen/nabız atan bir uyarı animasyonu/efekti (dash/burst/homing için görsel olarak FARKLI telegraph önerilir ki oyuncu hangi saldırının geldiğini okuyabilsin — şu an prototipte hepsi kırmızı halka, Unity'de Spine ile ayrıştırılabilir)
3. Dash (hızlı hamle, yön çizgisi/motion blur faydalı)
4. Burst attack (360° mermi fırlatma anı)
5. Homing attack (tek mermi fırlatma anı)
6. Recover (kısa "yorgun/açık" pozu — burst/homing sonrası, oyuncuya karşı-saldırı fırsatı penceresi)

**Event hook önerisi (Spine Unity runtime):** `AnimationState.Event` ile "telegraph başladı", "saldırı anı", "recover bitti" gibi noktaları Spine timeline'ından tetiklemek, `BossController`'ın FSM timer'larıyla Spine animasyon süresini senkronize eder — yani telegraph SÜRESİ (550/450/500ms) Spine animasyon klibinin süresiyle eşleşmeli, kod tarafı sabitleri override etmemeli.

---

## 7. Taşınmayacak / Yeniden Yapılacak Kısımlar

Bunlar prototipte "placeholder" niteliğinde — Unity'de gerçek sanat/ses ile SIFIRDAN yapılacak, mantık olarak taşınmasına gerek yok:
- Tüm `ctx.arc`/`ctx.fillRect` şekil çizimleri (drawChaserShape, drawBossShape, vb.) → gerçek sprite/Spine rig'lerle değişecek
- WebAudio `blip()` synth sesleri → gerçek SFX/müzik ile değişecek
- Parçacık sistemi (`burst`, `particles`) → Unity Particle System / VFX Graph ile yeniden yapılabilir (sayılar/zamanlamalar referans alınabilir ama görsel stil tamamen yeni)
- HUD çizimi (`renderHUD`, canvas text/rect) → Unity UI (Canvas/TextMeshPro) ile yeniden yapılacak — SADECE gösterilen VERİLER (can, kombo, dalga, boss HP barı, aktif yükseltmeler) taşınmalı, çizim kodu değil

---

## 8. Test / Parite Kontrol Listesi

Unity build'i prototiple karşılaştırırken şunları birebir doğrulayın (JAM-STATE.md'de not edilen "Unity build'de mutlaka test edilmeli" maddeleri de dahil):
- [ ] Oyuncu ivmeli hareketi aynı hissi veriyor mu (accel 2300 / decel 2900, hedef hız 320)
- [ ] İz minimum uzunluk (90px) altında kapanmıyor, maksimum (~552px, dalga ilerledikçe XP ile büyüyüp en fazla ~1104px'de kilitleniyor) üzerinde otomatik iptal oluyor
- [ ] Kombo bonusu hasar alınca ANINDA sıfırlanıyor
- [ ] Bariyer 3 saniye sonra kayboluyor, normal düşmanları durdurup hasarlıyor, BOSS'U ETKİLEMİYOR
- [ ] Heal Fill SADECE gerçek ölüm olunca tetikleniyor (ilmek içinde canlı düşman durması yetmiyor)
- [ ] Boss: 3 saldırı da doğru sırayla dönüyor (art arda aynısı gelmiyor), telegraph süresi kaçmaya yetiyor, arena sınırı hem oyuncuyu hem boss'u tutuyor
- [ ] Dinamik joystick: parmağın ilk değdiği nokta merkez oluyor, parmak kalkınca hareket son yönde devam ediyor, iz anında kesiliyor ve 1sn sonra kendiliğinden devam ediyor
- [ ] Mobil dokunmatik akışı GERÇEK TELEFONDA test edildi (prototipte hiç doğrulanmadı — JAM-STATE'te açık not)

---

## 9. Kaynak Dosya Referansı

Tüm sayı ve mantık `neonloop_prototype_20.html` dosyasından (v0.20, en güncel prototip) çıkarıldı. Herhangi bir belirsizlik/eksik durumunda önce o dosyanın ilgili fonksiyonuna bakın — bu doküman onun bire bir özeti, icadı/varsayımı yok. Tasarım niyeti ve önceki karar geçmişi için `NEONILMEKdesigndoc.md` (v1.8) ve `JAM-STATE.md`'ye bakın.
