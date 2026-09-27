using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

/// <summary>
/// Runs the load → parse → sort → search pipeline once, measuring wall-clock
/// time, frames elapsed, and the worst single-frame time for each stage.
/// Produces a markdown table you can paste straight into the README's
/// Profiler section.
///
/// Deliberately duplicates the pipeline steps (same approach as
/// SortJobTests / TestParse) instead of going through LoadService, so it
/// stays useful regardless of whatever error-handling shape LoadService
/// ends up taking.
///
/// Usage:
/// 1. Drop this on an empty GameObject in a scene.
/// 2. Set `path` to your CSV file.
/// 3. Optionally adjust the sample search queries below.
/// 4. Press Play. For numbers that matter, run a Development Build on the
///    target device instead — Editor timings aren't representative.
/// 5. Copy the Console output or the written file into the README table.
///
/// Scrolling is NOT measured here — it needs real scroll input. Play the
/// scene, fling the list to the bottom and back, and read the frame time
/// from the Stats overlay or the Profiler's CPU module.
/// </summary>
public class LeaderboardProfilerReport : MonoBehaviour
{
    [Header("Data")]
    [SerializeField]
    private string path;

    [Header("Search samples")]
    [SerializeField]
    private string[] usernameQueries =
    {
        // 401 mixed queries: sentinel no-match, real usernames, invalid usernames,
        // and partial/substring prefixes of real usernames — order shuffled on purpose.
        "Invincible", "HolyHa", "LoudInventor4512144", "MistyDriver5239935", "Infini", "ImperialSquid9412485", "MungoGizmo_INVALID815887", "Rare",
        "Cheerful", "RapidFlame58611", "CrazyDeveloper6556836", "RagingSorcerer3565926", "WeirdSerpent3300599", "QuixWidget_INVALID700861", "OutlawSeagull6828371", "MungoPickle_INVALID258252",
        "FuriousGamer7200109", "LuckyLeader7730362", "FuriousHunter5991200", "Comm", "FutureBandit9", "GrunkBagel_INVALID223800", "RandomGalax", "SwiftSw",
        "ToxicVoid1752209", "GribbleToaster_INVALID612714", "MightyGeneral557522", "ImperialMage", "CrazyPirate96", "RecklessConqueror4260248", "MagicalHorse4557959", "GalacticEmpire8417344",
        "AstralIce3479057", "BurningGuitarist7943759", "SnarfWidget_INVALID479324", "Electric", "EternalPisto", "SwiftEclipse7607623", "GribbleDoohickey_INVALID401924", "QuixNoodle_INVALID694315",
        "WantedNecromancer7199305", "QuixDoohickey_INVALID588625", "NobleS", "MungoToaster_INVALID765226", "CunningMage8350261", "RadioactiveMecha716768", "WobbleToaster_INVALID874230", "Mythical",
        "HiddenPrincess5744184", "CheerfulHunter4839031", "CriminalJet1449894", "CosmicPilot9358837", "ZippoGadget_INVALID291200", "LoudGhost5771838", "WeirdRaven8271706", "CarelessStorm4115221",
        "EternalWarlock7736317", "SnarfPickle_INVALID112649", "GloriousGun8255317", "MungoWidget_INVALID391945", "BrutalSatellite7122959", "BlorpWidget_INVALID124217", "FierceCity2304596", "RapidFox2227743",
        "AngryCanyon8906953", "BraveCat", "ShadowyVoid7307575", "Reb", "EnchantedHero2202372", "Wis", "GrunkNoodle_INVALID705136", "CrimsonPrin",
        "CleverLeopard2156225", "ShiningDetective391136", "GribbleDoohickey_INVALID714006", "AstralAngel9085628", "RoyalMercenary6020750", "FearlessWind13457", "NobleShark4940306", "SunnyKing9603913",
        "EternalFalcon8897902", "WeirdPr", "RainyCapt", "InfiniteMoose2281011", "NotoriousIce7065588", "InfiniteEclipse9069841", "Hid", "UnluckyLoser1880161",
        "MightyWanderer9403507", "HyperAngel7989018", "GribbleToaster_INVALID472974", "Noto", "AngryFinder9424491", "DaringBuffalo", "ProfessionalForest3783261", "GribbleWaffle_INVALID601871",
        "CarefulEagle9336657", "QuixToaster_INVALID258647", "CalmCrow8", "SilentMoose6750533", "ZorpNoodle_INVALID928494", "Smar", "QuixSprocket_INVALID920304", "QuixWidget_INVALID846702",
        "CarefulGladiat", "FearfulRunner4651917", "SnarfDoohickey_INVALID306261", "CriminalEagle7974124", "Powerful", "FearlessInferno701824", "AngryMountain3759329", "FamousPlayer237399",
        "FizzleGadget_INVALID786782", "ThunderDevil7235312", "LostQueen9", "StormyCleric8153558", "BlorpBagel_INVALID709851", "MungoNoodle_INVALID967017", "Astra", "RoyalGalaxy19765",
        "SilentTr", "FuriousDolphin3379735", "GoldenHacker3048116", "FizzleDoohickey_INVALID472834", "GrandDragon988", "UltraEngineer7108225", "AncientSeagull9802859", "InvincibleDog1615429",
        "ZorpBagel_INVALID961168", "Fiery", "NamelessCobra6722280", "GribbleGizmo_INVALID829070", "StormySerpent7725201", "RarePanda401743", "DigitalP", "HiddenExplorer8048232",
        "FrozenLeopard803721", "WobbleGizmo_INVALID731535", "HappyStallion4820348", "UnluckyBoss6129965", "FearlessSatellite28095", "GhostlyMoose2448160", "EternalDemon", "SunnyDeveloper1",
        "MagicalSerpent6537515", "DaringConqueror1640808", "AstralDreamer1029519", "CriminalIce2604616", "FearlessFalcon7787376", "GrunkGadget_INVALID981260", "ZippoBagel_INVALID274447", "WindySheriff5684599",
        "NobleSword8695693", "InfiniteFrost6858034", "LunarJaguar5992423", "UnstoppableAstronaut903", "LuckyPriest5488385", "SnarfNoodle_INVALID190122", "GrunkPickle_INVALID490487", "HappyIce8311151",
        "NobleDiver8933312", "AtomicParrot7038044", "Arcti", "WobbleWaffle_INVALID104292", "RareRi", "AncientRobin7775470", "QuixPickle_INVALID743898", "BraveJet2440",
        "ShadowProf", "MajesticDruid2777912", "SpecialSinger2991671", "RockyBandit4794533", "VolcanicSoul6267994", "ZippoGizmo_INVALID482348", "LuckyGladiator7644936", "ZorpDoohickey_INVALID784697",
        "NobleEagle178284", "FrozenPrince4895057", "RandomShark7406364", "ShiningSoldier4958294", "MungoGizmo_INVALID123658", "BlazingShield8358565", "FrozenDriver2376652", "WantedDancer6271727",
        "BrilliantStranger8126705", "GoldenDo", "VolcanicSpecter7768062", "SpookyUniverse5492129", "OceanicOrbi", "OutlawGeneral4461566", "SecretRanger205", "LunarMaster7471053",
        "HiddenOwl8776154", "ZippoToaster_INVALID215268", "CosmicMoon7336377", "WobbleWidget_INVALID619167", "UnluckyBea", "LightningWolf2130211", "Arc", "FortunateFarmer1225",
        "GoldenMaster3774390", "MegaMaster", "GrunkBagel_INVALID334083", "WobbleDoohickey_INVALID371764", "SuperSpecter7593573", "QuixNoodle_INVALID866676", "MistyDoctor6455927", "GrunkPickle_INVALID620528",
        "BlorpSprocket_INVALID660559", "EternalBarbarian1614305", "BurningMaster1749088", "GribbleDoohickey_INVALID228809", "Crim", "InfiniteEngineer744328", "FieryFox4534290", "GribbleBagel_INVALID975192",
        "MythicalVillain", "DarkReaper3455833", "FamousCosmonaut8322205", "ZippoSprocket_INVALID165271", "SnarfBagel_INVALID318904", "VeteranSerpent2732588", "FeralMonster4484406", "LuckyDesert4988507",
        "UltimateGuardian3790545", "PhantomAdmiral652634", "SilentSinger86512", "FrozenChampion6570597", "BlorpWidget_INVALID128356", "SnarfToaster_INVALID832948", "ForgottenWhal", "FearlessMe",
        "Brave", "CrimsonCrow74215", "VolcanicFox9704", "QuixToaster_INVALID207151", "ForgottenNecromancer968804", "GloriousMiner52", "FizzleSprocket_INVALID517406", "BlueKingdom6831",
        "HolyFarmer799003", "GrunkDoohickey_INVALID172103", "RegalSerpent5850400", "HolyRiver2154315", "ZippoGizmo_INVALID414328", "HyperLeader9812", "ZorpDoohickey_INVALID917857", "Enchante",
        "EternalWolf7043131", "ThunderLeopard8113101", "BrokenF", "SnarfGadget_INVALID950931", "RareHacker3283020", "GribblePickle_INVALID606098", "FizzleGizmo_INVALID767357", "CarelessAssassin6915169",
        "LightningParrot4552147", "MysticAlligator802678", "SolarQueen8419426", "WobbleGadget_INVALID814328", "Cheerf", "GloriousRoc", "CarelessDolphin4500708", "ShadowRider4338054",
        "FizzleSprocket_INVALID272975", "EternalGiant4", "MegaOspr", "GrunkWaffle_INVALID687472", "RebelMusician826", "GalacticDruid7104567", "BlessedLeopard5113490", "BlorpWidget_INVALID206393",
        "RapidCrocod", "ZippoWaffle_INVALID851438", "StrangeWave6146508", "GribbleGadget_INVALID155129", "MistyFox5", "Scar", "LonelyGuardian6532137", "Power",
        "VeteranKnight1923160", "FieryHorse38651", "RecklessMeteor688", "FizzleWaffle_INVALID774147", "AngryDe", "DigitalOsprey5462600", "Brutal", "MasterMusician3132045",
        "Famou", "CarefulGuardian9667561", "Unique", "FrozenJaguar5970993", "MysteriousOutlaw9486749", "RogueViper", "GrunkNoodle_INVALID632084", "Mig",
        "GribbleToaster_INVALID514002", "Rag", "RandomJet1646837", "CriminalSpecter6788481", "CyberAr", "SnarfWidget_INVALID667874", "FeralSpy3383068", "QuixGadget_INVALID162496",
        "FearlessRocket9780696", "DeadlyMiner2756883", "QuixPickle_INVALID207119", "SnarfNoodle_INVALID683705", "BlorpWidget_INVALID223514", "CunningGuitarist7273358", "ImperialGiant4619339", "DarkCast",
        "DustyExplorer9056734", "RareLord4433532", "ZorpPickle_INVALID905550", "TurboReaper3215901", "AngrySpecter299226", "CleverSeeker", "HappyS", "ElectricPistol633614",
        "GrunkBagel_INVALID617674", "ThunderBaron5109026", "NamelessSword2402198", "WobbleToaster_INVALID959077", "HiddenWi", "RoyalFlame9188362", "GrandSlayer260435", "ForgottenCosmos349684",
        "MungoSprocket_INVALID208566", "DeadlyHero2722341", "SpecialCloud5192931", "StellarKingdo", "FastPlanet89736", "QuixWaffle_INVALID597128", "CriminalPilot8201504", "FizzleSprocket_INVALID914983",
        "SnarfBagel_INVALID677814", "MungoSprocket_INVALID173248", "NamelessAlligator883", "Car", "SnarfSprocket_INVALID509940", "GribbleGadget_INVALID620801", "QuixWaffle_INVALID643578", "BlueEagl",
        "BlorpWaffle_INVALID539499", "LightningRider2852907", "RedMech", "ZippoGizmo_INVALID276211", "SnarfGadget_INVALID461004", "Fer", "MajesticT", "InvincibleGorilla8825507",
        "MungoPickle_INVALID309629", "SnarfBagel_INVALID702326", "CheerfulStar733923", "ScaryAlligator3770416", "FrozenDancer3631539", "GrunkSprocket_INVALID151998", "AtomicOsprey8823482", "MungoNoodle_INVALID800675",
        "ZippoSprocket_INVALID676129", "GribbleGadget_INVALID126739", "InvincibleRain8341243", "CloudySeeker601726", "CunningJet2821367", "CarelessScientist6318983", "CyberTra", "UltimateFarmer9070511",
        "RapidBarbarian1793234", "RockyTank1625855", "GribbleToaster_INVALID824035", "LuckyCobra9682450", "RagingIce1416401", "UnstoppableGorilla2513401", "VeteranSwan8351841", "UltimatePistol36",
        "LoudGun3489", "GribbleGadget_INVALID693851", "zzz_no_match", "EnchantedFire6107191", "DigitalTeacher6869972", "LuckyStorm30", "ScaryClimber94", "Outla",
        "InfamousVoid8788649", "CrazyEmpire317202", "AdventurousQueen8159031", "MasterStor", "BoldNinja9370600", "SilentChampion2679960", "ZippoDoohickey_INVALID427000", "SnarfPickle_INVALID958084",
        "SupremeAlligator8243150", "SavageCity698611", "MajesticDriver4073330", "RareOutlaw1837354", "FizzleBagel_INVALID483452", "Gra", "MegaSeagull4339967", "FutureTraveler6832399",
        "SneakyCaptain5153133",
    };

    [SerializeField]
    private int[] idQueries =
{
        // 401 mixed queries: sentinel out-of-range, real ids, invalid ids,
        // and partial-digit prefixes of real ids — order shuffled on purpose.
        41505, 957493, 772247, 493046611, 580100, 861723, 130310455, -487959,
        617890, 52634, 889234464, 671089, 167754, 972081266, 44, 240175,
        1002871, 72934, 21184195, 105908, 982154, 884, 988211, 58656,
        98247, 301, -948807, 49406, 455, 657925, 170556, -178262,
        439899, 63594, 6410, -455004, 340036, 735912, -992789, 961,
        944026547, 903566, 330777, 9894, 244099, 277371, 1580, 612983,
        774, 114976, 379202, 481742, 781178, 303446, -158493, 48051,
        45, 85342, 1000117, 13, 733747724, 280747, 1004314, 481142,
        -90964, 584005, 164133078, 418802, 52741, 272, -760007, 986495508,
        4, 179452, 307420, 1004492, 71675, 1004495, 397888, 1004460,
        834867140, 90, -485660, 88, 911528, 31, 2, 473680523,
        35078, 769, 834365493, 208497, 6007, 3949, 13850, 896866,
        41, 84901, 452746166, 329964, 299, 2376, -786580, 2114,
        98948, 141505983, 0, -779462, 557672693, 37092, 95326, 19213,
        490786, 275505, 396923, 693385, 598611159, 33660, 938267877, 277747,
        588509, 26226, 908574, 42601, 970343, 449246, 597501168, 107474,
        539741453, 243963, 376418, 31245, -89045, 560087, 21, 902593,
        171340, 752788, 80132, 776647, 859202737, 750, 1002400, 23889,
        709571, 627465975, 81, 1003433, 107176, 1002671, 927768, 84461,
        4575, 83668, 594732, 149812, 588638, 738798, 75, 1001971,
        954, 4558, 1001198, 219685, 687278, -348670, 1001596, 34,
        714826, 5, 732053, 74871, 7, 221232, 42, 6,
        1003886, 442667, 713329, 45562, 161949193, 501, 712426932, 549295686,
        35789, 8, 9, 1003554, 1001729, 64, 945, 982,
        9229, 6424, 607, 846336, 30, 95025, 145052, 1001074,
        941436, 965002334, 810621, 999, 69404, 517489, 18, 1002064,
        -992127, 844152, 1001278, 565159, 529904, 977, 118, 665854893,
        -420885, 130890, 571859, 53, 33327, 779770347, 578857, 1002125,
        146414, 625381, 471030, 27825, 163033, 475436, 800582, 1002899,
        46, 792496, 197710599, -28888, 1000176, 755732, 234054, 624835,
        509509165, 256788, 372529, 844963, 988713, 918939, 77, 876250085,
        659177, 717871, 593, 87, 398592, 403458, 97252, 388163,
        291705, -932196, 702730, 398383, 7273, 67, 666564, 12039,
        795668, 674080, 186155879, 1004805, 351120665, 670488, 783301, 830556,
        927658, 442418, -666729, 967097, 263627, 554817, -501254, 420652,
        555967725, 999999999, 50, 1, 362, 813695, 888663, 231149,
        570963085, 681454, 571413, 283061, 160266, -845679, 229975, 18848,
        777573, 356779, 648565, 902932, 26, 721591, 230284, 65,
        -166573, 66614, 222, 82628, 291370, 1000842, 633053, 605398,
        750801, 735393, -827469, 279947, -415067, 1000230, 764545, 928464,
        352945, 619177, 534278, 443144, 238969, 116740, 1736, 1717,
        167, -619512, 291477, 120117, 716752, 167415, 337, 3,
        225773, 1004882, 171, 631263, 15607, 665823, 6962, 414851,
        129, 153097788, -133210, 146317, 9237, 572142709, 713537, 1000499,
        765180, 562276, 101415, 1001744, 869694, 523482, 452, 1004106,
        6815, 563055, 382555, 654530573, 787353, -209002, 59473, 5322468,
        256703, -746055, 484715, 1001141, 201630, 400157, 3041, 67409234,
        638721, 379581, 51, 89, 222956, 360664, 258608, 874629,
        958973, 211, -187194, 840, 805636, 229259, 91162, 563210918,
        611879, 583, -756889, 288390, 935519, -839725, 848750, 679515,
        883795,
    };

    [SerializeField, Min(1)]
    private int searchBatchSize = 64;

    [Header("Output")]
    [SerializeField]
    private bool writeReportToFile = true;

    [SerializeField]
    private string reportFileName = "leaderboard_profiler_report.md";

    private readonly StringBuilder report = new StringBuilder();
    private readonly Stopwatch stopwatch = new Stopwatch();

    private int framesInStage;
    private float maxFrameSecondsInStage;
    private bool measuringFrames;

    private void Update()
    {
        if (!measuringFrames)
            return;

        framesInStage++;
        maxFrameSecondsInStage = Mathf.Max(maxFrameSecondsInStage, Time.unscaledDeltaTime);
    }

    private async void Start()
    {
        report.AppendLine("| Stage | Wall time (ms) | Frames elapsed | Worst single frame (ms) | Notes |");
        report.AppendLine("|---|---|---|---|---|");

        long managedBefore = Profiler.GetTotalAllocatedMemoryLong();

        // ---------------------------------------------
        // 1. Read file
        // ---------------------------------------------
        BeginStage();
        NativeArray<byte> fileBytes = await FileReader.ReadAsync(path);
        EndStage("File read (I/O)", $"{fileBytes.Length:N0} bytes");

        if (!fileBytes.IsCreated || fileBytes.Length == 0)
        {
            Debug.LogError("LeaderboardProfilerReport: could not read file, aborting.");
            FinishAndWrite();
            return;
        }

        // ---------------------------------------------
        // 2. Find line offsets
        // ---------------------------------------------
        var lineStartOffsets = new NativeList<int>(Allocator.Persistent);
        var lineLengths = new NativeList<int>(Allocator.Persistent);

        var findLinesJob = new FindLineOffsetsJob
        {
            FileBytes = fileBytes,
            LineStartOffsets = lineStartOffsets,
            LineLengths = lineLengths
        };

        BeginStage();
        JobHandle findLinesHandle = findLinesJob.Schedule();
        await WaitForJobAsync(findLinesHandle);
        EndStage("Line-offset scan", $"{lineStartOffsets.Length:N0} lines");

        int recordCount = lineStartOffsets.Length - 1;

        // ---------------------------------------------
        // 3. Parse
        // ---------------------------------------------
        var entries = new NativeArray<LeaderboardEntry>(recordCount, Allocator.Persistent);

        var parseJob = new ParseLineJob
        {
            FileBytes = fileBytes,
            LineStartOffsets = lineStartOffsets.AsArray(),
            LineLengths = lineLengths.AsArray(),
            Output = entries
        };

        BeginStage();
        JobHandle parseHandle = parseJob.Schedule(recordCount, 64);
        await WaitForJobAsync(parseHandle);
        EndStage("Parse", $"{recordCount:N0} records");

        lineStartOffsets.Dispose();
        lineLengths.Dispose();
        fileBytes.Dispose();

        // ---------------------------------------------
        // 4. Sort
        // ---------------------------------------------
        var sortJob = new SortJob { Entries = entries };

        BeginStage();
        JobHandle sortHandle = sortJob.Schedule();
        await WaitForJobAsync(sortHandle);
        EndStage("Sort", "descending by Score");

        // ---------------------------------------------
        // 5. Search samples
        // ---------------------------------------------
        foreach (int id in idQueries)
        {
            var results = new NativeList<int>(entries.Length, Allocator.Persistent);

            var job = new IdSearchJob
            {
                Entries = entries,
                Query = id,
                Results = results.AsParallelWriter()
            };

            BeginStage();
            JobHandle handle = job.Schedule(entries.Length, searchBatchSize);
            await WaitForJobAsync(handle);
            EndStage($"Search — ID prefix \"{id}\"", $"{results.Length:N0} matches");

            results.Dispose();
        }

        foreach (string username in usernameQueries)
        {
            var results = new NativeList<int>(entries.Length, Allocator.Persistent);

            var job = new UsernameSearchJob
            {
                Entries = entries,
                Query = new FixedString64Bytes(username),
                Results = results.AsParallelWriter()
            };

            BeginStage();
            JobHandle handle = job.Schedule(entries.Length, searchBatchSize);
            await WaitForJobAsync(handle);
            EndStage($"Search — username prefix \"{username}\"", $"{results.Length:N0} matches");

            results.Dispose();
        }

        // ---------------------------------------------
        // Memory summary
        // ---------------------------------------------
        long managedAfter = Profiler.GetTotalAllocatedMemoryLong();
        long managedDeltaKb = (managedAfter - managedBefore) / 1024;

        long nativeEntriesBytes = (long)recordCount * UnsafeUtility.SizeOf<LeaderboardEntry>();

        report.AppendLine();
        report.AppendLine($"Native `entries` array: **{nativeEntriesBytes / 1024f / 1024f:F2} MB** " +
            $"({recordCount:N0} records × {UnsafeUtility.SizeOf<LeaderboardEntry>()} bytes each)");
        report.AppendLine($"Managed heap delta during this run: **{managedDeltaKb:N0} KB** " +
            "(rough figure — includes this script's own allocations; use the Memory Profiler package for an isolated number).");
        report.AppendLine();
        report.AppendLine("| Steady-state scrolling | — | — | — | not automated — see the note below |");
        report.AppendLine();
        report.AppendLine("**Scrolling isn't covered by this script.** Play the scene, fling the list to the " +
            "bottom and back, and read the worst frame time from the Stats overlay or the Profiler's CPU module.");

        entries.Dispose();

        FinishAndWrite();
    }

    private void BeginStage()
    {
        framesInStage = 0;
        maxFrameSecondsInStage = 0f;
        measuringFrames = true;
        stopwatch.Restart();
    }

    private void EndStage(string name, string note = "")
    {
        measuringFrames = false;
        stopwatch.Stop();

        double ms = stopwatch.Elapsed.TotalMilliseconds;
        float worstFrameMs = maxFrameSecondsInStage * 1000f;

        report.AppendLine($"| {name} | {ms:F2} | {framesInStage} | {worstFrameMs:F2} | {note} |");

        Debug.Log($"[Profiler] {name}: {ms:F2} ms over {framesInStage} frame(s), worst frame {worstFrameMs:F2} ms");
    }

    private void FinishAndWrite()
    {
        string text = report.ToString();
        Debug.Log("----- Leaderboard Profiler Report -----\n" + text);

        if (!writeReportToFile)
            return;

        try
        {
            string filePath = Path.Combine(Application.persistentDataPath, reportFileName);
            File.WriteAllText(filePath, text);
            Debug.Log($"[Profiler] Report written to: {filePath}");
        }
        catch (Exception exception)
        {
            Debug.LogWarning("LeaderboardProfilerReport: failed to write report file.");
            Debug.LogException(exception);
        }
    }

    private static async Awaitable WaitForJobAsync(JobHandle handle)
    {
        try
        {
            while (!handle.IsCompleted)
                await Awaitable.NextFrameAsync();
        }
        finally
        {
            handle.Complete();
        }
    }
}
