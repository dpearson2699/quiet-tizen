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

  Console.WriteLine("LibraryTests: all passed");
  return 0;
 }
}
