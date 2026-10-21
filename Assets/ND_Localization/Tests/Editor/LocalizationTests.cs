using System.Collections.Generic;
using System.Text.RegularExpressions;
using ND.Localization.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ND.Localization.Tests
{
    public class LocalizationTests
    {
        private const string PrefsKey = "nd.localization.tests.locale";

        private readonly List<Object> m_Track = new List<Object>();
        private LocalizationSettings m_Settings;
        private Locale m_En, m_Vi, m_ZhCN;
        private StringTable m_Strings;
        private ColorTable m_Colors;

        private T Track<T>(T obj) where T : Object
        {
            m_Track.Add(obj);
            return obj;
        }

        private Locale MakeLocale(string code, Locale fallback = null)
        {
            var locale = Track(ScriptableObject.CreateInstance<Locale>());
            locale.code = code;
            locale.name = code;
            locale.fallback = fallback;
            return locale;
        }

        [SetUp]
        public void SetUp()
        {
            LocalizationService.ResetStatics(); // also drops subscribers left by earlier tests
            PlayerPrefs.DeleteKey(PrefsKey);

            m_En = MakeLocale("en");
            m_Vi = MakeLocale("vi", m_En);
            m_ZhCN = MakeLocale("zh-CN");

            m_Strings = Track(ScriptableObject.CreateInstance<StringTable>());
            m_Strings.tableName = "UI";
            m_Colors = Track(ScriptableObject.CreateInstance<ColorTable>());
            m_Colors.tableName = "Colors";

            m_Settings = Track(ScriptableObject.CreateInstance<LocalizationSettings>());
            m_Settings.locales = new List<Locale> { m_En, m_Vi, m_ZhCN };
            m_Settings.defaultLocale = m_En;
            m_Settings.tables = new List<LocalizationTable> { m_Strings, m_Colors };
            m_Settings.playerPrefsKey = PrefsKey;
            m_Settings.startupSelectors = new List<IStartupLocaleSelector> { new PlayerPrefsLocaleSelector() };
        }

        [TearDown]
        public void TearDown()
        {
            LocalizationService.ResetStatics(); // also drops subscribers left by earlier tests
            PlayerPrefs.DeleteKey(PrefsKey);
            foreach (var o in m_Track) if (o != null) Object.DestroyImmediate(o);
            m_Track.Clear();
        }

        private long AddString(string key, params (string code, string text)[] values)
        {
            long id = m_Strings.AddEntry(key);
            foreach (var (code, text) in values) m_Strings.SetValue(id, code, text);
            return id;
        }

        // ---------- table ----------

        [Test]
        public void Table_RenameKeepsId_AndReferenceStillResolves()
        {
            long id = AddString("greet", ("en", "Hello"));
            var reference = new LocalizedString();
            reference.SetReference(m_Strings, "greet");
            LocalizationService.Initialize(m_Settings);

            Assert.IsTrue(m_Strings.RenameEntry(id, "greeting"));

            Assert.AreEqual(id, reference.EntryId);
            Assert.AreEqual("Hello", reference.Resolve());
            Assert.IsTrue(m_Strings.TryGetId("greeting", out long byNewKey));
            Assert.AreEqual(id, byNewKey);
            Assert.IsFalse(m_Strings.TryGetId("greet", out _));
        }

        [Test]
        public void Table_DuplicateKey_IsRejectedWithError()
        {
            AddString("dup");
            LogAssert.Expect(LogType.Error, new Regex("already exists"));
            Assert.AreEqual(LocalizationTable.InvalidId, m_Strings.AddEntry("dup"));
            Assert.AreEqual(1, m_Strings.Count);
        }

        [Test]
        public void Table_RenameOntoExistingKey_IsRejected()
        {
            AddString("a");
            long b = AddString("b");
            LogAssert.Expect(LogType.Error, new Regex("already used"));
            Assert.IsFalse(m_Strings.RenameEntry(b, "a"));
            Assert.IsTrue(m_Strings.TryGetKey(b, out string key));
            Assert.AreEqual("b", key);
        }

        [Test]
        public void Table_IdsAreNotReusedAfterRemove()
        {
            long first = AddString("one");
            m_Strings.RemoveEntry(first);
            long second = AddString("two");
            Assert.AreNotEqual(first, second, "A removed id must not be handed out again, or stale references would silently point at new text.");
        }

        // ---------- service + fallback ----------

        [Test]
        public void Fallback_WalksLocaleChain_ThenDefault()
        {
            long onlyEn = AddString("only_en", ("en", "EN text"));
            long both = AddString("both", ("en", "EN"), ("vi", "VI"));
            long zhMissing = AddString("zh_missing", ("en", "Default EN"));

            LocalizationService.Initialize(m_Settings);
            LocalizationService.SetLocale("vi");
            Assert.IsTrue(LocalizationService.TryGetValue(m_Strings, both, out string v1));
            Assert.AreEqual("VI", v1);
            Assert.IsTrue(LocalizationService.TryGetValue(m_Strings, onlyEn, out string v2));
            Assert.AreEqual("EN text", v2, "vi falls back to en through Locale.fallback");

            LocalizationService.SetLocale("zh-CN"); // no explicit fallback → default locale
            Assert.IsTrue(LocalizationService.TryGetValue(m_Strings, zhMissing, out string v3));
            Assert.AreEqual("Default EN", v3);
        }

        [Test]
        public void Fallback_Cycle_Terminates()
        {
            m_En.fallback = m_Vi; // en → vi → en …
            long id = AddString("nowhere", ("zh-CN", "only zh"));
            LocalizationService.Initialize(m_Settings);
            LocalizationService.SetLocale("vi");

            LogAssert.Expect(LogType.Warning, new Regex("Missing translation"));
            Assert.IsFalse(LocalizationService.TryGetValue(m_Strings, id, out string _));
        }

        [Test]
        public void NotReady_ReadIsRejectedWithState()
        {
            long id = AddString("x", ("en", "x"));
            LogAssert.Expect(LogType.Warning, new Regex("state=NotReady"));
            Assert.IsFalse(LocalizationService.TryGetValue(m_Strings, id, out string _));
        }

        [Test]
        public void SetLocale_NotInSettings_IsRejected()
        {
            LocalizationService.Initialize(m_Settings);
            var stranger = MakeLocale("fr");
            LogAssert.Expect(LogType.Warning, new Regex("not in"));
            Assert.IsFalse(LocalizationService.SetLocale(stranger));
            Assert.AreEqual(m_En, LocalizationService.SelectedLocale);
        }

        [Test]
        public void GetString_ByTableNameAndKey()
        {
            AddString("title", ("en", "Title"), ("vi", "Tiêu đề"));
            LocalizationService.Initialize(m_Settings);
            LocalizationService.SetLocale("vi");
            Assert.AreEqual("Tiêu đề", LocalizationService.GetString("UI", "title"));
        }

        // ---------- startup selection ----------

        [Test]
        public void Startup_RestoresPlayerChoice_FromPreviousSession()
        {
            LocalizationService.Initialize(m_Settings);
            LocalizationService.SetLocale("vi");
            LocalizationService.Shutdown();

            LocalizationService.Initialize(m_Settings);
            Assert.AreEqual(m_Vi, LocalizationService.SelectedLocale);
        }

        [Test]
        public void Startup_NoSelectorMatch_UsesDefault()
        {
            LocalizationService.Initialize(m_Settings);
            Assert.AreEqual(m_En, LocalizationService.SelectedLocale);
        }

        [Test]
        public void SystemLanguage_MatchesRegionCode_AndLanguageOnlyCode()
        {
            Assert.AreEqual(m_ZhCN, SystemLanguageLocaleSelector.SelectFor(m_Settings, SystemLanguage.ChineseSimplified));
            Assert.AreEqual(m_Vi, SystemLanguageLocaleSelector.SelectFor(m_Settings, SystemLanguage.Vietnamese));
            Assert.IsNull(SystemLanguageLocaleSelector.SelectFor(m_Settings, SystemLanguage.Korean));

            var zhOnly = MakeLocale("zh");
            m_Settings.locales = new List<Locale> { m_En, zhOnly };
            Assert.AreEqual(zhOnly, SystemLanguageLocaleSelector.SelectFor(m_Settings, SystemLanguage.ChineseSimplified));
        }

        // ---------- references react to locale changes ----------

        [Test]
        public void LocalizedString_SubscribedBeforeInit_ReceivesFirstLocale_ThenEveryChange()
        {
            AddString("hello", ("en", "Hello"), ("vi", "Xin chào"));
            var reference = new LocalizedString();
            reference.SetReference(m_Strings, "hello");

            var received = new List<string>();
            reference.Changed += received.Add; // before Initialize: must not fire yet
            Assert.AreEqual(0, received.Count);

            LocalizationService.Initialize(m_Settings);
            LocalizationService.SetLocale("vi");
            LocalizationService.SetLocale("vi"); // same locale: no event

            CollectionAssert.AreEqual(new[] { "Hello", "Xin chào" }, received);

            reference.Changed -= received.Add;
            LocalizationService.SetLocale("en");
            Assert.AreEqual(2, received.Count, "No callbacks after unsubscribe");
        }

        [Test]
        public void LocalizedString_SubscribedAfterInit_FiresImmediately_AndFormatsArguments()
        {
            AddString("gold", ("en", "{0} gold"), ("vi", "{0} vàng"));
            var reference = new LocalizedString();
            reference.SetReference(m_Strings, "gold");
            LocalizationService.Initialize(m_Settings);

            string last = null;
            reference.Changed += s => last = s;
            Assert.AreEqual("{0} gold", last);

            reference.Arguments = new object[] { 25 };
            Assert.AreEqual("25 gold", last);

            LocalizationService.SetLocale("vi");
            Assert.AreEqual("25 vàng", last);
        }

        [Test]
        public void TwoReferences_SameEntry_BothUpdate_AndUnsubscribingOneKeepsTheOther()
        {
            AddString("ok", ("en", "OK"), ("vi", "Đồng ý"));
            var a = new LocalizedString();
            var b = new LocalizedString();
            a.SetReference(m_Strings, "ok");
            b.SetReference(m_Strings, "ok");
            LocalizationService.Initialize(m_Settings);

            string lastA = null, lastB = null;
            System.Action<string> onA = s => lastA = s;
            a.Changed += onA;
            b.Changed += s => lastB = s;
            a.Changed -= onA;

            LocalizationService.SetLocale("vi");
            Assert.AreEqual("OK", lastA);
            Assert.AreEqual("Đồng ý", lastB);
        }

        [Test]
        public void LocalizedColor_DiffersPerRegion()
        {
            // Same UI element: red is fine in zh-CN (luck), neutral white elsewhere.
            long id = m_Colors.AddEntry("emotion.happy.tint");
            m_Colors.SetValue(id, "en", Color.white);
            m_Colors.SetValue(id, "zh-CN", Color.red);
            var tint = new LocalizedColor();
            tint.SetReference(m_Colors, id);
            LocalizationService.Initialize(m_Settings);

            Color last = Color.clear;
            tint.Changed += c => last = c;
            Assert.AreEqual(Color.white, last);
            LocalizationService.SetLocale("zh-CN");
            Assert.AreEqual(Color.red, last);
            LocalizationService.SetLocale("vi"); // vi → en
            Assert.AreEqual(Color.white, last);
        }

        [Test]
        public void LocalizedAsset_WrongType_IsReportedNotCast()
        {
            var assets = Track(ScriptableObject.CreateInstance<AssetTable>());
            m_Settings.tables.Add(assets);
            long id = assets.AddEntry("portrait");
            assets.SetValue(id, "en", m_Vi); // a Locale asset where a Sprite is expected
            var sprite = new LocalizedSprite();
            sprite.SetReference(assets, id);
            LocalizationService.Initialize(m_Settings);

            LogAssert.Expect(LogType.Error, new Regex("expected Sprite"));
            Assert.IsFalse(sprite.TryGetValue(out Sprite _));
        }

        [Test]
        public void LocalizeStringEvent_UpdatesOnEnable_AndStopsWhenDisabled()
        {
            AddString("menu", ("en", "Menu"), ("vi", "Thực đơn"));
            LocalizationService.Initialize(m_Settings);

            var go = Track(new GameObject("label"));
            go.SetActive(false);
            var component = go.AddComponent<LocalizeStringEvent>();
            component.StringReference.SetReference(m_Strings, "menu");
            string shown = null;
            component.OnUpdate.AddListener(s => shown = s);

            go.SetActive(true);
            Assert.AreEqual("Menu", shown);
            LocalizationService.SetLocale("vi");
            Assert.AreEqual("Thực đơn", shown);

            go.SetActive(false);
            LocalizationService.SetLocale("en");
            Assert.AreEqual("Thực đơn", shown, "Disabled component must not receive updates");
            go.SetActive(true);
            Assert.AreEqual("Menu", shown, "Re-enabled component catches up immediately");
        }

        // ---------- LocalizableString (plain first, table later) ----------

        [Test]
        public void LocalizableString_IsPlainUntilConnected_ThenFollowsTheLocale()
        {
            var text = new LocalizableString { Text = "{0} gold" };
            text.Arguments = new object[] { 5 };
            Assert.IsFalse(text.IsLocalized);
            Assert.IsTrue(text.HasContent);
            Assert.AreEqual("5 gold", text.GetText(), "plain text works with no table and no initialized localization");

            long id = AddString("gold", ("en", "{0} gold"), ("vi", "{0} vàng"));
            text.SetReference(m_Strings, id);
            LocalizationService.Initialize(m_Settings);
            LocalizationService.SetLocale("vi");
            Assert.AreEqual("5 vàng", text.GetText());

            text.Unlink("{0} coins");
            Assert.IsFalse(text.IsLocalized);
            Assert.AreEqual("5 coins", text.GetText());
            Assert.IsTrue(m_Strings.Contains(id), "unlinking leaves the table entry in place");
        }

        // ---------- CSV ----------

        [Test]
        public void Csv_RoundTrip_PreservesIdsAndSpecialCharacters()
        {
            long id = AddString("quote", ("en", "He said \"hi\", then left.\nNew line"), ("vi", " leading space"));
            AddString("plain", ("en", "plain"));
            string csv = LocalizationCsv.Export(m_Strings, new[] { "en", "vi" });

            var copy = Track(ScriptableObject.CreateInstance<StringTable>());
            var report = LocalizationCsv.Import(copy, csv, clearEmptyCells: true);

            Assert.AreEqual(0, report.errors.Count, string.Join("\n", report.errors));
            Assert.AreEqual(2, report.added);
            Assert.IsTrue(copy.TryGetValue(id, "en", out string en));
            Assert.AreEqual("He said \"hi\", then left.\nNew line", en);
            Assert.IsTrue(copy.TryGetValue(id, "vi", out string vi));
            Assert.AreEqual(" leading space", vi);
            Assert.IsFalse(copy.HasValue(copy.Entries[1].id, "vi"), "empty cell stays empty");
        }

        [Test]
        public void Csv_RenamedKeyInFile_RenamesEntry_KeepingReferences()
        {
            long id = AddString("old_key", ("en", "Text"));
            var reference = new LocalizedString();
            reference.SetReference(m_Strings, id);

            string csv = $"id,key,en\r\n{id},new_key,Text v2\r\n";
            var report = LocalizationCsv.Import(m_Strings, csv, clearEmptyCells: false);

            Assert.AreEqual(1, report.renamed);
            Assert.AreEqual(0, report.added);
            LocalizationService.Initialize(m_Settings);
            Assert.AreEqual("Text v2", reference.Resolve());
        }

        [Test]
        public void Csv_EmptyCell_ClearsOrKeeps_ByOption()
        {
            long id = AddString("k", ("en", "EN"), ("vi", "VI"));
            const string csv = "key,en,vi\r\nk,EN,\r\n";

            LocalizationCsv.Import(m_Strings, csv, clearEmptyCells: false);
            Assert.IsTrue(m_Strings.HasValue(id, "vi"));

            LocalizationCsv.Import(m_Strings, csv, clearEmptyCells: true);
            Assert.IsFalse(m_Strings.HasValue(id, "vi"));
        }

        [Test]
        public void Csv_MissingKeyColumn_ReportsError_AndChangesNothing()
        {
            AddString("k", ("en", "EN"));
            var report = LocalizationCsv.Import(m_Strings, "name,en\r\nk,changed\r\n", clearEmptyCells: true);
            Assert.AreEqual(1, report.errors.Count);
            Assert.IsTrue(m_Strings.TryGetValue(m_Strings.Entries[0].id, "en", out string en));
            Assert.AreEqual("EN", en);
        }
    }
}
