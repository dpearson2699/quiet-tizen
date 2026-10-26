using System;

static class SourcesTests
{
 static void Equal(string expected, string actual, string context)
 {
  if (expected != actual) throw new Exception(context + ": expected '" + expected + "', got '" + actual + "'");
 }
 static void True(bool actual, string context)
 {
  if (!actual) throw new Exception(context + ": expected true");
 }
 static void False(bool actual, string context)
 {
  if (actual) throw new Exception(context + ": expected false");
 }

 public static int Main()
 {
  // The real probe payload from this TV: tuner (type 0) plus HDMI 2 (type 14),
  // both names empty because the HDMI device is powered off.
  string probe = "[{\"connectionState\":true,\"detectedName\":\"\",\"editName\":\"\",\"sourceType\":0,\"powerState\":0,\"iconPath\":\"/usr/apps/com.samsung.tv.theme-resource/shared/res/VD_ICONS/i_icon_device_m_tv.svg\"},"
   + "{\"connectionState\":true,\"detectedName\":\"\",\"editName\":\"\",\"sourceType\":14,\"powerState\":2,\"iconPath\":\"/usr/apps/com.samsung.tv.theme-resource/shared/res/VD_ICONS/i_icon_device_m_hdmi2.svg\"}]";
  var inputs = Sources.Parse(probe);
  Equal("1", inputs.Count.ToString(), "tuner filtered, one external input");
  Equal("14", inputs[0].SourceType.ToString(), "kept the HDMI source");
  Equal("HDMI 2", Sources.Label(inputs[0]), "empty names fall back to the port");
  True(Sources.IsGenericIcon(inputs[0].IconPath), "bare hdmi2 glyph is generic");
  if (Sources.BrandArt(inputs[0]) != null) throw new Exception("empty name must have no brand art");

  // Name resolution precedence.
  Equal("Living Room PS5", Sources.Label(new SourceInfo{SourceType=14, EditName="Living Room PS5", DetectedName="PlayStation 5"}), "editName wins");
  Equal("PlayStation 5", Sources.Label(new SourceInfo{SourceType=14, DetectedName="PlayStation 5"}), "detectedName next");
  Equal("HDMI 1", Sources.Label(new SourceInfo{SourceType=13}), "port default for 13");
  Equal("HDMI 4", Sources.Label(new SourceInfo{SourceType=16}), "port default for 16");

  // Brand art: recognized consoles use hand-drawn art, others fall through.
  Equal("feature-ps5.svg", Sources.BrandArt(new SourceInfo{DetectedName="PlayStation 5"}), "ps5 brand art");
  Equal("feature-ps5.svg", Sources.BrandArt(new SourceInfo{EditName="my PS4"}), "ps4 brand art");
  Equal("feature-switch.svg", Sources.BrandArt(new SourceInfo{DetectedName="Nintendo Switch"}), "switch brand art");
  if (Sources.BrandArt(new SourceInfo{DetectedName="Xbox Series X"}) != null) throw new Exception("xbox has no repo art yet");
  if (Sources.BrandArt(new SourceInfo{DetectedName="Apple TV"}) != null) throw new Exception("apple tv has no brand art");

  // Generic vs specific Samsung icons.
  True(Sources.IsGenericIcon("/x/i_icon_device_m_hdmi.svg"), "plain hdmi glyph generic");
  True(Sources.IsGenericIcon("/x/i_icon_device_m_hdmi4.svg"), "numbered hdmi glyph generic");
  True(Sources.IsGenericIcon(""), "empty icon treated as generic");
  False(Sources.IsGenericIcon("/x/i_icon_device_m_gamepad.svg"), "gamepad is a specific device icon");
  False(Sources.IsGenericIcon("/x/i_icon_device_m_streamingmediaplayer.svg"), "streaming player is specific");
  False(Sources.IsGenericIcon("/x/i_icon_device_m_blueray.svg"), "blu-ray is specific");

  // Art decision: the hybrid rule the owner chose.
  var bareArt=Sources.Art(inputs[0]);
  Equal("Illustration", bareArt.Kind.ToString(), "bare hdmi uses our card");
  Equal("feature-hdmi.svg", bareArt.Path, "bare hdmi uses the generic card");
  var ps5Art=Sources.Art(new SourceInfo{SourceType=14, DetectedName="PlayStation 5", IconPath="/x/i_icon_device_m_gamepad.svg"});
  Equal("Illustration", ps5Art.Kind.ToString(), "brand beats samsung glyph");
  Equal("feature-ps5.svg", ps5Art.Path, "ps5 uses our art even over samsung's gamepad");
  var bluArt=Sources.Art(new SourceInfo{SourceType=15, IconPath="/x/i_icon_device_m_blueray.svg"});
  Equal("Icon", bluArt.Kind.ToString(), "specific samsung glyph, no brand -> use samsung");
  Equal("/x/i_icon_device_m_blueray.svg", bluArt.Path, "icon path passed through for the TV to draw");
  var switchArt=Sources.Art(new SourceInfo{SourceType=15, DetectedName="Nintendo Switch", IconPath="/x/i_icon_device_m_gamepad.svg"});
  Equal("feature-switch.svg", switchArt.Path, "switch uses our art");

  // Robustness: never throw on bad input.
  Equal("0", Sources.Parse("not json").Count.ToString(), "garbage yields no inputs");
  Equal("0", Sources.Parse("").Count.ToString(), "empty yields no inputs");
  Equal("0", Sources.Parse("null").Count.ToString(), "null yields no inputs");

  Console.WriteLine("SourcesTests: all passed");
  return 0;
 }
}
