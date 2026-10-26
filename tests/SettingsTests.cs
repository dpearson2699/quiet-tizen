using System;
using System.Collections.Generic;

static class SettingsTests
{
 static void Equal(string expected, string actual, string context)
 {
  if (expected != actual) throw new Exception(context + ": expected '" + expected + "', got '" + actual + "'");
 }

 static string Names(List<SettingOption> options) => string.Join("|", options.ConvertAll(o => o.Value + "=" + o.Name));

 public static int Main()
 {
  // The S95C's picture mode list, trimmed: a name repeats for each source type.
  const string picture = "65541,Entertain;65536,Dynamic;327692,Dynamic;327705,Standard;65537,Standard;65552,Eco;65538,Movie;393217,Movie (Calibrated);458762,;";
  var allowed = new HashSet<string>{"65536", "65537", "65552", "65538", "327705"};
  var options = SettingsCatalog.Options(picture, allowed.Contains, "327705");
  Equal("65536=Dynamic|327705=Standard|65552=Eco|65538=Movie", Names(options), "allowed options, one per name, the current value standing for its name");
  Equal("65537=Standard", Names(SettingsCatalog.Options("65537,Standard;327705,Standard;", allowed.Contains, "65538")), "the first allowed value stands for a name when the current value is elsewhere");
  Equal("0=Off|1=On", Names(SettingsCatalog.Options("0,Off;1,On;", v => true, "0")), "a switch lists Off and On");
  Equal("", Names(SettingsCatalog.Options(",Off;,On;", v => true, "")), "entries without a value cannot be set");
  Equal("", Names(SettingsCatalog.Options(null, v => true, "0")), "no list means no options");
  Equal("4=Warm2", Names(SettingsCatalog.Options("3,Warm1;4,Warm2;", v => false, "4")), "the current value is kept even when the TV marks it unavailable");

  Equal("65552", SettingsCatalog.Step(options, "327705", 1), "Right moves to the next option");
  Equal("65536", SettingsCatalog.Step(options, "65538", 1), "Right on the last option wraps to the first");
  Equal("65538", SettingsCatalog.Step(options, "65536", -1), "Left on the first option wraps to the last");
  Equal("65536", SettingsCatalog.Step(options, "999", 1), "an unlisted value starts from the first option");
  Equal(null, SettingsCatalog.Step(new List<SettingOption>(), "0", 1), "no options means no step");

  Equal("50", SettingsCatalog.Slide(50, 0, 50, 1, 1).ToString(), "brightness stops at its maximum");
  Equal("0", SettingsCatalog.Slide(0, 0, 50, 1, -1).ToString(), "brightness stops at its minimum");
  Equal("37", SettingsCatalog.Slide(38, 0, 50, 1, -1).ToString(), "brightness moves one step");

  var rows = SettingsCatalog.Rows;
  Equal("14", rows.Length.ToString(), "the page lists fourteen rows");
  Equal("Picture mode|AI picture|Brightness|Color tone|Motion smoothing|Volume|Sound mode|Captions|Sleep timer|Energy saving|Game mode|Controllers|Network|All settings",
   string.Join("|", Array.ConvertAll(rows, r => r.Label)), "rows in the owner's order");
  if (Array.Exists(rows, r => r.Id == "soundoutput")) throw new Exception("sound output stays off the page: the owner uses only the TV speaker");
  Equal("Lifts voices so dialogue is easier to follow.", Array.Find(rows, r => r.Id == "sound-mode").AboutFor("Amplify"), "a sound mode explains itself");
  Equal("How the TV speaker is tuned.", Array.Find(rows, r => r.Id == "sound-mode").AboutFor("Stadium"), "an unknown mode falls back to the row's text");
  Equal("Select opens Samsung's settings menu.", SettingsCatalog.Keys(Array.Find(rows, r => r.Id == "all"), true), "a link says what it opens");
  Equal("The TV isn't offering this right now.", SettingsCatalog.Keys(Array.Find(rows, r => r.Id == "game-mode"), false), "an unavailable row says so");

  Equal("Wired", SettingsCatalog.NetworkName(3), "an Ethernet connection reads as Wired");
  Equal("Wi-Fi", SettingsCatalog.NetworkName(2), "a Wi-Fi connection reads as Wi-Fi");
  Equal("Not connected", SettingsCatalog.NetworkName(0), "no connection says so");
  Equal(null, Array.Find(rows, r => r.Id == "network").Shows, "the network row shows what the TV reports, not a fixed label");

  Console.WriteLine("SettingsTests: all passed");
  return 0;
 }
}
