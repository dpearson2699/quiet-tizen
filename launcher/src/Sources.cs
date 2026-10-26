using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

// One external input as the TV's source service reports it. The field names
// match the JSON from libsource-api.so get_source_list() (see launcher/probe).
public sealed class SourceInfo
{
 public int SourceType {get;set;}
 public string DetectedName {get;set;}="";
 public string EditName {get;set;}="";
 public string IconPath {get;set;}="";
 public bool ConnectionState {get;set;}
 public int PowerState {get;set;}
}

public enum SourceArtKind { Illustration, Icon }

// Which image a card draws. Illustration.Path is a repo asset filename under the
// launcher's resource root (a full-card SVG). Icon.Path is an absolute TV path to
// Samsung's own device glyph, drawn small on a tile.
public struct SourceArt
{
 public SourceArtKind Kind;
 public string Path;
}

// Resolve what the TV knows about each input into a label and an image, so the
// launcher shows the same names Samsung does and never hardcodes a device.
public static class Sources
{
 static readonly JsonSerializerOptions Opts=new JsonSerializerOptions{PropertyNameCaseInsensitive=true};
 // Samsung's bare port glyphs. When the service hands back one of these the TV
 // detected no specific device, so our own card stands in.
 static readonly Regex GenericIcon=new Regex(@"^i_icon_device_m_(hdmi\d*|tv|ext|av)\.svg$",RegexOptions.IgnoreCase);

 // Parse the source-service payload into real external inputs. sourceType 0 is
 // the built-in tuner, which the launcher does not list. Never throws: a
 // misbehaving service must not stop the home screen from rendering.
 public static List<SourceInfo> Parse(string json)
 {
  var inputs=new List<SourceInfo>();
  if(string.IsNullOrWhiteSpace(json))return inputs;
  List<SourceInfo> raw;
  try{raw=JsonSerializer.Deserialize<List<SourceInfo>>(json,Opts);}catch{return inputs;}
  if(raw==null)return inputs;
  foreach(var s in raw)if(s!=null&&s.SourceType>0)inputs.Add(s);
  return inputs;
 }

 // editName (what you set in Samsung's Edit Source) wins, then detectedName
 // (what the device reports over CEC/EDID), then the port's own name.
 public static string Label(SourceInfo s)
 {
  if(s==null)return "Input";
  if(!string.IsNullOrWhiteSpace(s.EditName))return s.EditName.Trim();
  if(!string.IsNullOrWhiteSpace(s.DetectedName))return s.DetectedName.Trim();
  return DefaultName(s.SourceType);
 }

 // sourceType 13..20 map to HDMI 1..8 on this panel (14 == HDMI 2).
 public static int HdmiNumber(int sourceType)=>sourceType>=13&&sourceType<=20?sourceType-12:0;
 public static string DefaultName(int sourceType){int n=HdmiNumber(sourceType);return n>0?"HDMI "+n:"Input";}

 // Hand-drawn art for a brand we recognize by name. Samsung ships only a
 // generic gamepad for every console, so our art is the better choice when the
 // name tells us the brand. null means no brand art applies.
 public static string BrandArt(SourceInfo s)
 {
  if(s==null)return null;
  string n=((s.EditName??"")+" "+(s.DetectedName??"")).ToLowerInvariant();
  if(n.Contains("playstation")||Regex.IsMatch(n,@"\bps[345]\b"))return "feature-ps5.svg";
  if(n.Contains("nintendo")||n.Contains("switch"))return "feature-switch.svg";
  return null;
 }

 // True when iconPath is only a bare HDMI/port glyph, so the launcher should
 // draw its own generic card instead of the TV's icon.
 public static bool IsGenericIcon(string iconPath)
 {
  if(string.IsNullOrWhiteSpace(iconPath))return true;
  int slash=iconPath.LastIndexOf('/');
  string file=slash>=0?iconPath.Substring(slash+1):iconPath;
  return GenericIcon.IsMatch(file);
 }

 // The hybrid rule the owner chose: our hand-drawn art for a brand we recognize,
 // else Samsung's own device icon when it has a specific one, else our generic
 // port card. Samsung ships no console art, so brand wins there; it does ship
 // Blu-ray/streaming/etc. glyphs, which beat our bare-port fallback.
 public static SourceArt Art(SourceInfo s)
 {
  string brand=BrandArt(s);
  if(brand!=null)return new SourceArt{Kind=SourceArtKind.Illustration,Path=brand};
  if(s!=null&&!IsGenericIcon(s.IconPath))return new SourceArt{Kind=SourceArtKind.Icon,Path=s.IconPath};
  return new SourceArt{Kind=SourceArtKind.Illustration,Path="feature-hdmi.svg"};
 }
}
