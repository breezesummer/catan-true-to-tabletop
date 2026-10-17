using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

// 颜色 ID 保持稳定；显示姓名和掷骰产生的行动顺序分别处理。
public static class PlayerIdentityModel
{
    public static readonly string[] ColorNames={"红色","蓝色","橙色","米白色"};
    public static readonly string[] AiNames={"哈基米","吃白饭的大肥鲸","糖包","我是奶龙"};
    public const int MaximumNameLength=20;
    public static string NormalizeName(string value)
    {
        var name=new string((value??"").Where(c=>!char.IsControl(c)).ToArray()).Trim();
        if(name.Length==0)return "玩家";
        return name.Length>MaximumNameLength?name.Substring(0,MaximumNameLength):name;
    }
    public static string ColorLabel(string id)
    {
        int number;
        return id!=null&&id.Length==2&&id[0]=='P'&&int.TryParse(id.Substring(1),out number)&&number>=1&&number<=4?ColorNames[number-1]:id??"玩家";
    }
    public static string[] ActionOrder(string first,IEnumerable<string> ids)
    {
        var colors=(ids??Array.Empty<string>()).Distinct().OrderBy(id=>id,StringComparer.Ordinal).ToArray();
        int start=Array.IndexOf(colors,first);if(start<0)start=0;
        return Enumerable.Range(0,colors.Length).Select(i=>colors[(start+i)%colors.Length]).ToArray();
    }
    public static string[] CreateNames(int count,string human,string humanName,int seed)
    {
        var random=new Random(seed);var available=AiNames.ToList();
        var names=new string[count];
        for(int i=0;i<count;i++)
        {
            if("P"+(i+1)==human){names[i]=NormalizeName(humanName);continue;}
            int next=random.Next(available.Count);names[i]=available[next];available.RemoveAt(next);
        }
        return names;
    }
    public static string ReplaceIds(string text,Func<string,string> display)
        =>Regex.Replace(text??"",@"(?<![A-Za-z0-9])P[1-4](?![A-Za-z0-9])",match=>display(match.Value));
}
