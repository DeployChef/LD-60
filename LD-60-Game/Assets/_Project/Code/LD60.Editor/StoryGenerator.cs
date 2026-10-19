using System;
using System.Collections.Generic;
using System.IO;
using LD60.Gameplay.Articles;
using LD60.Gameplay.Context;
using LD60.Gameplay.Story;
using LD60.UI;
using TMPro;
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
        private const string ComicsFolder = "Assets/_Project/Data/Story/Comics";
        private const string ArtFolder = "Assets/_Project/Art/DariyaArt";

        // 25 cells (5x5 grid) = 1.00 (100% fill) -> 1 cell = 0.04f
        private const float CellFill = 0.04f;

        private static Dictionary<string, ArticleDefinition> _articles;
        private static Dictionary<string, string> _articlePaths;
        private static Dictionary<ArticleKind, int> _kindCounters;
        private static Dictionary<string, (Sprite sprite, ArticlePictureLayout layout)> _preservedCustomPictures;
        private static Dictionary<string, GuestDefinition> _guests;
        private static Dictionary<string, StoryComicsData> _comics;
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

            // 2. Cache any custom pictures assigned by the user, then clean old story/article folders
            _defaultArticleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Blocking/Unit.png");
            CacheCustomArticleSprites();
            Debug.Log("[StoryGenerator] [Step 2/7] Cleaning old Story Chapters/Actions and Article folders...");
            CleanAllAssetsIncludingKindFolders(keepGuests: true);
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
            Debug.Log($"[StoryGenerator] Layouts ready: Picture1 (slot 1), Picture2 (slot 2), Picture3 (slot 3). Default placeholder sprite: {(_defaultArticleSprite != null ? _defaultArticleSprite.name : "NULL")}");

            // 5. Generate Guests
            Debug.Log("[StoryGenerator] [Step 4/7] Generating Guests...");
            GenerateGuests();
            AssetDatabase.SaveAssets();

            // 6. Generate all Articles into Kind subfolders with numbered filenames (e.g. "01 fish-whisper.asset")
            Debug.Log("[StoryGenerator] [Step 5/7] Generating Articles by Kind folders with sequential numbers...");
            GenerateArticles();
            AssetDatabase.SaveAssets();

            // 7. Load existing StoryComicsData assets (read-only: never recreated, cleaned, or modified) and generate 10 Days
            LoadExistingComics();
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

            // 9. Verify every generated asset on disk AND check TextMeshPro layout fit
            VerifyGeneratedStory(chapters);

            Debug.Log($"[StoryGenerator] === GENERATION COMPLETE: {_guests.Count} Guests, {_articles.Count} Articles across 4 Kind folders, {chapters.Length} Chapters wired into Game.unity! ===");
        }

        [MenuItem("LD60/Story/Clean All Story")]
        public static void CleanAllStory()
        {
            Debug.Log("[StoryGenerator] === CLEANING ALL STORY & ARTICLE ASSETS ===");
            Selection.activeObject = null;
            CleanAllAssetsIncludingKindFolders(keepGuests: false);
            WireStoryBookInScene(Array.Empty<ChapterDefinition>());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[StoryGenerator] === CLEAN COMPLETE: Removed all generated Chapters, Actions, Guests, and Articles. ===");
        }

        private static void CacheCustomArticleSprites()
        {
            _preservedCustomPictures = new Dictionary<string, (Sprite sprite, ArticlePictureLayout layout)>(StringComparer.Ordinal);
            if (!Directory.Exists(ArticlesFolder))
                return;

            var guids = AssetDatabase.FindAssets("t:ArticleDefinition", new[] { ArticlesFolder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var art = AssetDatabase.LoadAssetAtPath<ArticleDefinition>(path);
                if (art != null && !string.IsNullOrEmpty(art.Id) && art.Picture != null && art.Picture != _defaultArticleSprite)
                    _preservedCustomPictures[art.Id] = (art.Picture, art.PictureLayout);
            }

            if (_preservedCustomPictures.Count > 0)
                Debug.Log($"[StoryGenerator][Cache] Preserved {_preservedCustomPictures.Count} custom article pictures before rebuilding folders.");
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
                    var path = file.Replace("\\", "/");
                    // Shared appearance/settings assets are not generated story articles.
                    if (AssetDatabase.LoadAssetAtPath<ArticleDefinition>(path) == null)
                        continue;
                    if (AssetDatabase.DeleteAsset(path))
                        removed++;
                }
            }

            Debug.Log($"[StoryGenerator][Clean] Removed {removed} old story/root assets.");
        }

        private static void CleanAllAssetsIncludingKindFolders(bool keepGuests)
        {
            CleanStoryAndLegacyRootArticles();

            var removed = 0;
            if (!keepGuests && Directory.Exists(GuestsFolder))
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

            Debug.Log($"[StoryGenerator][Clean] Cleaned {removed} ArticleKind folders{(keepGuests ? "" : " and guest assets")}.");
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

        private static void LoadExistingComics()
        {
            _comics = new Dictionary<string, StoryComicsData>(StringComparer.OrdinalIgnoreCase);
            var comicNames = new[] { "Intro", "Fired", "Death", "WalkingDead", "IronLaw", "Fish", "Boat" };
            foreach (var name in comicNames)
            {
                var path = $"{ComicsFolder}/{name}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<StoryComicsData>(path);
                if (asset != null)
                {
                    _comics[name] = asset;
                }
                else
                {
                    Debug.LogWarning($"[StoryGenerator][Comics] Existing comic asset not found at '{path}'.");
                }
            }

            Debug.Log($"[StoryGenerator][Comics] Loaded {_comics.Count}/{comicNames.Length} existing StoryComicsData assets from '{ComicsFolder}' (preserved untouched).");
        }

        private static StoryComicsData GetComics(string name)
        {
            if (_comics != null && _comics.TryGetValue(name, out var data) && data != null)
                return data;
            return AssetDatabase.LoadAssetAtPath<StoryComicsData>($"{ComicsFolder}/{name}.asset");
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

            // =====================================================================
            // SPECIAL / GUEST ARTICLES (10)
            // =====================================================================
            AddArticle("fish-whisper", ArticleShape.Line2H, ArticleKind.Report, 0,
                "Old Pier Rumors",
                "Night boatmen swear a huge pike surfaced in the bay and snapped their lines.",
                0f, 1f, 0f);

            AddArticle("deputy-bribe", ArticleShape.Line2H, ArticleKind.Politics, 2,
                "Kind Words",
                "\"While I hold office, no worker goes without bread.\"",
                2f, 0f, 0f);

            AddArticle("official-seal", ArticleShape.Line2H, ArticleKind.Politics, 2,
                "Mill Order",
                "Inspectors found no strike at the mills. Every loom is running.",
                2f, 0f, 0f);

            AddArticle("speakeasy-ad", ArticleShape.Line2H, ArticleKind.Ad, 2,
                "Lily Club",
                "Quiet tables behind velvet curtains. Knock twice at the side door.",
                0f, 0f, 50f);

            AddArticle("anon-accounts", ArticleShape.Line2H, ArticleKind.Politics, 2,
                "Gold Leaks",
                "City funds are moving abroad through banks tied to our press boss.",
                2f, 1f, 0f);

            AddArticle("fish-ultimatum", ArticleShape.Line3H, ArticleKind.Report, 2,
                "Deep Ultimatum",
                "\"Pull every net and hook from the bay by tonight, or the water takes the piers.\"",
                0f, 2f, 0f);

            AddArticle("wanted-mask", ArticleShape.Line3V, ArticleKind.Politics, 2,
                "Wanted Man",
                "10,000 rubles for the man in the white mask. Anyone hiding him faces a military court.",
                3f, 0f, 0f);

            AddArticle("execution-list", ArticleShape.Line3V, ArticleKind.Politics, 2,
                "Firing Roster",
                "A typed list of 120 townsfolk shot at the quarry, signed by the Commandant and our Boss.",
                3f, 2f, 0f);

            AddArticle("anon-truth", ArticleShape.Banner5x2, ArticleKind.Social, 3,
                "THE DEAD EDITOR SPEAKS: \"THEY MISSED!\"",
                "\"You know my name. Last week State Security shot me in the yard and threw me into a lime pit because I refused to kill this story. The Commandant and our own publisher emptied the city treasury into foreign banks. Don't let them slip out on the night train. Take the streets!\"",
                0f, 10f, 0f);

            AddArticle("order-curfew", ArticleShape.Banner5x2, ArticleKind.Politics, 3,
                "MARTIAL LAW IN EFFECT AT SIX TONIGHT",
                "\"By order of the Garrison Command: all street assemblies are banned starting at 18:00. Patrols have orders to fire without warning on anyone found outside after the evening siren. Lock your shutters, stay away from the windows, and wait for morning instructions.\"",
                10f, 0f, 0f);

            // =====================================================================
            // POLITICS ARTICLES
            // =====================================================================
            AddArticle("curfew-extension", ArticleShape.Square, ArticleKind.Politics, 3,
                "Curfew Moved Up",
                "Doors must be bolted by six o'clock. Anyone caught outside without a night pass goes straight to the cells.",
                3f, 0f, 0f);

            AddArticle("curfew-talk", ArticleShape.Line3V, ArticleKind.Politics, 0,
                "Night Patrols",
                "The Commandant advises workers to head straight home after the whistle. Patrols are doubled.",
                3f, 0f, 0f);

            AddArticle("decree-order", ArticleShape.Line3V, ArticleKind.Politics, 0,
                "Street Rules",
                "City Hall reminds residents that stopping to talk in groups of four or more requires a permit.",
                3f, 0f, 0f);

            AddArticle("deputy-piety", ArticleShape.Line3V, ArticleKind.Politics, 0,
                "Orphan Gift",
                "Councilor Crowley sent two sacks of dried bread to St. Jude's Orphanage before the photographers left.",
                3f, 0f, 0f);

            AddArticle("dictator-death", ArticleShape.Line3V, ArticleKind.Politics, 2,
                "Flags Lowered",
                "The Leader died at four this morning. The Military Council has taken charge of all ministries.",
                3f, 0f, 0f);

            AddArticle("military-guns", ArticleShape.Line3V, ArticleKind.Politics, 2,
                "Armored Train",
                "An armored train pulled onto Track 1 at dawn with its turret turned toward the mill district.",
                3f, 0f, 0f);

            AddArticle("parade-prep", ArticleShape.Line3V, ArticleKind.Politics, 0,
                "Sunday March",
                "Every shop on Foundry Row must send thirty marchers to Sunday's parade. Foremen will take roll.",
                2f, 0f, 0f);

            AddArticle("shoe-minister", ArticleShape.Line3V, ArticleKind.Politics, 0,
                "Boot Report",
                "The Trade Minister toured Shoe Factory No. 4 today and praised the thickness of the new rubber soles.",
                3f, 0f, 0f);

            // =====================================================================
            // AD ARTICLES
            // =====================================================================
            AddArticle("ammunition-ad", ArticleShape.Line2V, ArticleKind.Ad, 0,
                "Steel Locks",
                "Heavy iron door bolts and window bars at Schultz Hardware.",
                0f, 0f, 25f);

            AddArticle("balsam-ad", ArticleShape.Line2H, ArticleKind.Ad, 2,
                "Pure Balsam",
                "Spiegel's bitter drops in grain spirit. Good for cold joints.",
                0f, 0f, 25f);

            AddArticle("boots-ad", ArticleShape.Line2V, ArticleKind.Ad, 2,
                "Rubber Boots",
                "Thick ditch waders. Keeps out river mud and dockside slush.",
                0f, 0f, 15f);

            AddArticle("brilliantine-ad", ArticleShape.Line2V, ArticleKind.Ad, 2,
                "Hair Pomade",
                "Keeps your hair parted through sea wind and rough questioning.",
                0f, 0f, 10f);

            AddArticle("candle-ad", ArticleShape.Line2V, ArticleKind.Ad, 0,
                "Wax Candles",
                "Tallow and wax candles sold by the dozen near the old bridge.",
                0f, 0f, 20f);

            AddArticle("funeral-ad", ArticleShape.Line3H, ArticleKind.Ad, 0,
                "Quiet Rest Parlors",
                "Pine and oak boxes with wagon delivery. Ten percent off for Printers' Union cardholders.",
                0f, 0f, 15f);

            AddArticle("hospital-ad", ArticleShape.Square, ArticleKind.Ad, 0,
                "Pine Hill Clinic",
                "Warm beds and mountain air for bad nerves or sleeplessness. No questions asked at intake.",
                0f, 0f, 30f);

            AddArticle("lottery-ad", ArticleShape.Line2V, ArticleKind.Ad, 0,
                "Big Lottery",
                "First prize is a cabin ticket to Stockholm. Draw is Friday.",
                0f, 0f, 10f);

            AddArticle("pawnshop-ad", ArticleShape.Line2H, ArticleKind.Ad, 0,
                "Goldman's Loan Shop",
                "Cash paid on the spot for pocket watches, silver spoons, and winter coats.",
                0f, 0f, 15f);

            AddArticle("roach-trap-ad", ArticleShape.Line2V, ArticleKind.Ad, 0,
                "Gray Powder",
                "Clears out cellar beetles and kitchen roaches overnight.",
                0f, 0f, 10f);

            AddArticle("smokes-ad", ArticleShape.Line2H, ArticleKind.Ad, 0,
                "Harbor Cut Tobacco",
                "Dark shag in tin boxes. Burns slow even in dockside damp.",
                0f, 0f, 5f);

            AddArticle("soap-ad", ArticleShape.Line2V, ArticleKind.Ad, 0,
                "Lye Soap",
                "Cuts right through axle grease and black printer's ink.",
                0f, 0f, 10f);

            AddArticle("tonic-ad", ArticleShape.Line2H, ArticleKind.Ad, 0,
                "Night Shift Drops",
                "Two spoons in hot tea will keep a sleepy clerk upright till dawn.",
                0f, 0f, 15f);

            AddArticle("whiskey-stock", ArticleShape.Line2H, ArticleKind.Ad, 0,
                "Cellar Scotch",
                "Twelve-year malt from a private estate. Last six crates in town.",
                0f, 0f, 20f);

            // =====================================================================
            // REPORTAGE ARTICLES (Russian and French stay in the original)
            // =====================================================================
            AddArticle("archive-break", ArticleShape.Line3H, ArticleKind.Report, 2,
                "Archive Break-In",
                "Someone in a white mask jimmied the back window at the press archive and took the staff files.",
                1f, 1f, 0f);

            AddArticle("cactus-blossom", ArticleShape.Line2H, ArticleKind.Report, 2,
                "Rare Cactus",
                "The conservatory cactus finally opened. Smells like dead smelt.",
                0f, 1f, 0f);

            AddArticle("chess-brawl", ArticleShape.Line2H, ArticleKind.Report, 2,
                "Chess Brawl",
                "Two waterworks clerks traded punches over a rook. Board lost.",
                0f, 1f, 0f);

            AddArticle("china-cricket-arena", ArticleShape.Line3H, ArticleKind.Report, 2,
                "Cricket King",
                "Last night's temple cricket bout ended at forty-seven straight wins. The champion took a rosewood sedan chair. The hall roared.",
                0f, 1f, 0f);

            AddArticle("china-flying-tea", ArticleShape.Line2H, ArticleKind.Report, 2,
                "Floating Tea",
                "Monks at an old Yunnan temple picked high mist tea. The leaves hang in the cup instead of sinking.",
                0f, 1f, 0f);

            AddArticle("city-clock", ArticleShape.Line2V, ArticleKind.Report, 2,
                "Clock Jam",
                "Town Hall's clock stopped at midnight. A rusted fishhook was wedged in the gear.",
                0f, 1f, 0f);

            AddArticle("cyborg-dog", ArticleShape.Square, ArticleKind.Report, 3,
                "Tin-Eyed Poodle",
                "That missing gray poodle turned up behind the battery works with a red glass eye and chewed through a padlock.",
                0f, 2f, 0f);

            AddArticle("elephant-beer", ArticleShape.Line2H, ArticleKind.Report, 2,
                "Drunk Jumbo",
                "The zoo elephant drained a mash tub at the brewery and passed out.",
                0f, 1f, 0f);

            AddArticle("elephant-escape", ArticleShape.Square, ArticleKind.Report, 0,
                "Loose Elephant",
                "Jumbo unlatched his gate at the menagerie this morning. Police ask boys not to throw turnips at him.",
                0f, 1f, 0f);

            AddArticle("elephant-job", ArticleShape.Line2H, ArticleKind.Report, 0,
                "Jumbo Hired",
                "The brewery is keeping Jumbo to haul barrels for a bucket of mash a day.",
                0f, 1f, 0f);

            AddArticle("found-dog-1", ArticleShape.Line2V, ArticleKind.Report, 0,
                "Odd Poodle",
                "Night watchmen saw the lost poodle by the transformer shed, humming like a wire.",
                0f, 1f, 0f);

            AddArticle("france-baguette-strike", ArticleShape.Line3H, ArticleKind.Report, 2,
                "Crise du Pain",
                "Les boulangers parisiens manifestent pour fixer la baguette à 65 centimètres.",
                0f, 1f, 0f);

            AddArticle("france-snail-race", ArticleShape.Line3H, ArticleKind.Report, 2,
                "Prix de l'Escargot",
                "La course d'escargots est suspendue après un soupçon de dopage au beurre.",
                0f, 1f, 0f);

            AddArticle("harbor-patrol", ArticleShape.Line2V, ArticleKind.Report, 0,
                "Bay Patrols",
                "Cutters are searching skiffs past the breakwater. Fishermen heard a fast launch.",
                1f, 1f, 0f);

            AddArticle("inventor-boom", ArticleShape.Line2H, ArticleKind.Report, 2,
                "Shed Blast",
                "Finch's peel boiler blew the roof off his shed on Elm Street.",
                0f, 1f, 0f);

            AddArticle("inventor-engine", ArticleShape.Line2V, ArticleKind.Report, 0,
                "Peel Engine",
                "A retired fitter on Elm Street claims his stove engine runs on potato skins.",
                0f, 1f, 0f);

            AddArticle("london-pigeon-strike", ArticleShape.Line2H, ArticleKind.Report, 2,
                "Idle Pigeons",
                "Trafalgar pigeons sat on the lions all morning and ignored stale crusts.",
                0f, 1f, 0f);

            AddArticle("london-tea-spill", ArticleShape.Line3H, ArticleKind.Report, 2,
                "Thames Tea Spill",
                "A lighter carrying three hundred chests of Earl Grey overturned by Tower Bridge at high tide.",
                0f, 1f, 0f);

            AddArticle("lost-dog", ArticleShape.Line2H, ArticleKind.Report, 2,
                "Lost Poodle",
                "Gray poodle answering to 'Barnaby' lost near Pier 2. Five-coin reward.",
                0f, 1f, 0f);

            AddArticle("mask-graffiti", ArticleShape.Line2V, ArticleKind.Report, 0,
                "Wall Chalk",
                "Someone painted a white mask on the court wall before the sentry came round.",
                1f, 1f, 0f);

            AddArticle("port-escape", ArticleShape.Line3H, ArticleKind.Report, 0,
                "Crowded Pier",
                "Six private launches left the coal slip before dawn with their cabin lamps blacked out.",
                0f, 1f, 0f);

            AddArticle("radio-dog-final", ArticleShape.Line2V, ArticleKind.Report, 0,
                "Wired Dog",
                "A telegraph clerk says the stray poodle carries a tiny receiver under its collar.",
                1f, 1f, 0f);

            AddArticle("radio-silence", ArticleShape.Line2V, ArticleKind.Report, 0,
                "Dead Air",
                "The state wavelength went dead at six. Receivers only pick up static.",
                0f, 1f, 0f);

            AddArticle("russia-bear-chess", ArticleShape.Square, ArticleKind.Report, 3,
                "Медведь обыграл министра",
                "В трактире у Спасских ворот цирковой медведь Потап за пять минут поставил мат министру и потребовал графин «Столичной» с груздями.",
                0f, 1f, 0f);

            AddArticle("russia-ice-cream", ArticleShape.Square, ArticleKind.Report, 3,
                "Пломбир в пургу",
                "Даже в лютый мороз и пургу очередь у ГУМа за пломбиром вытянулась через всю площадь.",
                0f, 1f, 0f);

            AddArticle("torn-nets", ArticleShape.Line2H, ArticleKind.Report, 2,
                "Torn Nets",
                "Something ripped three herring nets to shreds off the north jetty last night.",
                0f, 1f, 0f);

            AddArticle("usa-giant-pie", ArticleShape.Line3H, ArticleKind.Report, 2,
                "Forty-Foot Pie",
                "Manhattan bakers cooled a forty-foot apple pie on flatbed wagons and cut it with a saw.",
                0f, 1f, 0f);

            AddArticle("usa-prohibition-pipes", ArticleShape.Line3H, ArticleKind.Report, 2,
                "Bourbon in Pipes",
                "Plumbers in Chicago found a bootleg still hooked straight into an apartment water main.",
                0f, 1f, 0f);

            AddArticle("weather-fog", ArticleShape.Line2H, ArticleKind.Report, 2,
                "Harbor Fog",
                "Coal barges are anchored off the point until the fog lifts from the channel.",
                0f, 1f, 0f);

            AddArticle("weather-hopeless", ArticleShape.Line2H, ArticleKind.Report, 0,
                "Cold Rain",
                "Sleet from the north bay all week. Keep coal scuttles indoors.",
                0f, 1f, 0f);

            // =====================================================================
            // SOCIAL ARTICLES
            // =====================================================================
            AddArticle("blackout-city", ArticleShape.Square, ArticleKind.Social, 3,
                "District Dark",
                "Stokers walked out at the power station after dark. Neighbors are burning broken crates in iron drums.",
                1f, 3f, 0f);

            AddArticle("bread-queue-riot", ArticleShape.Square, ArticleKind.Social, 0,
                "Flour Store Clash",
                "The line outside Depot No. 3 broke the shutters when the carts came up empty. Mounted police cleared the lane.",
                1f, 3f, 0f);

            AddArticle("bread-ration", ArticleShape.Square, ArticleKind.Social, 0,
                "Half-Loaf Limit",
                "Bakers may sell no more than half a rye loaf per ration card starting Monday.",
                1f, 2f, 0f);

            AddArticle("coal-shortage", ArticleShape.Line3H, ArticleKind.Social, 0,
                "Cold Boilers",
                "No coal cars reached the river tenements this week. Families are stuffing rags into window frames.",
                0f, 2f, 0f);

            AddArticle("factory-hunger", ArticleShape.Line3V, ArticleKind.Social, 0,
                "Gate Pickets",
                "Wives of the second shift stood by the foundry gate all morning with empty soup pails.",
                1f, 3f, 0f);

            AddArticle("foundry-shift", ArticleShape.Square, ArticleKind.Social, 0,
                "Foundry Overtime",
                "Furnace No. 2 poured twelve extra tons of pig iron yesterday. The men asked for hot tea on the night turn.",
                0f, 3f, 0f);

            AddArticle("hospital-rumor", ArticleShape.Line3H, ArticleKind.Social, 0,
                "Late Cars at Clinic",
                "Three black sedans sat outside the state clinic until daybreak with their motors running.",
                1f, 2f, 0f);

            AddArticle("love-ad-1", ArticleShape.Dot, ArticleKind.Social, 0,
                "Blue Beret",
                "Girl on Tram 4: I still have your left glove. Oliver.",
                0f, 1f, 0f);

            AddArticle("love-ad-2", ArticleShape.Dot, ArticleKind.Social, 0,
                "Reply: No!",
                "Oliver, quit ringing the tram bell under my window!",
                0f, 1f, 0f);

            AddArticle("love-ad-3", ArticleShape.Line2H, ArticleKind.Social, 0,
                "Tram Wedding",
                "Driver Oliver and Miss Clara of Tram 4 were married at the depot at noon.",
                0f, 2f, 0f);

            AddArticle("peace-talks", ArticleShape.Line3H, ArticleKind.Social, 0,
                "Women on the Tracks",
                "Twenty mothers sat down across the rails at Depot Square so the troop lorry could not pass.",
                0f, 2f, 0f);

            AddArticle("riot-fires", ArticleShape.Square, ArticleKind.Social, 3,
                "Trams Tipped Over",
                "Crowds pried up cobblestones on Market Street and pushed two tram cars sideways across the bridge road.",
                2f, 3f, 0f);

            AddArticle("strike-barricades", ArticleShape.Line3V, ArticleKind.Social, 0,
                "White Sheets",
                "Recruits in the Third Barracks hung white bedsheets from the upper windows and opened the yard gate.",
                1f, 3f, 0f);

            AddArticle("strike-rumor", ArticleShape.Square, ArticleKind.Social, 0,
                "Looms Idle",
                "Half the weaving shed walked out at noon over unpaid wages. Management locked the canteen.",
                1f, 3f, 0f);

            AddArticle("train-station-rush", ArticleShape.Line3H, ArticleKind.Social, 0,
                "Packed Platform",
                "Every berth on the night express is sold out. Clerks with leather trunks are sleeping on the baggage carts.",
                0f, 2f, 0f);

            AddArticle("turnip-fire", ArticleShape.Line3H, ArticleKind.Social, 0,
                "Root Cellar Fire",
                "Warehouse 6 burned to the brickwork last night. The winter turnip store is gone.",
                0f, 2f, 0f);

            // =====================================================================
            // DAILY CHRONICLE & CLASSIFIED FILLERS (at most 1 crisp Dot per day!)
            // =====================================================================
            // Day 1 extras
            AddArticle("tram-schedule", ArticleShape.Line3V, ArticleKind.Report, 0,
                "Slow Trams",
                "Cars on the river line are running forty minutes apart while crews splice a snapped overhead wire.",
                0f, 1f, 0f);
            AddArticle("harbor-dredging", ArticleShape.Square, ArticleKind.Report, 0,
                "Pier 3 Dredging",
                "The steam dredge brought up black silt and old cable off Pier 3 all morning.",
                0f, 1f, 0f);
            AddArticle("bakery-notice", ArticleShape.Line2H, ArticleKind.Social, 0,
                "Bakery No. 2",
                "Yellow ration slips honored after seven. Bring your own cloth sack.",
                0f, 1f, 0f);
            AddArticle("galosh-repair", ArticleShape.Dot, ArticleKind.Ad, 0,
                "Boot Patch",
                "Torn galoshes vulcanized while you wait on Mill Lane.",
                0f, 0f, 5f);

            // Day 2 extras
            AddArticle("chimney-sweep-ad", ArticleShape.Line2V, ArticleKind.Ad, 0,
                "Flue Sweep",
                "Blocked stove pipes and flues brushed clean for two coins.",
                0f, 0f, 5f);
            AddArticle("library-hours", ArticleShape.Dot, ArticleKind.Social, 0,
                "Warm Hall",
                "Public reading room keeps the coal stove lit till 8.",
                0f, 1f, 0f);

            // Day 3 extras
            AddArticle("bridge-repairs", ArticleShape.Square, ArticleKind.Report, 0,
                "Iron Bridge Work",
                "Riveters are working on the north span today. Heavy coal wagons must take the ferry.",
                0f, 1f, 0f);
            AddArticle("sewing-machines-ad", ArticleShape.Line3H, ArticleKind.Ad, 0,
                "Treadle Repairs",
                "Belts and heavy needles for Singer machines on Canal Street. Will trade for lard or flour.",
                0f, 0f, 10f);

            // Day 4 extras
            AddArticle("boiler-inspection", ArticleShape.Square, ArticleKind.Report, 0,
                "Steam Pipe Check",
                "City fitters are testing cellar valves along the canal. Do not touch the street vents.",
                0f, 1f, 0f);
            AddArticle("streetcar-detour", ArticleShape.Line3H, ArticleKind.Report, 0,
                "Square Closed",
                "Wagons must go round behind the cathedral today while cadets drill on the cobbles.",
                1f, 0f, 0f);
            AddArticle("typewriter-ribbon-ad", ArticleShape.Dot, ArticleKind.Ad, 0,
                "Ink Ribbons",
                "Fresh black silk ribbons for Underwood typewriters.",
                0f, 0f, 10f);

            // Day 5 extras
            AddArticle("waterworks-notice", ArticleShape.Square, ArticleKind.Social, 0,
                "Boil Tap Water",
                "River intake screens broke last night. Boil all drinking water for ten minutes before use.",
                0f, 2f, 0f);
            AddArticle("scrap-metal-drive", ArticleShape.Line3H, ArticleKind.Politics, 0,
                "Scrap Iron Drive",
                "Schoolboys hauled four tons of old iron bedframes to the foundry yard this afternoon.",
                2f, 0f, 0f);
            AddArticle("kerosene-lamps-ad", ArticleShape.Line2V, ArticleKind.Ad, 0,
                "Lamp Wicks",
                "Glass chimneys and flat cotton wicks at the corner chandler's.",
                0f, 0f, 10f);
            AddArticle("lost-briefcase", ArticleShape.Dot, ArticleKind.Report, 0,
                "Lost Folio",
                "Brown leather folder left in a hackney cab by the bank.",
                1f, 0f, 0f);

            // Day 6 extras
            AddArticle("harbor-foghorn", ArticleShape.Square, ArticleKind.Report, 0,
                "Stuck Foghorn",
                "The steam horn on the breakwater jammed open at two in the morning and blew until the boiler ran dry.",
                0f, 1f, 0f);
            AddArticle("military-band-rehearsal", ArticleShape.Line2H, ArticleKind.Report, 0,
                "Brass Practice",
                "The garrison band practiced funeral marches in the rain for three hours.",
                1f, 0f, 0f);
            AddArticle("canned-sprats-ad", ArticleShape.Line2V, ArticleKind.Ad, 0,
                "Tin Sprats",
                "Salted sprats in oil. Three tins for a silver piece at the pier.",
                0f, 0f, 10f);

            // Day 7 extras
            AddArticle("checkpoint-passes", ArticleShape.Square, ArticleKind.Politics, 0,
                "New Pass Stamp",
                "Old white work passes expire Friday. Night-shift men need the purple garrison stamp.",
                2f, 0f, 0f);
            AddArticle("telegraph-delays", ArticleShape.Line3V, ArticleKind.Report, 0,
                "Wire Delays",
                "Private telegrams are suspended until linemen repair the poles south of the marsh.",
                1f, 1f, 0f);
            AddArticle("suitcases-ad", ArticleShape.Line2H, ArticleKind.Ad, 0,
                "Canvas Trunks",
                "Strapped travelling trunks with brass locks. Ready to carry.",
                0f, 0f, 15f);
            AddArticle("lost-keys", ArticleShape.Dot, ArticleKind.Report, 0,
                "Lost Keys",
                "Ring of six brass keys found on the tram steps.",
                0f, 1f, 0f);

            // Day 8 extras
            AddArticle("generator-curfew", ArticleShape.Square, ArticleKind.Report, 0,
                "Backup Dynamos",
                "The print shop and telegraph office are running on oil engines while the street mains are dead.",
                0f, 1f, 0f);
            AddArticle("foreign-consulate-closed", ArticleShape.Line2V, ArticleKind.Report, 0,
                "Envoys Gone",
                "The Northern Trade Mission burned its papers and locked the gate.",
                1f, 1f, 0f);
            AddArticle("night-watch-roster", ArticleShape.Dot, ArticleKind.Report, 0,
                "Yard Watch",
                "Tenements must post two men at the gate after dark.",
                1f, 0f, 0f);

            // Day 9 extras
            AddArticle("garrison-patrols", ArticleShape.Square, ArticleKind.Politics, 0,
                "Bridge Checkpoints",
                "Machine-gun carts are parked at all three river bridges. Soldiers are opening every handcart.",
                2f, 0f, 0f);
            AddArticle("bank-withdrawals-halted", ArticleShape.Line2V, ArticleKind.Social, 0,
                "Bank Shut",
                "The Municipal Savings Bank pulled its iron grate down at nine.",
                0f, 2f, 0f);
            AddArticle("boarding-planks-ad", ArticleShape.Dot, ArticleKind.Ad, 0,
                "Pine Planks",
                "Rough pine boards and keg nails for shop fronts.",
                0f, 0f, 15f);

            // Day 10 extras
            AddArticle("abandoned-posts", ArticleShape.Square, ArticleKind.Report, 0,
                "Empty Guard Booths",
                "Nobody was manning the sandbag post on Foundry Avenue at dawn. A kettle was still boiling on the stove.",
                0f, 1f, 0f);
            AddArticle("empty-streets-dawn", ArticleShape.Dot, ArticleKind.Report, 0,
                "Quiet Dawn",
                "Not a tram bell rang at six. Everyone is indoors.",
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

            // Restore custom sprite & layout if the user had assigned one to this article ID
            (Sprite sprite, ArticlePictureLayout layout) customPic = default;
            var preservedCustomSprite = _preservedCustomPictures != null && _preservedCustomPictures.TryGetValue(id, out customPic) && customPic.sprite != null;
            var spriteToUse = preservedCustomSprite ? customPic.sprite : defaultSprite;
            var layoutToUse = preservedCustomSprite && customPic.layout != null && customPic.layout.Slot != 1
                ? customPic.layout
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
                StepSay("shock", "Morning, pal. Guess the big chair is yours now, for whatever that's worth."),
                StepSay("shock", "Two inspectors dragged the old Chief out before dawn. Didn't even let him grab his coat."),
                StepSay("sympathy", "The Boss is already upstairs yelling for page proofs. Today's wire clippings are in the folder on your desk."),
                StepSay("default", "Fill at least 72% of the sheet, and keep the ads away from the political pieces or the censor will have our hides. I'll go light the linotype pot."),
                StepExit()
            ));

            // 2. Early ending hook (if idle-fired triggers)
            actions.Add(MakeEnding(folder, "02_early_ending", ChapterWhen.Context, 0f, "ending_reached", 1f, GetComics("Fired")));

            // 3. Morning drop (EnterWork)
            actions.Add(MakeCapsule(folder, "03_morning_drop", ChapterWhen.EnterWork, "morning-drop-1"));

            // 4. Boss warning (15s)
            actions.Add(MakeVisit(folder, "04_boss_warning", ChapterWhen.AfterWorkSeconds, 15f, "", 0f, "", 0f, _guests["boss"],
                StepEnter("smirk"),
                StepChoose("smirk", "Don't get too comfortable in that chair. I want the page packed tight, and keep the ads away from the political column. I can't afford another fine from the Ministry.",
                    "Understood, Boss.", 2,
                    "Where did they take the Chief?", 3),
                StepSay("smirk", "Good. Keep your head down and paste the type.", next: 4),
                StepSay("shout", "Keep your nose in your own paste pot! He's cutting timber up north, and you'll be holding the other end of his saw if the paper is late!", next: 4),
                StepExit()
            ));

            // 5. Babka rumors (fill 0.44 = 11 cells)
            actions.Add(MakeVisit(folder, "05_babka_arrives", ChapterWhen.FieldFill, 0.44f, "", 0f, "", 0f, _guests["babka"],
                StepEnter("whisper"),
                StepChoose("whisper", "Son, have you heard what happened down at the pier? The night boatmen swear a pike as big as a hog stuck its head out of the water and cursed the foreman in plain speech! Put it in the paper so folk stay off the docks!",
                    "Leave the note here.", 2,
                    "I'm busy, Mrs. Marple.", 4),
                StepGive("fish-whisper", 1, remember: "babka_promise", value: 1f, next: 3),
                StepSay("smirk", "Bless your heart, dearie. At least you still listen to working folk.", next: 5),
                StepSay("fear", "One day in a leather chair and you're already too grand for the neighbors.", next: 5),
                StepExit()
            ));

            AddIdleActions(folder, actions);
            actions.Add(MakeClose(folder, "99_close_day", ChapterWhen.PaperStamped));

            // 26 cells in morning-drop-1 (1.04) + 2 cells in guest-stash-1 = 28 cells (1.12), required = 0.72 (18 cells)
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

            actions.Add(MakeEnding(folder, "01_early_ending", ChapterWhen.Context, 0f, "ending_reached", 1f, GetComics("Fired")));
            actions.Add(MakeCapsule(folder, "02_morning_drop", ChapterWhen.EnterWork, "morning-drop-2"));

            // Deputy visit (15s)
            actions.Add(MakeVisit(folder, "03_deputy_visit", ChapterWhen.AfterWorkSeconds, 15f, "", 0f, "", 0f, _guests["deputy"],
                StepEnter("smug"),
                StepChoose("smug", "So you're the new boy. I'm Councilor Crowley. There's a notice in your folder about the bread I sent to the orphanage. Make sure it's on the page today.",
                    "Space costs extra.", 2,
                    "I print what fits.", 5),
                StepGive("money", 40, remember: "took_deputy_bribe", value: 1f, next: 3),
                StepGive("deputy-bribe", 1, next: 4),
                StepSay("smug", "You learn quick. Here's forty coins for your trouble, and slip this quote from me in while you're at it.", next: 6),
                StepSay("anger", "Careful, boy. The last man who sat at this desk thought he could lecture me too, and look where it got him.", remember: "angry_deputy", value: 1f, next: 6),
                StepExit()
            ));

            // Buddy advice (fill 0.48 = 12 cells)
            actions.Add(MakeVisit(folder, "04_buddy_advice", ChapterWhen.FieldFill, 0.48f, "", 0f, "", 0f, _guests["buddy"],
                StepEnter("sympathy"),
                StepSay("sympathy", "Just stepping out for a smoke. Did you read that item about the break-in at the press archive? They say the guy wore a white mask."),
                StepSay("smile", "By the way, if a clipping doesn't fit today's sheet, drop it in the Save tray on your desk. It'll stay there till tomorrow. Anything left loose on the blotter gets swept out by the night cleaner."),
                StepExit()
            ));

            AddIdleActions(folder, actions);
            actions.Add(MakeClose(folder, "99_close_day", ChapterWhen.PaperStamped));

            // 26 cells in morning-drop-2 (1.04) + 2 cells in guest-stash-2 = 28 cells (1.12), required = 0.72 (18 cells)
            return CreateChapter("Day2", "Day 2: Gold Mine", 0.72f,
                new[] { (ArticleKind.Ad, ArticleKind.Politics) },
                actions.ToArray(),
                ("morning-drop-2", "Fill at least 72% of the page (18/25 cells). Keep Ads away from Politics.",
                    new[] { "balsam-ad", "deputy-piety", "archive-break", "weather-fog", "foundry-shift", "soap-ad", "elephant-escape", "russia-ice-cream", "chimney-sweep-ad", "library-hours" }),
                ("guest-stash-2", "Hand-delivered by guests.",
                    new[] { "deputy-bribe" })
            );
        }

        private static ChapterDefinition BuildDay3()
        {
            var folder = EnsureActionDayFolder("Day3");
            var actions = new List<ChapterAction>();

            actions.Add(MakeEnding(folder, "01_early_ending", ChapterWhen.Context, 0f, "ending_reached", 1f, GetComics("Fired")));
            actions.Add(MakeCapsule(folder, "02_morning_drop", ChapterWhen.EnterWork, "morning-drop-3"));

            // Official visit (15s)
            actions.Add(MakeVisit(folder, "03_official_visit", ChapterWhen.AfterWorkSeconds, 15f, "", 0f, "", 0f, _guests["official"],
                StepEnter("cold"),
                StepSay("cold", "Good morning. The weavers in the river ward are talking strike again, and the Ministry wants the papers quiet."),
                StepChoose("scorn", "Today's instruction is simple: do not place social complaints next to official notices. We do not want readers connecting the two.",
                    "I'll keep them apart.", 3,
                    "The mills are unheated.", 5),
                StepGive("official-seal", 1, remember: "official_loyal", value: 1f, next: 4),
                StepSay("cold", "Sensible. Put this inspection note in the paper as well. People calm down when they read the looms are running.", next: 6),
                StepSay("threat", "You are paid to arrange type, young man, not to audit coal rations. Keep talking like that and I will have your name added to the tribunal list.", remember: "official_doubt", value: 1f, next: 6),
                StepExit()
            ));

            // Bandit scout (fill 0.48 = 12 cells)
            actions.Add(MakeVisit(folder, "04_bandit_scout", ChapterWhen.FieldFill, 0.48f, "", 0f, "", 0f, _guests["bandit"],
                StepEnter("smoke"),
                StepSay("smoke", "Afternoon, chief. Just wanted a look at the new fellow behind the desk."),
                StepSay("smirk", "Your predecessor and my boys had a quiet arrangement. Don't go playing hero with the shipping news and we'll get along fine. I'll be back tomorrow with some paid copy."),
                StepExit()
            ));

            AddIdleActions(folder, actions);
            actions.Add(MakeClose(folder, "99_close_day", ChapterWhen.PaperStamped));

            // 26 cells in morning-drop-3 (1.04) + 2 cells in guest-stash-3 = 28 cells (1.12), required = 0.72 (18 cells)
            return CreateChapter("Day3", "Day 3: Still Waters", 0.72f,
                new[] { (ArticleKind.Politics, ArticleKind.Social) },
                actions.ToArray(),
                ("morning-drop-3", "Fill at least 72% of the page (18/25 cells). Directive: never place Politics next to Social pieces.",
                    new[] { "found-dog-1", "torn-nets", "decree-order", "strike-rumor", "tonic-ad", "love-ad-1", "weather-hopeless", "france-snail-race", "bridge-repairs", "sewing-machines-ad" }),
                ("guest-stash-3", "Hand-delivered by guests.",
                    new[] { "official-seal" })
            );
        }

        private static ChapterDefinition BuildDay4()
        {
            var folder = EnsureActionDayFolder("Day4");
            var actions = new List<ChapterAction>();

            actions.Add(MakeEnding(folder, "01_early_ending", ChapterWhen.Context, 0f, "ending_reached", 1f, GetComics("Fired")));
            actions.Add(MakeCapsule(folder, "02_morning_drop", ChapterWhen.EnterWork, "morning-drop-4"));

            // Bandit visit (15s)
            actions.Add(MakeVisit(folder, "03_bandit_visit", ChapterWhen.AfterWorkSeconds, 15f, "", 0f, "", 0f, _guests["bandit"],
                StepEnter("smirk"),
                StepSay("smirk", "Rough weather out there, scribe. My boys just opened a back-room parlor on Canal Street called the Lily Club. Good Scotch, no uniform crowd."),
                StepChoose("threat", "Run this notice today and fifty coins stay on your blotter. Turn me down, and you'll be walking home by the canal in the dark.",
                    "Leave the fifty.", 3,
                    "Find another paper.", 6),
                StepGive("money", 50, remember: "mob_deal", value: 1f, next: 4),
                StepGive("speakeasy-ad", 1, next: 5),
                StepSay("smirk", "Pleasure doing business. I'll look for it in the morning edition.", next: 7),
                StepSay("cold", "Suit yourself, four-eyes. Just remember how cold that canal water gets in November.", remember: "angry_bandit", value: 1f, next: 7),
                StepExit()
            ));

            // Official followup (fill 0.32 = 8 cells)
            actions.Add(MakeVisit(folder, "04_official_followup", ChapterWhen.FieldFill, 0.32f, "", 0f, "", 0f, _guests["official"],
                StepEnter("cold"),
                StepSayRule("cold", "The Directorate was pleased with yesterday's layout. Here is twenty coins from the press fund. Keep the page clean of tavern notices.",
                    "official_loyal", 0f, next: 2),
                StepGiveRule("money", 20, "official_loyal", 0f, next: 5),
                StepSay("scorn", "I went over yesterday's sheet, Editor. Your remarks about the mill hands went into my ledger.", next: 4),
                StepSay("threat", "I have sent a copy of your file to the State Inspector. Watch your step.", remember: "heat", value: 1f, next: 5),
                StepExit()
            ));

            // Babka queue rumor (fill 0.56 = 14 cells)
            actions.Add(MakeVisit(folder, "05_babka_queue_rumor", ChapterWhen.FieldFill, 0.56f, "", 0f, "", 0f, _guests["babka"],
                StepEnter("panting"),
                StepSay("panting", "Lord save us, I nearly lost my shawl in the crush! Down at Flour Store No. 3 the crowd broke the shutters, and the mounted police came down the lane with flats of their sabers!"),
                StepChoose("whisper", "The dockers say army barges are hauling the grain out after midnight while we stand in line for chaff. Are you going to print what happened at the store?",
                    "I'll run the story.", 3,
                    "Go home and lock your door.", 4),
                StepSay("default", "Thank you, son. Let the whole town read what they did to hungry women.", next: 5),
                StepSay("default", "Aye, everybody's afraid of the troopers now. God help us this winter.", next: 5),
                StepExit()
            ));

            AddIdleActions(folder, actions);
            actions.Add(MakeClose(folder, "99_close_day", ChapterWhen.PaperStamped));

            // 26 cells in morning-drop-4 (1.04) + 2 cells in guest-stash-4 = 28 cells (1.12), required = 0.80 (20 cells)
            return CreateChapter("Day4", "Day 4: Shadow Syndicate", 0.80f,
                new[] { (ArticleKind.Ad, ArticleKind.Politics) },
                actions.ToArray(),
                ("morning-drop-4", "Fill at least 80% of the page (20/25 cells). Keep Ads away from Politics.",
                    new[] { "mask-graffiti", "bread-queue-riot", "curfew-talk", "pawnshop-ad", "inventor-engine", "brilliantine-ad", "usa-giant-pie", "boiler-inspection", "streetcar-detour", "typewriter-ribbon-ad" }),
                ("guest-stash-4", "Hand-delivered by guests.",
                    new[] { "speakeasy-ad" })
            );
        }

        private static ChapterDefinition BuildDay5()
        {
            var folder = EnsureActionDayFolder("Day5");
            var actions = new List<ChapterAction>();

            actions.Add(MakeEnding(folder, "01_early_ending", ChapterWhen.Context, 0f, "ending_reached", 1f, GetComics("Fired")));
            actions.Add(MakeCapsule(folder, "02_morning_drop", ChapterWhen.EnterWork, "morning-drop-5"));

            // Chekist inquiry (15s)
            actions.Add(MakeVisit(folder, "03_chekist_inquiry", ChapterWhen.AfterWorkSeconds, 15f, "", 0f, "", 0f, _guests["chekist"],
                StepEnter("cigar"),
                StepSayRule("anger", "We raided an unlicensed bar on Canal Street last night and found your newspaper on the tables. Run another ad for smugglers and you'll be riding north in a boxcar.",
                    "mob_deal", 0f, remember: "heat", value: 1f, next: 3),
                StepSay("calm", "You kept the bootleggers out of the paper yesterday. Good. We are looking for a man in a white mask who has been slipping into print shops after dark. If he shows up here, ring the guard post.", next: 3),
                StepExit()
            ));

            // Buddy sandwich care (fill 0.40 = 10 cells)
            actions.Add(MakeVisit(folder, "04_buddy_sandwich", ChapterWhen.FieldFill, 0.40f, "", 0f, "", 0f, _guests["buddy"],
                StepEnter("smile"),
                StepSay("smile", "Easy, pal. I passed the Inspector on the stairs and you look like you haven't slept since Tuesday."),
                StepSay("sympathy", "Here. My wife packed an extra ham sandwich in wax paper. Eat something before you pass out on the galley proofs."),
                StepGive("food-sandwich", 1),
                StepExit()
            ));

            // Anon first visit (fill 0.60 = 15 cells)
            actions.Add(MakeVisit(folder, "05_anon_first_visit", ChapterWhen.FieldFill, 0.60f, "", 0f, "", 0f, _guests["anon"],
                StepEnter("mask"),
                StepSay("mask", "Don't reach for the bell. Five days in that chair and you still think the Boss is going to protect you?"),
                StepSay("mask", "Look at this sheet. Bank transfers from the municipal coal fund straight to Zurich, signed by the Mayor and your publisher. Put it on the page if you have a spine."),
                StepGive("anon-accounts", 1),
                StepChoose("mask", "Well?",
                    "Who are you?", 5,
                    "Get out before I call the sentry!", 6),
                StepSay("mask", "The man who sat at this desk before you. Keep the type dry, kid.", next: 7),
                StepSay("rage", "Call them if you want. They'll shoot you right after me for reading it.", next: 7),
                StepExit()
            ));

            AddIdleActions(folder, actions);

            actions.Add(MakeEnding(folder, "98_early_arrest", ChapterWhen.PaperStamped, 1f, "heat", 3f, GetComics("Death")));
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

            actions.Add(MakeEnding(folder, "01_early_ending", ChapterWhen.Context, 0f, "ending_reached", 1f, GetComics("Fired")));
            actions.Add(MakeCapsule(folder, "02_morning_drop", ChapterWhen.EnterWork, "morning-drop-6"));

            // Fishman visit (15s)
            actions.Add(MakeVisit(folder, "03_fishman_visit", ChapterWhen.AfterWorkSeconds, 15f, "", 0f, "", 0f, _guests["fishman"],
                StepEnter("gurgle"),
                StepSay("gurgle", "Your room stinks of coal smoke, dry-lander. Your dredging barges and herring nets are choking our channels in the bay."),
                StepChoose("anger", "Put our warning in tonight's paper. Every boat and net off the water by sundown, or I come back with a harpoon.",
                    "I'll print the warning.", 3,
                    "Get your wet boots off my floor!", 5),
                StepGive("fish-ultimatum", 1, remember: "fish_ally", value: 1f, next: 4),
                StepSay("gurgle", "Smart minnow. See that the boatmen read it.", next: 6),
                StepSay("shot", "Then lock your shutters tonight, dry-lander. The tide comes up the drainpipes.", remember: "fish_angry", value: 2f, next: 6),
                StepExit()
            ));

            // Official parade demand (fill 0.32 = 8 cells)
            actions.Add(MakeVisit(folder, "04_official_parade", ChapterWhen.FieldFill, 0.32f, "", 0f, "", 0f, _guests["official"],
                StepEnter("cold"),
                StepSay("cold", "Sunday is the Garrison March. The Commandant expects a cheerful paper. I don't want to see a single line about cold boilers."),
                StepChoose("scorn", "Give the parade notice a clean column today, and I will have the Inspector pull your name from his watch list.",
                    "The march goes in.", 3,
                    "Half the ward is out of coal.", 6),
                StepGive("money", 30, remember: "official_loyal", value: 1f, next: 4),
                StepRemember("heat", 0f, next: 5),
                StepSay("cold", "Good. Here is thirty coins from the Directorate, and I'll tell the Inspector to drop his inquiry.", next: 8),
                StepRemember("official_loyal", 0f, next: 7),
                StepSay("threat", "Then deal with the Inspector yourself. I am done making excuses for this shop.", remember: "heat", value: 2f, next: 8),
                StepExit()
            ));

            // Buddy port anxiety (fill 0.56 = 14 cells)
            actions.Add(MakeVisit(folder, "05_buddy_port_anxiety", ChapterWhen.FieldFill, 0.56f, "", 0f, "", 0f, _guests["buddy"],
                StepEnter("shock"),
                StepSay("shock", "Tell me I'm seeing things. Did a fellow with river weed on his coat just walk down the back stairs, leaving wet puddles all the way to the alley?"),
                StepChoose("whisper", "The whole harbor's gone mad today. Troops blocked off Pier 2, and that stray poodle with the red glass eye bit through a chain at the battery works. Just keep the ads away from the political column so we can go home in one piece.",
                    "We'll get through the shift.", 3,
                    "Go check the ink rollers.", 4),
                StepSay("smile", "Yeah. Only a few days left in the week. I'll keep the press warm.", next: 5),
                StepSay("default", "Right, right. Back to the cellar I go.", next: 5),
                StepExit()
            ));

            AddIdleActions(folder, actions);

            // Fishman revenge if fish_angry >= 1 when stamping
            actions.Add(MakeVisit(folder, "97_fish_shot_visit", ChapterWhen.PaperStamped, 1f, "fish_angry", 1f, "", 0f, _guests["fishman"],
                StepEnter("shot"),
                StepSay("shot", "I warned you, dry-lander. No warning in the paper, so the bay comes to collect."),
                StepExit()
            ));
            actions.Add(MakeEnding(folder, "98_fish_shot_ending", ChapterWhen.PaperStamped, 1f, "fish_angry", 1f, GetComics("Fish")));
            actions.Add(MakeClose(folder, "99_close_day", ChapterWhen.PaperStamped));

            // 26 cells in morning-drop-6 (1.04) + 3 cells in guest-stash-6 = 29 cells (1.16), required = 0.80 (20 cells)
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

            actions.Add(MakeEnding(folder, "01_early_ending", ChapterWhen.Context, 0f, "ending_reached", 1f, GetComics("Fired")));
            actions.Add(MakeCapsule(folder, "02_morning_drop", ChapterWhen.EnterWork, "morning-drop-7"));

            // Chekist order (15s)
            actions.Add(MakeVisit(folder, "03_chekist_order", ChapterWhen.AfterWorkSeconds, 15f, "", 0f, "", 0f, _guests["chekist"],
                StepEnter("calm"),
                StepSay("calm", "The doctors have been at the palace since midnight. The Leader's heart is failing, and we are clearing the streets before dark."),
                StepChoose("anger", "Put this wanted notice for the masked man on today's page. If you balk, my men will lock this shop and take you down to the foundry cellars tonight.",
                    "Give me the notice.", 3,
                    "This is a newspaper, not a wall poster.", 5),
                StepGive("wanted-mask", 1, next: 4),
                StepSay("cigar", "Smart choice. Make sure it's legible.", next: 6),
                StepSay("anger", "So that's how it is. Finish your layout, then. The van will be downstairs when the press stops.", remember: "heat", value: 3f, next: 6),
                StepExit()
            ));

            // Babka pies (fill 0.44 = 11 cells)
            actions.Add(MakeVisit(folder, "04_babka_pies", ChapterWhen.FieldFill, 0.44f, "", 0f, "", 0f, _guests["babka"],
                StepEnter("whisper"),
                StepSay("whisper", "Look at the dark rings under your eyes, dearie. I brought you two hot cabbage pies in a cloth while the stove was still warm."),
                StepSay("fear", "My niece works at the station. She says the Mayor put his wife and six trunks on the midnight express. When the big shots start packing silver, trouble isn't far behind."),
                StepGive("food-pie", 1),
                StepExit()
            ));

            // Bandit warning & prep (fill 0.64 = 16 cells)
            actions.Add(MakeVisit(folder, "05_bandit_warning", ChapterWhen.FieldFill, 0.64f, "", 0f, "", 0f, _guests["bandit"],
                StepEnter("grim"),
                StepSay("grim", "Word from the palace is the old man won't last the week. Tomorrow the garrison is going to lock the bridges."),
                StepSay("business", "Here's thirty coins we owed you. My boys are fueling a fast launch behind the coal slip. Keep 120 coins in your coat if you want a seat across the bay when the shooting starts."),
                StepGive("money", 30),
                StepExit()
            ));

            AddIdleActions(folder, actions);

            // Late arrest if heat >= 3 when stamping
            actions.Add(MakeVisit(folder, "97_late_arrest_visit", ChapterWhen.PaperStamped, 1f, "heat", 3f, "", 0f, _guests["chekist"],
                StepEnter("anger"),
                StepSay("anger", "That's your last issue. The van is at the curb, and my men are padlocking the press room. Put your coat on."),
                StepExit()
            ));
            actions.Add(MakeEnding(folder, "98_late_arrest_ending", ChapterWhen.PaperStamped, 1f, "heat", 3f, GetComics("Death")));
            actions.Add(MakeClose(folder, "99_close_day", ChapterWhen.PaperStamped));

            // 26 cells in morning-drop-7 (1.04) + 3 cells in guest-stash-7 = 29 cells (1.16), required = 0.80 (20 cells)
            return CreateChapter("Day7", "Day 7: Ghost Hunt", 0.80f,
                new[] { (ArticleKind.Ad, ArticleKind.Politics) },
                actions.ToArray(),
                ("morning-drop-7", "Fill at least 80% of the page (20/25 cells). Never mix Ads with Politics.",
                    new[] { "hospital-rumor", "curfew-extension", "lottery-ad", "inventor-boom", "chess-brawl", "france-baguette-strike", "checkpoint-passes", "telegraph-delays", "suitcases-ad", "lost-keys" }),
                ("guest-stash-7", "Hand-delivered by guests.",
                    new[] { "wanted-mask" })
            );
        }

        private static ChapterDefinition BuildDay8()
        {
            var folder = EnsureActionDayFolder("Day8");
            var actions = new List<ChapterAction>();

            actions.Add(MakeEnding(folder, "01_early_ending", ChapterWhen.Context, 0f, "ending_reached", 1f, GetComics("Fired")));
            actions.Add(MakeCapsule(folder, "02_morning_drop", ChapterWhen.EnterWork, "morning-drop-8"));

            // Fishman followup (15s)
            actions.Add(MakeVisit(folder, "03_fishman_followup", ChapterWhen.AfterWorkSeconds, 15f, "", 0f, "", 0f, _guests["fishman"],
                StepEnter("gurgle"),
                StepSay("gurgle", "The piers are quiet today, dry-lander. The fishermen pulled their nets up and left the channel clear."),
                StepSay("gold", "We pay our debts. These silver pieces came out of an old wreck off the bar. Spend them before your town burns down."),
                StepGive("money", 30),
                StepExit()
            ));

            // Official fleeing (fill 0.24 = 6 cells)
            actions.Add(MakeVisit(folder, "04_official_flee", ChapterWhen.FieldFill, 0.24f, "", 0f, "", 0f, _guests["official"],
                StepEnter("scorn"),
                StepSay("scorn", "The stokers shut off the current at the Ministry, and there's a crowd burning tar barrels at the corner. Nobody answers the telephone at headquarters."),
                StepSay("cold", "I have a seat on the six o'clock train to the border. You can stay here and paste clippings until the roof falls in."),
                StepSayRule("cold", "Here is the last twenty coins in my desk drawer. Municipal paper won't buy coffee across the border anyway.",
                    "official_loyal", 0f, next: 4),
                StepGiveRule("money", 20, "official_loyal", 0f, next: 5),
                StepExit()
            ));

            // Boss confession & bonus (fill 0.44 = 11 cells)
            actions.Add(MakeVisit(folder, "05_boss_confession", ChapterWhen.FieldFill, 0.44f, "", 0f, "", 0f, _guests["boss"],
                StepEnter("smirk"),
                StepSay("smirk", "Still pasting? The Commandant just tore up my press mortgage today. All I had to do last week was hand over your old Chief when he tried to print those quarry names."),
                StepSay("smirk", "Here's twenty coins. Keep the presses turning while I take my valise down to the station."),
                StepGive("money", 20),
                StepExit()
            ));

            // Anon unmasking (fill 0.64 = 16 cells)
            actions.Add(MakeVisit(folder, "06_anon_unmask", ChapterWhen.FieldFill, 0.64f, "", 0f, "", 0f, _guests["anon"],
                StepEnter("mask"),
                StepSay("mask", "Slide the bolt. Patrols are on the corner."),
                StepSay("hole", "Take a good look, kid."),
                StepSay("hole", "Recognize me now? Their pistol ball took the bridge of my nose off at the quarry, and the rain washed the lime out of my eyes. The Boss traded my neck to clear his press debts."),
                StepSay("hole", "This is the quarry execution roster with both their signatures at the bottom. Put it in the paper, or tuck it in your Save tray for tomorrow."),
                StepGive("execution-list", 1),
                StepChoose("hole", "Are you with me?",
                    "I'm with you, Chief.", 7,
                    "They'll shoot us both!", 8),
                StepSay("hole", "Good lad. I knew I taught you right. Keep that proof safe until the final run.", remember: "anon_trust", value: 1f, next: 9),
                StepSay("rage", "They'll shoot you anyway when the ink runs out, you fool!", next: 9),
                StepExit()
            ));

            AddIdleActions(folder, actions);
            actions.Add(MakeClose(folder, "99_close_day", ChapterWhen.PaperStamped));

            // 26 cells in morning-drop-8 (1.04) + 3 cells in guest-stash-8 = 29 cells (1.16), required = 0.88 (22 cells)
            return CreateChapter("Day8", "Day 8: Behind the Mask", 0.88f,
                new[] { (ArticleKind.Ad, ArticleKind.Politics) },
                actions.ToArray(),
                ("morning-drop-8", "Fill at least 88% of the page (22/25 cells). Tension is at the breaking point. Lock your office door.",
                    new[] { "blackout-city", "train-station-rush", "radio-dog-final", "candle-ad", "elephant-job", "funeral-ad", "usa-prohibition-pipes", "generator-curfew", "foreign-consulate-closed", "night-watch-roster" }),
                ("guest-stash-8", "Hand-delivered by guests.",
                    new[] { "execution-list" })
            );
        }

        private static ChapterDefinition BuildDay9()
        {
            var folder = EnsureActionDayFolder("Day9");
            var actions = new List<ChapterAction>();

            actions.Add(MakeEnding(folder, "01_early_ending", ChapterWhen.Context, 0f, "ending_reached", 1f, GetComics("Fired")));
            actions.Add(MakeEnding(folder, "01b_boat_ending", ChapterWhen.Context, 0f, "boat_ending", 1f, GetComics("Boat")));
            actions.Add(MakeCapsule(folder, "02_morning_drop", ChapterWhen.EnterWork, "morning-drop-9"));

            // Chekist pressure (15s)
            actions.Add(MakeVisit(folder, "03_chekist_pressure", ChapterWhen.AfterWorkSeconds, 15f, "", 0f, "", 0f, _guests["chekist"],
                StepEnter("anger"),
                StepSay("anger", "The Leader died at four this morning. Half the third regiment has locked itself in the barracks, and we are putting a machine-gun crew in your lobby tomorrow."),
                StepSay("calm", "At dawn I will bring you the Martial Law proclamation. Run it across the front page and this newspaper is yours. Print one line from the mutineers, and we shoot you against the paper rolls."),
                StepExit()
            ));

            // Bandit flee offer (fill 0.48 = 12 cells)
            actions.Add(MakeVisit(folder, "04_bandit_flee_offer", ChapterWhen.FieldFill, 0.48f, "", 0f, "", 0f, _guests["bandit"],
                StepEnter("smirk"),
                StepSay("smirk", "Smell that, four-eyes? They're burning tyres by the bridge, and armored cars just turned onto Market Street."),
                StepSay("business", "Our launch leaves the coal slip at midnight. One hundred and twenty coins buys the last seat across the bay. Are you coming, or staying to get shot over a printing press?"),
                StepChooseRule("business", "Got the 120 coins?",
                    "Here's the money. Let's go.", 5,
                    "I'm finishing tomorrow's paper.", 7,
                    "money", 119.5f),
                StepSay("cold", "You're short on cash, pal. Keep your head down tomorrow.", next: 8),
                StepGive("money", -120, next: 6),
                StepSay("smirk", "Smartest thing you've done all week. Leave the paste brush and come out the back way.", remember: "boat_ending", value: 1f, next: 8),
                StepSay("cold", "Your funeral, scribe. Don't say I didn't offer.", next: 8),
                StepExit()
            ));

            // Buddy eve talk (fill 0.68 = 17 cells)
            actions.Add(MakeVisit(folder, "05_buddy_eve_talk", ChapterWhen.FieldFill, 0.68f, "", 0f, "", 0f, _guests["buddy"],
                StepEnter("anxiety"),
                StepSay("anxiety", "The radio tower went dead an hour ago, and the telegraph wires are cut. Nobody in town knows what's happening outside their own street."),
                StepSay("handshake", "Our rotary press is the only thing left working in the whole district. Whatever comes through that door tomorrow morning, make it count."),
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

            actions.Add(MakeEnding(folder, "01_early_ending", ChapterWhen.Context, 0f, "ending_reached", 1f, GetComics("Fired")));
            actions.Add(MakeCapsule(folder, "02_morning_drop", ChapterWhen.EnterWork, "morning-drop-10"));

            // Anon final entry (10s)
            actions.Add(MakeVisit(folder, "03_anon_final_entry", ChapterWhen.AfterWorkSeconds, 10f, "", 0f, "", 0f, _guests["anon"],
                StepEnter("hole"),
                StepSay("hole", "The soldiers at the bridge are waiting to see what comes off our press before they pick a side."),
                StepSay("hole", "Here is my signed statement and the quarry ledger. Lay it across the top of the page. When the bundles hit the street, the garrison won't fire on the crowd."),
                StepGive("anon-truth", 1),
                StepSay("hole", "It's your page now, kid."),
                StepExit()
            ));

            // Chekist final entry (25s)
            actions.Add(MakeVisit(folder, "04_chekist_final_entry", ChapterWhen.AfterWorkSeconds, 25f, "", 0f, "", 0f, _guests["chekist"],
                StepEnter("anger"),
                StepSay("anger", "We have a Maxim gun set up across from your front door, Editor."),
                StepSay("calm", "Here is the Martial Law decree. Put it on the front page and the street crowds will scatter before noon. Do that, and the print shop is yours."),
                StepGive("order-curfew", 1),
                StepSay("cigar", "If I see that dead man's name in print, nobody walks out of this building."),
                StepExit()
            ));

            AddIdleActions(folder, actions);

            // Final epilogue visits depending on which 5x2 manifesto was printed (social > 6 for Anon, politics > 6 for Chekist)
            actions.Add(MakeVisit(folder, "96_finale_anon_victory", ChapterWhen.PaperStamped, 1f, "social", 6f, "", 0f, _guests["anon"],
                StepEnter("hole"),
                StepSay("hole", "Hear that outside? The machine-gun crew at the corner just unbolted their tripod and handed the papers out to the line!"),
                StepSay("hole", "They couldn't keep us under the lime, kid. Come on down to the press room, the boys are opening a bottle."),
                StepExit()
            ));

            actions.Add(MakeVisit(folder, "97_finale_chekist_order", ChapterWhen.PaperStamped, 1f, "politics", 6f, "", 0f, _guests["chekist"],
                StepEnter("cigar"),
                StepSay("cigar", "The streets cleared twenty minutes after the boys threw the bundles off the cart. And we caught your masked friend in the coal alley."),
                StepSay("calm", "Here is your permanent press warrant. Keep the ink ready; the Council will have fresh decrees for you on Monday."),
                StepExit()
            ));

            // Finish game EndingActions (WalkingDead for Anon manifesto, IronLaw for Chekist decree)
            actions.Add(MakeEnding(folder, "98_ending_anon_victory", ChapterWhen.PaperStamped, 1f, "social", 6f, GetComics("WalkingDead")));
            actions.Add(MakeEnding(folder, "98_ending_chekist_order", ChapterWhen.PaperStamped, 1f, "politics", 6f, GetComics("IronLaw")));
            actions.Add(MakeEnding(folder, "99_finish_game", ChapterWhen.PaperStamped, 1f, "", 0f, GetComics("WalkingDead")));

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
                StepSay("shout", "Are you asleep at the table?! The carts are waiting in the alley! Stamp that sheet in three minutes or I'll turn you over to the patrol myself!"),
                StepExit()
            ));

            actions.Add(MakeVisit(folder, "91_idle_fired", ChapterWhen.AfterWorkSeconds, 480f, "", 0f, "", 0f, _guests["boss"],
                StepEnter("shout"),
                StepSay("shout", "Too late! The morning train left without our bundles! Clear out your drawer and get off my stairs!", remember: "ending_reached", value: 1f),
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

        private static EndingAction MakeEnding(string folder, string name, ChapterWhen when, float at, string parameter, float minimum, StoryComicsData comics = null)
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
            if (comics != null)
                so.FindProperty("comics").objectReferenceValue = comics;
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

            var seqPlayer = UnityEngine.Object.FindAnyObjectByType<SequencePlayer>();
            if (seqPlayer != null)
            {
                var seqSo = new SerializedObject(seqPlayer);
                var introAsset = GetComics("Intro");
                if (introAsset != null)
                    seqSo.FindProperty("_introComics").objectReferenceValue = introAsset;
                var comicsPlayer = UnityEngine.Object.FindAnyObjectByType<StoryComicsPlayer>(FindObjectsInactive.Include);
                if (comicsPlayer != null)
                    seqSo.FindProperty("_storyComics").objectReferenceValue = comicsPlayer;
                seqSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(seqPlayer);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[StoryGenerator][Scene] Wired StoryBook in '{scene.path}' with {chapters.Length} chapters and finale='{(credits != null ? credits.name : "none")}'.");
        }

        private static void VerifyGeneratedStory(ChapterDefinition[] chapters)
        {
            var emptyArticles = 0;
            var missingArticles = 0;
            var overflowingArticles = 0;

            var shapePrefabs = new Dictionary<ArticleShape, string>
            {
                { ArticleShape.Dot, "Assets/_Project/Prefabs/Articles/ArticleDot.prefab" },
                { ArticleShape.Line2H, "Assets/_Project/Prefabs/Articles/ArticleLine2H.prefab" },
                { ArticleShape.Line2V, "Assets/_Project/Prefabs/Articles/ArticleLine2V.prefab" },
                { ArticleShape.Line3H, "Assets/_Project/Prefabs/Articles/ArticleLine3H.prefab" },
                { ArticleShape.Line3V, "Assets/_Project/Prefabs/Articles/ArticleLine3V.prefab" },
                { ArticleShape.Square, "Assets/_Project/Prefabs/Articles/ArticleSquare.prefab" },
                { ArticleShape.Banner5x2, "Assets/_Project/Prefabs/Articles/ArticleBanner5x2.prefab" }
            };

            var tempInstances = new Dictionary<ArticleShape, GameObject>();
            try
            {
                foreach (var kv in shapePrefabs)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(kv.Value);
                    if (prefab != null)
                        tempInstances[kv.Key] = UnityEngine.Object.Instantiate(prefab);
                }

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
                        continue;
                    }

                    if (tempInstances.TryGetValue(reloaded.Shape, out var instance) && instance != null)
                    {
                        var slot = reloaded.PictureLayout != null ? reloaded.PictureLayout.Slot : 1;
                        var faceName = slot == 3 ? "Face3" : slot == 2 ? "Face2" : "Face1";
                        var faceTransform = instance.transform.Find(faceName);
                        var face = faceTransform != null ? faceTransform.GetComponent<ArticleFace>() : null;
                        if (face != null)
                        {
                            face.gameObject.SetActive(true);
                            var testSprite = slot >= 2 ? (reloaded.Picture != null ? reloaded.Picture : _defaultArticleSprite) : null;
                            face.Apply(reloaded.Title, reloaded.Body, testSprite);
                            var texts = face.GetComponentsInChildren<TMPro.TMP_Text>(true);
                            foreach (var t in texts)
                            {
                                t.ForceMeshUpdate(true);
                                if (t.isTextOverflowing)
                                {
                                    overflowingArticles++;
                                    Debug.LogWarning($"[StoryGenerator][Verify][Overflow] Article '{id}' ({reloaded.Shape}, {faceName}) overflows {t.gameObject.name}! TextLen={t.text.Length}, FontSize={t.fontSize:F1} (min={t.fontSizeMin:F1}), Rect={t.rectTransform.rect.width:F1}x{t.rectTransform.rect.height:F1}");
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                foreach (var inst in tempInstances.Values)
                {
                    if (inst != null)
                        UnityEngine.Object.DestroyImmediate(inst);
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

            if (emptyArticles == 0 && missingArticles == 0 && brokenCapsuleRefs == 0 && overflowingArticles == 0)
                Debug.Log($"[StoryGenerator][Verify] SUCCESS: Verified all {_articlePaths.Count} articles on disk and {chapters.Length} chapters — 0 empty articles, 0 missing files, 0 broken capsule references, 0 overflowing texts!");
            else
                Debug.LogWarning($"[StoryGenerator][Verify] SUMMARY: emptyArticles={emptyArticles}, missingArticles={missingArticles}, brokenCapsuleRefs={brokenCapsuleRefs}, overflowingArticles={overflowingArticles}.");
        }
    }
}
