using System;
using System.Collections.Generic;

// Choice and Slider rows are Samsung settings items. Volume goes through the
// sound manager, and a Link opens a screen of Samsung's own settings app.
public enum SettingKind { Choice, Slider, Volume, Link }

public sealed class SettingRow
{
 public string Id, Label, About, Opens, MenuItem, Shows;
 public SettingKind Kind;
 public bool Gap;
 public Dictionary<string,string> AboutByName;
 public string AboutFor(string name) => AboutByName != null && name != null && AboutByName.TryGetValue(name, out var text) ? text : About;
}

public sealed class SettingOption { public string Value, Name; }

public static class SettingsCatalog
{
 public static readonly SettingRow[] Rows = {
  new SettingRow{Id="picturemode", Label="Picture mode", Kind=SettingKind.Choice, About="How the TV tunes the picture.",
   AboutByName=new Dictionary<string,string>{
    {"Dynamic","The brightest, most saturated look. Good in a sunny room."},
    {"Standard","A balanced look for everyday watching."},
    {"Eco","Dims the picture a little to use less power."},
    {"Movie","Warmer and softer, for films in a dim room."},
    {"Filmmaker Mode","Shows films and shows the way they were graded."}}},
  new SettingRow{Id="intelligent-mode", Label="AI picture", Kind=SettingKind.Choice,
   About="Samsung's Intelligent Mode. It adjusts the picture to what is playing and to the light in your room."},
  new SettingRow{Id="brightness", Label="Brightness", Kind=SettingKind.Slider, About="How brightly the screen glows. Lower it for a dark room."},
  new SettingRow{Id="color-tone", Label="Color tone", Kind=SettingKind.Choice, About="Warm2 is closest to how films are mastered. Cool looks bluer."},
  new SettingRow{Id="picture-clarity-mode", Label="Motion smoothing", Kind=SettingKind.Choice,
   About="Adds extra frames so fast movement looks smoother. Off keeps films looking like films. Samsung calls this Picture Clarity."},
  new SettingRow{Id="volume", Label="Volume", Kind=SettingKind.Volume, Gap=true, About="Select mutes and unmutes."},
  new SettingRow{Id="sound-mode", Label="Sound mode", Kind=SettingKind.Choice, About="How the TV speaker is tuned.",
   AboutByName=new Dictionary<string,string>{
    {"Standard","The speaker as tuned at the factory."},
    {"Adaptive Sound","Tunes the speaker to your room and to what is playing."},
    {"Amplify","Lifts voices so dialogue is easier to follow."}}},
  new SettingRow{Id="caption-setting", Label="Captions", Kind=SettingKind.Choice, Gap=true,
   About="The TV's own captions, for inputs and broadcasts. Streaming apps keep their own subtitle setting."},
  new SettingRow{Id="sleeptimer", Label="Sleep timer", Kind=SettingKind.Choice, About="Turns the TV off after this long."},
  new SettingRow{Id="energy-saving-solution", Label="Energy saving", Kind=SettingKind.Choice, About="Lowers screen brightness to use less power."},
  new SettingRow{Id="game-mode", Label="Game mode", Kind=SettingKind.Choice, Gap=true, About="Cuts the delay between your controller and the screen."},
  new SettingRow{Id="controllers", Label="Controllers", Kind=SettingKind.Link, MenuItem="add-bluetooth-keyboard", Shows="Pair", Opens="Samsung's pairing screen",
   About="To pair a DualSense, hold PS and Create until the light blinks, then select this."},
  new SettingRow{Id="network", Label="Network", Kind=SettingKind.Link, Gap=true, MenuItem="networksettings", Opens="Samsung's network settings",
   About="Opens Samsung's Wi-Fi and network settings."},
  new SettingRow{Id="all", Label="All settings", Kind=SettingKind.Link, Shows="Samsung", Opens="Samsung's settings menu",
   About="Opens Samsung's own settings menu, for everything not listed here."}};

 // Samsung lists options as "value,name;" and gives one name several values,
 // one per source type. The current value always stands for its name.
 public static List<SettingOption> Options(string list, Func<string,bool> allowed, string current)
 {
  var options = new List<SettingOption>();
  foreach (var entry in (list ?? "").Split(';'))
  {
   int comma = entry.IndexOf(',');
   if (comma <= 0 || comma == entry.Length - 1) continue;
   var option = new SettingOption{Value=entry.Substring(0, comma), Name=entry.Substring(comma + 1)};
   if (option.Value != current && !allowed(option.Value)) continue;
   int same = options.FindIndex(o => o.Name == option.Name);
   if (same < 0) options.Add(option);
   else if (option.Value == current) options[same] = option;
  }
  return options;
 }

 // Left and Right walk the options and wrap; a value the TV did not list starts from the first.
 public static string Step(List<SettingOption> options, string current, int direction)
 {
  if (options.Count == 0) return null;
  int at = options.FindIndex(o => o.Value == current);
  if (at < 0) return options[0].Value;
  return options[(at + direction % options.Count + options.Count) % options.Count].Value;
 }

 // Tizen's memory/dnet/status: 0 off, 1 cellular, 2 Wi-Fi, 3 Ethernet, 4 Bluetooth, 5 proxy.
 static readonly Dictionary<int,string> NetworkNames = new Dictionary<int,string>{{0,"Not connected"},{2,"Wi-Fi"},{3,"Wired"}};
 public static string NetworkName(int status) => NetworkNames.TryGetValue(status, out var name) ? name : "Connected";

 public static double Slide(double value, double min, double max, double step, int direction) => Math.Max(min, Math.Min(max, value + direction * step));

 public static string Keys(SettingRow row, bool available)
 {
  if (!available) return "The TV isn't offering this right now.";
  return row.Kind switch{
   SettingKind.Link => "Select opens " + row.Opens + ".",
   SettingKind.Volume => "Left and Right change the volume. Select mutes.",
   _ => "Left and Right change it."};
 }
}
