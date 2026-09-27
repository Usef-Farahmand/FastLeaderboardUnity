# Leaderboard 1M Records — خلاصه فنی

![Unity Profiler و Game View لیدربورد حین جستجو](./search_screenshot.png)

## Load و Parse

فایل CSV با `FileReader.ReadAsync` (بر پایه‌ی `File.ReadAllBytesAsync`) به‌صورت Async خونده و در یک `NativeArray<byte>` کپی می‌شه. یک `FindLineOffsetsJob` (`IJob`, Burst) یک‌بار کل بافر رو اسکن می‌کنه و ابتدا/طول هر خط رو پیدا می‌کنه. سپس `ParseLineJob` (`IJobParallelFor`, Burst, batch size ۶۴) هر خط رو مستقیم روی بایت‌ها Parse می‌کنه — بدون `string.Split` — و نتیجه در `NativeArray<LeaderboardEntry>` با `Allocator.Persistent` می‌ریزه. انتظار برای تکمیل هر دو Job با `Awaitable.NextFrameAsync` (نه `Complete()` مستقیم) انجام می‌شه تا Main Thread هیچ فریمی بلاک نشه.

## Sort

مرتب‌سازی با متد توکار `NativeArray<T>.Sort(IComparer<T>)` (Introsort) داخل یک `SortJob` (`IJob`, Burst) روی `Score` نزولی انجام می‌شه، روی یک Worker Thread و بدون بلاک کردن Main Thread. این روش انتخاب شد چون Sort فقط یک‌بار در Load اتفاق می‌افته (نه در هر Search)، پس پیچیدگی اضافه‌ی یک Radix Sort یا Merge Sort دست‌ساز در برابر سودش توجیه نداشت؛ Introsort برای ۱ میلیون رکورد عملکرد O(n log n) کافی و قابل‌اعتمادی می‌ده.

## Search و Filtering

ورودی کاربر با Debounce (۰.۱۵ ثانیه) کنترل می‌شه تا هر keystroke یک Job جدید نسازه. Query عددی به `IdSearchJob` و Query متنی به `UsernameSearchJob` می‌ره — هر دو `IJobParallelFor` با **Prefix Match** (Username به‌صورت Case-insensitive روی `FixedString64Bytes`) که کل ۱ میلیون رکورد رو موازی روی همه‌ی Core ها اسکن می‌کنن؛ نتایج با `NativeList<int>.ParallelWriter` بدون Resize جمع می‌شن. Linear Scan موازی به‌جای Hash Map/Trie انتخاب شد چون Prefix Match نیاز به ساخت و نگه‌داری یک ایندکس جانبی داره که برای این مقیاس (چند میلی‌ثانیه در هر Scan) توجیه‌پذیر نبود.

## بهینه‌سازی UI برای ۱ میلیون رکورد

ترکیب **Object Pooling** (`ItemContainer` فقط به‌اندازه‌ی آیتم‌های قابل‌مشاهده در Viewport + Buffer آبجکت می‌سازه) و **Virtualization** (`ItemScrollView` بر اساس موقعیت اسکرول فقط پول موجود رو Reposition/Repopulate می‌کنه، نه رندر کل لیست). هزینه‌ی رندر مستقل از تعداد کل رکوردهاست و فقط به تعداد آیتم‌های داخل Viewport وابسته‌ست؛ نتایج فیلترشده هم از همین مسیر رد می‌شن.

## محدودیت‌ها و Trade-offها

- `FixedString64Bytes` ظرفیت محدود داره (~۶۱ بایت UTF8)؛ یوزرنیم‌های طولانی‌تر Truncate/Error می‌شن.
- خواندن فایل دو کپی از داده در حافظه ایجاد می‌کنه (`byte[]` مدیریت‌شده + `NativeArray<byte>`).
- هر Search یک Full Scan جدید روی کل داده انجام می‌ده (بدون Index)؛ برای مقیاس‌های بسیار بزرگ‌تر از ۱ میلیون ممکنه ساخت ایندکس جانبی لازم بشه.
