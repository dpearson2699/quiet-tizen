using System;
using System.Collections.Generic;

static class RecentTests
{
 const string Own="com.samsung.tv.bixby-promotion", Netflix="org.tizen.netflix-app", YouTube="com.samsung.tv.cobalt-yt";

 static void Equal(string expected, string actual, string context)
 {
  if (expected != actual) throw new Exception(context + ": expected '" + expected + "', got '" + actual + "'");
 }

 public static int Main()
 {
  var library = new List<LibraryApp>{
   new LibraryApp{Name="Netflix", Id=Netflix, Asset="tile-netflix.png"},
   new LibraryApp{Name="YouTube", Id=YouTube, Asset="tile-youtube.png"},
   new LibraryApp{Name="Get more apps", Id="org.volt.apps", Asset="glass-button.svg", Utility=true}};
  var none = RecentItem.None;
  var youtube = new RecentItem.App(library[1]);

  Equal("app:" + Netflix, Recent.Pick(Own, Netflix, Own, library, 14, none).Key, "Quiet Home in front resumes the app before it");
  Equal("app:" + Netflix, Recent.Pick("", Netflix, Own, library, 14, none).Key, "no current app falls back to the previous one");
  Equal("app:" + Netflix, Recent.Pick(null, Netflix, Own, library, 14, none).Key, "unreadable current app falls back to the previous one");
  Equal("app:" + YouTube, Recent.Pick(YouTube, Netflix, Own, library, 14, none).Key, "an app still marked current is the one to resume");

  var input = Recent.Pick(Own, Recent.Viewer, Own, library, 14, youtube);
  Equal("input:14", input.Key, "the TV viewer means the last input");
  Equal("input:16", Recent.Pick(Recent.Viewer, Netflix, Own, library, 16, none).Key, "the TV viewer as current means its input");

  Equal("app:" + YouTube, Recent.Pick(Own, "org.tizen.menu", Own, library, 14, youtube).Key, "Samsung settings keep the prior app");
  Equal("app:" + YouTube, Recent.Pick(Own, Own, Own, library, 14, youtube).Key, "Quiet Home itself keeps the prior app");
  Equal("app:" + YouTube, Recent.Pick(Own, "com.samsung.tv.csfs", Own, library, 14, youtube).Key, "Samsung's home keeps the prior app");
  Equal("input:14", Recent.Pick(Own, "org.tizen.menu", Own, library, 14, input).Key, "a prior input is kept");
  Equal("none", Recent.Pick(Own, "org.volt.apps", Own, library, 14, none).Key, "a utility tile is not an app to resume");
  Equal("none", Recent.Pick(Own, "unknown.app", Own, library, 14, none).Key, "an unknown app keeps nothing");
  Equal("none", Recent.Pick(Own, null, Own, library, 14, none).Key, "no previous app keeps nothing");

  var refreshed = new List<LibraryApp>{new LibraryApp{Name="YouTube", Id=YouTube, Asset="/opt/store/304x304.png", Fit="tile"}};
  var kept = Recent.Pick(Own, "org.tizen.menu", Own, refreshed, 14, youtube) as RecentItem.App;
  Equal("/opt/store/304x304.png", kept?.Item.Asset, "a kept app carries the library's current art");
  Equal("none", Recent.Pick(Own, "org.tizen.menu", Own, new List<LibraryApp>{library[0]}, 14, youtube).Key, "a deleted prior app is dropped");

  Console.WriteLine("RecentTests: all passed");
  return 0;
 }
}
