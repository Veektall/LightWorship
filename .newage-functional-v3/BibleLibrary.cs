using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using NewAgeWorship.Core.Services;

namespace NewAgeWorship.Desktop
{
    public sealed class BiblePassage
    {
        public string Reference { get; set; }
        public string RequestedReference { get; set; }
        public string Text { get; set; }
        public string Translation { get; set; }
        public string Licence { get; set; }
        public string SourceVerseKey { get; set; }
        public override string ToString() => Reference + " • " + Translation + " — " + Text;
    }

    public sealed class BibleTranslationInfo
    {
        public string Code { get; set; }
        public string DisplayName { get; set; }
        public string Licence { get; set; }
        public override string ToString() => Code;
    }

    internal sealed class BibleJsonDocument
    {
        public string version { get; set; }
        public Dictionary<string, Dictionary<string, Dictionary<string,string>>> books { get; set; }
    }

    public sealed class BibleLibrary : IDisposable
    {
        private readonly SQLiteConnection _db;
        private readonly List<BibleTranslationInfo> _translations = new List<BibleTranslationInfo>();
        public IList<BibleTranslationInfo> Translations => _translations.AsReadOnly();

        public BibleLibrary(string appDataDirectory, string bibleZipPath)
        {
            if(!File.Exists(bibleZipPath)) throw new FileNotFoundException("Bible translation pack is missing.",bibleZipPath);
            Directory.CreateDirectory(appDataDirectory);
            var dbPath=Path.Combine(appDataDirectory,"bible-library-v2.sqlite");
            _db=new SQLiteConnection("Data Source="+dbPath+";Version=3;");
            _db.Open();
            var hash=HashFile(bibleZipPath);
            EnsureSchema();
            if(!string.Equals(ReadMeta("source_hash"),hash,StringComparison.OrdinalIgnoreCase) || TranslationCount()<1)
                Import(bibleZipPath,hash);
            LoadTranslations();
            if(_translations.Count==0) throw new InvalidDataException("The Bible pack did not contain any usable translations.");
        }

        public bool TryGetPassage(ScriptureReference reference,string translation,out BiblePassage passage)
        {
            passage=null;
            if(reference==null || string.IsNullOrWhiteSpace(translation)) return false;
            var book=CanonicalBook(reference.Book);
            var requestedEnd=reference.VerseEnd ?? reference.VerseStart;
            using(var cmd=_db.CreateCommand())
            {
                cmd.CommandText="SELECT verse_start,verse_end,verse_key,text FROM verses WHERE version=@ver AND book=@book AND chapter=@ch AND verse_end>=@s AND verse_start<=@e ORDER BY verse_start,verse_end,rowid";
                cmd.Parameters.AddWithValue("@ver",translation);
                cmd.Parameters.AddWithValue("@book",book);
                cmd.Parameters.AddWithValue("@ch",reference.Chapter);
                cmd.Parameters.AddWithValue("@s",reference.VerseStart);
                cmd.Parameters.AddWithValue("@e",requestedEnd);
                using(var r=cmd.ExecuteReader())
                {
                    var parts=new List<string>(); var keys=new List<string>(); int actualStart=int.MaxValue,actualEnd=0;
                    while(r.Read())
                    {
                        var start=r.GetInt32(0); var end=r.GetInt32(1); var key=r.GetString(2); var text=r.GetString(3).Trim();
                        actualStart=Math.Min(actualStart,start); actualEnd=Math.Max(actualEnd,end); keys.Add(key);
                        parts.Add((reference.VerseEnd.HasValue || start!=end ? key+". " : "")+text);
                    }
                    if(parts.Count==0) return false;
                    var actual=book+" "+reference.Chapter+":"+actualStart+(actualEnd!=actualStart?"-"+actualEnd:"");
                    passage=new BiblePassage
                    {
                        RequestedReference=reference.ToString(),Reference=actual,Text=string.Join(" ",parts),Translation=translation,
                        Licence="User-supplied translation pack — verify redistribution rights before public release",
                        SourceVerseKey=string.Join(",",keys.Distinct())
                    };
                    return true;
                }
            }
        }

        public IList<BiblePassage> Search(string query,string translation,int limit=5)
        {
            var results=new List<BiblePassage>();
            if(string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(translation)) return results;
            var tokens=Regex.Matches(query.ToLowerInvariant(),@"[a-z0-9']+").Cast<Match>().Select(m=>m.Value).Where(x=>x.Length>1).Take(8).ToArray();
            if(tokens.Length==0) return results;
            var match=string.Join(" AND ",tokens.Select(t=>"text:"+t.Replace("\"","")));
            try
            {
                using(var cmd=_db.CreateCommand())
                {
                    cmd.CommandText="SELECT book,chapter,verse_key,text FROM verse_fts WHERE version=@ver AND verse_fts MATCH @q LIMIT @n";
                    cmd.Parameters.AddWithValue("@ver",translation); cmd.Parameters.AddWithValue("@q",match); cmd.Parameters.AddWithValue("@n",limit);
                    using(var r=cmd.ExecuteReader()) while(r.Read())
                    {
                        var book=r.GetString(0); var chapter=r.GetInt32(1); var key=r.GetString(2);
                        results.Add(new BiblePassage{Reference=book+" "+chapter+":"+key,RequestedReference=book+" "+chapter+":"+key,Text=r.GetString(3).Trim(),Translation=translation,Licence="User-supplied translation pack — verify redistribution rights before public release",SourceVerseKey=key});
                    }
                }
            }
            catch(SQLiteException)
            {
                using(var cmd=_db.CreateCommand())
                {
                    cmd.CommandText="SELECT book,chapter,verse_key,text FROM verses WHERE version=@ver AND text LIKE @q LIMIT @n";
                    cmd.Parameters.AddWithValue("@ver",translation); cmd.Parameters.AddWithValue("@q","%"+query+"%"); cmd.Parameters.AddWithValue("@n",limit);
                    using(var r=cmd.ExecuteReader()) while(r.Read())
                    {
                        var book=r.GetString(0); var chapter=r.GetInt32(1); var key=r.GetString(2);
                        results.Add(new BiblePassage{Reference=book+" "+chapter+":"+key,RequestedReference=book+" "+chapter+":"+key,Text=r.GetString(3).Trim(),Translation=translation,Licence="User-supplied translation pack — verify redistribution rights before public release",SourceVerseKey=key});
                    }
                }
            }
            return results;
        }

        public BibleTranslationInfo GetTranslation(string code)=>_translations.FirstOrDefault(x=>string.Equals(x.Code,code,StringComparison.OrdinalIgnoreCase));

        private void EnsureSchema()
        {
            using(var c=_db.CreateCommand())
            {
                c.CommandText="CREATE TABLE IF NOT EXISTS meta(key TEXT PRIMARY KEY,value TEXT); CREATE TABLE IF NOT EXISTS translations(version TEXT PRIMARY KEY,display_name TEXT NOT NULL,licence TEXT NOT NULL);";
                c.ExecuteNonQuery();
            }
        }

        private int TranslationCount(){try{using(var c=_db.CreateCommand()){c.CommandText="SELECT COUNT(*) FROM translations";return Convert.ToInt32(c.ExecuteScalar(),CultureInfo.InvariantCulture);}}catch{return 0;}}
        private string ReadMeta(string key){try{using(var c=_db.CreateCommand()){c.CommandText="SELECT value FROM meta WHERE key=@k";c.Parameters.AddWithValue("@k",key);return Convert.ToString(c.ExecuteScalar(),CultureInfo.InvariantCulture);}}catch{return null;}}

        private void Import(string zipPath,string sourceHash)
        {
            using(var tx=_db.BeginTransaction())
            {
                using(var c=_db.CreateCommand())
                {
                    c.Transaction=tx;
                    c.CommandText="DROP TABLE IF EXISTS verses; DROP TABLE IF EXISTS verse_fts; DELETE FROM translations; DELETE FROM meta; CREATE TABLE verses(version TEXT NOT NULL,book TEXT NOT NULL,chapter INTEGER NOT NULL,verse_start INTEGER NOT NULL,verse_end INTEGER NOT NULL,verse_key TEXT NOT NULL,text TEXT NOT NULL); CREATE INDEX ix_verses_ref ON verses(version,book,chapter,verse_start,verse_end); CREATE VIRTUAL TABLE verse_fts USING fts4(version,book,chapter,verse_start,verse_end,verse_key,text);";
                    c.ExecuteNonQuery();
                }
                using(var insert=_db.CreateCommand())
                using(var fts=_db.CreateCommand())
                {
                    insert.Transaction=tx; fts.Transaction=tx;
                    insert.CommandText="INSERT INTO verses(version,book,chapter,verse_start,verse_end,verse_key,text) VALUES(@ver,@b,@ch,@s,@e,@k,@t)";
                    fts.CommandText="INSERT INTO verse_fts(version,book,chapter,verse_start,verse_end,verse_key,text) VALUES(@ver,@b,@ch,@s,@e,@k,@t)";
                    AddVerseParameters(insert); AddVerseParameters(fts);
                    using(var zip=ZipFile.OpenRead(zipPath))
                    {
                        foreach(var entry in zip.Entries.Where(e=>e.Name.EndsWith(".json",StringComparison.OrdinalIgnoreCase)).OrderBy(e=>e.Name,StringComparer.OrdinalIgnoreCase))
                        {
                            BibleJsonDocument doc;
                            using(var sr=new StreamReader(entry.Open(),Encoding.UTF8,true))
                            {
                                var json=sr.ReadToEnd();
                                var js=new JavaScriptSerializer{MaxJsonLength=int.MaxValue,RecursionLimit=2048};
                                doc=js.Deserialize<BibleJsonDocument>(json);
                            }
                            if(doc==null || doc.books==null || doc.books.Count<66) throw new InvalidDataException(entry.Name+" does not contain a complete 66-book Bible.");
                            var version=string.IsNullOrWhiteSpace(doc.version)?Path.GetFileNameWithoutExtension(entry.Name):doc.version.Trim();
                            InsertTranslation(tx,version);
                            foreach(var bookEntry in doc.books)
                            {
                                var book=CanonicalBook(bookEntry.Key);
                                foreach(var chapterEntry in bookEntry.Value)
                                {
                                    int chapter; if(!int.TryParse(chapterEntry.Key,NumberStyles.None,CultureInfo.InvariantCulture,out chapter)) continue;
                                    foreach(var verseEntry in chapterEntry.Value)
                                    {
                                        int verseStart,verseEnd; if(!ParseVerseKey(verseEntry.Key,out verseStart,out verseEnd)) continue;
                                        SetVerseParameters(insert,version,book,chapter,verseStart,verseEnd,verseEntry.Key,verseEntry.Value??""); insert.ExecuteNonQuery();
                                        SetVerseParameters(fts,version,book,chapter,verseStart,verseEnd,verseEntry.Key,verseEntry.Value??""); fts.ExecuteNonQuery();
                                    }
                                }
                            }
                        }
                    }
                }
                using(var meta=_db.CreateCommand()){meta.Transaction=tx;meta.CommandText="INSERT OR REPLACE INTO meta(key,value) VALUES('source_hash',@v)";meta.Parameters.AddWithValue("@v",sourceHash);meta.ExecuteNonQuery();}
                tx.Commit();
            }
        }

        private static void AddVerseParameters(SQLiteCommand cmd)
        {
            cmd.Parameters.Add("@ver",System.Data.DbType.String);cmd.Parameters.Add("@b",System.Data.DbType.String);cmd.Parameters.Add("@ch",System.Data.DbType.Int32);cmd.Parameters.Add("@s",System.Data.DbType.Int32);cmd.Parameters.Add("@e",System.Data.DbType.Int32);cmd.Parameters.Add("@k",System.Data.DbType.String);cmd.Parameters.Add("@t",System.Data.DbType.String);
        }
        private static void SetVerseParameters(SQLiteCommand cmd,string version,string book,int chapter,int start,int end,string key,string text)
        {
            cmd.Parameters["@ver"].Value=version;cmd.Parameters["@b"].Value=book;cmd.Parameters["@ch"].Value=chapter;cmd.Parameters["@s"].Value=start;cmd.Parameters["@e"].Value=end;cmd.Parameters["@k"].Value=key;cmd.Parameters["@t"].Value=(text??"").Trim();
        }

        private void InsertTranslation(SQLiteTransaction tx,string version)
        {
            using(var c=_db.CreateCommand()){c.Transaction=tx;c.CommandText="INSERT OR REPLACE INTO translations(version,display_name,licence) VALUES(@v,@d,@l)";c.Parameters.AddWithValue("@v",version);c.Parameters.AddWithValue("@d",version);c.Parameters.AddWithValue("@l","User-supplied translation pack — verify redistribution rights before public release");c.ExecuteNonQuery();}
        }

        private void LoadTranslations()
        {
            _translations.Clear();
            using(var c=_db.CreateCommand()){c.CommandText="SELECT version,display_name,licence FROM translations ORDER BY version";using(var r=c.ExecuteReader())while(r.Read())_translations.Add(new BibleTranslationInfo{Code=r.GetString(0),DisplayName=r.GetString(1),Licence=r.GetString(2)});}
        }

        private static bool ParseVerseKey(string key,out int start,out int end)
        {
            start=end=0;if(string.IsNullOrWhiteSpace(key))return false;var m=Regex.Match(key.Trim(),@"^(?<s>\d+)(?:-(?<e>\d+))?$");if(!m.Success)return false;
            if(!int.TryParse(m.Groups["s"].Value,NumberStyles.None,CultureInfo.InvariantCulture,out start))return false;end=start;
            if(m.Groups["e"].Success&&!int.TryParse(m.Groups["e"].Value,NumberStyles.None,CultureInfo.InvariantCulture,out end))return false;return start>0&&end>=start;
        }
        private static string CanonicalBook(string book)
        {
            var s=(book??"").Replace('_',' ').Trim();if(string.Equals(s,"Psalm",StringComparison.OrdinalIgnoreCase))return "Psalms";return s;
        }
        private static string HashFile(string path){using(var sha=SHA256.Create())using(var s=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
        public void Dispose(){_db.Dispose();}
    }
}
