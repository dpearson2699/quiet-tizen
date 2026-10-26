using System;
using System.Collections.Generic;

static class LibraryTests
{
 static void Equal(string expected, string actual, string context)
 {
  if (expected != actual) throw new Exception(context + ": expected '" + expected + "', got '" + actual + "'");
 }
 static InstalledApp Tv(string id, string label, string type, string icon = "", bool hidden = false) =>
  new InstalledApp{Id=id, Label=label, Type=type, IconPath=icon, NoDisplay=hidden};
 static string Names(List<LibraryApp> apps) => string.Join(",", apps.ConvertAll(a => a.Name));
 static string Ids(List<LibraryApp> apps) => string.Join(",", apps.ConvertAll(a => a.Id));
 static LibraryApp App(string id) => new LibraryApp{Name=id.ToUpperInvariant(), Id=id};

 public static int Main()
 {
  var curated = new List<LibraryApp>{
   new LibraryApp{Name="Netflix", Id="org.tizen.netflix-app", Asset="app-icons/netflix.svg", Fit="contain", Width=370, Height=100},
   new LibraryApp{Name="YouTube", Id="com.samsung.tv.cobalt-yt", Asset="app-icons/youtube.jpg", Width=1920, Height=1080},
   new LibraryApp{Name="Hulu", Id="LBUAQX1exg.Hulu", Asset="placeholders/card-5.svg", Width=640, Height=360},
   new LibraryApp{Name="Amazon Luna", Id="CmeyUlnhYY.LunaWebApp", Asset="app-icons/amazon-luna.jpg", Width=740, Height=346},
   new LibraryApp{Name="Apps", Id="org.volt.apps", Asset="glass-button.svg", Utility=true, Width=640, Height=360},
  };
  // Rows as this S95C reports them (app-schema-1.txt), trimmed to the cases that matter.
  var installed = new List<InstalledApp>{
   Tv("org.tizen.netflix-app", "Netflix", "capp", "/opt/usr/apps/org.tizen.netflix-app/shared/res/org.tizen.netflix-app.png"),
   Tv("com.samsung.tv.cobalt-yt", "Youtube", "capp"),
   Tv("9Ur5IzDKqV.TizenYouTube", "YouTube", "webapp", "/opt/usr/apps/9Ur5IzDKqV/shared/res/9Ur5IzDKqV.TizenYouTube.png"),
   Tv("LBUAQX1exg.Hulu", "Hulu", "webapp", "/opt/usr/apps/LBUAQX1exg/shared/res/LBUAQX1exg.Hulu.png"),
   Tv("gzcc4LRFBF.Peacock", "Peacock TV", "webapp", "/opt/usr/apps/gzcc4LRFBF/shared/res/gzcc4LRFBF.Peacock.png"),
   Tv("3KA0pm7a7V.TubiTV", "Tubi - Free Movies ＆ TV", "webapp", "/opt/usr/apps/3KA0pm7a7V/shared/res/3KA0pm7a7V.TubiTV.png"),
   Tv("25emanual1.emanual", "User Guide", "webapp", "/opt/usr/apps/25emanual1/shared/res/25emanual1.emanual.png"),
   Tv("com.samsung.tv.CSAccount", "CSAccount", "dotnet-inhouse", "/x.png"),
   Tv("com.samsung.tv.adplayer", "AdPlayer", "dotnet-inhouse", "/x.png"),
   Tv("MCmYXNxgcu.DisneyPlusService", "DisneyService", "webapp", "", true),
  };
  var lib = AppLibrary.Merge(curated, installed);
  Equal("Netflix,YouTube,Hulu,Peacock TV,Tubi,Apps", Names(lib), "favorites first, uninstalled dropped, rest by name, utility last");
  Equal("app-icons/netflix.svg", lib[0].Asset, "curated publisher art wins over the tv icon");
  Equal("/opt/usr/apps/LBUAQX1exg/shared/res/LBUAQX1exg.Hulu.png", lib[2].Asset, "placeholder art yields to the tv icon");
  Equal("icon", lib[2].Fit, "tv icons draw as icons");
  Equal("Hulu", lib[2].Name, "curated name kept when its art is replaced");
  Equal("gzcc4LRFBF.Peacock", lib[3].Id, "store app the catalog never listed still appears");
  if (lib.Exists(a => a.Id == "9Ur5IzDKqV.TizenYouTube")) throw new Exception("store YouTube duplicates the curated YouTube");
  if (lib.Exists(a => a.Id == "CmeyUlnhYY.LunaWebApp")) throw new Exception("curated app not on this TV must not appear");

  Equal("Netflix,YouTube,Hulu,Amazon Luna,Apps", Names(AppLibrary.Merge(curated, new List<InstalledApp>())), "no installed list keeps the curated catalog");
  Equal("Netflix,YouTube,Hulu,Amazon Luna,Apps", Names(AppLibrary.Merge(curated, null)), "failed query keeps the curated catalog");
  var bare = AppLibrary.Merge(curated, new List<InstalledApp>{Tv("abcdefghij.NoIcon", "No Icon", "webapp")});
  var noIcon = bare.Find(a => a.Id == "abcdefghij.NoIcon");
  Equal("name", noIcon.Fit, "an app without any icon becomes a name card");
  if (AppLibrary.HasImage(noIcon)) throw new Exception("name cards must not request an image");

  Equal("Tubi", AppLibrary.Clean("Tubi - Free Movies ＆ TV"), "store tagline trimmed");
  Equal("Disney+", AppLibrary.Clean("Disney+"), "plain label untouched");

  // Store identity travels with the app whichever artwork wins.
  var stored = new List<InstalledApp>(installed);
  stored[0] = new InstalledApp{Id="org.tizen.netflix-app", Label="Netflix", Type="capp", IconPath="/x.png", StoreId="3201907018807", Removable=true};
  stored.Add(new InstalledApp{Id="PvWgqxV3Xa.YouTubeTV", Label="YouTube TV", Type="webapp", IconPath="/opt/share/webappservice/apps_icon/all/3201707014489/304x304.png", StoreId="3201707014489", Removable=true});
  var storeLib = AppLibrary.Merge(curated, stored);
  Equal("3201907018807", storeLib[0].StoreId, "curated entry keeps its store id");
  Equal("app-icons/netflix.svg", storeLib[0].Asset, "store id does not replace curated art");
  if (!storeLib[0].Removable) throw new Exception("curated entry keeps removability");
  if (curated[0].StoreId != "") throw new Exception("merge must not mutate the curated catalog");
  var ytv = storeLib.Find(a => a.Id == "PvWgqxV3Xa.YouTubeTV");
  Equal("3201707014489", ytv.StoreId, "tv entry carries its store id");
  if (storeLib.Find(a => a.Id == "gzcc4LRFBF.Peacock").Removable) throw new Exception("apps WAS does not know are not removable");

  Equal("608x608.png", AppLibrary.BestIcon(new[]{"40x40.png","304x304.png","608x608.png","emanual_myapps.png","208x208.png"}), "largest square tile wins");
  Equal("", AppLibrary.BestIcon(new[]{"emanual_myapps.png","300x200.png"}), "no square tile");
  Equal("", AppLibrary.BestIcon(null), "missing folder");

  // Dock: three defaults until the owner edits it, then their saved order.
  var apps = new List<LibraryApp>{App("a"),App("b"),App("c"),App("d"),App("e"),App("f"),App("g"),new LibraryApp{Name="Apps",Id="org.volt.apps",Utility=true}};
  Equal("a,b,c", Ids(HomeDock.Resolve(null, apps)), "unedited dock shows the first three apps");
  Equal("d,a", Ids(HomeDock.Resolve(new List<string>{"d","gone","a","d","org.volt.apps"}, apps)), "saved order kept; uninstalled, duplicate and utility ids dropped");
  Equal("", Ids(HomeDock.Resolve(new List<string>(), apps)), "an emptied dock stays empty");
  Equal("a,b,c,d,e", Ids(HomeDock.Resolve(new List<string>{"a","b","c","d","e","f"}, apps)), "dock holds at most five apps");
  var ids = new List<string>{"a","b","c"};
  Equal("a,b,c,d", string.Join(",", HomeDock.Add(ids, "d")), "add appends");
  Equal("a,b,c", string.Join(",", ids), "edits return a new list");
  Equal("a,b,c", string.Join(",", HomeDock.Add(ids, "b")), "adding a docked app is a no-op");
  Equal("a,b,c,d,e", string.Join(",", HomeDock.Add(new List<string>{"a","b","c","d","e"}, "f")), "a full dock ignores add");
  Equal("a,c", string.Join(",", HomeDock.Remove(ids, "b")), "remove");
  Equal("b,a,c", string.Join(",", HomeDock.Move(ids, "b", -1)), "move left");
  Equal("a,c,b", string.Join(",", HomeDock.Move(ids, "b", 1)), "move right");
  Equal("a,b,c", string.Join(",", HomeDock.Move(ids, "c", 1)), "move past the end is a no-op");
  Equal("a,b,c", string.Join(",", HomeDock.Parse(HomeDock.Serialize(ids))), "round trip");
  if (HomeDock.Parse("not json") != null || HomeDock.Parse("") != null) throw new Exception("bad saved dock reads as unedited");

  // Options: what the hold-Select menu offers for an app.
  var removable = new LibraryApp{Name="Hulu", Id="b", StoreId="1", Removable=true};
  Equal("open,move,remove,delete", string.Join(",", AppOptions.For(removable, ids, true)), "docked app on Home");
  Equal("open,move,remove", string.Join(",", AppOptions.For(App("a"), ids, true)), "no store id, no delete");
  Equal("open,remove", string.Join(",", AppOptions.For(App("a"), new List<string>{"a"}, true)), "a lone dock app has nowhere to move");
  Equal("open,remove,delete", string.Join(",", AppOptions.For(removable, ids, false)), "docked app in Your Apps");
  Equal("open,add", string.Join(",", AppOptions.For(App("d"), ids, false)), "undocked app can be added");
  Equal("open", string.Join(",", AppOptions.For(App("f"), new List<string>{"a","b","c","d","e"}, false)), "full dock offers no add");

  Console.WriteLine("LibraryTests: all passed");
  return 0;
 }
}
