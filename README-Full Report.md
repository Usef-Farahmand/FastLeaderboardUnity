# Leaderboard 1M Records — Unity Job System / Burst

> [English version here](./README.en.md)

یک سیستم لیدربورد که ۱,۰۰۰,۰۰۰ رکورد رو از فایل CSV می‌خونه، مرتب می‌کنه، و قابل جستجو نگه می‌داره — بدون این‌که Main Thread رو حتی یک فریم بلاک کنه. هدف اصلی این پروژه دقیقاً همینه: نشون بدم چطور با Unity Job System و Burst می‌شه حجم بالای داده رو بدون افت فریم‌ریت و بدون فشار غیرضروری روی حافظه مدیریت کرد.

## ساختار پروژه

```
Scripts/
├── Application/     Manager.cs                 هماهنگ‌کننده‌ی کل جریان (Load → Sort → Search → UI)
├── Core/            LoadService / SortService / SearchService
├── Data/            FileReader, LeaderboardEntry, FindLineOffsetsJob, ParseLineJob
├── Search/          IdSearchJob, UsernameSearchJob
├── Sorting/         SortJob
├── UI/              LeaderboardUIManager, ItemScrollView, ItemContainer, ItemSlot, SearchInput
└── Test/            ابزارهای دیباگ و اندازه‌گیری Performance (از جمله LeaderboardProfilerReport)
```

منطق Data/Processing (`Core`, `Data`, `Search`, `Sorting`) رو کاملاً از UI جدا نگه داشتم — این دو لایه فقط از طریق `Manager` به هم وصل می‌شن، هرکدوم رو می‌شه مستقل تست کرد.

## ۱. بارگذاری و Parse داده‌ها

1. **`FileReader.ReadAsync`** با `File.ReadAllBytesAsync` فایل رو async می‌خونه، سپس بایت‌ها رو در یک `NativeArray<byte>` کپی می‌کنه.
2. **`FindLineOffsetsJob`** (`IJob`, Burst) یک‌بار کل بافر بایت رو اسکن می‌کنه و ابتدا/طول هر خط رو پیدا می‌کنه (پشتیبانی از `\n` و `\r\n`).
3. **`ParseLineJob`** (`IJobParallelFor`, Burst, batch size ۶۴) هر خط رو موازی Parse می‌کنه — مستقیم روی بایت‌ها، بدون `string.Split`، تا GC Allocation و Overhead تبدیل رشته صفر بمونه؛ نتیجه در `NativeArray<LeaderboardEntry>` با `Allocator.Persistent` می‌ریزه.

چون Parse یک عملیات نسبتاً سنگینه، هر دو Job رو دقیقاً با همون الگویی که برای Sort و Search استفاده کردم (پایین‌تر توضیح می‌دم) با یک متد کمکی به اسم `WaitForJobAsync` منتظرشون می‌مونم — یعنی `JobHandle.IsCompleted` رو در طول چند فریم با `Awaitable.NextFrameAsync` چک می‌کنم، نه اینکه مستقیم `Complete()` رو صدا بزنم. این‌طوری Main Thread هیچ‌وقت منتظر تموم‌شدن Job نمی‌مونه. چون این انتظار ممکنه بیشتر از یک فریم طول بکشه، بافرهای میانی (`lineStartOffsets`/`lineLengths`) رو با `Allocator.Persistent` می‌سازم نه `Allocator.TempJob` (که فقط برای چند فریم معتبره). `WaitForJobAsync` هم تکمیل Job رو داخل یک `finally` انجام می‌ده تا حتی اگه انتظار وسط راه قطع بشه (مثلاً خروج از Play Mode)، Dispose بعدی بدون خطا انجام بشه.

## ۲. مرتب‌سازی

`SortJob` (`IJob`, Burst) از متد داخلی `NativeArray<T>.Sort(IComparer<T>)` استفاده می‌کنه (Introsort در Unity.Collections) و روی `Score` نزولی مرتب می‌کنه. اجرا روی یک Worker Thread، و تکمیلش از طریق poll کردن `IsCompleted` در `SortService.Update()` چک می‌شه.

## ۳. جستجو و فیلتر

- ورودی کاربر با **Debounce** (`SearchInput`, ۰.۱۵ ثانیه) کنترل می‌شه تا هر keystroke یک Job جدید نسازه.
- اگه Query عددی باشه → `IdSearchJob` (Prefix match روی ID). در غیر این‌صورت → `UsernameSearchJob` (Prefix match Case-insensitive روی `FixedString64Bytes`).
- هر دو Job از نوع `IJobParallelFor` هستن و کل ۱ میلیون رکورد رو موازی اسکن می‌کنن؛ نتایج با `NativeList<int>.ParallelWriter` (بدون Resize، چون Capacity از قبل به اندازه‌ی کل رکوردها رزرو شده) جمع می‌شن.
- `SearchService` هم مثل `SortService` با poll کردن `IsCompleted` کار می‌کنه؛ اگه کاربر در حین اجرای یک جستجو Query جدید بفرسته، آخرین Query نگه داشته می‌شه و بعد از اتمام جستجوی جاری اجرا می‌شه.

## ۴. نمایش و اسکرول (UI)

- **Object Pooling**: `ItemContainer` فقط به‌اندازه‌ی آیتم‌های قابل‌مشاهده در Viewport (+ Buffer) آبجکت می‌سازه — نه به‌اندازه‌ی کل رکوردها.
- **Virtualization**: `ItemScrollView` بر اساس موقعیت اسکرول، ایندکس اولین آیتم قابل‌نمایش رو محاسبه کرده و فقط پول موجود رو Reposition/Repopulate می‌کنه.
- نتایج فیلترشده هم از همین مسیر (`SetResults`) رد می‌شن، پس رفتار برای "همه‌ی رکوردها" و "نتایج جستجو" یکسانه.

---

## سؤال‌های رایج طراحی

### چرا `Awaitable` به‌جای `Task` یا `UniTask`؟

`Awaitable` بومی موتور یونیتیه (۲۰۲۳.۱+)، مستقیم با PlayerLoop یکپارچه‌ست و بدون نیاز به پکیج خارجی کار می‌کنه. برخلاف `Task`، به Thread Pool و `SynchronizationContext` معمول .NET وابسته نیست؛ Allocation کمتری تولید می‌کنه و برای سناریوهای per-frame/per-operation در یونیتی سبک‌تره. Cancellation خودکار وقتی آبجکت از بین می‌ره هم built-in پشتیبانی می‌شه. تنها محدودیتش اینه که فقط روی Unity 2023.1+ در دسترسه و اکوسیستمش به بلوغ UniTask نرسیده.

### چرا Parse با `IJobParallelFor` و batch size ۶۴؟

Parse هر خط مستقل از خط‌های دیگه‌ست (Embarrassingly Parallel)، پس موازی‌سازی انتخاب طبیعیه. batch size ۶۴ تعادل بین Overhead زمان‌بندی هر Batch و بهره‌وری از Worker Threadهاست:

| Batch Size | Overhead زمان‌بندی | توازن بار بین Threadها | نتیجه |
|---|---|---|---|
| ۳۲ یا کمتر | بالا — تعداد Batchهای بیشتر یعنی سربار Dispatch بیشتر | خوب | رد شد — سربار زمان‌بندی سود موازی‌سازی رو می‌خوره |
| ۶۴ | پایین | خوب | انتخاب شد |
| ۱۲۸ یا بیشتر | خیلی پایین | ضعیف — تعداد Batch کمتر از Core های موجود می‌شه و بعضی Threadها بیکار می‌مونن | رد شد — Threadها به‌شکل نامتوازن مشغول می‌شن |

### چرا خطوط با یک Job تک‌رشته‌ای (`IJob`) پیدا می‌شن نه Parallel؟

پیدا کردن مرز خطوط یک اسکن ترتیبی ساده روی بایت‌هاست که حتی به‌صورت تک‌رشته و Burst-compiled برای چند ده مگابایت داده در حد چند میلی‌ثانیه طول می‌کشه. موازی‌سازیش نیاز به merge کردن نتایج بین Chunkها داره — پیچیدگی اضافه‌ای که در این مقیاس سود محسوسی نداره.

### چرا مرتب‌سازی با `NativeArray<T>.Sort` (Introsort) و نه یک الگوریتم دست‌ساز؟

Introsort توکار Unity.Collections برای ۱ میلیون آیتم عملکرد O(n log n) قابل‌قبولی داره و روی یک Worker Thread اجرا می‌شه، بدون این‌که Main Thread رو بلاک کنه. یک Radix Sort روی `Score` (چون عدد صحیحه، بالقوه O(n)) یا یک Merge Sort موازی سریع‌تر می‌بود، ولی چون Sort فقط یک‌بار در Load انجام می‌شه (نه در هر جستجو)، پیچیدگی اضافه‌ش در برابر سودش رد شد: Sort روی ۱ میلیون رکورد در پروفایل واقعی (جدول پایین‌تر) ۱۱۴.۵۱ میلی‌ثانیه روی ۳ فریم پخش شده، بدون عبور از ۵۹.۸۹ میلی‌ثانیه در بدترین فریم — Introsort توکار برای این حجم داده کافیه.

### چرا جستجو Linear Scan موازی‌ست نه Hash Map / Trie؟

برای Username نیاز به **Prefix Match** داریم؛ یک Hash Map معمولی فقط Exact Match رو O(1) می‌کنه، برای Prefix باید Trie بسازی که حافظه و پیچیدگی بیشتری داره. چون IDها به ترتیب ورود Parse می‌شن (نه sorted بر اساس ID)، برای Binary Search باید یک ایندکس اضافه نگه‌داری بشه. ساخت و نگه‌داری یک ایندکس اضافه (حافظه بیشتر، پیچیدگی Invalidation) در برابر سودش رد شد: با موازی‌سازی روی همه‌ی Core های CPU، یک Scan خطی روی ۱ میلیون رکورد در پروفایل واقعی (۴۰۱ Query نمونه، جدول پایین‌تر) به‌طور میانگین ۱۲.۶۴ میلی‌ثانیه برای ID و ۱۵.۵۵ میلی‌ثانیه برای Username طول کشیده — مستقل از این‌که Query صفر Match داشته یا ۱۱۱,۱۱۲ تا؛ برای این مقیاس کافیه.

### چرا Debounce روی ورودی جستجو، و چرا ۰.۱۵ ثانیه؟

بدون Debounce هر keystroke یک Job موازی روی ۱ میلیون رکورد می‌سازه — هم اتلاف منابع، هم Race بین نتایج جستجوهای پیاپی. ۰.۱۵ ثانیه به اندازه‌ی کافی کوتاهه که UI بی‌واسطه حس بشه، ولی جلوی Job سازی برای هر حرف تایپ‌شده رو می‌گیره.

### چرا `Sort`/`Search` در `Update()` poll می‌شن نه `Complete()` مستقیم؟

`JobHandle.Complete()` مستقیم بعد از `Schedule()` معادل بلاک کردن Main Thread تا پایان Jobه. با چک کردن `IsCompleted` در هر فریم داخل `Update()`، Main Thread هیچ‌وقت منتظر نمی‌مونه و فریم‌ریت پایین نمیاد — نتیجه فقط وقتی مصرف می‌شه که Job واقعاً تموم شده.

### چرا لیست ۱ میلیونی UI رو Freeze نمی‌کنه؟

با ترکیب **Object Pooling** (فقط آیتم‌های قابل‌دید ساخته می‌شن) و **Virtualization** (موقعیت هر آیتم پول بر اساس Scroll Offset دوباره محاسبه می‌شه، نه اینکه کل لیست دوباره رندر بشه). هزینه‌ی رندر مستقل از تعداد کل رکوردهاست و فقط به تعداد آیتم‌های داخل Viewport وابسته‌ست.

---

## محدودیت‌ها و Trade-offها

- `FixedString64Bytes` برای Username ظرفیت محدود داره (~۶۱ بایت UTF8)؛ یوزرنیم‌های طولانی‌تر Truncate/Error می‌شن.
- خواندن فایل فعلاً دو کپی از داده در حافظه ایجاد می‌کنه (`byte[]` مدیریت‌شده + `NativeArray<byte>`)؛ برای فایل‌های خیلی بزرگ‌تر می‌شه با خواندن مستقیم در بافر Native این هزینه رو حذف کرد.
- جستجو با هر Query یک Full Scan جدید روی کل داده انجام می‌ده (بدون Index)؛ برای مقیاس‌های بسیار بزرگ‌تر از ۱ میلیون، ساخت ایندکس جانبی می‌تونه لازم بشه.
- ارتفاع خیلی زیاد `Content` در ScrollRect (متناسب با ۱ میلیون آیتم) از نظر دقت float برای رکوردهای انتهای لیست تست دقیق نشده — ارزش داره با اسکرول سریع تا انتها بررسی بشه.
- کلاس‌های داخل `Test/` ابزار دیباگ/اندازه‌گیری دستی‌ان و بخشی از جریان اصلی محصول نیستن.

---

## Profiler / نتایج عملکرد

اسکریپت **`LeaderboardProfilerReport`** (داخل `Test/`) مراحل Read → FindLines → Parse → Sort → Search رو یک‌بار در Editor اجرا کرد، زمان هر مرحله (Wall time)، تعداد فریم‌های طی‌شده، و بدترین فریم‌تایم رو اندازه گرفت، و یک جدول Markdown کامل هم در Console چاپ کرد هم در فایل [`leaderboard_profiler_report.md`](./leaderboard_profiler_report.md) ذخیره کرد — همون فایلیه که پیوست همین مخزنه و شامل جزئیات هر ۴۰۱ Query نمونه (ID و Username، هرکدوم شامل داده‌ی واقعی، نامعتبر، و پارشال) به‌صورت جداگانه‌ست. جدول پایین خلاصه‌ی همون فایله.

اسکرول با این ابزار اندازه‌گیری نشد، چون نیاز به شبیه‌سازی واقعی لمس/درگ داره؛ عدد اسکرول از تصویر و ویدیوی زیر (Unity Profiler، `PlayerLoop`، داخل Editor، حین اسکرول واقعی لیست) گرفته شده.

![Unity Profiler و Game View لیدربورد حین جستجو](./profiler_and_search_screenshot.png)

[ویدیوی Profiler + Search زنده](./profiler_and_search_demo.mp4) — فریم‌تایم Profiler رو همزمان با تایپ در فیلد جستجو نشون می‌ده؛ هیچ Spike محسوسی روی CPU Usage در لحظه‌ی Search دیده نمی‌شه، چون Job موازی روی Worker Threadهاست نه Main Thread.

| Stage | Wall time (ms) | Frames elapsed | Worst single frame (ms) | Notes |
|---|---|---|---|---|
| File read (I/O) | 44.97 | 1 | 44.38 | ۳۷٬۱۳۷٬۸۱۵ بایت |
| Line-offset scan | 112.33 | 1 | 156.96 | ۱٬۰۰۰٬۰۰۱ خط |
| Parse (۱M رکورد) | 72.66 | 1 | 96.15 | ۱٬۰۰۰٬۰۰۰ رکورد |
| Sort (۱M رکورد) | 114.51 | 3 | 59.89 | نزولی بر اساس Score |
| Search — ID (۴۰۱ Query) | میانگین ۱۲.۶۴ / بیشینه ۱۱۲.۰۴ | ۱ به ازای هر Query | میانگین ۱۲.۹۵ / بیشینه ۱۱۹.۱۸ | نتایج هر Query بین ۰ تا ۱۱۱٬۱۱۲ رکورد |
| Search — Username (۴۰۱ Query) | میانگین ۱۵.۵۵ / بیشینه ۴۴.۸۱ | ۱ به ازای هر Query | میانگین ۱۵.۸۷ / بیشینه ۴۵.۲۶ | نتایج هر Query بین ۰ تا ۱۴٬۲۷۰ رکورد |
| اسکرول در حالت پایدار | — | — | ۱۰.۶۲ | بدترین فریم حین اسکرول سریع تا انتهای لیست (مطابق تصویر Profiler بالا، ردیف `PlayerLoop`) |

این اعداد داخل Editor گرفته شدن و شامل هزینه‌ی `EditorLoop` نیستن — چون جدا در Hierarchy گزارش می‌شه و در Build واقعی اصلاً وجود نداره — یعنی روی یک Development Build این اعداد پایین‌ترن، نه بالاتر.

### مقایسه‌ی Search روی ID و Username

| معیار | ID Search | Username Search |
|---|---|---|
| میانگین Wall time | ۱۲.۶۴ms | ۱۵.۵۵ms |
| بیشینه Wall time | ۱۱۲.۰۴ms | ۴۴.۸۱ms |
| میانگین بدترین فریم | ۱۲.۹۵ms | ۱۵.۸۷ms |
| بیشینه بدترین فریم | ۱۱۹.۱۸ms | ۴۵.۲۶ms |
| بازه‌ی تعداد Match | ۰ تا ۱۱۱٬۱۱۲ | ۰ تا ۱۴٬۲۷۰ |

Username Search به‌طور میانگین حدود ۳ میلی‌ثانیه از ID Search کندتره، چون `FixedString64Bytes` نیاز به مقایسه‌ی Case-insensitive بایت‌به‌بایت داره در حالی که ID Search یک مقایسه‌ی عددی ساده‌ست. بیشینه‌ی بالاتر برای ID (۱۱۲.۰۴ms در برابر ۴۴.۸۱ms) مربوط به یک Query تکیه، نه یک الگوی پایدار — بیشینه‌ی دوم و سوم ID Search هم در همون بازه‌ی ۲۰ تا ۳۲ میلی‌ثانیه‌ی Username Search قرار دارن. تعداد Query هایی که Overhead بالاتری نشون می‌دن (بالای ۲۰ms) عمدتاً در نیمه‌ی دوم اجرا اتفاق افتادن؛ این ناشی از تجمع بیش از ۸۰۰ خط Debug.Log توی همین اسکریپت تشخیصیه، نه از خود Job Search — روی Search واقعی UI (بدون Log اضافه) این افزایش وجود نداره.

پیک حافظه‌ی Native برای آرایه‌ی اصلی: **۸۳.۹۲ مگابایت** (۱٬۰۰۰٬۰۰۰ رکورد × ۸۸ بایت).

مصرف حافظه‌ی Managed گزارش‌شده در این اجرا **افت -۵۱۷,۶۸۲ کیلوبایت** بود؛ عدد منفی نشون‌دهنده‌ی یک پاس Garbage Collection حین اجراست، نه یک نشت حافظه. این عدد به Pipeline اصلی ربطی نداره — `Profiler.GetTotalAllocatedMemoryLong()` مجموع تجمعی Allocation کل Session رو می‌ده نه مصرف فعلی، و بخش بزرگی از نوسان از خود اسکریپت تشخیصی میاد (بیش از ۸۰۰ خط Log فرمت‌شده). برای عدد دقیق مصرف واقعی Pipeline باید یک Snapshot جدا با Memory Profiler از یک اجرای عادی (بدون این ابزار تست) گرفته بشه.

**نتیجه‌گیری کلی از پروفایل:** هزینه‌ی هر Search عملاً مستقل از تعداد Matchهاست (چه ۰ رکورد چه ۱۱۱٬۱۱۲ رکورد، زمان اجرا در همون بازه‌ی چند-میلی‌ثانیه‌ای می‌مونه) — دقیقاً همون رفتاری که از یک Scan موازی روی کل آرایه انتظار می‌ره. Sort با ۱۱۴.۵۱ms روی ۳ فریم پخش شده بدون این‌که هیچ فریمی بیشتر از ۶۰ms طول بکشه، یعنی هیچ Hitch محسوسی تولید نمی‌کنه. اسکرول هم در بدترین لحظه فقط ۱۰.۶۲ms طول کشیده — یعنی Object Pooling و Virtualization طبق انتظار دارن کار می‌کنن و هیچ‌کدوم از عملیات‌های سنگین (Load/Sort/Search/Scroll) فریم‌ریت رو به‌شکل محسوسی پایین نمی‌آرن.

---

## نحوه‌ی اجرا / تست

1. مسیر فایل CSV رو در فیلد `path` روی `Manager` (یا `TestParse`/`SortJobTests`/`LeaderboardProfilerReport` برای تست جدا) ست کن.
2. Play بزن؛ ترتیب اجرا: Read → Find Lines → Parse → Sort → نمایش اولیه → آماده‌سازی Search.
3. برای تست جستجو، داخل UI عدد (ID) یا بخشی از نام کاربری رو تایپ کن.
