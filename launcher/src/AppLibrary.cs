using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

public sealed class LibraryApp
{
 public string Name {get;set;}
 public string Id {get;set;}
 public string Asset {get;set;}
 public string Description {get;set;}
 public string Fit {get;set;}="cover";
 public string Background {get;set;}="#FFFFFF";
 public int Width {get;set;}
 public int Height {get;set;}
 public float[] Crop {get;set;}
 public bool Utility {get;set;}
 // Samsung's app-service identity; set from the TV, never from the catalog.
 [JsonIgnore] public string StoreId {get;set;}="";
 [JsonIgnore] public bool Removable {get;set;}
}

// One app as the TV's application manager reports it.
public sealed class InstalledApp
{
 public string Id {get;set;}="";
 public string Label {get;set;}="";
 public string IconPath {get;set;}="";
 public string Type {get;set;}="";
 public bool NoDisplay {get;set;}
 public string StoreId {get;set;}="";
 public bool Removable {get;set;}
}

public static class AppLibrary
{
 public const int Columns=8, Rows=4, PageSize=Columns*Rows;
 // Samsung's preinstalled media apps that the owner treats as their own. Every
 // other system app (settings panels, OOBE, ads, art templates) stays hidden.
 static readonly HashSet<string> MediaApps=new HashSet<string>{
  "org.tizen.netflix-app","org.tizen.primevideo","com.samsung.tv.aria-video",
  "org.tizen.apple.apple-music","org.tizen.browser","com.samsung.tv.cobalt-yt"};
 static readonly HashSet<string> Hidden=new HashSet<string>{"25emanual1.emanual"};

 // Store apps carry a ten-character store package prefix, e.g. "gzcc4LRFBF.Peacock".
 public static bool IsUserApp(InstalledApp a)
 {
  if(a==null||a.NoDisplay||string.IsNullOrWhiteSpace(a.Id)||string.IsNullOrWhiteSpace(a.Label)||Hidden.Contains(a.Id))return false;
  if(MediaApps.Contains(a.Id))return true;
  int dot=a.Id.IndexOf('.');
  return a.Type=="webapp"&&dot==10;
 }


 // The list Quiet Home shows: what is installed, curated favorites first in
 // catalog order, the rest by name. Every app draws as the TV's own square
 // tile, like Samsung's home; curated art stands in only when the TV has none.
 // With no installed list, the curated catalog stands alone.
 public static List<LibraryApp> Merge(List<LibraryApp> curated,IEnumerable<InstalledApp> installed)
 {
  curated??=new List<LibraryApp>();
  var visible=new Dictionary<string,InstalledApp>();
  if(installed!=null)foreach(var a in installed)if(IsUserApp(a))visible[a.Id]=a;
  if(visible.Count==0)return new List<LibraryApp>(curated);
  var result=new List<LibraryApp>();
  var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  LibraryApp utility=null;
  foreach(var app in curated)
  {
   if(app.Utility){utility=app;continue;}
   if(!visible.TryGetValue(app.Id,out var tv)||!names.Add(app.Name))continue;
   result.Add(string.IsNullOrEmpty(tv.IconPath)?WithStore(app,tv):FromTv(tv,app.Name,app.Description));
  }
  var rest=new List<InstalledApp>(visible.Values);
  rest.Sort((x,y)=>string.Compare(Clean(x.Label),Clean(y.Label),StringComparison.OrdinalIgnoreCase));
  foreach(var tv in rest)
  {
   if(result.Exists(a=>a.Id==tv.Id)||!names.Add(Clean(tv.Label)))continue;
   result.Add(FromTv(tv,Clean(tv.Label),""));
  }
  if(utility!=null)result.Add(utility);
  return result;
 }

 // Store labels often append a tagline: "Tubi - Free Movies & TV".
 public static string Clean(string label)
 {
  string s=(label??"").Replace('＆','&').Trim();
  int cut=s.IndexOf(" - ",StringComparison.Ordinal);
  return cut>0?s.Substring(0,cut).Trim():s;
 }

 static LibraryApp FromTv(InstalledApp tv,string name,string description)=>new LibraryApp{
  Name=name,Id=tv.Id,Asset=tv.IconPath??"",Description=description??"",Fit=string.IsNullOrEmpty(tv.IconPath)?"name":"tile",Background="#1F2329",Width=512,Height=512,
  StoreId=tv.StoreId??"",Removable=tv.Removable};

 static LibraryApp WithStore(LibraryApp app,InstalledApp tv)=>new LibraryApp{
  Name=app.Name,Id=app.Id,Asset=app.Asset,Description=app.Description,Fit=app.Fit,Background=app.Background,Width=app.Width,Height=app.Height,Crop=app.Crop,Utility=app.Utility,
  StoreId=tv.StoreId??"",Removable=tv.Removable};

 static readonly Regex SquareTile=new Regex(@"^(\d+)x\1\.png$");
 // Samsung's icon folders hold one square tile per size ("304x304.png"); the
 // largest scales down best. Other files there are unrelated artwork.
 public static string BestIcon(IEnumerable<string> fileNames)
 {
  string best="";int size=0;
  if(fileNames!=null)foreach(var name in fileNames){var m=SquareTile.Match(name??"");if(m.Success&&int.TryParse(m.Groups[1].Value,out var n)&&n>size){size=n;best=name;}}
  return best;
 }

 // Utility tiles and apps with no image of any kind draw no artwork view.
 public static bool HasImage(LibraryApp app)=>!app.Utility&&app.Fit!="name";
 public static List<LibraryApp> Load(string path)
 {
  var apps=JsonSerializer.Deserialize<List<LibraryApp>>(File.ReadAllText(path));
  if(apps==null||apps.Count==0)throw new InvalidDataException("App library is empty.");
  var ids=new HashSet<string>();
  foreach(var app in apps)
  {
   if(string.IsNullOrWhiteSpace(app.Name)||string.IsNullOrWhiteSpace(app.Id)||!ids.Add(app.Id)
      ||string.IsNullOrWhiteSpace(app.Asset)||Path.IsPathRooted(app.Asset)||app.Asset.Contains("..")
      ||app.Width<=0||app.Height<=0)
   throw new InvalidDataException("Invalid app library entry.");
   var crop=app.Crop;
   if(crop!=null&&(crop.Length!=4||!Array.TrueForAll(crop,float.IsFinite)
      ||crop[0]<0||crop[1]<0||crop[2]<=0||crop[3]<=0
      ||crop[0]+crop[2]>1||crop[1]+crop[3]>1))
    throw new InvalidDataException("Invalid artwork crop.");
  }
  return apps;
 }
 public static int PageCount(int count)=>(count+PageSize-1)/PageSize;
 public static int VisibleCount(int count,int page)=>Math.Max(0,Math.Min(PageSize,count-page*PageSize));
 // Return an absolute catalog index. -1 moves focus to the navigation tabs.
 public static int Move(int count,int absolute,string key)
 {
  if(count<=0)return -1;
  absolute=Math.Clamp(absolute,0,count-1);
  int page=absolute/PageSize,local=absolute%PageSize,col=local%Columns,row=local/Columns;
  int pageStart=page*PageSize,visible=VisibleCount(count,page);
  if(key=="Left")return Math.Max(0,absolute-1);
  if(key=="Right")return Math.Min(count-1,absolute+1);
  if(key=="Up")
  {
   if(row>0)return absolute-Columns;
   if(page==0)return -1;
   return pageStart-Columns+col;
  }
  if(key=="Down")
  {
   if((row+1)*Columns<visible)return pageStart+Math.Min(local+Columns,visible-1);
   int next=pageStart+PageSize;
   return next<count?Math.Min(next+col,count-1):absolute;
  }
  return absolute;
 }
}

// The Home dock: the owner's saved app IDs, or the first three apps until they
// edit it. Edits return new lists so a failed save never leaves a half change.
public static class HomeDock
{
 public const int Max=10,Default=3;
 public static List<LibraryApp> Resolve(List<string> saved,List<LibraryApp> library)
 {
  var apps=library.FindAll(a=>!a.Utility);
  if(saved==null)return apps.GetRange(0,Math.Min(Default,apps.Count));
  var result=new List<LibraryApp>();
  foreach(var id in saved){var app=apps.Find(a=>a.Id==id);if(app!=null&&!result.Contains(app)&&result.Count<Max)result.Add(app);}
  return result;
 }
 public static List<string> Add(List<string> ids,string id){var r=new List<string>(ids);if(!r.Contains(id)&&r.Count<Max)r.Add(id);return r;}
 public static List<string> Remove(List<string> ids,string id){var r=new List<string>(ids);r.Remove(id);return r;}
 public static List<string> Move(List<string> ids,string id,int delta)
 {
  var r=new List<string>(ids);int i=r.IndexOf(id),j=i+delta;
  if(i<0||j<0||j>=r.Count)return r;
  r[i]=r[j];r[j]=id;return r;
 }
 public static List<string> Parse(string json){try{return string.IsNullOrWhiteSpace(json)?null:JsonSerializer.Deserialize<List<string>>(json);}catch{return null;}}
 public static string Serialize(List<string> ids)=>JsonSerializer.Serialize(ids);
}

// What holding Select on an app offers, in menu order.
public static class AppOptions
{
 public static List<string> For(LibraryApp app,List<string> dock,bool onHome)
 {
  var o=new List<string>{"open"};
  int i=dock.IndexOf(app.Id);
  if(onHome&&i>=0&&dock.Count>1)o.Add("move");
  if(i>=0)o.Add("remove");
  else if(dock.Count<HomeDock.Max)o.Add("add");
  if(app.Removable&&!string.IsNullOrEmpty(app.StoreId))o.Add("delete");
  return o;
 }
}
