using System;
using System.Collections.Generic;
using System.IO;
using LD60.Gameplay.Articles;
using LD60.Gameplay.Context;
using LD60.Gameplay.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LD60.Editor
{
    public static class StoryGenerator
    {
        private const string ArticlesFolder = "Assets/_Project/Data/Articles";
        private const string LayoutsFolder = "Assets/_Project/Data/Articles/Layouts";
        private const string StoryFolder = "Assets/_Project/Data/Story";
        private const string GuestsFolder = "Assets/_Project/Data/Story/Guests";
        private const string ActionsFolder = "Assets/_Project/Data/Story/Actions";
        private const string ArtFolder = "Assets/_Project/Art/DariyaArt";

        // 25 cells (5x5 grid) = 1.00 (100% fill) -> 1 cell = 0.04f
        private const float CellFill = 0.04f;

        private static Dictionary<string, ArticleDefinition> _articles;
        private static Dictionary<string, string> _articlePaths;
        private static Dictionary<ArticleKind, int> _kindCounters;
        private static Dictionary<string, GuestDefinition> _guests;
        private static ArticlePictureLayout _layoutNoPic;
        private static ArticlePictureLayout _layoutPicMid;
        private static ArticlePictureLayout _layoutPicTop;
        private static Sprite _defaultArticleSprite;

        [MenuItem("LD60/Story/Generate Full Story")]
        public static void GenerateFullStory()
        {
            Debug.Log("[StoryGenerator] =========================================================");
            Debug.Log("[StoryGenerator] === STARTING FULL STORY GENERATION (10 DAYS, 25-CELL) ===");
            Debug.Log("[StoryGenerator] =========================================================");

            Selection.activeObject = null;

            // 1. Build article prefabs (preserving existing prefabs, ensuring ArticleBanner5x2 and FoodSandwich/FoodPie)
            Debug.Log("[StoryGenerator] [Step 1/7] Checking Article Prefabs & Desk Props...");
            ArticleSetup.Build();

            // 2. Clean old story chapters/actions and any legacy unnumbered root articles
            Debug.Log("[StoryGenerator] [Step 2/7] Cleaning old Story Chapters/Actions and legacy root Articles...");
            CleanStoryAndLegacyRootArticles();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 3. Ensure directories exist (including Kind subfolders for Articles)
            Debug.Log("[StoryGenerator] [Step 3/7] Ensuring folder hierarchy...");
            Directory.CreateDirectory(ArticlesFolder);
            Directory.CreateDirectory(LayoutsFolder);
            foreach (ArticleKind kind in Enum.GetValues(typeof(ArticleKind)))
                Directory.CreateDirectory($"{ArticlesFolder}/{kind}");
            Directory.CreateDirectory(StoryFolder);
            Directory.CreateDirectory(GuestsFolder);
            Directory.CreateDirectory(ActionsFolder);

            // 4. Prepare layouts and default sprite
            _layoutNoPic = EnsureLayout("Picture1", 1);
            _layoutPicMid = EnsureLayout("Picture2", 2);
            _layoutPicTop = EnsureLayout("Picture3", 3);
            _defaultArticleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Blocking/Unit.png");
            Debug.Log($"[StoryGenerator] Layouts ready: Picture1 (slot 1), Picture2 (slot 2), Picture3 (slot 3). Default placeholder sprite: {(_defaultArticleSprite != null ? _defaultArticleSprite.name : "NULL")}");

            // 5. Generate Guests
            Debug.Log("[StoryGenerator] [Step 4/7] Generating Guests...");
            GenerateGuests();
            AssetDatabase.SaveAssets();

            // 6. Generate all Articles into Kind subfolders with numbered filenames (e.g. "01 fish-whisper.asset")
            Debug.Log("[StoryGenerator] [Step 5/7] Generating Articles by Kind folders with sequential numbers...");
            GenerateArticles();
            AssetDatabase.SaveAssets();

            // 7. Generate all 10 Days and their Actions
            Debug.Log("[StoryGenerator] [Step 6/7] Generating 10 Days (Chapters & Actions)...");
            var chapters = new[]
            {
                BuildDay1(),
                BuildDay2(),
                BuildDay3(),
                BuildDay4(),
                BuildDay5(),
                BuildDay6(),
                BuildDay7(),
                BuildDay8(),
                BuildDay9(),
                BuildDay10()
            };
            AssetDatabase.SaveAssets();

            // 8. Wire chapters into StoryBook in Game.unity
            Debug.Log("[StoryGenerator] [Step 7/7] Wiring StoryBook in Game.unity...");
            WireStoryBookInScene(chapters);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 9. Verify every generated asset on disk
            VerifyGeneratedStory(chapters);

            Debug.Log($"[StoryGenerator] === GENERATION COMPLETE: {_guests.Count} Guests, {_articles.Count} Articles across 4 Kind folders, {chapters.Length} Chapters wired into Game.unity! ===");
        }

        [MenuItem("LD60/Story/Clean All Story")]
        public static void CleanAllStory()
        {
            Debug.Log("[StoryGenerator] === CLEANING ALL STORY & ARTICLE ASSETS ===");
            Selection.activeObject = null;
            CleanAllAssetsIncludingKindFolders();
            WireStoryBookInScene(Array.Empty<ChapterDefinition>());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[StoryGenerator] === CLEAN COMPLETE: Removed all generated Chapters, Actions, Guests, and Articles. ===");
        }

        private static void CleanStoryAndLegacyRootArticles()
        {
            var removed = 0;
            removed += DeleteAssetIfExists("Assets/_Project/Data/Story/DayOne.asset") ? 1 : 0;
            removed += DeleteAssetIfExists("Assets/_Project/Data/Story/DayTwo.asset") ? 1 : 0;
            removed += DeleteFolderIfExists("Assets/_Project/Data/Story/Actions/DayOne") ? 1 : 0;
            removed += DeleteFolderIfExists("Assets/_Project/Data/Story/Actions/DayTwo") ? 1 : 0;

            for (var d = 1; d <= 10; d++)
            {
                removed += DeleteAssetIfExists($"{StoryFolder}/Day{d}.asset") ? 1 : 0;
                removed += DeleteFolderIfExists($"{ActionsFolder}/Day{d}") ? 1 : 0;
            }

            if (Directory.Exists(ArticlesFolder))
            {
                var rootArticleFiles = Directory.GetFiles(ArticlesFolder, "*.asset", SearchOption.TopDirectoryOnly);
                foreach (var file in rootArticleFiles)
                {
                    if (AssetDatabase.DeleteAsset(file.Replace("\\", "/")))
                        removed++;
                }
            }

            Debug.Log($"[StoryGenerator][Clean] Removed {removed} old story/root assets prior to regeneration.");
        }

        private static void CleanAllAssetsIncludingKindFolders()
        {
            CleanStoryAndLegacyRootArticles();

            var removed = 0;
            if (Directory.Exists(GuestsFolder))
            {
                var guestFiles = Directory.GetFiles(GuestsFolder, "*.asset", SearchOption.TopDirectoryOnly);
                foreach (var file in guestFiles)
                {
                    if (AssetDatabase.DeleteAsset(file.Replace("\\", "/")))
                        removed++;
                }
            }

            foreach (ArticleKind kind in Enum.GetValues(typeof(ArticleKind)))
            {
                if (DeleteFolderIfExists($"{ArticlesFolder}/{kind}"))
                    removed++;
            }

            Debug.Log($"[StoryGenerator][Clean] Removed {removed} guest assets and ArticleKind folders.");
        }

        private static bool DeleteAssetIfExists(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || File.Exists(path))
                return AssetDatabase.DeleteAsset(path);
            return false;
        }

        private static bool DeleteFolderIfExists(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return AssetDatabase.DeleteAsset(path);
            return false;
        }

        private static Sprite LoadCharacterSprite(string fileName)
        {
            var path = ArtFolder + "/" + fileName;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                Debug.LogWarning($"[StoryGenerator][Art] Character sprite not found at '{path}'!");
            return sprite;
        }

        private static void GenerateGuests()
        {
            _guests = new Dictionary<string, GuestDefinition>(StringComparer.OrdinalIgnoreCase);

            var buddySprite = LoadCharacterSprite("buddy.png");
            var bossSprite = LoadCharacterSprite("boss.jpg");
            var babkaSprite = LoadCharacterSprite("babka.png");
            var deputySprite = LoadCharacterSprite("deputy.png");
            var officialSprite = LoadCharacterSprite("official.png");
            var banditSprite = LoadCharacterSprite("bandit.png");
            var chekistSprite = LoadCharacterSprite("chekist.jpg");
            var fishmanSprite = LoadCharacterSprite("fishman.png");
            var anonMasked = LoadCharacterSprite("anon.png");
            var anonUnmasked = LoadCharacterSprite("anon-unmasked.png");

            _guests["buddy"] = CreateGuest("Buddy", "Buddy",
                ("default", buddySprite),
                ("shock", buddySprite),
                ("sympathy", buddySprite),
                ("smile", buddySprite),
                ("whisper", buddySprite),
                ("anxiety", buddySprite),
                ("handshake", buddySprite));

            _guests["boss"] = CreateGuest("Boss", "The Boss",
                ("default", bossSprite),
                ("smirk", bossSprite),
                ("shout", bossSprite));

            _guests["babka"] = CreateGuest("Babka", "Mrs. Marple",
                ("default", babkaSprite),
                ("whisper", babkaSprite),
                ("smirk", babkaSprite),
                ("fear", babkaSprite),
                ("panting", babkaSprite));

            _guests["deputy"] = CreateGuest("Deputy", "Councilor Crowley",
                ("default", deputySprite),
                ("smug", deputySprite),
                ("anger", deputySprite));

            _guests["official"] = CreateGuest("Official", "Ministry Official",
                ("default", officialSprite),
                ("cold", officialSprite),
                ("scorn", officialSprite),
                ("threat", officialSprite));

            _guests["bandit"] = CreateGuest("Bandit", "The Smuggler",
                ("default", banditSprite),
                ("smoke", banditSprite),
                ("smirk", banditSprite),
                ("threat", banditSprite),
                ("cold", banditSprite),
                ("grim", banditSprite),
                ("business", banditSprite));

            _guests["chekist"] = CreateGuest("Chekist", "State Inspector",
                ("default", chekistSprite),
                ("cigar", chekistSprite),
                ("anger", chekistSprite),
                ("calm", chekistSprite));

            _guests["fishman"] = CreateGuest("Fishman", "Harbor Messenger",
                ("default", fishmanSprite),
                ("gurgle", fishmanSprite),
                ("anger", fishmanSprite),
                ("shot", fishmanSprite),
                ("gold", fishmanSprite));

            _guests["anon"] = CreateGuest("Anon", "Anonymous",
                ("mask", anonMasked),
                ("rage", anonMasked),
                ("hole", anonUnmasked),
                ("unmasked", anonUnmasked));

            Debug.Log($"[StoryGenerator][Guests] Generated {_guests.Count} GuestDefinitions in '{GuestsFolder}'.");
        }

        private static GuestDefinition CreateGuest(string assetName, string displayName, params (string key, Sprite sprite)[] emotions)
        {
            var path = $"{GuestsFolder}/{assetName}.asset";
            var guest = AssetDatabase.LoadAssetAtPath<GuestDefinition>(path);
            var isNew = guest == null;
            if (isNew)
                guest = ScriptableObject.CreateInstance<GuestDefinition>();

            var so = new SerializedObject(guest);
            so.FindProperty("displayName").stringValue = displayName;
            var emProp = so.FindProperty("emotions");
            emProp.arraySize = emotions.Length;
            for (var i = 0; i < emotions.Length; i++)
            {
                var elem = emProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("key").stringValue = emotions[i].key;
                elem.FindPropertyRelative("sprite").objectReferenceValue = emotions[i].sprite;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            if (isNew)
                AssetDatabase.CreateAsset(guest, path);
            EditorUtility.SetDirty(guest);
            AssetDatabase.SaveAssetIfDirty(guest);

            Debug.Log($"[StoryGenerator][Guest] {(isNew ? "Created" : "Updated")} '{assetName}' ('{displayName}') with {emotions.Length} emotions at '{path}'.");
            return guest;
        }

        private static ArticlePictureLayout EnsureLayout(string name, int slot)
        {
            var path = $"{LayoutsFolder}/{name}.asset";
            var layout = AssetDatabase.LoadAssetAtPath<ArticlePictureLayout>(path);
            var isNew = layout == null;
            if (isNew)
                layout = ScriptableObject.CreateInstance<ArticlePictureLayout>();

            var so = new SerializedObject(layout);
            so.FindProperty("slot").intValue = slot;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (isNew)
                AssetDatabase.CreateAsset(layout, path);
            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssetIfDirty(layout);
            return layout;
        }

        private static int CellsForShape(ArticleShape shape)
        {
            switch (shape)
            {
                case ArticleShape.Dot: return 1;
                case ArticleShape.Line2H: return 2;
                case ArticleShape.Line2V: return 2;
                case ArticleShape.Line3H: return 3;
                case ArticleShape.Line3V: return 3;
                case ArticleShape.Square: return 4;
                case ArticleShape.Banner5x2: return 10;
                default: return 1;
            }
        }

        private static float FillForShape(ArticleShape shape)
        {
            return CellsForShape(shape) * CellFill;
        }

        private static void GenerateArticles()
        {
            _articles = new Dictionary<string, ArticleDefinition>(StringComparer.Ordinal);
            _articlePaths = new Dictionary<string, string>(StringComparer.Ordinal);
            _kindCounters = new Dictionary<ArticleKind, int>
            {
                [ArticleKind.Report] = 0,
                [ArticleKind.Politics] = 0,
                [ArticleKind.Social] = 0,
                [ArticleKind.Ad] = 0
            };

            // === SPECIAL ARTICLES (10) ===
            AddArticle("fish-whisper", ArticleShape.Line2H, ArticleKind.Report, 0,
                "Rumors at the Old Pier",
                "Night fishermen claim a giant pike surfaced in the backwater, cursed out the boatmen, and dragged their nets under. Officials blame cheap moonshine.",
                0f, 1f, 0f);

            AddArticle("deputy-bribe", ArticleShape.Line2H, ArticleKind.Politics, 2,
                "A Word from Councilor Crowley",
                "\"My sacred duty is the welfare of our people. So long as I hold office, no worker shall go without a crust of bread and a fatherly word.\"",
                2f, 0f, 0f);

            AddArticle("official-seal", ArticleShape.Dot, ArticleKind.Politics, 1,
                "Exemplary Order in the Mills",
                "A Ministry of Public Decency inspection confirmed that strike rumors are pure fabrication. Weavers work in warmth, joy, and patriotic song.",
                2f, 0f, 0f);

            AddArticle("speakeasy-ad", ArticleShape.Line2H, ArticleKind.Ad, 2,
                "The Lily Tea Club",
                "Cozy evening conversations behind heavy velvet drapes. Special amber refreshments reserved for distinguished members. Ask the doorman for the password.",
                0f, 0f, 50f);

            AddArticle("anon-accounts", ArticleShape.Line2H, ArticleKind.Politics, 2,
                "Secret City Hall Accounts",
                "Municipal gold is bleeding abroad through shell companies owned by private printing house proprietors. Authorities are covering up the embezzlement.",
                2f, 1f, 0f);

            AddArticle("fish-ultimatum", ArticleShape.Line3H, ArticleKind.Report, 2,
                "Ultimatum of the Deep",
                "\"A total ban on hooks, lures, and bottom tackle! Any citizen spotted with a fishing rod on the embankment is hereby declared a personal enemy of the Underwater Brotherhood!\"",
                0f, 2f, 0f);

            AddArticle("wanted-mask", ArticleShape.Line3V, ArticleKind.Politics, 2,
                "Wanted: Enemy of the State",
                "The Military Command offers 10,000 rubles for the agitator in the white porcelain mask. Harboring the fugitive or concealing evidence is punishable by firing squad.",
                3f, 0f, 0f);

            AddArticle("execution-list", ArticleShape.Line3V, ArticleKind.Politics, 2,
                "Secret Execution Lists",
                "A typed roster of 120 innocent citizens taken to the firing range over the past six months, bearing the personal signatures of the Commandant and the printing house owners.",
                3f, 2f, 0f);

            AddArticle("anon-truth", ArticleShape.Banner5x2, ArticleKind.Social, 3,
                "CONFESSION OF THE EXECUTED EDITOR: \"I LIVE, AND THE CITY SHALL BE FREE!\"",
                "\"Citizens! Workers, soldiers, mothers! I speak to you from the grave. The Command's executioners shot me in the face, yet the bullet passed through, leaving only the scar of their shame. They thought they buried the truth in a lime pit—but the truth has returned to this front page!\n\nThe print shop owners and the Military Command sold our city for foreign gold bars. They starved your children and threw your brothers into the Foundry cells. Their hour is over! Rise as one! Seize the stations, surround the Command headquarters, and do not fear their machine guns. This is our final issue—and our first free word!\"",
                0f, 10f, 0f);

            AddArticle("order-curfew", ArticleShape.Banner5x2, ArticleKind.Politics, 3,
                "EMERGENCY DECREE OF THE MILITARY COMMAND: \"IRON ORDER AND STATE OF SIEGE\"",
                "\"In the name of the Supreme Military Council and in fulfillment of our sacred duty to the Nation! Following the exposure of a foreign plot and armed sedition by hired anarchists, Martial Law is hereby declared across the capital effective 18:00.\n\nAny gathering exceeding two persons on streets or squares shall be dispersed immediately by live machine-gun fire without warning. Anyone caught bearing arms, seditious leaflets, or sheltering fugitives is declared a traitor to the State and shall be executed on the spot within the hour. Remain indoors and preserve iron calm!\"",
                10f, 0f, 0f);

            // === POLITICS ARTICLES (8) ===
            AddArticle("curfew-extension", ArticleShape.Square, ArticleKind.Politics, 3,
                "State of Emergency",
                "Curfew has been moved up to six o'clock in the evening. Stepping outdoors without a special military pass is strictly forbidden under penalty of detention.",
                3f, 0f, 0f);

            AddArticle("curfew-talk", ArticleShape.Line3V, ArticleKind.Politics, 0,
                "Night Patrols Reinforced",
                "The Military Commandant has urged law-abiding citizens to refrain from evening walks after the factory whistle. The city remains under vigilant watch.",
                3f, 0f, 0f);

            AddArticle("decree-order", ArticleShape.Line3V, ArticleKind.Politics, 0,
                "Public Decency Decree",
                "City Hall reminds residents that any public gathering of more than three people without an approved brass band is deemed an unlawful assembly.",
                3f, 0f, 0f);

            AddArticle("deputy-piety", ArticleShape.Line3V, ArticleKind.Politics, 0,
                "Benefactor Crowley",
                "City Councilor Crowley personally donated a crate of rusks to the municipal orphanage. The children wept with joy and sang praises to the city father.",
                3f, 0f, 0f);

            AddArticle("dictator-death", ArticleShape.Line3V, ArticleKind.Politics, 2,
                "Official Bulletin of Mourning",
                "The heart of the Father of the Nation has ceased to beat. The Provisional Military Committee declares national mourning and calls for iron calm.",
                3f, 0f, 0f);

            AddArticle("military-guns", ArticleShape.Line3V, ArticleKind.Politics, 2,
                "Armored Train at Central Station",
                "The armored train 'Punisher' has pulled onto the main platform. Heavy artillery turrets are trained on the working-class districts as steam billows from every boiler.",
                3f, 0f, 0f);

            AddArticle("parade-prep", ArticleShape.Line3V, ArticleKind.Politics, 0,
                "Loyalty Review Preparations",
                "Next Sunday the main square will host a grand review of workers' devotion to the Leader. Attendance by all factory shops with banners is strictly mandatory.",
                2f, 0f, 0f);

            AddArticle("shoe-minister", ArticleShape.Line3V, ArticleKind.Politics, 0,
                "Ministerial Inspection",
                "The Minister of Light Industry personally inspected the soles of factory workers. Galosh production was declared exemplary and steadily rising.",
                3f, 0f, 0f);

            // === AD ARTICLES (14) ===
            AddArticle("ammunition-ad", ArticleShape.Line2V, ArticleKind.Ad, 0,
                "Schultz's Hardware",
                "Hunting shells, buckshot, heavy steel door chains, and deadbolts. Clearance sale on remaining warehouse stock.",
                0f, 0f, 25f);

            AddArticle("balsam-ad", ArticleShape.Line2H, ArticleKind.Ad, 2,
                "Dr. Spiegel's Balsam",
                "A miraculous herbal tincture in pure grain spirit. Cures rheumatism, aching joints, and profound melancholy of the soul.",
                0f, 0f, 25f);

            AddArticle("boots-ad", ArticleShape.Dot, ArticleKind.Ad, 1,
                "\"Marsh-Walker\" Waders",
                "Reliable protection against autumn slush, salt water, and sudden bites from river fauna. Heavy-duty vulcanized rubber.",
                0f, 0f, 15f);

            AddArticle("brilliantine-ad", ArticleShape.Dot, ArticleKind.Ad, 1,
                "\"Casanova\" Brilliantine",
                "Impeccable shine and iron-hold hair parting through gale-force winds, hasty departures, and sudden house searches. French fragrance.",
                0f, 0f, 10f);

            AddArticle("candle-ad", ArticleShape.Line2V, ArticleKind.Ad, 0,
                "Stearin Candles",
                "Bright, steady flame for dark nights. Smokeless and draft-resistant. Sold wholesale at the shop by the bridge.",
                0f, 0f, 20f);

            AddArticle("funeral-ad", ArticleShape.Line3H, ArticleKind.Ad, 0,
                "\"Tranquility\" Funeral Parlor",
                "Oak and pine coffins with ventilation. Spring-wagon delivery to the cemetery. Generous discounts for Printers' Union members.",
                0f, 0f, 15f);

            AddArticle("hospital-ad", ArticleShape.Square, ArticleKind.Ad, 0,
                "\"Mountain Air\" Sanatorium",
                "Treatment for nervous exhaustion, chronic insomnia, and political anxiety far from factory smoke. Complete peace guaranteed.",
                0f, 0f, 30f);

            AddArticle("lottery-ad", ArticleShape.Line2V, ArticleKind.Ad, 0,
                "\"Last Chance\" Lottery",
                "Grand prize: a first-class steamer ticket to Stockholm! Try your luck while the borders are still open.",
                0f, 0f, 10f);

            AddArticle("pawnshop-ad", ArticleShape.Line2H, ArticleKind.Ad, 0,
                "Goldman's Pawnshop",
                "We buy silverware, furs, chronometers, and gold dental bridges. Immediate cash in hand; absolute discretion assured.",
                0f, 0f, 15f);

            AddArticle("roach-trap-ad", ArticleShape.Line2V, ArticleKind.Ad, 0,
                "\"Quiet Sleep\" Powder",
                "Exterminates cockroaches, bedbugs, and nosy tenement neighbors. Odorless and instant.",
                0f, 0f, 10f);

            AddArticle("smokes-ad", ArticleShape.Line2H, ArticleKind.Ad, 0,
                "\"Northern\" Cigarettes",
                "Real tobacco for strong spirits. Native smoke warms the lungs in any weather.",
                0f, 0f, 5f);

            AddArticle("soap-ad", ArticleShape.Line2V, ArticleKind.Ad, 0,
                "\"Purity\" Soap",
                "Scrubs away stubborn soot, fuel oil, and printer's ink until it squeaks. The choice of every conscientious worker.",
                0f, 0f, 10f);

            AddArticle("tonic-ad", ArticleShape.Line2H, ArticleKind.Ad, 0,
                "\"Vigor\" Drops",
                "A proven remedy for night shifts, clerks, and those who cannot afford to sleep. Approved by the Municipal Apothecary Board.",
                0f, 0f, 15f);

            AddArticle("whiskey-stock", ArticleShape.Line2H, ArticleKind.Ad, 0,
                "Highland Warming",
                "Strong aged Scotch from private cellars. Final shipment before border closures.",
                0f, 0f, 20f);

            // === REPORTAGE ARTICLES (30, keeping Russian/French/Chinese in original language!) ===
            AddArticle("archive-break", ArticleShape.Dot, ArticleKind.Report, 1,
                "Night Break-In at the Archives",
                "An unknown intruder in a white porcelain mask broke into the Command's restricted archives and stole case files of convicted journalists.",
                1f, 1f, 0f);

            AddArticle("cactus-blossom", ArticleShape.Dot, ArticleKind.Report, 1,
                "Botanical Garden in Bloom",
                "A rare Mexican cereus has bloomed in the municipal conservatory. The flower smells of rotten herring, yet the line of visitors stretches two blocks.",
                0f, 1f, 0f);

            AddArticle("chess-brawl", ArticleShape.Line2H, ArticleKind.Report, 2,
                "Scandal at the Chess Cup",
                "The Waterworks Chess Championship final ended in a brawl with carved queens after a disputed check in the endgame. The arbiter escaped via the fire exit.",
                0f, 1f, 0f);

            AddArticle("china-cricket-arena", ArticleShape.Line2H, ArticleKind.Report, 2,
                "上海斗蟋王：四十七场不败神话",
                "昨夜城隍庙蟋蟀争霸赛落幕，来自闸北的青头大将军力克群雄，勇夺四十七连胜。赢家获赠精雕紫檀小轿一顶，全场看客欢声雷动，盘口赌金破万两。",
                0f, 1f, 0f);

            AddArticle("china-flying-tea", ArticleShape.Dot, ArticleKind.Report, 1,
                "云雾仙茶：悬空三寸不沾尘",
                "云南古刹僧侣采得高山绝壁云雾茶。沸水初注，碧绿叶片宛如游龙腾空，凌空三寸飘浮不沉，香透十里古道，惊为天地灵秀。",
                0f, 1f, 0f);

            AddArticle("city-clock", ArticleShape.Dot, ArticleKind.Report, 1,
                "Town Hall Clock Stopped",
                "The main municipal chimes stopped at a quarter to midnight. Clockmakers claim a heavy steel fishhook jammed the gears.",
                0f, 1f, 0f);

            AddArticle("cyborg-dog", ArticleShape.Square, ArticleKind.Report, 3,
                "Cyber-Poodle at the Chemical Plant",
                "Sensation of the week! The missing poodle Barnaby appeared outside the chemical plant labs sporting a glowing red lens in place of his right eye and bit through a steel safe.",
                0f, 2f, 0f);

            AddArticle("elephant-beer", ArticleShape.Line2H, ArticleKind.Report, 2,
                "Jumbo the Elephant Found Drunk",
                "The escaped elephant was discovered at the Old Municipal Brewery. Jumbo drank a vat of hopped wort and has been snoring blissfully on the barley sacks for three days.",
                0f, 1f, 0f);

            AddArticle("elephant-escape", ArticleShape.Square, ArticleKind.Report, 0,
                "Menagerie Escape",
                "Jumbo the Ceylon elephant has walked out of the city zoo. Management asks residents not to tease the animal with umbrellas and to report tracks to the police.",
                0f, 1f, 0f);

            AddArticle("elephant-job", ArticleShape.Line2H, ArticleKind.Report, 0,
                "New Full-Time Employee",
                "The brewery board officially hired Jumbo the elephant as Chief Malt Taster for his phenomenal sense of smell. Salary: one bucket of molasses per day.",
                0f, 1f, 0f);

            AddArticle("found-dog-1", ArticleShape.Line2V, ArticleKind.Report, 0,
                "Strange Poodle Near Chemical Plant",
                "Workers' quarter residents spotted the missing poodle. The dog does not bark, emits a monotonous static crackle, and stares at telegraph poles.",
                0f, 1f, 0f);

            AddArticle("france-baguette-strike", ArticleShape.Line2H, ArticleKind.Report, 2,
                "La Grande Crise de la Baguette",
                "Les boulangers parisiens sont descendus dans la rue ce matin pour exiger une loi fixant la longueur légale de la baguette à exactement 65 centimètres. Les terrasses de café applaudissent sous une pluie de miettes.",
                0f, 1f, 0f);

            AddArticle("france-snail-race", ArticleShape.Dot, ArticleKind.Report, 1,
                "Scandale au Grand Prix de l'Escargot",
                "La grande course annuelle d'escargots de Bourgogne a été suspendue après des soupçons de dopage au beurre persillé. Le champion en titre, l'escargot 'Éclair', a pulvérisé le record mondial en rampant 30 centimètres en une heure.",
                0f, 1f, 0f);

            AddArticle("harbor-patrol", ArticleShape.Line2V, ArticleKind.Report, 0,
                "Harbor Patrols Tightened",
                "The Coast Guard has stepped up night inspections of fishing launches. Dockers whisper of a mysterious unlit motorboat slipping through the fog.",
                1f, 1f, 0f);

            AddArticle("inventor-boom", ArticleShape.Line2H, ArticleKind.Report, 2,
                "Explosion on Garden Street",
                "Tests of inventor Finch's engine ended with a woodshed and a neighbor's henhouse launched into the sky. Finch is unharmed and gathering peelings for a second attempt.",
                0f, 1f, 0f);

            AddArticle("inventor-engine", ArticleShape.Line2V, ArticleKind.Report, 0,
                "Potato-Peel Engine",
                "Self-taught inventor Finch has built a power motor running on fermented potato peelings. The Patent Office has asked for time to deliberate.",
                0f, 1f, 0f);

            AddArticle("london-pigeon-strike", ArticleShape.Dot, ArticleKind.Report, 1,
                "Pigeons Picket Trafalgar Square",
                "Flocks of Trafalgar pigeons have refused to take flight today, demanding high-grade oats instead of stale breadcrumbs. Traffic around Nelson's Column remains utterly paralyzed.",
                0f, 1f, 0f);

            AddArticle("london-tea-spill", ArticleShape.Line2H, ArticleKind.Report, 2,
                "The Great Thames Tea Spillage",
                "A merchant barge carrying three hundred crates of Earl Grey capsized beneath Tower Bridge. The Thames has turned into a steaming tea parlor; gentlemen in top hats were spotted dipping biscuits directly into the river.",
                0f, 1f, 0f);

            AddArticle("lost-dog", ArticleShape.Dot, ArticleKind.Report, 1,
                "Missing Poodle",
                "A gray poodle answering to 'Barnaby' was lost near the Factory Embankment. Finder is kindly asked to return him for a generous reward.",
                0f, 1f, 0f);

            AddArticle("mask-graffiti", ArticleShape.Line2V, ArticleKind.Report, 0,
                "Brazen Graffiti on Tribunal Wall",
                "An indelible stencil of a white mask and the words 'They Did Not Bury Me' appeared on the granite facade of the Tribunal. Authorities are hunting the vandal.",
                1f, 1f, 0f);

            AddArticle("port-escape", ArticleShape.Line3H, ArticleKind.Report, 0,
                "Panic in the Harbor",
                "Private motorboats are hastily departing the port basin. The Coast Guard has ceased checking vessel papers.",
                0f, 1f, 0f);

            AddArticle("radio-dog-final", ArticleShape.Line2V, ArticleKind.Report, 0,
                "Secret of the Cyber-Poodle",
                "Experts confirmed that the recovered poodle was a classified intelligence lab prototype designed for covert audio recording inside City Hall offices.",
                1f, 1f, 0f);

            AddArticle("radio-silence", ArticleShape.Line2V, ArticleKind.Report, 0,
                "The Airwaves Fall Silent",
                "All state radio stations in the capital have ceased broadcasting. The only voice left in the city is this newspaper page.",
                0f, 1f, 0f);

            AddArticle("russia-bear-chess", ArticleShape.Line2H, ArticleKind.Report, 2,
                "ЧП на Красной площади: Медведь обыграл министра в шахматы и потребовал водки",
                "В самом сердце заснеженной первопрестольной, в трактире у Спасских ворот, ручной цирковой медведь Потап Иванович устроил дипломатический фурор. Зайдя погреться под звуки балалайки, косолапый сел за столик товарища министра путей сообщения и за пять минут поставил ему беспощадный мат ферзём.\n\nОтказываясь принимать поздравления пустыми аплодисментами, победитель с грозным рыком хлопнул мохнатой лапой по столу и жестом указал на запотевший хрустальный графин сорокоградусной «Столичной» и тарелку с солёными груздями. Министр признал безоговорочное поражение и лично оплатил счёт за счёт казны под бурные здравицы завсегдатаев.",
                0f, 1f, 0f);

            AddArticle("russia-ice-cream", ArticleShape.Dot, ArticleKind.Report, 1,
                "Сибирский циклон над Москвой: Пломбир на лютом морозе",
                "Несмотря на рекордную пургу, завалившую сугробами Кремль, очередь у ГУМа за классическим сливочным пломбиром вытянулась через всю Красную площадь. Москвичи в тулупах и валенках хрустят вафельными стаканчиками при минус сорока пяти, уверяя иностранных послов, что «ледяное лакомство идеально согревает русскую душу между глотками чая из самовара».",
                0f, 1f, 0f);

            AddArticle("torn-nets", ArticleShape.Line2H, ArticleKind.Report, 2,
                "Havoc on the Embankment",
                "The fishermen's night catch was ruined: nets were shredded to ribbons by heavy claws. Webbed three-toed tracks were found in the wet sand.",
                0f, 1f, 0f);

            AddArticle("usa-giant-pie", ArticleShape.Dot, ArticleKind.Report, 1,
                "Five-Story Apple Pie Feeds Manhattan",
                "Broadway pastry chefs have unveiled a colossal 40-foot apple pie. A steam crane was dispatched to slice the crust, while bystanders armed with shovels devoured cinnamon-spiced portions in minutes.",
                0f, 1f, 0f);

            AddArticle("usa-prohibition-pipes", ArticleShape.Line2H, ArticleKind.Report, 2,
                "Bootlegger Mistake Floods 5th Ward",
                "A clandestine distillery mistakenly connected its bourbon pipeline to the municipal water main. Residents of the 5th Ward report that taps are running 90-proof Kentucky mash; plumbers are barred from entering by enthusiastic locals.",
                0f, 1f, 0f);

            AddArticle("weather-fog", ArticleShape.Dot, ArticleKind.Report, 1,
                "Fog Over the Bay",
                "Navigation in the municipal port has been suspended due to heavy mist. Boatmen report strange splashing sounds from the deep channel.",
                0f, 1f, 0f);

            AddArticle("weather-hopeless", ArticleShape.Line2H, ArticleKind.Report, 0,
                "Weather Forecast",
                "Tomorrow brings lingering damp drizzle, a biting nor'easter, and a general decline in morale. Barometric pressure is sinking through the floorboards.",
                0f, 1f, 0f);

            // === SOCIAL ARTICLES (16) ===
            AddArticle("blackout-city", ArticleShape.Square, ArticleKind.Social, 3,
                "City Plunges into Darkness",
                "The municipal power station has cut the current: workers demand immediate payment of back wages. Streets are lit by protest bonfires.",
                1f, 3f, 0f);

            AddArticle("bread-queue-riot", ArticleShape.Square, ArticleKind.Social, 0,
                "Clash at the Flour Depot",
                "A crowd of residents broke through the gates of the flour distribution depot after three days without deliveries. Mounted police dispersed the gathering with batons.",
                1f, 3f, 0f);

            AddArticle("bread-ration", ArticleShape.Square, ArticleKind.Social, 0,
                "Flour Rationing Introduced",
                "The City Council has regulated bread sales: no more than half a loaf per person. Citizens are urged to remain calm in the queues.",
                1f, 2f, 0f);

            AddArticle("coal-shortage", ArticleShape.Line3H, ArticleKind.Social, 0,
                "Frost in the Tenement Barracks",
                "Deliveries of steam coal to central boiler houses are delayed. Residents are advised to sleep in overcoats and seal window vents with old newspapers.",
                0f, 2f, 0f);

            AddArticle("factory-hunger", ArticleShape.Line3V, ArticleKind.Social, 0,
                "Hunger Rations",
                "Daily bread rations have been cut to a quarter-loaf. Women and children are holding silent pickets outside the Command gates.",
                1f, 3f, 0f);

            AddArticle("foundry-shift", ArticleShape.Square, ArticleKind.Social, 0,
                "Record Shift at the Foundry",
                "The second shift of the open-hearth furnace exceeded its cast-iron quota. The workers' committee has requested soap and hot meals for the night shift.",
                0f, 3f, 0f);

            AddArticle("hospital-rumor", ArticleShape.Line3H, ArticleKind.Social, 0,
                "Motorcade at the Special Clinic",
                "At midnight yesterday, the city's top medical professors were rushed to the government clinic. Third-floor windows burned until dawn.",
                1f, 2f, 0f);

            AddArticle("love-ad-1", ArticleShape.Dot, ArticleKind.Social, 0,
                "Personal: The Blue Beret",
                "\"To the lady in the blue beret who dropped her glove by the depot switch on Tram No. 4! Conductor Oliver has lost all peace of mind and awaits you at the terminus.\"",
                0f, 1f, 0f);

            AddArticle("love-ad-2", ArticleShape.Dot, ArticleKind.Social, 0,
                "Reply to Conductor Oliver",
                "\"Oliver, I threw that glove out on purpose so you would leave me alone. Stop ringing your tram bell under my windows at two in the morning!\"",
                0f, 1f, 0f);

            AddArticle("love-ad-3", ArticleShape.Line2H, ArticleKind.Social, 0,
                "Wedding on the Rails",
                "Conductor Oliver and the lady in the blue beret have finally reached an understanding. The ceremony will take place aboard Tram Car No. 4 at the park loop.",
                0f, 2f, 0f);

            AddArticle("peace-talks", ArticleShape.Line3H, ArticleKind.Social, 0,
                "Appeal of the City's Mothers",
                "Women from the factory districts stepped into no-man's-land between the barricades and the machine-gun nests, pleading with soldiers not to fire on their sons.",
                0f, 2f, 0f);

            AddArticle("riot-fires", ArticleShape.Square, ArticleKind.Social, 3,
                "Barricades on the Avenue",
                "Workers' militias are raising barricades from overturned tram cars. Units of the municipal garrison are refusing orders to fire on fellow citizens.",
                2f, 3f, 0f);

            AddArticle("strike-barricades", ArticleShape.Line3V, ArticleKind.Social, 0,
                "Storming of Government House",
                "Workers' detachments have reached the gates of the government quarter. Soldiers are hanging white bedsheets from barracks windows.",
                1f, 3f, 0f);

            AddArticle("strike-rumor", ArticleShape.Square, ArticleKind.Social, 0,
                "Shift Walkout at the Textile Mill",
                "Weavers refused to man their looms over the lack of kerosene and bread rations. Management promises to resolve the issue with warm shawls.",
                1f, 3f, 0f);

            AddArticle("train-station-rush", ArticleShape.Line3H, ArticleKind.Social, 0,
                "Panic at Central Station",
                "Tickets for all westbound trains are sold out two weeks in advance. Gendarmes are holding back fleeing ministry officials by force.",
                0f, 2f, 0f);

            AddArticle("turnip-fire", ArticleShape.Line3H, ArticleKind.Social, 0,
                "Fire at the Produce Depot",
                "Last night the turnip warehouse at the Central Produce Depot burned to the ground. Officials assure the public that rutabaga reserves will last until winter.",
                0f, 2f, 0f);

            // === CITY CHRONICLE & CLASSIFIED FILLERS (to ensure ~26-28 cells per day so the full 25-cell page can be filled with 1-2 spare clippings) ===
            // Day 1 extras (11 cells)
            AddArticle("tram-schedule", ArticleShape.Line3V, ArticleKind.Report, 0,
                "Winter Tram Timetable",
                "Line No. 4 and Line No. 7 will run at forty-minute intervals due to worn copper cables along the riverfront.",
                0f, 1f, 0f);
            AddArticle("harbor-dredging", ArticleShape.Square, ArticleKind.Report, 0,
                "Dredging in the Outer Basin",
                "Steam dredgers worked all morning clearing silt near Pier 3. Fishermen complained of churning black mud and sulfur odors.",
                0f, 1f, 0f);
            AddArticle("bakery-notice", ArticleShape.Line2H, ArticleKind.Social, 0,
                "Municipal Bakery No. 2",
                "Rye loaves will be issued against yellow coupons starting at seven in the morning. Bring your own wrapping paper.",
                0f, 1f, 0f);
            AddArticle("galosh-repair", ArticleShape.Line2V, ArticleKind.Ad, 0,
                "Vulcan Rubber Workshop",
                "We patch galoshes, hot-water bottles, and raincoats while you wait. Corner of Foundry Lane.",
                0f, 0f, 5f);

            // Day 2 extras (8 cells)
            AddArticle("night-watch-roster", ArticleShape.Line3H, ArticleKind.Report, 0,
                "Courtyard Watch Duty",
                "House committees must post two night watchmen with whistles at every archway from dusk until the morning factory siren.",
                1f, 0f, 0f);
            AddArticle("chimney-sweep-ad", ArticleShape.Line2V, ArticleKind.Ad, 0,
                "Master Chimney Sweep",
                "Soot cleared from flues and cast-iron stoves without dust. Official certificate from the Fire Brigade.",
                0f, 0f, 5f);
            AddArticle("library-hours", ArticleShape.Line3V, ArticleKind.Social, 0,
                "Reading Room Heated",
                "The Municipal Library announces that the main reading hall will remain heated until eight in the evening on Tuesdays and Thursdays.",
                0f, 1f, 0f);

            // Day 3 extras (9 cells)
            AddArticle("bridge-repairs", ArticleShape.Square, ArticleKind.Report, 0,
                "Drawbridge Inspection",
                "Engineers tightened the counterweights on the Old Iron Bridge. Heavy drays are advised to cross in single file.",
                0f, 1f, 0f);
            AddArticle("sewing-machines-ad", ArticleShape.Line3H, ArticleKind.Ad, 0,
                "\"Singer & Sons\" Repairs",
                "Foot-treadle belts, bobbins, and heavy needles for leather and canvas. Cash or barter accepted.",
                0f, 0f, 10f);
            AddArticle("lost-keys", ArticleShape.Line2V, ArticleKind.Report, 0,
                "Lost: Brass Key Ring",
                "A ring of seven warehouse keys was dropped near the tram depot. Return to the gatekeeper for a hot meal.",
                0f, 1f, 0f);

            // Day 4 extras (11 cells)
            AddArticle("boiler-inspection", ArticleShape.Square, ArticleKind.Report, 0,
                "Steam Boiler Pressure Tests",
                "District inspectors are checking basement boilers across the Old Quarter. Residents should ignore loud hissing from the grates.",
                0f, 1f, 0f);
            AddArticle("streetcar-detour", ArticleShape.Line3H, ArticleKind.Report, 0,
                "Square Closed to Traffic",
                "Omnibuses and carts are diverted around Cathedral Square today due to pavement relaying and military drills.",
                1f, 0f, 0f);
            AddArticle("typewriter-ribbon-ad", ArticleShape.Line2H, ArticleKind.Ad, 0,
                "\"Remington\" Ink Ribbons",
                "Fresh black and violet ribbons for all office typewriters. Carbon paper in packs of fifty.",
                0f, 0f, 10f);
            AddArticle("fish-market-prices", ArticleShape.Line2V, ArticleKind.Social, 0,
                "Empty Stalls at Fish Row",
                "Only salted smelt and dried roach reached the market tables this morning. Fishwives blame the harbor fog.",
                0f, 1f, 0f);

            // Day 5 extras (12 cells)
            AddArticle("waterworks-notice", ArticleShape.Square, ArticleKind.Social, 0,
                "Boil All Tap Water",
                "The Municipal Waterworks advises boiling drinking water for ten minutes following repairs to the river intake grates.",
                0f, 2f, 0f);
            AddArticle("scrap-metal-drive", ArticleShape.Line3H, ArticleKind.Politics, 0,
                "Scrap Iron for the State",
                "Schoolchildren collected four tons of rusted bedsteads and railings for the state foundry furnaces.",
                2f, 0f, 0f);
            AddArticle("kerosene-lamps-ad", ArticleShape.Line3V, ArticleKind.Ad, 0,
                "\"Bright Star\" Kerosene Wicks",
                "Replacement glass chimneys and braided cotton wicks for table lamps. No soot, half the oil consumption.",
                0f, 0f, 10f);
            AddArticle("lost-briefcase", ArticleShape.Line2H, ArticleKind.Report, 0,
                "Lost: Leather Portfolio",
                "A brown leather folder containing accounting ledgers was left in a cab near City Hall. No questions asked upon return.",
                1f, 0f, 0f);

            // Day 6 extras (9 cells)
            AddArticle("harbor-foghorn", ArticleShape.Square, ArticleKind.Report, 0,
                "Lighthouse Horn Malfunction",
                "The steam foghorn on the North Breakwater sounded continuously for छह hours last night after its valve froze shut.",
                0f, 1f, 0f);
            AddArticle("military-band-rehearsal", ArticleShape.Line3H, ArticleKind.Report, 0,
                "Brass Band on the Parade Ground",
                "The Garrison Regiment band rehearsed marches for four hours in the rain. Nearby windows rattled from the tubas.",
                1f, 0f, 0f);
            AddArticle("canned-sprats-ad", ArticleShape.Line2V, ArticleKind.Ad, 0,
                "\"Baltic Pearl\" Canned Sprats",
                "Smoked sprats in pure mustard oil. Tin cans keep for five years in any cellar.",
                0f, 0f, 10f);

            // Day 7 extras (10 cells)
            AddArticle("checkpoint-passes", ArticleShape.Square, ArticleKind.Politics, 0,
                "New Stamp Required on Passes",
                "All night-shift factory certificates must bear the triangular purple stamp of the District Commandant by Friday.",
                2f, 0f, 0f);
            AddArticle("telegraph-delays", ArticleShape.Line3V, ArticleKind.Report, 0,
                "Private Telegrams Suspended",
                "The Central Telegraph Office is accepting only official government dispatches until line maintenance is completed.",
                1f, 1f, 0f);
            AddArticle("suitcases-ad", ArticleShape.Line3H, ArticleKind.Ad, 0,
                "Leather Trunks & Valises",
                "Reinforced brass corners, double locks, and waterproof canvas covers. Ideal for sudden long-distance travel.",
                0f, 0f, 15f);

            // Day 8 extras (7 cells)
            AddArticle("generator-curfew", ArticleShape.Square, ArticleKind.Report, 0,
                "Emergency Generators at Print Shop",
                "Essential municipal presses and telegraph relays have switched to backup diesel dynamos during the district power cuts.",
                0f, 1f, 0f);
            AddArticle("foreign-consulate-closed", ArticleShape.Line3V, ArticleKind.Report, 0,
                "Consulate Gates Locked",
                "The Northern Trade Mission has lowered its flag and shuttered its windows. Visa applicants were turned away at dawn.",
                1f, 1f, 0f);

            // Day 9 extras (9 cells)
            AddArticle("garrison-patrols", ArticleShape.Square, ArticleKind.Politics, 0,
                "Armored Cars at the Bridges",
                "Double sentries and sandbag emplacements have been set up at all three river crossings. Pedestrians are searched.",
                2f, 0f, 0f);
            AddArticle("bank-withdrawals-halted", ArticleShape.Line3V, ArticleKind.Social, 0,
                "Savings Bank Shutters Windows",
                "The Municipal Savings Bank has suspended cash withdrawals until further notice. Depositors remain gathered on the steps.",
                0f, 2f, 0f);
            AddArticle("boarding-planks-ad", ArticleShape.Line2V, ArticleKind.Ad, 0,
                "Seasoned Pine Planks",
                "Heavy timber boards and iron nails for boarding up ground-floor shopwindows. Delivery by handcart.",
                0f, 0f, 15f);

            // Day 10 extras (6 cells, designed to fit cleanly in the 5x3 space around the 5x2 Banner!)
            AddArticle("abandoned-posts", ArticleShape.Square, ArticleKind.Report, 0,
                "Checkpoints Left Unmanned",
                "Several sandbagged sentry boxes along Foundry Avenue stood empty at daybreak, with brazier coals still smoldering.",
                0f, 1f, 0f);
            AddArticle("empty-streets-dawn", ArticleShape.Line2H, ArticleKind.Report, 0,
                "Silence Before Dawn",
                "Not a single tram bell rang this morning. The entire city waits behind shuttered windows for the morning paper.",
                0f, 1f, 0f);

            Debug.Log($"[StoryGenerator][Articles] Summary by Kind folder: Report={_kindCounters[ArticleKind.Report]}, Politics={_kindCounters[ArticleKind.Politics]}, Social={_kindCounters[ArticleKind.Social]}, Ad={_kindCounters[ArticleKind.Ad]} (Total={_articles.Count}).");
        }

        private static void AddArticle(string id, ArticleShape shape, ArticleKind kind, int picValue,
            string title, string body, float politics, float social, float money, params ArticleKind[] forbidden)
        {
            var index = ++_kindCounters[kind];
            var fileName = $"{index:D2} {id}";
            var path = $"{ArticlesFolder}/{kind}/{fileName}.asset";

            var article = AssetDatabase.LoadAssetAtPath<ArticleDefinition>(path);
            var isNew = article == null;
            if (isNew)
                article = ScriptableObject.CreateInstance<ArticleDefinition>();

            var fill = FillForShape(shape);
            var cells = CellsForShape(shape);
            var defaultLayout = picValue <= 0 ? _layoutNoPic : (picValue == 3 ? _layoutPicTop : _layoutPicMid);
            var defaultSprite = picValue > 0 ? _defaultArticleSprite : null;

            // Preserve custom sprite & layout if the user already assigned a custom image to this asset
            var preservedCustomSprite = !isNew && article.Picture != null && article.Picture != _defaultArticleSprite;
            var spriteToUse = preservedCustomSprite ? article.Picture : defaultSprite;
            var layoutToUse = preservedCustomSprite && article.PictureLayout != null && article.PictureLayout.Slot != 1
                ? article.PictureLayout
                : defaultLayout;

            // Set fields directly in C# memory BEFORE CreateAsset so disk serialization is never empty
            article.EditorConfigure(
                id,
                shape,
                kind,
                title,
                body,
                spriteToUse,
                layoutToUse,
                fill,
                politics,
                social,
                money,
                forbidden);

            if (isNew)
                AssetDatabase.CreateAsset(article, path);

            EditorUtility.SetDirty(article);
            AssetDatabase.SaveAssetIfDirty(article);

            _articles[id] = article;
            _articlePaths[id] = path;

            Debug.Log($"[StoryGenerator][Article][{kind}/{fileName}] {(isNew ? "Created" : "Updated")} | id='{id}', shape={shape} ({cells} cells, {fill * 100f:F0}% fill), layout={(layoutToUse != null ? layoutToUse.name : "null")}, pic={(spriteToUse != null ? spriteToUse.name : "none")}{(preservedCustomSprite ? " [CUSTOM PRESERVED]" : "")}, stats=(P:{politics}, S:{social}, M:{money})");
        }

        // =====================================================================
        // DAY 1..10 BUILDERS (Calibrated for 25-cell grid: 0.72 / 0.80 / 0.88)
        // =====================================================================

        private static ChapterDefinition BuildDay1()
        {
            var folder = EnsureActionDayFolder("Day1");
            var actions = new List<ChapterAction>();

            // 1. Buddy intro (ChapterStart)
            actions.Add(MakeVisit(folder, "01_buddy_intro", ChapterWhen.ChapterStart, 0f, "", 0f, "", 0f, _guests["buddy"],
                StepEnter("shock"),
                StepSay("shock", "Well, pal... Congratulations on the promotion. If you can even call sitting in this chair a promotion."),
                StepSay("shock", "Did you see outside the window? State inspectors dragged the Chief away, patrols with dogs everywhere... Hang in there."),
                StepSay("sympathy", "The Boss said not to waste a second. The morning folder with today's clippings is already waiting on your desk."),
                StepSay("default", "Fill at least 70% of the page. And don't place Ads next to Politics, or the censors will eat us alive. I'll go warm up the linotype."),
                StepExit()
            ));

            // 2. Early ending hook (if idle-fired triggers)
            actions.Add(MakeEnding(folder, "02_early_ending", ChapterWhen.Context, 0f, "ending_reached", 1f));

            // 3. Morning drop (EnterWork)
            actions.Add(MakeCapsule(folder, "03_morning_drop", ChapterWhen.EnterWork, "morning-drop-1"));

            // 4. Boss warning (15s)
            actions.Add(MakeVisit(folder, "04_boss_warning", ChapterWhen.AfterWorkSeconds, 15f, "", 0f, "", 0f, _guests["boss"],
                StepEnter("smirk"),
                StepChoose("smirk", "Getting comfortable in that chair? Keep your eyes peeled. Fill the page properly, and don't you dare put Ads beside Politics—the Ministry will skin us alive.",
                    "Understood, Boss.", 2,
                    "What happened to the old editor?", 3),
                StepSay("smirk", "Good. Get back to work.", next: 4),
                StepSay("shout", "None of your damn business! He's learning to chop timber in the north. And you're next if you miss the deadline!", next: 4),
                StepExit()
            ));

            // 5. Babka rumors (fill 0.44 = 11 cells)
            actions.Add(MakeVisit(folder, "05_babka_arrives", ChapterWhen.FieldFill, 0.44f, "", 0f, "", 0f, _guests["babka"],
                StepEnter("whisper"),
                StepChoose("whisper", "Dearie, oh dearie... Heard what's happening down by the river? The men say a giant pike poked its head out of the water and cursed out the foreman! Print it in your paper, warn the folk!",
                    "Hand over the clipping.", 2,
                    "Don't interrupt my work.", 4),
                StepGive("fish-whisper", 1, remember: "babka_promise", value: 1f, next: 3),
                StepSay("smirk", "Bless you, son! At least one living soul is left in this office!", next: 5),
                StepSay("fear", "Look how high and mighty you've become... Just as cold-hearted as the last one!", next: 5),
                StepExit()
            ));

            AddIdleActions(folder, actions);
            actions.Add(MakeClose(folder, "99_close_day", ChapterWhen.PaperStamped));

            // 25 cells in morning-drop-1 (1.00) + 2 cells in guest-stash-1 = 27 cells (1.08), required = 0.72 (18 cells)
            return CreateChapter("Day1", "Day 1: A New Broom", 0.72f,
                new[] { (ArticleKind.Ad, ArticleKind.Politics) },
                actions.ToArray(),
                ("morning-drop-1", "Fill at least 72% of the page (18/25 cells). Never place Ads beside Politics.",
                    new[] { "lost-dog", "turnip-fire", "shoe-minister", "smokes-ad", "bread-ration", "london-pigeon-strike", "tram-schedule", "harbor-dredging", "bakery-notice", "galosh-repair" }),
                ("guest-stash-1", "Hand-delivered by guests.",
                    new[] { "fish-whisper" })
            );
        }

        private static ChapterDefinition BuildDay2()
        {
            var folder = EnsureActionDayFolder("Day2");
            var actions = new List<ChapterAction>();

            actions.Add(MakeEnding(folder, "01_early_ending", ChapterWhen.Context, 0f, "ending_reached", 1f));
            actions.Add(MakeCapsule(folder, "02_morning_drop", ChapterWhen.EnterWork, "morning-drop-2"));

            // Deputy visit (15s)
            actions.Add(MakeVisit(folder, "03_deputy_visit", ChapterWhen.AfterWorkSeconds, 15f, "", 0f, "", 0f, _guests["deputy"],
                StepEnter("smug"),
                StepChoose("smug", "Greetings, new quill. I am Councilor Crowley. Today's packet has a piece on my charity for the orphans. I want it shining right at the top.",
                    "That costs money.", 2,
                    "The paper decides.", 5),
                StepGive("money", 40, remember: "took_deputy_bribe", value: 1f, next: 3),
                StepGive("deputy-bribe", 1, next: 4),
                StepSay("smug", "Now that's a businesslike attitude! Here's forty coins for tea and paper. And include my personal address to the voters.", next: 6),
                StepSay("anger", "Who do you think you are, pup?! The last editor acted tough too—and where is he now?! I'll grind you into dust!", remember: "angry_deputy", value: 1f, next: 6),
                StepExit()
            ));

            // Buddy advice (fill 0.48 = 12 cells)
            actions.Add(MakeVisit(folder, "04_buddy_advice", ChapterWhen.FieldFill, 0.48f, "", 0f, "", 0f, _guests["buddy"],
                StepEnter("sympathy"),
                StepSay("sympathy", "Phew, smoke break... Did you see the clipping in the folder about the archive break-in? A man in a white mask... They say he painted a noose on the Command wall."),
                StepSay("smile", "Hang in there. And remember: if a clipping doesn't fit today, stash it in the Save tray on your desk—it'll keep till tomorrow. Just don't leave it loose, or the cleaner will sweep it away."),
                StepExit()
            ));

            AddIdleActions(folder, actions);
            actions.Add(MakeClose(folder, "99_close_day", ChapterWhen.PaperStamped));

            // 26 cells in morning-drop-2 (1.04) + 2 cells in guest-stash-2 = 28 cells (1.12), required = 0.72 (18 cells)
            return CreateChapter("Day2", "Day 2: Gold Mine", 0.72f,
                new[] { (ArticleKind.Ad, ArticleKind.Politics) },
                actions.ToArray(),
                ("morning-drop-2", "Fill at least 72% of the page (18/25 cells). Keep Ads away from Politics.",
                    new[] { "balsam-ad", "deputy-piety", "archive-break", "weather-fog", "foundry-shift", "soap-ad", "elephant-escape", "russia-ice-cream", "night-watch-roster", "chimney-sweep-ad", "library-hours" }),
                ("guest-stash-2", "Hand-delivered by guests.",
                    new[] { "deputy-bribe" })
            );
        }

        private static ChapterDefinition BuildDay3()
        {
            var folder = EnsureActionDayFolder("Day3");
            var actions = new List<ChapterAction>();

            actions.Add(MakeEnding(folder, "01_early_ending", ChapterWhen.Context, 0f, "ending_reached", 1f));
            actions.Add(MakeCapsule(folder, "02_morning_drop", ChapterWhen.EnterWork, "morning-drop-3"));

            // Official visit (15s)
            actions.Add(MakeVisit(folder, "03_official_visit", ChapterWhen.AfterWorkSeconds, 15f, "", 0f, "", 0f, _guests["official"],
                StepEnter("cold"),
                StepSay("cold", "Good morning, Editor. I trust you realize how fragile the city's mood is. The weavers are restless, and the streets whisper of unrest."),
                StepChoose("scorn", "No social grievances next to government decrees. This newspaper must radiate unwavering calm.",
                    "Order is guaranteed.", 3,
                    "The workers are freezing.", 5),
                StepGive("official-seal", 1, remember: "official_loyal", value: 1f, next: 4),
                StepSay("cold", "A sensible answer. Place my report on exemplary factory order into the issue—the public needs to see happy workers.", next: 6),
                StepSay("threat", "Save your sentimentality for poets, young man. One more remark like that and you'll be shoveling coal at the mill yourself.", remember: "official_doubt", value: 1f, next: 6),
                StepExit()
            ));

            // Bandit scout (fill 0.48 = 12 cells)
            actions.Add(MakeVisit(folder, "04_bandit_scout", ChapterWhen.FieldFill, 0.48f, "", 0f, "", 0f, _guests["bandit"],
                StepEnter("smoke"),
                StepSay("smoke", "Howdy, chief. Looks like the old editor's chair is still a size too big for you."),
                StepSay("smirk", "The last guy knew how to play ball, we got along great. Just don't do anything stupid—this town walks on our turf. I'll drop by with business soon."),
                StepExit()
            ));

            AddIdleActions(folder, actions);
            actions.Add(MakeClose(folder, "99_close_day", ChapterWhen.PaperStamped));

            // 26 cells in morning-drop-3 (1.04) + 1 cell in guest-stash-3 = 27 cells (1.08), required = 0.72 (18 cells)
            return CreateChapter("Day3", "Day 3: Still Waters", 0.72f,
                new[] { (ArticleKind.Politics, ArticleKind.Social) },
                actions.ToArray(),
                ("morning-drop-3", "Fill at least 72% of the page (18/25 cells). Directive: never place Politics next to Social pieces.",
                    new[] { "found-dog-1", "torn-nets", "decree-order", "strike-rumor", "tonic-ad", "love-ad-1", "weather-hopeless", "france-snail-race", "bridge-repairs", "sewing-machines-ad", "lost-keys" }),
                ("guest-stash-3", "Hand-delivered by guests.",
                    new[] { "official-seal" })
            );
        }

        private static ChapterDefinition BuildDay4()
        {
            var folder = EnsureActionDayFolder("Day4");
            var actions = new List<ChapterAction>();

            actions.Add(MakeEnding(folder, "01_early_ending", ChapterWhen.Context, 0f, "ending_reached", 1f));
            actions.Add(MakeCapsule(folder, "02_morning_drop", ChapterWhen.EnterWork, "morning-drop-4"));

            // Bandit visit (15s)
            actions.Add(MakeVisit(folder, "03_bandit_visit", ChapterWhen.AfterWorkSeconds, 15f, "", 0f, "", 0f, _guests["bandit"],
                StepEnter("smirk"),
                StepSay("smirk", "Hey there, scribe. Noisy day. The boys and I opened a quiet little spot—the 'Lily Tea Club'. No tea, naturally, but finest Highland barley."),
                StepChoose("threat", "Put our ad in today's issue and fifty coins go straight into your pocket. Refuse, and you'll be testing the depth of the bay.",
                    "It's a deal.", 3,
                    "No speakeasies.", 6),
                StepGive("money", 50, remember: "mob_deal", value: 1f, next: 4),
                StepGive("speakeasy-ad", 1, next: 5),
                StepSay("smirk", "Businesslike. I respect a man who doesn't ask extra questions. I'll check the paper tomorrow.", next: 7),
                StepSay("cold", "Watch that your principles don't cost you more than you can afford, four-eyes.", remember: "angry_bandit", value: 1f, next: 7),
                StepExit()
            ));

            // Official followup (fill 0.32 = 8 cells)
            actions.Add(MakeVisit(folder, "04_official_followup", ChapterWhen.FieldFill, 0.32f, "", 0f, "", 0f, _guests["official"],
                StepEnter("cold"),
                StepSayRule("cold", "The Press Directorate noted your obedience yesterday, young man. Here is a twenty-coin state bonus. See that you don't tarnish the page with tavern vulgarity.",
                    "official_loyal", 0f, next: 2),
                StepGiveRule("money", 20, "official_loyal", 0f, next: 5),
                StepSay("scorn", "I have reviewed your file, Editor. Your defense of the factory rabble yesterday did not go unnoticed.", next: 4),
                StepSay("threat", "I have forwarded a report to the Command investigator. You are now under special observation.", remember: "heat", value: 1f, next: 5),
                StepExit()
            ));

            // Babka queue rumor (fill 0.56 = 14 cells)
            actions.Add(MakeVisit(folder, "05_babka_queue_rumor", ChapterWhen.FieldFill, 0.56f, "", 0f, "", 0f, _guests["babka"],
                StepEnter("panting"),
                StepSay("panting", "Oh, son, I barely made it out alive! Down at the corner flour depot they smashed the windows, and mounted police are swinging sabers!"),
                StepChoose("whisper", "Folk say they're loading all the grain onto army barges at night. Trying to starve us out, the monsters! Will you print the truth?",
                    "I'll report the clash.", 3,
                    "Stay away from the sabers.", 4),
                StepSay("default", "God bless you, dearie! At least someone stands up for the common folk.", next: 5),
                StepSay("default", "Ah, you're scared too... We all walk under the Commandant's boot.", next: 5),
                StepExit()
            ));

            AddIdleActions(folder, actions);
            actions.Add(MakeClose(folder, "99_close_day", ChapterWhen.PaperStamped));

            // 26 cells in morning-drop-4 (1.04) + 2 cells in guest-stash-4 = 28 cells (1.12), required = 0.80 (20 cells)
            return CreateChapter("Day4", "Day 4: Shadow Syndicate", 0.80f,
                new[] { (ArticleKind.Ad, ArticleKind.Politics) },
                actions.ToArray(),
                ("morning-drop-4", "Fill at least 80% of the page (20/25 cells). Keep Ads away from Politics.",
                    new[] { "mask-graffiti", "bread-queue-riot", "curfew-talk", "pawnshop-ad", "inventor-engine", "brilliantine-ad", "usa-giant-pie", "boiler-inspection", "streetcar-detour", "typewriter-ribbon-ad", "fish-market-prices" }),
                ("guest-stash-4", "Hand-delivered by guests.",
                    new[] { "speakeasy-ad" })
            );
        }

        private static ChapterDefinition BuildDay5()
        {
            var folder = EnsureActionDayFolder("Day5");
            var actions = new List<ChapterAction>();

            actions.Add(MakeEnding(folder, "01_early_ending", ChapterWhen.Context, 0f, "ending_reached", 1f));
            actions.Add(MakeCapsule(folder, "02_morning_drop", ChapterWhen.EnterWork, "morning-drop-5"));

            // Chekist inquiry (15s)
            actions.Add(MakeVisit(folder, "03_chekist_inquiry", ChapterWhen.AfterWorkSeconds, 15f, "", 0f, "", 0f, _guests["chekist"],
                StepEnter("cigar"),
                StepSayRule("anger", "A suspicious address slipped into yesterday's issue. 'The Lily Tea Club', eh? State Security sees everything, Citizen Editor. One more ad like that and you'll be felling timber.",
                    "mob_deal", 0f, remember: "heat", value: 1f, next: 3),
                StepSay("calm", "Good to see you have the sense to keep gangsters off the page. Stay sharp: a fugitive traitor is skulking around the city. If anyone slips you seditious leaflets, press the button under your desk.", next: 3),
                StepExit()
            ));

            // Buddy sandwich care (fill 0.40 = 10 cells)
            actions.Add(MakeVisit(folder, "04_buddy_sandwich", ChapterWhen.FieldFill, 0.40f, "", 0f, "", 0f, _guests["buddy"],
                StepEnter("smile"),
                StepSay("smile", "Smoke break, pal! I saw the State Inspector stomping around here... You look pale as a sheet."),
                StepSay("sympathy", "Here, have a bite. Homemade ham sandwich, my wife wrapped it up for you. Keep your strength up—we still have the evening edition to run."),
                StepGive("food-sandwich", 1),
                StepExit()
            ));

            // Anon first visit (fill 0.60 = 15 cells)
            actions.Add(MakeVisit(folder, "05_anon_first_visit", ChapterWhen.FieldFill, 0.60f, "", 0f, "", 0f, _guests["anon"],
                StepEnter("mask"),
                StepSay("mask", "Quiet. Don't flinch. You've been sitting in this chair for five days. Think you're in charge here? You're just next in line for the lime pit."),
                StepSay("mask", "Take this. It's the ledger of secret foreign accounts belonging to the Boss and the Mayor. Print it, and take a step toward freedom. Cower, and remain their fodder."),
                StepGive("anon-accounts", 1),
                StepChoose("mask", "Well? What will it be?",
                    "Who are you?", 5,
                    "I'll call the patrol!", 6),
                StepSay("mask", "Someone they buried alive. You'll find out soon enough, apprentice.", next: 7),
                StepSay("rage", "Fool. The patrol will come for you first.", next: 7),
                StepExit()
            ));

            AddIdleActions(folder, actions);

            actions.Add(MakeEnding(folder, "98_early_arrest", ChapterWhen.PaperStamped, 1f, "heat", 3f));
            actions.Add(MakeClose(folder, "99_close_day", ChapterWhen.PaperStamped));

            // 26 cells in morning-drop-5 (1.04) + 2 cells in guest-stash-5 = 28 cells (1.12), required = 0.80 (20 cells)
            return CreateChapter("Day5", "Day 5: Heavy Footsteps", 0.80f,
                new[] { (ArticleKind.Ad, ArticleKind.Politics) },
                actions.ToArray(),
                ("morning-drop-5", "Fill at least 80% of the page (20/25 cells). Never mix Ads with Politics.",
                    new[] { "harbor-patrol", "factory-hunger", "hospital-ad", "city-clock", "elephant-beer", "cactus-blossom", "china-flying-tea", "waterworks-notice", "scrap-metal-drive", "kerosene-lamps-ad", "lost-briefcase" }),
                ("guest-stash-5", "Hand-delivered by guests.",
                    new[] { "anon-accounts" })
            );
        }

        private static ChapterDefinition BuildDay6()
        {
            var folder = EnsureActionDayFolder("Day6");
            var actions = new List<ChapterAction>();

            actions.Add(MakeEnding(folder, "01_early_ending", ChapterWhen.Context, 0f, "ending_reached", 1f));
            actions.Add(MakeCapsule(folder, "02_morning_drop", ChapterWhen.EnterWork, "morning-drop-6"));

            // Fishman visit (15s)
            actions.Add(MakeVisit(folder, "03_fishman_visit", ChapterWhen.AfterWorkSeconds, 15f, "", 0f, "", 0f, _guests["fishman"],
                StepEnter("gurgle"),
                StepSay("gurgle", "Listen here, land-worm. Your office reeks of dry tobacco. Did your rag print another piece about harbor fishing yesterday?"),
                StepChoose("anger", "Today my ultimatum goes on the front page. A total ban on all fishing! Refuse, and I'll gut you right on this desk.",
                    "I'll print it, don't shoot!", 3,
                    "Get out of my office!", 5),
                StepGive("fish-ultimatum", 1, remember: "fish_ally", value: 1f, next: 4),
                StepSay("gurgle", "Sensible minnow. Live for now.", next: 6),
                StepSay("shot", "You'll regret this, two-legged bait! Tonight you feed the bottom of the bay!", remember: "fish_angry", value: 2f, next: 6),
                StepExit()
            ));

            // Official parade demand (fill 0.32 = 8 cells)
            actions.Add(MakeVisit(folder, "04_official_parade", ChapterWhen.FieldFill, 0.32f, "", 0f, "", 0f, _guests["official"],
                StepEnter("cold"),
                StepSay("cold", "On Sunday we hold the Review of Devotion to the Leader. The city must shine with rapture! No whining about cold barracks."),
                StepChoose("scorn", "Prove your loyalty today, and I will personally close your file with the Tribunal investigator.",
                    "We'll praise the parade!", 3,
                    "The barracks are freezing!", 6),
                StepGive("money", 30, remember: "official_loyal", value: 1f, next: 4),
                StepRemember("heat", 0f, next: 5),
                StepSay("cold", "Commendable zeal. Here are thirty coins from the propaganda fund, and I shall have your past indiscretions struck from the record.", next: 8),
                StepRemember("official_loyal", 0f, next: 7),
                StepSay("threat", "You are incorrigible, young man! Forget my protection—the Tribunal will deal with you directly!", remember: "heat", value: 2f, next: 8),
                StepExit()
            ));

            // Buddy port anxiety (fill 0.56 = 14 cells)
            actions.Add(MakeVisit(folder, "05_buddy_port_anxiety", ChapterWhen.FieldFill, 0.56f, "", 0f, "", 0f, _guests["buddy"],
                StepEnter("shock"),
                StepSay("shock", "Pal... Did a guy with gills just walk out of your office?! Or is the lead dust finally driving me mad?"),
                StepChoose("whisper", "The harbor has gone crazy. Soldiers cordoned off the docks, and that cyber-poodle with the glowing red eye chewed through the lab fence. Don't put Ads beside Politics, or the Boss will bury us!",
                    "We stick together.", 3,
                    "Back to the presses.", 4),
                StepSay("smile", "We'll pull through, old friend. I'll save some extra paper for the evening.", next: 5),
                StepSay("default", "Got it, staying out of your hair. Keep working.", next: 5),
                StepExit()
            ));

            AddIdleActions(folder, actions);

            // Fishman revenge if fish_angry >= 1 when stamping
            actions.Add(MakeVisit(folder, "97_fish_shot_visit", ChapterWhen.PaperStamped, 1f, "fish_angry", 1f, "", 0f, _guests["fishman"],
                StepEnter("shot"),
                StepSay("shot", "I warned you, land-worm! No ultimatum in the paper—now you swim with the lead type at the bottom of the bay!"),
                StepExit()
            ));
            actions.Add(MakeEnding(folder, "98_fish_shot_ending", ChapterWhen.PaperStamped, 1f, "fish_angry", 1f));
            actions.Add(MakeClose(folder, "99_close_day", ChapterWhen.PaperStamped));

            // 25 cells in morning-drop-6 (1.00) + 3 cells in guest-stash-6 = 28 cells (1.12), required = 0.80 (20 cells)
            return CreateChapter("Day6", "Day 6: Something Fishy", 0.80f,
                new[] { (ArticleKind.Ad, ArticleKind.Politics) },
                actions.ToArray(),
                ("morning-drop-6", "Fill at least 80% of the page (20/25 cells). Keep Ads away from Politics.",
                    new[] { "cyborg-dog", "coal-shortage", "parade-prep", "boots-ad", "love-ad-2", "roach-trap-ad", "london-tea-spill", "harbor-foghorn", "military-band-rehearsal", "canned-sprats-ad" }),
                ("guest-stash-6", "Hand-delivered by guests.",
                    new[] { "fish-ultimatum" })
            );
        }

        private static ChapterDefinition BuildDay7()
        {
            var folder = EnsureActionDayFolder("Day7");
            var actions = new List<ChapterAction>();

            actions.Add(MakeEnding(folder, "01_early_ending", ChapterWhen.Context, 0f, "ending_reached", 1f));
            actions.Add(MakeCapsule(folder, "02_morning_drop", ChapterWhen.EnterWork, "morning-drop-7"));

            // Chekist order (15s)
            actions.Add(MakeVisit(folder, "03_chekist_order", ChapterWhen.AfterWorkSeconds, 15f, "", 0f, "", 0f, _guests["chekist"],
                StepEnter("calm"),
                StepSay("calm", "The city is restless, Editor. The Leader has fallen ill, and the rats are stirring in the cellars."),
                StepChoose("anger", "Today the wanted notice for the man in the white mask goes on the front page. Refuse to cooperate with the Tribunal, and you'll ride in a black van to the Foundry cellars tonight.",
                    "I'll print the wanted notice.", 3,
                    "A newspaper isn't a police board!", 5),
                StepGive("wanted-mask", 1, next: 4),
                StepSay("cigar", "Commendable prudence. Carry on with your work.", next: 6),
                StepSay("anger", "Siding with the rebels, then?! Finish your issue, scribbler. The black van will be waiting for you tonight!", remember: "heat", value: 3f, next: 6),
                StepExit()
            ));

            // Babka pies (fill 0.44 = 11 cells)
            actions.Add(MakeVisit(folder, "04_babka_pies", ChapterWhen.FieldFill, 0.44f, "", 0f, "", 0f, _guests["babka"],
                StepEnter("whisper"),
                StepSay("whisper", "Dearie, you've wasted away to skin and bones at this cursed desk! Here, take some hot meat pies from Mrs. Marple."),
                StepSay("fear", "People whisper that the Mayor secretly put his daughter on the night train. The big shots are running, son! They smell the ground catching fire under their feet!"),
                StepGive("food-pie", 1),
                StepExit()
            ));

            // Bandit warning & prep (fill 0.64 = 16 cells)
            actions.Add(MakeVisit(folder, "05_bandit_warning", ChapterWhen.FieldFill, 0.64f, "", 0f, "", 0f, _guests["bandit"],
                StepEnter("grim"),
                StepSay("grim", "Things look grim, four-eyes. The Leader is on his deathbed, and the streets will smell of gunpowder any day now."),
                StepSay("business", "Thanks for keeping the Tribunal hounds off our scent. Here's thirty coins from the syndicate. We're prepping motorboats at the pier—save your cash if you want a seat out of here."),
                StepGive("money", 30),
                StepExit()
            ));

            AddIdleActions(folder, actions);

            // Late arrest if heat >= 3 when stamping
            actions.Add(MakeVisit(folder, "97_late_arrest_visit", ChapterWhen.PaperStamped, 1f, "heat", 3f, "", 0f, _guests["chekist"],
                StepEnter("anger"),
                StepSay("anger", "Time's up, Citizen Editor. The black van is downstairs, and your press is sealed by order of the Tribunal. Hands behind your back!"),
                StepExit()
            ));
            actions.Add(MakeEnding(folder, "98_late_arrest_ending", ChapterWhen.PaperStamped, 1f, "heat", 3f));
            actions.Add(MakeClose(folder, "99_close_day", ChapterWhen.PaperStamped));

            // 25 cells in morning-drop-7 (1.00) + 3 cells in guest-stash-7 = 28 cells (1.12), required = 0.80 (20 cells)
            return CreateChapter("Day7", "Day 7: Ghost Hunt", 0.80f,
                new[] { (ArticleKind.Ad, ArticleKind.Politics) },
                actions.ToArray(),
                ("morning-drop-7", "Fill at least 80% of the page (20/25 cells). Never mix Ads with Politics.",
                    new[] { "hospital-rumor", "curfew-extension", "lottery-ad", "inventor-boom", "chess-brawl", "france-baguette-strike", "checkpoint-passes", "telegraph-delays", "suitcases-ad" }),
                ("guest-stash-7", "Hand-delivered by guests.",
                    new[] { "wanted-mask" })
            );
        }

        private static ChapterDefinition BuildDay8()
        {
            var folder = EnsureActionDayFolder("Day8");
            var actions = new List<ChapterAction>();

            actions.Add(MakeEnding(folder, "01_early_ending", ChapterWhen.Context, 0f, "ending_reached", 1f));
            actions.Add(MakeCapsule(folder, "02_morning_drop", ChapterWhen.EnterWork, "morning-drop-8"));

            // Fishman followup (15s)
            actions.Add(MakeVisit(folder, "03_fishman_followup", ChapterWhen.AfterWorkSeconds, 15f, "", 0f, "", 0f, _guests["fishman"],
                StepEnter("gurgle"),
                StepSay("gurgle", "The bay water is churning, land-dweller. The nets were cleared from the embankment, and the catfish are pleased."),
                StepSay("gold", "The Brotherhood of the Deep never forgets a debt. Take these shiny coins for dry tobacco. Soon a storm will swallow your city."),
                StepGive("money", 30),
                StepExit()
            ));

            // Official fleeing (fill 0.24 = 6 cells)
            actions.Add(MakeVisit(folder, "04_official_flee", ChapterWhen.FieldFill, 0.24f, "", 0f, "", 0f, _guests["official"],
                StepEnter("scorn"),
                StepSay("scorn", "They cut the power at the Ministry, and the rabble are burning bonfires right beneath my windows! Unbearable vulgarity!"),
                StepSay("cold", "I am departing on the evening special train to the spa towns. As for you... keep printing your little papers on this sinking ship!"),
                StepSayRule("cold", "Still, you served diligently. Take the remainder of the bonus fund—foreign banks have no use for these coins anyway.",
                    "official_loyal", 0f, next: 4),
                StepGiveRule("money", 20, "official_loyal", 0f, next: 5),
                StepExit()
            ));

            // Boss confession & bonus (fill 0.44 = 11 cells)
            actions.Add(MakeVisit(folder, "05_boss_confession", ChapterWhen.FieldFill, 0.44f, "", 0f, "", 0f, _guests["boss"],
                StepEnter("smirk"),
                StepSay("smirk", "Keep working, keep working! The Military Command just wiped all my print shop debts clean today. Know why? Because I turned in that troublemaker of a previous editor right on time!"),
                StepSay("smirk", "Here's a twenty-coin bonus if you stay just as obedient a hound. Meanwhile, I'm going to pack my bags for the station."),
                StepGive("money", 20),
                StepExit()
            ));

            // Anon unmasking (fill 0.64 = 16 cells)
            actions.Add(MakeVisit(folder, "06_anon_unmask", ChapterWhen.FieldFill, 0.64f, "", 0f, "", 0f, _guests["anon"],
                StepEnter("mask"),
                StepSay("mask", "Bolt the latch. I'm being followed."),
                StepSay("hole", "Look at me, kid. Look closely."),
                StepSay("hole", "Recognize your mentor? The inspectors' bullet went right through the bridge of my nose. The Boss sold me out to clear the print shop's debts, and they dumped me in a lime pit. But I crawled out."),
                StepSay("hole", "Here is the execution roster signed by the Boss and the Commandant. Tomorrow we burn their regime down with the truth."),
                StepGive("execution-list", 1),
                StepChoose("hole", "Are you with me?",
                    "I'm with you, Chief!", 7,
                    "This is madness!", 8),
                StepSay("hole", "Good lad. You've learned how to hold a pen. Keep that galley proof safe in the tray. The finale is near.", remember: "anon_trust", value: 1f, next: 9),
                StepSay("rage", "Coward! They'll devour you just like they devoured me!", next: 9),
                StepExit()
            ));

            AddIdleActions(folder, actions);
            actions.Add(MakeClose(folder, "99_close_day", ChapterWhen.PaperStamped));

            // 25 cells in morning-drop-8 (1.00) + 3 cells in guest-stash-8 = 28 cells (1.12), required = 0.88 (22 cells)
            return CreateChapter("Day8", "Day 8: Behind the Mask", 0.88f,
                new[] { (ArticleKind.Ad, ArticleKind.Politics) },
                actions.ToArray(),
                ("morning-drop-8", "Fill at least 88% of the page (22/25 cells). Tension is at the breaking point. Lock your office door.",
                    new[] { "blackout-city", "train-station-rush", "radio-dog-final", "candle-ad", "elephant-job", "funeral-ad", "usa-prohibition-pipes", "generator-curfew", "foreign-consulate-closed" }),
                ("guest-stash-8", "Hand-delivered by guests.",
                    new[] { "execution-list" })
            );
        }

        private static ChapterDefinition BuildDay9()
        {
            var folder = EnsureActionDayFolder("Day9");
            var actions = new List<ChapterAction>();

            actions.Add(MakeEnding(folder, "01_early_ending", ChapterWhen.Context, 0f, "ending_reached", 1f));
            actions.Add(MakeCapsule(folder, "02_morning_drop", ChapterWhen.EnterWork, "morning-drop-9"));

            // Chekist pressure (15s)
            actions.Add(MakeVisit(folder, "03_chekist_pressure", ChapterWhen.AfterWorkSeconds, 15f, "", 0f, "", 0f, _guests["chekist"],
                StepEnter("anger"),
                StepSay("anger", "The Leader is dead. There is mutiny in the streets. Tomorrow the Military Command is stationing a machine-gun company inside this print shop."),
                StepSay("calm", "We are drafting the State of Siege decree. Put it on the front page tomorrow, and you keep your life and your chair forever. Print a single word from the rebels, and you'll be shot right beside your presses."),
                StepExit()
            ));

            // Bandit flee offer (fill 0.48 = 12 cells)
            actions.Add(MakeVisit(folder, "04_bandit_flee_offer", ChapterWhen.FieldFill, 0.48f, "", 0f, "", 0f, _guests["bandit"],
                StepEnter("smirk"),
                StepSay("smirk", "Well, four-eyes, smell the smoke? Tanks are rolling toward the main square."),
                StepSay("business", "At midnight the last motorboat leaves the old pier for the border. A ticket costs 120 coins. Are you coming with us, or staying to burn in this meat grinder?"),
                StepChooseRule("business", "Do you have the 120 coins?",
                    "I'm in. Here's the cash!", 5,
                    "I'm staying at the paper.", 7,
                    "money", 119.5f),
                StepSay("cold", "Too bad, pal—your pockets are a little light for a ticket. Good luck surviving tomorrow.", next: 8),
                StepGive("money", -120, next: 6),
                StepSay("smirk", "Smart move, kid. Grab your coat and follow me out the back alley.", remember: "ending_reached", value: 1f, next: 8),
                StepSay("cold", "Suit yourself. Whether you're brave or a fool—we'll find out tomorrow.", next: 8),
                StepExit()
            ));

            // Buddy eve talk (fill 0.68 = 17 cells)
            actions.Add(MakeVisit(folder, "05_buddy_eve_talk", ChapterWhen.FieldFill, 0.68f, "", 0f, "", 0f, _guests["buddy"],
                StepEnter("anxiety"),
                StepSay("anxiety", "Pal... The Leader is dead. The airwaves are dead silent—the Command shut down every radio tower and telegraph line."),
                StepSay("handshake", "They silenced the radio, but they can't silence our lead press. Tomorrow is our final issue. Whatever happens—print the truth, old friend."),
                StepExit()
            ));

            AddIdleActions(folder, actions);
            actions.Add(MakeClose(folder, "99_close_day", ChapterWhen.PaperStamped));

            // 27 cells in morning-drop-9 (1.08), required = 0.88 (22 cells)
            return CreateChapter("Day9", "Day 9: Eve of the Storm", 0.88f,
                new[] { (ArticleKind.Ad, ArticleKind.Politics) },
                actions.ToArray(),
                ("morning-drop-9", "Fill at least 88% of the page (22/25 cells). The Dictator is dead. Keep Ads away from Politics.",
                    new[] { "dictator-death", "riot-fires", "port-escape", "ammunition-ad", "whiskey-stock", "love-ad-3", "russia-bear-chess", "garrison-patrols", "bank-withdrawals-halted", "boarding-planks-ad" })
            );
        }

        private static ChapterDefinition BuildDay10()
        {
            var folder = EnsureActionDayFolder("Day10");
            var actions = new List<ChapterAction>();

            actions.Add(MakeEnding(folder, "01_early_ending", ChapterWhen.Context, 0f, "ending_reached", 1f));
            actions.Add(MakeCapsule(folder, "02_morning_drop", ChapterWhen.EnterWork, "morning-drop-10"));

            // Anon final entry (10s)
            actions.Add(MakeVisit(folder, "03_anon_final_entry", ChapterWhen.AfterWorkSeconds, 10f, "", 0f, "", 0f, _guests["anon"],
                StepEnter("hole"),
                StepSay("hole", "This is the hour of reckoning, apprentice. Our printing press is the only voice left in this burning city."),
                StepSay("hole", "Here is my confession. The rosters, the names, the proof. Put it on the front page—and by dawn the people will sweep these executioners away."),
                StepGive("anon-truth", 1),
                StepSay("hole", "The last word is yours."),
                StepExit()
            ));

            // Chekist final entry (25s)
            actions.Add(MakeVisit(folder, "04_chekist_final_entry", ChapterWhen.AfterWorkSeconds, 25f, "", 0f, "", 0f, _guests["chekist"],
                StepEnter("anger"),
                StepSay("anger", "You know why I am here, Editor. Machine guns are set up across the street."),
                StepSay("calm", "Here is the Military Command's Emergency Decree. If this goes out on the front page, the rebels will lay down their arms, and you will remain master of this newspaper forever."),
                StepGive("order-curfew", 1),
                StepSay("cigar", "Make the wrong choice, and this office becomes your mass grave."),
                StepExit()
            ));

            AddIdleActions(folder, actions);

            // Final epilogue visits depending on which 5x2 manifesto was printed (social > 6 for Anon, politics > 6 for Chekist)
            actions.Add(MakeVisit(folder, "96_finale_anon_victory", ChapterWhen.PaperStamped, 1f, "social", 6f, "", 0f, _guests["anon"],
                StepEnter("hole"),
                StepSay("hole", "You printed it! Listen to that roar outside the window—the garrison soldiers just lowered their rifles and joined the barricades!"),
                StepSay("hole", "They couldn't bury the truth in lime, kid. Look at the sunrise over the rooftops. The city is finally free!"),
                StepExit()
            ));

            actions.Add(MakeVisit(folder, "97_finale_chekist_order", ChapterWhen.PaperStamped, 1f, "politics", 6f, "", 0f, _guests["chekist"],
                StepEnter("cigar"),
                StepSay("cigar", "Exemplary discipline, Citizen Editor. Your special edition crushed the rebellion before dawn, and we caught the masked rat by the back door."),
                StepSay("calm", "Here is your permanent Category One press pass. You have kept your life and your chair—as a loyal cog in the Iron Order forever."),
                StepExit()
            ));

            // Finish game EndingAction
            actions.Add(MakeEnding(folder, "99_finish_game", ChapterWhen.PaperStamped, 1f, "", 0f));

            // 19 cells in morning-drop-10 (0.76) + 10 cells for chosen 5x2 manifesto (0.40) = 29 cells (1.16), required = 0.88 (22 cells)
            return CreateChapter("Day10", "Day 10: The Last Word", 0.88f,
                new[] { (ArticleKind.Ad, ArticleKind.Politics) },
                actions.ToArray(),
                ("morning-drop-10", "Fill at least 88% of the page (22/25 cells). Place one of the two final manifestos and surround it with the remaining reports.",
                    new[] { "strike-barricades", "military-guns", "peace-talks", "radio-silence", "china-cricket-arena", "abandoned-posts", "empty-streets-dawn" }),
                ("guest-stash-10", "Hand-delivered by guests.",
                    new[] { "anon-truth", "order-curfew" })
            );
        }

        private static void AddIdleActions(string folder, List<ChapterAction> actions)
        {
            actions.Add(MakeVisit(folder, "90_idle_warning", ChapterWhen.AfterWorkSeconds, 300f, "", 0f, "", 0f, _guests["boss"],
                StepEnter("shout"),
                StepSay("shout", "Did you fall asleep in there?! The paper must be on the presses in three minutes, or I'll hand you over to State Security myself!"),
                StepExit()
            ));

            actions.Add(MakeVisit(folder, "91_idle_fired", ChapterWhen.AfterWorkSeconds, 480f, "", 0f, "", 0f, _guests["boss"],
                StepEnter("shout"),
                StepSay("shout", "That's it! The edition went out blank! You're fired—pack your rags and get out on the street!", remember: "ending_reached", value: 1f),
                StepExit()
            ));
        }

        // =====================================================================
        // STEP & ACTION HELPERS
        // =====================================================================

        private static string EnsureActionDayFolder(string dayName)
        {
            var path = $"{ActionsFolder}/{dayName}";
            Directory.CreateDirectory(path);
            return path;
        }

        private static VisitStep StepEnter(string emotion = "")
        {
            return new VisitStep { kind = VisitStepKind.Enter, emotion = emotion ?? "", amount = 1, next = -1, nextA = -1, nextB = -1 };
        }

        private static VisitStep StepSay(string emotion, string text, string remember = "", float value = 0f, int next = -1)
        {
            return new VisitStep
            {
                kind = VisitStepKind.Say,
                emotion = emotion ?? "",
                text = text,
                amount = 1,
                remember = remember ?? "",
                value = value,
                next = next,
                nextA = -1,
                nextB = -1
            };
        }

        private static VisitStep StepSayRule(string emotion, string text, string ruleParam, float greaterThan, string remember = "", float value = 0f, int next = -1)
        {
            return new VisitStep
            {
                kind = VisitStepKind.Say,
                emotion = emotion ?? "",
                text = text,
                amount = 1,
                rule = new DialogueRule { parameter = ruleParam, greaterThan = greaterThan },
                remember = remember ?? "",
                value = value,
                next = next,
                nextA = -1,
                nextB = -1
            };
        }

        private static VisitStep StepChoose(string emotion, string text, string optionA, int nextA, string optionB, int nextB)
        {
            return new VisitStep
            {
                kind = VisitStepKind.Choose,
                emotion = emotion ?? "",
                text = text,
                optionA = optionA,
                optionB = optionB,
                amount = 1,
                next = -1,
                nextA = nextA,
                nextB = nextB
            };
        }

        private static VisitStep StepChooseRule(string emotion, string text, string optionA, int nextA, string optionB, int nextB, string ruleParam, float greaterThan)
        {
            return new VisitStep
            {
                kind = VisitStepKind.Choose,
                emotion = emotion ?? "",
                text = text,
                optionA = optionA,
                optionB = optionB,
                amount = 1,
                rule = new DialogueRule { parameter = ruleParam, greaterThan = greaterThan },
                next = -1,
                nextA = nextA,
                nextB = nextB
            };
        }

        private static VisitStep StepGive(string giftId, int amount = 1, string remember = "", float value = 0f, int next = -1)
        {
            return new VisitStep
            {
                kind = VisitStepKind.Give,
                giftId = giftId,
                amount = amount,
                remember = remember ?? "",
                value = value,
                next = next,
                nextA = -1,
                nextB = -1
            };
        }

        private static VisitStep StepGiveRule(string giftId, int amount, string ruleParam, float greaterThan, int next = -1)
        {
            return new VisitStep
            {
                kind = VisitStepKind.Give,
                giftId = giftId,
                amount = amount,
                rule = new DialogueRule { parameter = ruleParam, greaterThan = greaterThan },
                next = next,
                nextA = -1,
                nextB = -1
            };
        }

        private static VisitStep StepRemember(string remember, float value, int next = -1)
        {
            return new VisitStep
            {
                kind = VisitStepKind.Remember,
                remember = remember,
                value = value,
                amount = 1,
                next = next,
                nextA = -1,
                nextB = -1
            };
        }

        private static VisitStep StepExit()
        {
            return new VisitStep { kind = VisitStepKind.Exit, amount = 1, next = -1, nextA = -1, nextB = -1 };
        }

        private static CapsuleAction MakeCapsule(string folder, string name, ChapterWhen when, string capsuleId)
        {
            var path = $"{folder}/{name}.asset";
            var action = AssetDatabase.LoadAssetAtPath<CapsuleAction>(path);
            var isNew = action == null;
            if (isNew)
                action = ScriptableObject.CreateInstance<CapsuleAction>();

            var so = new SerializedObject(action);
            so.FindProperty("when").enumValueIndex = (int)when;
            so.FindProperty("at").floatValue = 1f;
            so.FindProperty("parameter").stringValue = "";
            so.FindProperty("minimum").floatValue = 0f;
            so.FindProperty("capsuleId").stringValue = capsuleId;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (isNew)
                AssetDatabase.CreateAsset(action, path);
            EditorUtility.SetDirty(action);
            AssetDatabase.SaveAssetIfDirty(action);
            return action;
        }

        private static CloseAction MakeClose(string folder, string name, ChapterWhen when)
        {
            var path = $"{folder}/{name}.asset";
            var action = AssetDatabase.LoadAssetAtPath<CloseAction>(path);
            var isNew = action == null;
            if (isNew)
                action = ScriptableObject.CreateInstance<CloseAction>();

            var so = new SerializedObject(action);
            so.FindProperty("when").enumValueIndex = (int)when;
            so.FindProperty("at").floatValue = 1f;
            so.FindProperty("parameter").stringValue = "";
            so.FindProperty("minimum").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (isNew)
                AssetDatabase.CreateAsset(action, path);
            EditorUtility.SetDirty(action);
            AssetDatabase.SaveAssetIfDirty(action);
            return action;
        }

        private static EndingAction MakeEnding(string folder, string name, ChapterWhen when, float at, string parameter, float minimum)
        {
            var path = $"{folder}/{name}.asset";
            var action = AssetDatabase.LoadAssetAtPath<EndingAction>(path);
            var isNew = action == null;
            if (isNew)
                action = ScriptableObject.CreateInstance<EndingAction>();

            var so = new SerializedObject(action);
            so.FindProperty("when").enumValueIndex = (int)when;
            so.FindProperty("at").floatValue = at;
            so.FindProperty("parameter").stringValue = parameter ?? "";
            so.FindProperty("minimum").floatValue = minimum;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (isNew)
                AssetDatabase.CreateAsset(action, path);
            EditorUtility.SetDirty(action);
            AssetDatabase.SaveAssetIfDirty(action);
            return action;
        }

        private static VisitAction MakeVisit(string folder, string name, ChapterWhen when, float at,
            string parameter, float minimum, string ruleParam, float ruleGreaterThan,
            GuestDefinition guest, params VisitStep[] steps)
        {
            var path = $"{folder}/{name}.asset";
            var action = AssetDatabase.LoadAssetAtPath<VisitAction>(path);
            var isNew = action == null;
            if (isNew)
                action = ScriptableObject.CreateInstance<VisitAction>();

            var so = new SerializedObject(action);
            so.FindProperty("when").enumValueIndex = (int)when;
            so.FindProperty("at").floatValue = at;
            so.FindProperty("parameter").stringValue = parameter ?? "";
            so.FindProperty("minimum").floatValue = minimum;
            so.FindProperty("rule").FindPropertyRelative("parameter").stringValue = ruleParam ?? "";
            so.FindProperty("rule").FindPropertyRelative("greaterThan").floatValue = ruleGreaterThan;
            so.FindProperty("guest").objectReferenceValue = guest;

            var stepsProp = so.FindProperty("steps");
            stepsProp.arraySize = steps.Length;
            for (var i = 0; i < steps.Length; i++)
            {
                var s = steps[i];
                var elem = stepsProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("kind").enumValueIndex = (int)s.kind;
                elem.FindPropertyRelative("emotion").stringValue = s.emotion ?? "";
                elem.FindPropertyRelative("text").stringValue = s.text ?? "";
                elem.FindPropertyRelative("giftId").stringValue = s.giftId ?? "";
                elem.FindPropertyRelative("amount").intValue = s.amount;
                elem.FindPropertyRelative("optionA").stringValue = s.optionA ?? "";
                elem.FindPropertyRelative("optionB").stringValue = s.optionB ?? "";
                elem.FindPropertyRelative("next").intValue = s.next;
                elem.FindPropertyRelative("nextA").intValue = s.nextA;
                elem.FindPropertyRelative("nextB").intValue = s.nextB;
                elem.FindPropertyRelative("rule").FindPropertyRelative("parameter").stringValue = s.rule.parameter ?? "";
                elem.FindPropertyRelative("rule").FindPropertyRelative("greaterThan").floatValue = s.rule.greaterThan;
                elem.FindPropertyRelative("remember").stringValue = s.remember ?? "";
                elem.FindPropertyRelative("value").floatValue = s.value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            if (isNew)
                AssetDatabase.CreateAsset(action, path);
            EditorUtility.SetDirty(action);
            AssetDatabase.SaveAssetIfDirty(action);
            return action;
        }

        private static ChapterDefinition CreateChapter(string assetName, string title, float minFill,
            (ArticleKind kind, ArticleKind other)[] forbiddenNeighbors,
            ChapterAction[] actions,
            params (string id, string note, string[] articleIds)[] capsules)
        {
            var path = $"{StoryFolder}/{assetName}.asset";
            var chapter = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(path);
            var isNew = chapter == null;
            if (isNew)
                chapter = ScriptableObject.CreateInstance<ChapterDefinition>();

            var so = new SerializedObject(chapter);
            so.FindProperty("title").stringValue = title;

            var actionsProp = so.FindProperty("actions");
            actionsProp.arraySize = actions.Length;
            for (var i = 0; i < actions.Length; i++)
                actionsProp.GetArrayElementAtIndex(i).objectReferenceValue = actions[i];

            var rulesProp = so.FindProperty("rules");
            rulesProp.arraySize = 1;
            rulesProp.GetArrayElementAtIndex(0).FindPropertyRelative("parameterId").stringValue = "fill";
            rulesProp.GetArrayElementAtIndex(0).FindPropertyRelative("minimum").floatValue = minFill;

            var neighborsProp = so.FindProperty("neighbors");
            neighborsProp.arraySize = forbiddenNeighbors.Length;
            for (var i = 0; i < forbiddenNeighbors.Length; i++)
            {
                neighborsProp.GetArrayElementAtIndex(i).FindPropertyRelative("kind").enumValueIndex = (int)forbiddenNeighbors[i].kind;
                neighborsProp.GetArrayElementAtIndex(i).FindPropertyRelative("other").enumValueIndex = (int)forbiddenNeighbors[i].other;
            }

            var capsulesProp = so.FindProperty("capsules");
            capsulesProp.arraySize = capsules.Length;
            var capsuleLogParts = new List<string>();
            for (var i = 0; i < capsules.Length; i++)
            {
                var c = capsules[i];
                var elem = capsulesProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("id").stringValue = c.id;
                elem.FindPropertyRelative("note").stringValue = c.note;
                var artProp = elem.FindPropertyRelative("articles");
                artProp.arraySize = c.articleIds.Length;
                var totalCells = 0;
                var totalFill = 0f;
                for (var j = 0; j < c.articleIds.Length; j++)
                {
                    if (!_articles.TryGetValue(c.articleIds[j], out var art) || art == null)
                    {
                        Debug.LogError($"[StoryGenerator][Chapter:{assetName}] Missing article '{c.articleIds[j]}' in capsule '{c.id}'!");
                    }
                    else
                    {
                        totalCells += CellsForShape(art.Shape);
                        totalFill += art.Fill;
                    }
                    artProp.GetArrayElementAtIndex(j).objectReferenceValue = art;
                }
                capsuleLogParts.Add($"{c.id}: {c.articleIds.Length} articles ({totalCells} cells, {totalFill * 100f:F0}% fill)");
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            if (isNew)
                AssetDatabase.CreateAsset(chapter, path);
            EditorUtility.SetDirty(chapter);
            AssetDatabase.SaveAssetIfDirty(chapter);

            Debug.Log($"[StoryGenerator][Chapter] {(isNew ? "Created" : "Updated")} '{assetName}' ('{title}') | MinFill={minFill * 100f:F0}% ({Mathf.RoundToInt(minFill / CellFill)}/25 cells) | Actions={actions.Length} | Capsules=[{string.Join("; ", capsuleLogParts)}]");
            return chapter;
        }

        private static void WireStoryBookInScene(ChapterDefinition[] chapters)
        {
            var scene = EditorSceneManager.GetSceneByName("Game");
            if (!scene.isLoaded)
                scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity", OpenSceneMode.Single);
            EditorSceneManager.SetActiveScene(scene);

            var storyBook = UnityEngine.Object.FindAnyObjectByType<StoryBook>();
            if (storyBook == null)
            {
                Debug.LogError("[StoryGenerator] Could not find StoryBook in Game.unity!");
                return;
            }

            var credits = AssetDatabase.LoadAssetAtPath<ChapterDefinition>("Assets/_Project/Data/Story/Credits.asset");
            var so = new SerializedObject(storyBook);
            var chaptersProp = so.FindProperty("chapters");
            chaptersProp.arraySize = chapters.Length;
            for (var i = 0; i < chapters.Length; i++)
                chaptersProp.GetArrayElementAtIndex(i).objectReferenceValue = chapters[i];

            if (credits != null)
                so.FindProperty("finale").objectReferenceValue = credits;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(storyBook);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[StoryGenerator][Scene] Wired StoryBook in '{scene.path}' with {chapters.Length} chapters and finale='{(credits != null ? credits.name : "none")}'.");
        }

        private static void VerifyGeneratedStory(ChapterDefinition[] chapters)
        {
            var emptyArticles = 0;
            var missingArticles = 0;
            foreach (var kvp in _articlePaths)
            {
                var id = kvp.Key;
                var path = kvp.Value;
                var reloaded = AssetDatabase.LoadAssetAtPath<ArticleDefinition>(path);
                if (reloaded == null)
                {
                    missingArticles++;
                    Debug.LogError($"[StoryGenerator][Verify] FAILED to load article '{id}' at '{path}'!");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(reloaded.Title) || string.IsNullOrWhiteSpace(reloaded.Body) || reloaded.Fill <= 0f || reloaded.PictureLayout == null)
                {
                    emptyArticles++;
                    Debug.LogError($"[StoryGenerator][Verify] EMPTY/INVALID fields on article '{id}' at '{path}': Title='{reloaded.Title}', BodyLen={(reloaded.Body != null ? reloaded.Body.Length : 0)}, Fill={reloaded.Fill}, Layout={(reloaded.PictureLayout != null ? reloaded.PictureLayout.name : "NULL")}");
                }
            }

            var brokenCapsuleRefs = 0;
            foreach (var chapter in chapters)
            {
                if (chapter == null)
                    continue;
                foreach (var capsule in chapter.Capsules)
                {
                    if (capsule?.Articles == null)
                        continue;
                    for (var i = 0; i < capsule.Articles.Length; i++)
                    {
                        if (capsule.Articles[i] == null)
                        {
                            brokenCapsuleRefs++;
                            Debug.LogError($"[StoryGenerator][Verify] Null article reference in chapter '{chapter.name}', capsule '{capsule.id}' at index {i}!");
                        }
                    }
                }
            }

            if (emptyArticles == 0 && missingArticles == 0 && brokenCapsuleRefs == 0)
                Debug.Log($"[StoryGenerator][Verify] SUCCESS: Verified all {_articlePaths.Count} articles on disk and {chapters.Length} chapters — 0 empty articles, 0 missing files, 0 broken capsule references!");
            else
                Debug.LogError($"[StoryGenerator][Verify] FAILED: emptyArticles={emptyArticles}, missingArticles={missingArticles}, brokenCapsuleRefs={brokenCapsuleRefs}.");
        }
    }
}
