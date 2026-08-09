using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace NewAgeWorship.Core.Services
{
    public sealed class ScriptureReference
    {
        public string Book { get; set; }
        public int Chapter { get; set; }
        public int VerseStart { get; set; }
        public int? VerseEnd { get; set; }
        public override string ToString() => Book + " " + Chapter + ":" + VerseStart + (VerseEnd.HasValue && VerseEnd.Value != VerseStart ? "-" + VerseEnd.Value : "");
    }

    public sealed class ScriptureReferenceParser
    {
        private static readonly string[] Books = {
            "Genesis","Exodus","Leviticus","Numbers","Deuteronomy","Joshua","Judges","Ruth","1 Samuel","2 Samuel","1 Kings","2 Kings","1 Chronicles","2 Chronicles","Ezra","Nehemiah","Esther","Job","Psalms","Proverbs","Ecclesiastes","Song of Solomon","Isaiah","Jeremiah","Lamentations","Ezekiel","Daniel","Hosea","Joel","Amos","Obadiah","Jonah","Micah","Nahum","Habakkuk","Zephaniah","Haggai","Zechariah","Malachi","Matthew","Mark","Luke","John","Acts","Romans","1 Corinthians","2 Corinthians","Galatians","Ephesians","Philippians","Colossians","1 Thessalonians","2 Thessalonians","1 Timothy","2 Timothy","Titus","Philemon","Hebrews","James","1 Peter","2 Peter","1 John","2 John","3 John","Jude","Revelation"
        };
        private readonly Dictionary<string,string> _aliases;
        private static readonly Dictionary<string,int> Small = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase){{"zero",0},{"one",1},{"two",2},{"three",3},{"four",4},{"five",5},{"six",6},{"seven",7},{"eight",8},{"nine",9},{"ten",10},{"eleven",11},{"twelve",12},{"thirteen",13},{"fourteen",14},{"fifteen",15},{"sixteen",16},{"seventeen",17},{"eighteen",18},{"nineteen",19},{"twenty",20},{"thirty",30},{"forty",40},{"fifty",50},{"sixty",60},{"seventy",70},{"eighty",80},{"ninety",90}};

        public ScriptureReferenceParser()
        {
            _aliases = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach(var b in Books) _aliases[Normalize(b)] = b;
            Add("psalm","Psalms"); Add("ps","Psalms"); Add("rev","Revelation"); Add("revelations","Revelation"); Add("jn","John"); Add("johns gospel","John"); Add("rom","Romans");
            Add("first samuel","1 Samuel"); Add("second samuel","2 Samuel"); Add("first kings","1 Kings"); Add("second kings","2 Kings"); Add("first chronicles","1 Chronicles"); Add("second chronicles","2 Chronicles");
            Add("first corinthians","1 Corinthians"); Add("second corinthians","2 Corinthians"); Add("first thessalonians","1 Thessalonians"); Add("second thessalonians","2 Thessalonians"); Add("first timothy","1 Timothy"); Add("second timothy","2 Timothy"); Add("first peter","1 Peter"); Add("second peter","2 Peter"); Add("first john","1 John"); Add("second john","2 John"); Add("third john","3 John");
        }

        public bool TryParse(string input,out ScriptureReference result)
        {
            result=null; if(string.IsNullOrWhiteSpace(input))return false;
            var s=NormalizeInput(input); string book,matched; if(!TryBook(s,out book,out matched))return false;
            var tail=s.Substring(matched.Length).Trim();
            var m=Regex.Match(tail,@"^(?<c>\d+)\s*(?:[:\s]\s*(?<v>\d+)(?:\s*-\s*(?<e>\d+))?)?$",RegexOptions.IgnoreCase);
            if(!m.Success||!m.Groups["v"].Success)return false;
            int c,v,e;if(!int.TryParse(m.Groups["c"].Value,NumberStyles.None,CultureInfo.InvariantCulture,out c)||c<=0)return false;if(!int.TryParse(m.Groups["v"].Value,NumberStyles.None,CultureInfo.InvariantCulture,out v)||v<=0)return false;
            int? end=null;if(m.Groups["e"].Success){if(!int.TryParse(m.Groups["e"].Value,out e)||e<v)return false;end=e;}
            result=new ScriptureReference{Book=book,Chapter=c,VerseStart=v,VerseEnd=end};return true;
        }

        public bool TryParseSpoken(string input,out ScriptureReference result)
        {
            if(TryParse(input,out result))return true; result=null;if(string.IsNullOrWhiteSpace(input))return false;
            var s=NormalizeInput(input); string book,matched;if(!TryBook(s,out book,out matched))return false;var tail=s.Substring(matched.Length).Trim();
            tail=Regex.Replace(tail,@"\bchapter\b"," chapter ",RegexOptions.IgnoreCase);tail=Regex.Replace(tail,@"\bverses?\b"," verse ",RegexOptions.IgnoreCase);tail=Regex.Replace(tail,@"\bthrough\b|\bto\b"," to ",RegexOptions.IgnoreCase);tail=Normalize(tail);
            int chapter,verse,end;
            var cm=Regex.Match(tail,@"^(?<c>.+?)\s+chapter\s+(?<cc>.+?)\s+verse\s+(?<v>.+?)(?:\s+to\s+(?<e>.+))?$",RegexOptions.IgnoreCase);
            if(cm.Success && TryNumber(cm.Groups["cc"].Value,out chapter) && TryNumber(cm.Groups["v"].Value,out verse)){int? ve=null;if(cm.Groups["e"].Success&&TryNumber(cm.Groups["e"].Value,out end))ve=end;result=new ScriptureReference{Book=book,Chapter=chapter,VerseStart=verse,VerseEnd=ve};return Valid(result);}
            var vm=Regex.Match(tail,@"^(?<c>.+?)\s+verse\s+(?<v>.+?)(?:\s+to\s+(?<e>.+))?$",RegexOptions.IgnoreCase);
            if(vm.Success && TryNumber(vm.Groups["c"].Value,out chapter) && TryNumber(vm.Groups["v"].Value,out verse)){int? ve=null;if(vm.Groups["e"].Success&&TryNumber(vm.Groups["e"].Value,out end))ve=end;result=new ScriptureReference{Book=book,Chapter=chapter,VerseStart=verse,VerseEnd=ve};return Valid(result);}
            var words=tail.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
            if(words.Length>=2)
            {
                for(int split=1;split<words.Length;split++)
                {
                    if(TryNumber(string.Join(" ",words.Take(split)),out chapter)&&TryNumber(string.Join(" ",words.Skip(split)),out verse))
                    {var candidate=new ScriptureReference{Book=book,Chapter=chapter,VerseStart=verse};if(Valid(candidate)){result=candidate;return true;}}
                }
            }
            return false;
        }

        private bool TryBook(string s,out string book,out string matched){book=null;matched=null;foreach(var kv in _aliases.OrderByDescending(k=>k.Key.Length)){if(s.StartsWith(kv.Key+" ",StringComparison.OrdinalIgnoreCase)||s==kv.Key){book=kv.Value;matched=kv.Key;return true;}}return false;}
        private static bool TryNumber(string phrase,out int value){value=0;phrase=Normalize(phrase).Replace("-"," ");int direct;if(int.TryParse(phrase,out direct)){value=direct;return direct>0;}var tokens=phrase.Split(' ');int total=0,current=0;foreach(var t in tokens){int n;if(Small.TryGetValue(t,out n)){current+=n;continue;}if(t=="hundred"){current=Math.Max(1,current)*100;continue;}if(t=="and")continue;return false;}total+=current;value=total;return value>0;}
        private static bool Valid(ScriptureReference r)=>r!=null&&r.Chapter>0&&r.Chapter<=150&&r.VerseStart>0&&r.VerseStart<=176&&(!r.VerseEnd.HasValue||r.VerseEnd.Value>=r.VerseStart);
        private static string NormalizeInput(string input){var s=input.ToLowerInvariant();s=Regex.Replace(s,@"\bbook of\b"," ");s=Regex.Replace(s,@"\bfirst\b","1");s=Regex.Replace(s,@"\bsecond\b","2");s=Regex.Replace(s,@"\bthird\b","3");s=s.Replace(","," ").Replace("."," ");return Normalize(s);}
        private void Add(string alias,string canonical){_aliases[Normalize(alias)]=canonical;}
        private static string Normalize(string s)=>Regex.Replace((s??"").ToLowerInvariant().Trim(),@"\s+"," ");
    }
}
