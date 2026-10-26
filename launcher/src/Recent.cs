using System.Collections.Generic;

// What Home leads with: the app or input the owner was last in, or nothing yet.
public abstract record RecentItem
{
 RecentItem(){}
 public sealed record App(LibraryApp Item):RecentItem;
 public sealed record Input(int SourceType):RecentItem;
 public sealed record Nothing:RecentItem;
 public static readonly RecentItem None=new Nothing();
 public string Key=>this switch{App a=>"app:"+a.Item.Id,Input i=>"input:"+i.SourceType,_=>"none"};
}

public static class Recent
{
 public const string Viewer="org.tizen.tv-viewer";

 // current and previous are the TV's full-screen app IDs. While Quiet Home is
 // in front it is current, so the app before it is the one to resume.
 public static RecentItem Pick(string current,string previous,string ownId,List<LibraryApp> library,int lastSource,RecentItem prior)
 {
  string candidate=string.IsNullOrEmpty(current)||current==ownId?previous:current;
  if(candidate==Viewer)return new RecentItem.Input(lastSource);
  var app=Find(library,candidate);
  if(app!=null)return new RecentItem.App(app);
  // Settings, Samsung's own screens and Quiet Home itself are not worth resuming.
  // The prior app is looked up again so it carries the library's current art,
  // and an app deleted since then is dropped.
  if(prior is RecentItem.App p){var fresh=Find(library,p.Item.Id);return fresh==null?RecentItem.None:new RecentItem.App(fresh);}
  return prior;
 }

 static LibraryApp Find(List<LibraryApp> library,string id)=>
  string.IsNullOrEmpty(id)||library==null?null:library.Find(a=>a.Id==id&&!a.Utility);
}
