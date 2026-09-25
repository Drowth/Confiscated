using System;
using System.Linq;
using System.Text.RegularExpressions;
namespace Confiscated
{
    public static class DetentionLineMatcher
    {
        public const string RequiredLine="Make good choices";
        public const int RequiredCount=3;
        public const float MinimumScore=.8f;
        public static string Normalize(string value)
        {
            value=(value??"").ToLowerInvariant().Replace('’','\'');
            value=Regex.Replace(value,@"\bi['’]?ll\b","i will");
            value=Regex.Replace(value,@"\b(tmr|tmrw|tmrrow|tomoz|2moro|2morrow)\b","tomorrow");
            value=Regex.Replace(value,@"\b(2|two)\b","to");
            value=Regex.Replace(value,@"\bb\b","be");
            value=Regex.Replace(value,@"\b(bttr|btr)\b","better");
            value=Regex.Replace(value,@"\b(gd|gud)\b","good");
            value=Regex.Replace(value,@"\b(chcs|chces)\b","choices");
            value=Regex.Replace(value,@"\bprom\b","promise");
            return Regex.Replace(Regex.Replace(value,@"[^\p{L}\p{N}\s]",""),@"\s+"," ").Trim();
        }
        public static float Score(string line)
        {
            string a=Normalize(RequiredLine),b=Normalize(line);
            if(b.Length==0||b.Length>a.Length*2)return 0;
            int[] previous=Enumerable.Range(0,b.Length+1).ToArray(),current=new int[b.Length+1];
            for(int i=1;i<=a.Length;i++)
            {
                current[0]=i;
                for(int j=1;j<=b.Length;j++)current[j]=Math.Min(Math.Min(previous[j]+1,current[j-1]+1),previous[j-1]+(a[i-1]==b[j-1]?0:1));
                var swap=previous;previous=current;current=swap;
            }
            return 1f-(float)previous[b.Length]/Math.Max(a.Length,b.Length);
        }
        public static int CountValid(string text)=> (text??"").Replace("\r","").Split('\n').Count(line=>Score(line)+.00001f>=MinimumScore);
    }
}
