using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using Tizen.Applications;
using Tizen.TV.DownloadableApplications;

public sealed class QuietHome : NUIApplication
{
 const string Root="/opt/usr/apps/com.samsung.tv.bixby-promotion/res/quiet-home/";
 const string StateFile="/tmp/quiet-home-state.txt";
 // App tiles match Samsung's home: one rounded square size on the dock and in Your Apps.
 // Samsung's home app row (applist02): 14-unit squircles, 2-unit gaps, 1.2x focus; 8 px per unit at 1080p.
 const int Tile=112,TileGap=16,DockPitch=Tile+TileGap,GridPitchX=230,GridPitchY=190;
 const float TileFocusScale=1.2f,TileSquareness=.7f;
 // Samsung's reorder (FLUX NList): a 12-unit gap parts the row, 4-unit arrows flank the app, each slide runs 300 ms on FLUX's basic curve.
 const int MoveGap=96,MoveArrow=32;
 const string FocusStroke="/usr/apps/com.samsung.tv.theme-resource/shared/res/PRINCIPLE/FLUX/i_focus_stroke/i_focus_stroke_squircle_13.svg";
 enum SlotKind { Tile, Input, Setting, Pill, Resume }
 sealed class Slot { public SlotKind Kind; public View Card,Shadow,FocusRing; public ImageView Icon; public TextLabel Label; public string Name,Action,Description; public int Row,X,Y,W,H; }
 // Page numbers are tab positions. Home has no tab strip of its own.
 const int HomePage=0,AppsPage=1,InputsPage=2,SettingsPage=3;
 static readonly string[] PageNames={"Home","Apps","Inputs","Settings"};
 // Home's rows, top to bottom. Up and Down skip a row with no slots.
 const int PillRow=0,ResumeRow=1,DockRow=2;
 const int PreviewX=672,PreviewY=144,PreviewW=1152,PreviewH=648,DockLeft=96,DockY=856,TileNameW=480;
 const string OwnId="com.samsung.tv.bixby-promotion";
 sealed class CachedArtwork { public ImageView Image; public View Parent; public string Asset,Fit; }
 readonly List<Slot> slots=new List<Slot>();
 readonly List<View> scene=new List<View>();
 readonly Dictionary<string,CachedArtwork> artworkCache=new Dictionary<string,CachedArtwork>();
 View artworkPreloadRoot;
 int artworkViewsCreated,pageArtworkCold;
 List<LibraryApp> curated=new List<LibraryApp>(),library=new List<LibraryApp>();
 System.Threading.Tasks.Task<IEnumerable<ApplicationInfo>> installedQuery;
 Tizen.NUI.Timer installedPoll;
 string librarySource="catalog",storeSource="";
 List<string> dockIds;
 // Move mode: the dock app being repositioned and its unsaved order.
 string moving;
 List<string> moveIds;
 View moveDim;
 View[] moveArrows;
 List<string> menuOptions;
 int menuIndex;
 LibraryApp menuApp;
 readonly List<View> overlay=new List<View>();
 readonly List<View> menuRows=new List<View>();
 readonly List<TextLabel> menuLabels=new List<TextLabel>();
 bool pendingBuild,selectHeld,selectConsumed;
 Tizen.NUI.Timer holdTimer,uninstallPoll;
 AppManager appService;
 AppManager.InstallEventHandler uninstallHandler;
 volatile string uninstallOutcome;
 LibraryApp uninstalling;
 static readonly Dictionary<string,string> OptionText=new Dictionary<string,string>{
  {"open","Open"},{"move","Move"},{"remove","Remove from Home"},
  {"add","Add to Home"},{"delete","Delete app"},{"confirm-delete","Delete"},{"cancel","Cancel"}};
 int libraryPage;
 string libraryError="";
 TextLabel clock, notice, detail;
 TextLabel[] tabs;
 TextLabel pageHint,tileName;
 Animation focusAnimation,moveAnimation;
 bool fontLoaded;
 Tizen.NUI.Timer clockTimer;
 int page, selected, lastSource, dockFocus;
 bool onTabs,paused;
 RecentItem recent=RecentItem.None;
 LivePreview preview;
 string recentFrom="";
 readonly List<SourceInfo> inputs=new List<SourceInfo>();
 string editing="";
 long buildMilliseconds;
 int currentVolume, currentMute;
 readonly Dictionary<string,string> settingValues=new Dictionary<string,string>();
 readonly Dictionary<string,string> settingNames=new Dictionary<string,string>();
 readonly Dictionary<string,bool> settingEnabled=new Dictionary<string,bool>();
 double brightnessMin,brightnessMax=50,brightnessStep=1;
 string lastKey="";
 long lastKeyAt;
 readonly Color ink=new Color(0.94f,0.94f,0.96f,1);
 readonly Color muted=new Color(0.62f,0.63f,0.67f,1);
 readonly Color white=new Color(1,1,1,1);
 readonly Color accent=new Color(0.36f,0.43f,0.96f,1);
 readonly Color homeInk=Rgb(0xF5F5F5),dim=Rgb(0x8A8A8A),raised=Rgb(0x272727),shelf=Rgb(0x181818),black=Rgb(0),clear=new Color(0,0,0,0);
 static Color Rgb(int n)=>new Color(((n>>16)&255)/255f,((n>>8)&255)/255f,(n&255)/255f,1);
 [DllImport("libsource-api.so")] static extern int connect_source(int sourceType);
 [DllImport("libsource-api.so")] static extern IntPtr get_source_list();
 [DllImport("libc.so.6")] static extern void free(IntPtr p);
 [DllImport("libvconf.so.0")] static extern int vconf_get_int(string key,out int value);
 [DllImport("libvconf.so.0")] static extern IntPtr vconf_get_str(string key);
 [DllImport("libcapi-appfw-app-control.so.0")] static extern int app_control_create(out IntPtr app);
 [DllImport("libcapi-appfw-app-control.so.0")] static extern int app_control_set_app_id(IntPtr app,string id);
 [DllImport("libcapi-appfw-app-control.so.0")] static extern int app_control_set_operation(IntPtr app,string operation);
 [DllImport("libcapi-appfw-app-control.so.0")] static extern int app_control_add_extra_data(IntPtr app,string key,string value);
 [DllImport("libcapi-appfw-app-control.so.0")] static extern int app_control_send_launch_request(IntPtr app,IntPtr cb,IntPtr data);
 [DllImport("libcapi-appfw-app-control.so.0")] static extern int app_control_destroy(IntPtr app);
 [DllImport("libcapi-media-sound-manager-tv.so.0")] static extern int sound_manager_get_master_volume(out int value);
 [DllImport("libcapi-media-sound-manager-tv.so.0")] static extern int sound_manager_set_master_volume(int value);
 [DllImport("libcapi-media-sound-manager-tv.so.0")] static extern int sound_manager_get_master_mute(out int value);
 [DllImport("libcapi-media-sound-manager-tv.so.0")] static extern int sound_manager_set_master_mute(int value);
 [DllImport("libsettings-api.so")] static extern bool CSharpGetItemValue(string id,out string name,out string value);
 [DllImport("libsettings-api.so")] static extern bool CSharpSetItemValue(string id,string value);
 [DllImport("libsettings-api.so")] static extern bool CSharpGetItemEnable(string id,out bool enabled);
 [DllImport("libsettings-api.so")] static extern bool CSharpGetItemMinMaxStepValue(string id,out double min,out double max,out double step);



 protected override void OnCreate()
 {
  base.OnCreate();
  try{fontLoaded=FontClient.Instance.AddCustomFontDirectory(Root+"fonts");}catch{fontLoaded=false;}
  try{curated=AppLibrary.Load(Root+"apps.json");library=new List<LibraryApp>(curated);}catch(Exception ex){libraryError=ex.Message;}
  Window.Default.BackgroundColor=new Color(0,0,0,1);
  Window.Default.Title="Quiet Home";Window.Default.KeyEvent+=OnKey;
  // The live preview's app window sits under this one and shows through a hole.
  try{Window.Default.SetTransparency(true);}catch{}
  preview=new LivePreview(PreviewX,PreviewY,PreviewW,PreviewH,WriteState);
  try{dockIds=HomeDock.Parse(File.ReadAllText(DockFile));}catch{dockIds=null;}
  PreloadArtwork();QueryInstalled();
  RefreshSource();UpdateRecent();selected=-1;BuildPage();
  clockTimer=new Tizen.NUI.Timer(30000);
  clockTimer.Tick+=(s,e)=>{if(clock!=null)clock.Text=DateTime.Now.ToString("h:mm tt");return true;};clockTimer.Start();WriteState("created");
 }
 protected override void OnResume()
 {
  base.OnResume();paused=false;clockTimer?.Start();RefreshSource();QueryInstalled();
  if(page==SettingsPage){RefreshSettings();UpdateFocus(false);}
  if(UpdateRecent()&&page==HomePage){selected=-1;BuildPage();}else SyncPreview();
  WriteState("resumed");
 }
 protected override void OnAppControlReceived(AppControlReceivedEventArgs e){base.OnAppControlReceived(e);string requested; if(e.ReceivedAppControl.ExtraData.TryGet("quiet_page",out requested)&&requested=="settings")OpenSettings("settings_request");else if(requested=="apps"){page=AppsPage;onTabs=false;selected=0;BuildPage();}else Home("home_request");}
 protected override void OnPause(){base.OnPause();paused=true;editing="";holdTimer?.Stop();selectHeld=false;if(menuOptions!=null)CloseMenu();if(moving!=null)EndMove(false);clockTimer?.Stop();preview.Release("pause");WriteState("paused");}
 protected override void OnTerminate(){preview?.Release("terminate");base.OnTerminate();}
 string DockFile=>DirectoryInfo.Data+"dock.json";
 void Home(string activity)
 {
  clockTimer?.Start();RefreshSource();editing="";
  bool rebuild=UpdateRecent()||page!=HomePage;
  page=HomePage;onTabs=false;selected=-1;
  if(rebuild)BuildPage();else{selected=DefaultFocus();UpdateFocus();SyncPreview();}
  WriteState(activity);
 }
 void OpenSettings(string activity){clockTimer?.Start();page=SettingsPage;onTabs=false;selected=0;editing="";BuildPage();WriteState(activity);}
 // Returns whether Home now leads with something else. In memory only.
 bool UpdateRecent()
 {
  string current=VconfString("memory/appfw/current_fullscreen_appid"),previous=VconfString("memory/appfw/previous_fullscreen_appid");
  string before=recent.Key;recentFrom=current+"|"+previous;
  recent=Recent.Pick(current,previous,OwnId,library,lastSource,recent);
  return recent.Key!=before;
 }
 static string VconfString(string key)
 {
  try{IntPtr p=vconf_get_str(key);if(p==IntPtr.Zero)return null;string v=Marshal.PtrToStringAnsi(p);free(p);return v;}catch{return null;}
 }
 void RefreshSource(){
  try{IntPtr p=get_source_list();if(p!=IntPtr.Zero){string json=Marshal.PtrToStringAnsi(p);free(p);var parsed=Sources.Parse(json);if(parsed.Count>0){inputs.Clear();inputs.AddRange(parsed);}}}catch{}
  try{int n;if(vconf_get_int("memory/system/source_type",out n)==0&&n>0)lastSource=n;}catch{}
  if(inputs.Count>0&&!inputs.Exists(s=>s.SourceType==lastSource))lastSource=inputs[0].SourceType;
 }
 int SourceIndex(){for(int i=0;i<inputs.Count;i++)if(inputs[i].SourceType==lastSource)return i;return 0;}
 View Add(View v){scene.Add(v);Window.Default.Add(v);return v;}
 View Panel(int x,int y,int w,int h,Color color,int radius=18){return Add(new View{Position2D=new Position2D(x,y),Size2D=new Size2D(w,h),BackgroundColor=color,CornerRadius=new Vector4(radius,radius,radius,radius)});}
 // A slot's card is positioned by its center so focus can scale it in place.
 View Card(int x,int y,int w,int h,Color color,int radius)
 {var card=Panel(x+w/2,y+h/2,w,h,color,radius);card.PivotPoint=new Position(.5f,.5f,.5f);card.PositionUsesPivotPoint=true;return card;}
 TextLabel Text(string text,int x,int y,int w,int h,float size,Color color,string face="Medium")
 {return (TextLabel)Add(Label(text,x,y,w,h,size,color,face));}
 TextLabel Label(string text,int x,int y,int w,int h,float size,Color color,string face="Medium")
 {return new TextLabel{Text=text,Position2D=new Position2D(x,y),Size2D=new Size2D(w,h),PixelSize=size,FontFamily="Quiet Inter "+face,TextColor=color,VerticalAlignment=VerticalAlignment.Center};}
 void BuildPage()
 {
  var stopwatch=Stopwatch.StartNew();
  if(focusAnimation!=null){focusAnimation.Stop();focusAnimation.Dispose();focusAnimation=null;}
  if(moveAnimation!=null){moveAnimation.Stop();moveAnimation.Dispose();moveAnimation=null;}
  DropArrows();moving=null;moveIds=null;
  ParkArtwork();pageArtworkCold=0;
  preview.Detach();
  foreach(var v in scene){Window.Default.Remove(v);v.Dispose();}scene.Clear();slots.Clear();tabs=null;
  if(page==HomePage)BuildHome();
  else{
   Text(page==AppsPage?"Your Apps":page==InputsPage?"Inputs":"Quick Settings",76,55,1350,100,76,ink,"Display");
   tabs=new TextLabel[PageNames.Length];int[] tx={54,218,363,530};int[] tw={150,131,153,182};
   for(int i=0;i<tabs.Length;i++){tabs[i]=Text(PageNames[i],tx[i],158,tw[i],68,30,muted);tabs[i].HorizontalAlignment=HorizontalAlignment.Center;}
   clock=Text(DateTime.Now.ToString("h:mm tt"),1540,170,300,45,24,muted);clock.HorizontalAlignment=HorizontalAlignment.End;
   if(page==AppsPage){
    BuildLibrary();
   }else if(page==InputsPage){
    BuildInputs();
   }else{
    RefreshSettings();
    SettingsTile("Volume","volume","Select · Left / Right to adjust","volume",0,0);
    SettingsTile("Brightness","brightness","Select · Left / Right to adjust","brightness",1,0);
    SettingsTile("Game Mode","game-mode","Select to switch on or off","game",2,0);
    SettingsTile("Sleep Timer","sleeptimer","Select to change the timer","sleep",3,0);
    SettingsTile("Sound","mute","Select to mute or unmute","mute",0,1);
    SettingsTile("Controllers","controllers","DualSense · hold PS + Create","controller",1,1);
    SettingsTile("Network","network","Wi-Fi and connection setup","network",2,1);
    SettingsTile("Advanced","app:org.tizen.menu","Open Samsung's full settings","settings",3,1);
   }
   moveDim=null;
   detail=Text("",80,1030,1320,35,20,muted);
   notice=Text("",80,1008,1760,34,22,accent);
   pageHint=Text(page==AppsPage?(AppLibrary.PageCount(library.Count)<=1?"Hold Select for options · Back to Home":libraryPage+1<AppLibrary.PageCount(library.Count)?"Hold Select for options · Down for more":"Hold Select for options · Up for previous"):"Back to Home",1110,1030,730,35,20,muted);pageHint.HorizontalAlignment=HorizontalAlignment.End;
  }
  if(selected<0)selected=DefaultFocus();
  selected=Math.Max(0,Math.Min(selected,slots.Count-1));UpdateFocus(false);
  buildMilliseconds=stopwatch.ElapsedMilliseconds;SyncPreview();WriteState("page_ready");
 }
 // A virtual screen is assigned only while Home shows a recent app and Quiet Home is in front.
 void SyncPreview()
 {
  if(paused)preview.Release("paused");
  else if(page==HomePage&&recent is RecentItem.App a)preview.Show(a.Item.Id);
  else preview.ReleaseSoon();
 }
 // Home leads with what the owner was last in: its preview on the right, its
 // name and Resume on the left, the dock below, inputs and settings as pills.
 void BuildHome()
 {
  Pill("Inputs","page:"+InputsPage,"hdmi",17,80,136);
  Pill("Settings","page:"+SettingsPage,"settings",24,224,160);
  clock=Text(DateTime.Now.ToString("h:mm tt"),1524,66,300,44,30,dim);clock.HorizontalAlignment=HorizontalAlignment.End;
  if(recent is RecentItem.Nothing){var empty=Text("Pick an app to start",96,630,1000,70,48,homeInk,"Semibold");empty.VerticalAlignment=VerticalAlignment.Bottom;}
  else{
   // Drawn under the preview, so only its border shows around it.
   var ring=Panel(PreviewX-12,PreviewY-12,PreviewW+24,PreviewH+24,black,12);
   ring.BorderlineWidth=4;ring.BorderlineColor=white;ring.BorderlineOffset=-1;ring.Opacity=0;
   Still();
   if(recent is RecentItem.App){var hole=preview.Hole();if(hole!=null)Add(hole);}
   var name=Text(RecentName(),96,550,512,150,60,homeInk,"Display");name.MultiLine=true;name.Ellipsis=true;name.VerticalAlignment=VerticalAlignment.Bottom;
   var resume=Card(96,732,208,60,raised,30);
   var play=new ImageView{ResourceUrl=Root+"symbol-play.svg",Position2D=new Position2D(24,18),Size2D=new Size2D(24,24),Color=homeInk};resume.Add(play);
   var label=Label("Resume",60,0,140,60,30,homeInk,"Semibold");resume.Add(label);
   slots.Add(new Slot{Kind=SlotKind.Resume,Card=resume,FocusRing=ring,Icon=play,Label=label,Name="Resume",Action="resume",Description="",Row=ResumeRow,X=96,Y=732,W=208,H=60});
  }
  var docked=DockApps();
  for(int i=0;i<docked.Count;i++)Dock(docked[i].Name,"app:"+docked[i].Id,i);
  Dock("Your Apps","page:"+AppsPage,docked.Count);
  tileName=Text("",0,984,TileNameW,32,24,homeInk,"Semibold");tileName.HorizontalAlignment=HorizontalAlignment.Center;
  pageHint=Text("Hold Select for options",1124,986,700,28,20,dim);pageHint.HorizontalAlignment=HorizontalAlignment.End;
  moveDim=Add(new View{Size2D=new Size2D(1920,1080),BackgroundColor=new Color(0,0,0,.5f),Opacity=0});
  detail=Text("",824,986,1000,28,20,dim);detail.HorizontalAlignment=HorizontalAlignment.End;
  notice=Text("",416,70,1088,36,24,homeInk);notice.HorizontalAlignment=HorizontalAlignment.Center;
 }
 // What the preview shows when the app is not live: its tile, or an input's card art.
 void Still()
 {
  Panel(PreviewX,PreviewY,PreviewW,PreviewH,shelf,0);
  if(recent is RecentItem.App a){const int size=256;TileCard(PreviewX+(PreviewW-size)/2,PreviewY+(PreviewH-size)/2,a.Item,size);return;}
  var art=Sources.Art(RecentSource());
  if(art.Kind==SourceArtKind.Illustration)Add(new ImageView{ResourceUrl=Root+art.Path,Position2D=new Position2D(PreviewX,PreviewY),Size2D=new Size2D(PreviewW,PreviewH)});
  else Add(new ImageView{ResourceUrl=art.Path,Position2D=new Position2D(PreviewX+(PreviewW-256)/2,PreviewY+(PreviewH-256)/2),Size2D=new Size2D(256,256)});
 }
 SourceInfo RecentSource()=>recent is RecentItem.Input i?inputs.Find(s=>s.SourceType==i.SourceType):null;
 string RecentName()=>recent switch{
  RecentItem.App a=>a.Item.Name,
  RecentItem.Input i=>RecentSource() is SourceInfo s?Sources.Label(s):Sources.DefaultName(i.SourceType),
  _=>""};
 void Pill(string label,string action,string icon,int iconWidth,int x,int w)
 {
  var card=Card(x,64,w,48,clear,24);
  var glyph=new ImageView{ResourceUrl=Root+"symbol-"+icon+".png",Position2D=new Position2D(16+(24-iconWidth)/2,12),Size2D=new Size2D(iconWidth,24),Color=dim};card.Add(glyph);
  var text=Label(label,48,0,w-48,48,24,dim);card.Add(text);
  slots.Add(new Slot{Kind=SlotKind.Pill,Card=card,Icon=glyph,Label=text,Name=label,Action=action,Description=label,Row=PillRow,X=x,Y=64,W=w,H=48});
 }
 void BuildLibrary()
 {
  if(library.Count==0){Text("Apps could not be loaded.",80,300,1500,70,32,ink);return;}
  libraryPage=Math.Clamp(libraryPage,0,AppLibrary.PageCount(library.Count)-1);
  int start=libraryPage*AppLibrary.PageSize,count=AppLibrary.VisibleCount(library.Count,libraryPage);
  for(int i=0;i<count;i++)
  {
   var app=library[start+i];int col=i%AppLibrary.Columns,row=i/AppLibrary.Columns,x=80+col*GridPitchX,y=270+row*GridPitchY;
   var card=TileCard(x,y,app);
   var label=Text(app.Name,x-(GridPitchX-Tile)/2,y+Tile+12,GridPitchX-10,34,22,muted);
   label.HorizontalAlignment=HorizontalAlignment.Center;label.Ellipsis=true;
   slots.Add(new Slot{Kind=SlotKind.Tile,Card=card,FocusRing=FocusRing(card),Label=label,Name=app.Name,Action="app:"+app.Id,Description=app.Description,Row=row,X=x,Y=y,W=Tile,H=Tile});
  }
  Text(library.FindAll(a=>!a.Utility).Count+" apps",80,218,500,30,21,muted);
  if(AppLibrary.PageCount(library.Count)>1){
   var pages=Text((libraryPage+1)+" / "+AppLibrary.PageCount(library.Count),1650,218,190,30,21,muted);
   pages.HorizontalAlignment=HorizontalAlignment.End;
  }
 }
 Color HexColor(string hex)
 {
  try{int n=Convert.ToInt32(hex.TrimStart('#'),16);return new Color(((n>>16)&255)/255f,((n>>8)&255)/255f,(n&255)/255f,1);}
  catch{return white;}
 }
 View TileCard(int x,int y,LibraryApp app,int size=Tile)
 {
  var card=SquircleCard(x,y,HexColor(app.Background),size);
  AddAppArtwork(card,app,size,size);return card;
 }
 View SquircleCard(int x,int y,Color color,int size=Tile)
 {
  var card=Card(x,y,size,size,color,size/2);
  card.CornerSquareness=new Vector4(TileSquareness,TileSquareness,TileSquareness,TileSquareness);return card;
 }
 // The white inner outline Samsung's home draws on a focused squircle tile.
 View FocusRing(View card)
 {
  View ring=File.Exists(FocusStroke)?new ImageView{ResourceUrl=FocusStroke}:new View{BackgroundColor=new Color(0,0,0,0),
   CornerRadius=new Vector4(Tile/2,Tile/2,Tile/2,Tile/2),CornerSquareness=new Vector4(TileSquareness,TileSquareness,TileSquareness,TileSquareness),
   BorderlineWidth=4,BorderlineColor=white,BorderlineOffset=-1};
  ring.Size2D=new Size2D(Tile,Tile);ring.Opacity=0;card.Add(ring);return ring;
 }
 void AddAppArtwork(View card,LibraryApp app,int w,int h)
 {
  if(app.Fit=="name")card.Add(NameLabel(app.Name,w,h));
  else if(!app.Utility){
   var cached=GetArtwork(app,w,h);
   if(cached.Image.LoadingStatus!=ImageView.LoadingStatusType.Ready)pageArtworkCold++;
   cached.Parent.Remove(cached.Image);card.Add(cached.Image);cached.Parent=card;
   if(cached.Image.LoadingStatus==ImageView.LoadingStatusType.Failed)card.Add(NameLabel(app.Name,w,h));
  }
  if(app.Utility){
   card.Add(new View{Position2D=new Position2D(w/2-20,h/2-2),Size2D=new Size2D(40,4),BackgroundColor=ink});
   card.Add(new View{Position2D=new Position2D(w/2-2,h/2-20),Size2D=new Size2D(4,40),BackgroundColor=ink});
  }
 }
 TextLabel NameLabel(string name,int w,int h)=>new TextLabel{Text=name,Size2D=new Size2D(w,h),PixelSize=h/5f,FontFamily="Quiet Inter Semibold",TextColor=white,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,MultiLine=true};
 void PreloadArtwork()
 {
  // Keep the real image views alive on the scene; moving between pages never
  // destroys their decoded textures. The zero-opacity parent is never drawn.
  artworkPreloadRoot=new View{Size2D=new Size2D(1920,1080),Opacity=0};
  Window.Default.Add(artworkPreloadRoot);
  foreach(var app in library)if(AppLibrary.HasImage(app))GetArtwork(app,Tile,Tile);
 }
 List<LibraryApp> DockApps()=>HomeDock.Resolve(dockIds,library);
 List<string> DockIds()=>DockApps().ConvertAll(a=>a.Id);
 // The installed-app query is asynchronous; a main-loop timer applies its result
 // so every scene change stays on the UI thread.
 void QueryInstalled()
 {
  if(installedQuery!=null&&!installedQuery.IsCompleted)return;
  try{installedQuery=ApplicationManager.GetInstalledApplicationsAsync();}catch(Exception ex){librarySource="catalog:"+ex.GetType().Name;return;}
  installedPoll?.Stop();
  installedPoll=new Tizen.NUI.Timer(100);
  installedPoll.Tick+=(s,e)=>{if(!installedQuery.IsCompleted)return true;ApplyInstalled();return false;};
  installedPoll.Start();
 }
 void ApplyInstalled()
 {
  var tv=new List<InstalledApp>();
  try{foreach(var a in installedQuery.Result)tv.Add(new InstalledApp{Id=a.ApplicationId,Label=a.Label,IconPath=a.IconPath,Type=a.ApplicationType,NoDisplay=a.IsNoDisplay});}
  catch(Exception ex){librarySource="catalog:"+ex.GetBaseException().GetType().Name;WriteState("library_failed");return;}
  EnrichFromStore(tv);
  var merged=AppLibrary.Merge(curated,tv);
  bool changed=merged.Count!=library.Count||!merged.TrueForAll(a=>library.Exists(b=>b.Id==a.Id&&b.Asset==a.Asset));
  library=merged;librarySource="installed:"+tv.Count;
  if(changed){foreach(var app in library)if(AppLibrary.HasImage(app))GetArtwork(app,Tile,Tile);
   UpdateRecent();
   if(menuOptions!=null||moving!=null)pendingBuild=true;else if(editing==""&&(page==HomePage||page==AppsPage))BuildPage();}
  WriteState("library_ready");
 }
 // Samsung's app service knows each app's store ID, whether it can be removed,
 // and the square tiles its own home draws. Package icons are often placeholders.
 void EnrichFromStore(List<InstalledApp> tv)
 {
  try{
   var store=new Dictionary<string,DownloadableApp>();
   foreach(var d in new AppDB().GetAllAppList())if(!string.IsNullOrEmpty(d.AppId))store[d.AppId]=d;
   foreach(var a in tv){
    if(!store.TryGetValue(a.Id,out var d))continue;
    a.StoreId=d.StoreId??"";a.Removable=d.IsRemovable;
    string dir=d.IconPath??"";
    if(Directory.Exists(dir)){string best=AppLibrary.BestIcon(Array.ConvertAll(Directory.GetFiles(dir),System.IO.Path.GetFileName));if(best!="")a.IconPath=System.IO.Path.Combine(dir,best);}
   }
   storeSource="store:"+store.Count;
  }catch(Exception ex){storeSource="store:"+ex.GetBaseException().GetType().Name;}
 }
 CachedArtwork GetArtwork(LibraryApp app,int w,int h)
 {
  string key=app.Id+"@"+w+"x"+h;
  if(artworkCache.TryGetValue(key,out var cached)){
   if(cached.Asset==app.Asset&&cached.Fit==app.Fit)return cached;
   // The installed-app query replaced catalog art with the TV's tile.
   cached.Parent.Remove(cached.Image);cached.Image.Dispose();artworkCache.Remove(key);
  }
  // TV tiles fill the square; curated stand-in art sits inside it.
  bool tile=app.Fit=="tile";
  int iw=tile?w:w-24,ih=tile?h:h-24;var crop=app.Crop;
  float cropWidth=crop==null?1:crop[2],cropHeight=crop==null?1:crop[3];
  var image=new ImageView{
   Position2D=new Position2D((w-iw)/2,(h-ih)/2),Size2D=new Size2D(iw,ih),
   FittingMode=FittingModeType.ShrinkToFit,
   SamplingMode=SamplingModeType.BoxThenLanczos,
   DesiredWidth=(int)Math.Ceiling(iw*2/cropWidth),DesiredHeight=(int)Math.Ceiling(ih*2/cropHeight),
   ReleasePolicy=ReleasePolicyType.Destroyed};
  if(tile){image.CornerRadius=new Vector4(w/2f,w/2f,w/2f,w/2f);image.CornerSquareness=new Vector4(TileSquareness,TileSquareness,TileSquareness,TileSquareness);}
  if(crop!=null)image.PixelArea=new RelativeVector4(crop[0],crop[1],crop[2],crop[3]);
  cached=new CachedArtwork{Image=image,Parent=artworkPreloadRoot,Asset=app.Asset,Fit=app.Fit};
  artworkCache.Add(key,cached);artworkViewsCreated++;
  image.ResourceReady+=(s,e)=>{
   if(image.LoadingStatus==ImageView.LoadingStatusType.Failed&&cached.Parent!=artworkPreloadRoot)cached.Parent.Add(NameLabel(app.Name,w,h));
   WriteState("artwork_ready");};
  artworkPreloadRoot.Add(image);image.ResourceUrl=System.IO.Path.IsPathRooted(app.Asset)?app.Asset:Root+app.Asset;
  return cached;
 }
 void ParkArtwork()
 {
  foreach(var cached in artworkCache.Values)if(cached.Parent!=artworkPreloadRoot){
   cached.Parent.Remove(cached.Image);artworkPreloadRoot.Add(cached.Image);cached.Parent=artworkPreloadRoot;
  }
 }
 int ArtworkReadyCount()
 {
  int count=0;foreach(var cached in artworkCache.Values)
   if(cached.Image.LoadingStatus==ImageView.LoadingStatusType.Ready)count++;
  return count;
 }
 string ArtworkPending()
 {
  var pending=new List<string>();
  foreach(var entry in artworkCache)
   if(entry.Value.Image.LoadingStatus!=ImageView.LoadingStatusType.Ready)
    pending.Add(entry.Key+":"+entry.Value.Image.LoadingStatus);
  return string.Join(";",pending);
 }
 void SettingsTile(string name,string action,string description,string icon,int column,int row)
 {
  int x=80+column*450,y=262+row*332,w=410,h=286;
  var card=Card(x,y,w,h,clear,24);
  card.Add(new ImageView{ResourceUrl=Root+"quick-"+icon+".svg",Size2D=new Size2D(w,h)});
  card.Add(Label(name,26,25,348,42,29,white,"Semibold"));
  var value=Label("",26,151,358,56,38,white,"Display");card.Add(value);
  card.Add(Label(description,26,226,370,34,20,new Color(.95f,.96f,1,1)));
  slots.Add(new Slot{Kind=SlotKind.Setting,Card=card,Label=value,Name=name,Action=action,Description=description,Row=row,X=x,Y=y,W=w,H=h});
 }
 // One card per external input the TV reports. The 430 cap keeps a single input
 // as a portrait card rather than stretching it across the whole row.
 void BuildInputs()
 {
  if(inputs.Count==0){InputCard(null,745,262,430,442);return;}
  int gap=40,n=inputs.Count,h=442,y=262;
  int w=Math.Min((1760-gap*(n-1))/n,430);
  int startX=80+(1760-(w*n+gap*(n-1)))/2;
  for(int i=0;i<n;i++)InputCard(inputs[i],startX+i*(w+gap),y,w,h);
 }
 void InputCard(SourceInfo s,int x,int y,int w,int h)
 {
  string label=s==null?"Open settings":Sources.Label(s);
  string action=s==null?"app:org.tizen.menu":"hdmi:"+s.SourceType;
  string port=s==null?"":Sources.DefaultName(s.SourceType);
  string description=s==null?"Samsung inputs and settings":(label==port?"External input":port);
  var art=s==null?new SourceArt{Kind=SourceArtKind.Illustration,Path="feature-hdmi.svg"}:Sources.Art(s);
  var shadow=Add(new ImageView{ResourceUrl=Root+"shadow-portrait.svg",Position2D=new Position2D(x-24,y-10),Size2D=new Size2D(w+48,h+64)});
  var card=Card(x,y,w,h,clear,18);
  if(art.Kind==SourceArtKind.Illustration){
   card.Add(new ImageView{ResourceUrl=Root+art.Path,Size2D=new Size2D(w,h)});
  }else{
   card.BackgroundColor=new Color(.145f,.16f,.2f,1);
   int g=Math.Min(w,h)*2/5;
   card.Add(new ImageView{ResourceUrl=art.Path,Position2D=new Position2D((w-g)/2,(h-g)/2-20),Size2D=new Size2D(g,g)});
  }
  var caption=Text(label,x,716,w,45,31,ink,"Semibold");caption.HorizontalAlignment=HorizontalAlignment.Center;
  var sub=Text(description,x,760,w,36,24,muted);sub.HorizontalAlignment=HorizontalAlignment.Center;
  slots.Add(new Slot{Kind=SlotKind.Input,Card=card,Shadow=shadow,Label=caption,Name=label,Action=action,Description=description,Row=0,X=x,Y=y,W=w,H=h});
 }
 int DockX(int n,int at)=>DockLeft+n*DockPitch+(at<0||n==at?0:n<at?-MoveGap/2:MoveGap/2);
 void Dock(string label,string action,int n)
 {
  int x=DockX(n,-1),y=DockY;View card;
  var app=action.StartsWith("app:")?library.Find(a=>a.Id==action.Substring(4)):null;
  if(app!=null)card=TileCard(x,y,app);
  else{
   card=SquircleCard(x,y,new Color(1,1,1,.09f));
   card.Add(new ImageView{ResourceUrl=Root+"symbol-apps.png",Color=ink,Position2D=new Position2D(Tile/2-22,Tile/2-22),Size2D=new Size2D(44,44)});
  }
  slots.Add(new Slot{Kind=SlotKind.Tile,Card=card,FocusRing=FocusRing(card),Name=label,Action=action,Description=label,Row=DockRow,X=x,Y=y,W=Tile,H=Tile});
 }
 List<int> Row(int row){var r=new List<int>();for(int i=0;i<slots.Count;i++)if(slots[i].Row==row)r.Add(i);return r;}
 List<int> Rows(){var r=new List<int>();foreach(var s in slots)if(!r.Contains(s.Row))r.Add(s.Row);r.Sort();return r;}
 int DefaultFocus()
 {
  if(page!=HomePage)return 0;
  int resume=slots.FindIndex(s=>s.Kind==SlotKind.Resume);
  return resume>=0?resume:Math.Max(0,slots.FindIndex(s=>s.Row==DockRow));
 }
 // Settings keeps the column between its rows; Home's dock remembers its tile.
 int Enter(int row,int column)
 {
  var r=Row(row);
  if(page==SettingsPage)return r[Math.Min(column,r.Count-1)];
  if(page==HomePage&&row==DockRow)return r[Math.Min(dockFocus,r.Count-1)];
  return r[0];
 }
 void MoveVertical(int d)
 {
  if(onTabs){if(d>0&&slots.Count>0){onTabs=false;selected=Enter(Rows()[0],0);UpdateFocus();}return;}
  var rows=Rows();int row=slots[selected].Row,at=rows.IndexOf(row)+d;
  if(at<0){if(page==HomePage)return;onTabs=true;}
  else if(at<rows.Count)selected=Enter(rows[at],Row(row).IndexOf(selected));
  else return;
  UpdateFocus();
 }
 void MoveAcross(int d)
 {
  if(onTabs){int next=page+d;if(next<0||next>=PageNames.Length)return;page=next;onTabs=next!=HomePage;selected=next==HomePage?-1:0;BuildPage();return;}
  var r=Row(slots[selected].Row);int n=r.IndexOf(selected);selected=r[(n+d+r.Count)%r.Count];UpdateFocus();
 }
 void UpdateFocus(bool animate=true)
 {
  if(focusAnimation!=null){focusAnimation.Stop();focusAnimation.Dispose();focusAnimation=null;}
  if(animate){focusAnimation=new Animation(180);if(page==HomePage)focusAnimation.DefaultAlphaFunction=new AlphaFunction(new Vector2(.22f,1),new Vector2(.36f,1));}
  if(tabs!=null)for(int i=0;i<tabs.Length;i++){tabs[i].TextColor=i==page?ink:muted;tabs[i].FontFamily="Quiet Inter "+(i==page?"Semibold":"Medium");tabs[i].BackgroundColor=onTabs&&i==page?new Color(1,1,1,.14f):new Color(0,0,0,0);tabs[i].CornerRadius=new Vector4(34,34,34,34);}
  for(int i=0;i<slots.Count;i++){
   var s=slots[i];bool f=!onTabs&&selected==i;
   // Cards in the first row lift and carry a hairline when focused.
   bool card=(s.Kind==SlotKind.Input||s.Kind==SlotKind.Setting)&&s.Row==0;
   float scale=!f?1:s.Kind==SlotKind.Tile?TileFocusScale:s.Kind==SlotKind.Setting?1.035f:s.Kind==SlotKind.Input?1.05f:1;
   s.Card.BorderlineWidth=card?1:0;
   s.Card.BorderlineColor=new Color(1,1,1,f?.8f:.12f);
   var pos=new Position(s.X+s.W/2f,s.Y+s.H/2f-(f&&card?6:0),0);
   if(animate){focusAnimation.AnimateTo(s.Card,"Scale",new Vector3(scale,scale,1));focusAnimation.AnimateTo(s.Card,"Position",pos);}else{s.Card.Scale=new Vector3(scale,scale,1);s.Card.Position=pos;}
   if(s.Shadow!=null)s.Shadow.Opacity=f?1:.55f;
   if(s.FocusRing!=null)s.FocusRing.Opacity=f?1:0;
   if(s.Kind==SlotKind.Pill||s.Kind==SlotKind.Resume){
    s.Card.BackgroundColor=f?white:s.Kind==SlotKind.Pill?clear:raised;
    var c=f?black:s.Kind==SlotKind.Pill?dim:homeInk;s.Icon.Color=c;s.Label.TextColor=c;
   }else if(s.Label!=null&&s.Kind!=SlotKind.Setting){
    s.Label.FontFamily="Quiet Inter "+(f?"Semibold":"Medium");
    if(s.Kind==SlotKind.Tile)s.Label.TextColor=f?ink:muted;
   }
   if(f)s.Card.RaiseToTop();
  }
  if(focusAnimation!=null)focusAnimation.Play();
  if(page==HomePage)HomeHints();
  else{
   detail.Text=page==AppsPage&&!onTabs&&slots.Count>0?slots[selected].Name+" · "+slots[selected].Description:"";
   if(page==SettingsPage){
    slots[0].Label.Text=currentVolume<0?"Unavailable":currentVolume.ToString();
    slots[1].Label.Text=SettingName("brightness");
    slots[2].Label.Text=SettingName("game-mode");
    slots[3].Label.Text=SettingName("sleeptimer");
    slots[4].Label.Text=currentMute<0?"Unavailable":currentMute==1?"Muted":"Sound on";
    slots[5].Label.Text="Pair controller";
    slots[6].Label.Text="Connections";
    slots[7].Label.Text="More settings";
    detail.Text="Your everyday controls";
   }
   if(editing!="")detail.Text="Left / Right to adjust "+editing+" · Select or Back to finish";
  }
  WriteState("focus");
 }
 // The focused dock tile's name sits under it, and the options hint shows only for an app.
 // The dock also remembers that tile, wherever a Move left it, for the next Down.
 void HomeHints(Animation slide=null)
 {
  var s=onTabs||selected<0||selected>=slots.Count?null:slots[selected];
  bool dock=s!=null&&s.Row==DockRow;
  if(dock)dockFocus=Row(DockRow).IndexOf(selected);
  tileName.Text=dock?s.Name:"";
  if(dock){float x=s.X+Tile/2f-TileNameW/2f;if(slide!=null)slide.AnimateTo(tileName,"PositionX",x);else tileName.PositionX=x;}
  pageHint.Opacity=moving==null&&SelectedApp()!=null?1:0;
  detail.Text=moving!=null?"Left / Right to move · Select to keep · Back to cancel":"";
 }
 void OnKey(object sender,Window.KeyEventArgs e)
 {
  if(e.Key==null)return;string k=e.Key.KeyPressedName;bool select=k=="Return"||k=="KP_Enter";
  if(select&&e.Key.State==Key.StateType.Up){ReleaseSelect();return;}
  if(e.Key.State!=Key.StateType.Down||select&&selectHeld)return;
  long now=Environment.TickCount64;if(k==lastKey&&now-lastKeyAt<180)return;lastKey=k;lastKeyAt=now;
  try{
   if(k=="XF86Home"){if(menuOptions!=null)CloseMenu();if(moving!=null)EndMove(false);Home("home_key");return;}
   if(menuOptions!=null){MenuKey(k);return;}
   if(moving!=null){MoveKey(k);return;}
   if(k=="XF86SysMenu"||k=="XF86More"){OpenSettings("settings_key");return;}
   if(select&&editing==""&&SelectedApp()!=null){PressSelect();return;}
   if(page==AppsPage&&HandleLibraryKey(k))return;
   if(editing!=""){if(k=="Left"||k=="Right"){int d=k=="Left"?-1:1;if(editing=="volume")ChangeVolume(d);else ChangeBrightness(d);}else if(k=="Return"||k=="KP_Enter"||k=="XF86Back"||k=="Back"||k=="Escape"){editing="";UpdateFocus();}return;}
   if(k=="Up"||k=="Down")MoveVertical(k=="Up"?-1:1);
   else if(k=="Left"||k=="Right")MoveAcross(k=="Left"?-1:1);
   else if(select){if(onTabs){onTabs=false;selected=0;UpdateFocus();}else Activate();}
   else if(k=="XF86Back"||k=="Back"||k=="Escape"||k=="XF86Exit"){if(page==HomePage)ResumeRecent();else Home("back");}
  }catch(Exception ex){ShowError(ex.Message);}
 }
 bool HandleLibraryKey(string key)
 {
  if(key=="XF86Back"||key=="Back"||key=="Escape"||key=="XF86Exit"){Home("apps_back");return true;}
  if(library.Count==0)return key=="Up"||key=="Down";
  if(onTabs&&key=="Down"){onTabs=false;selected=0;UpdateFocus();return true;}
  if(onTabs)return false;
  if(key=="Left"||key=="Right"||key=="Up"||key=="Down"){
   int next=AppLibrary.Move(library.Count,libraryPage*AppLibrary.PageSize+selected,key);
   if(next<0){onTabs=true;UpdateFocus();return true;}
   int nextPage=next/AppLibrary.PageSize;selected=next%AppLibrary.PageSize;
   if(nextPage!=libraryPage){libraryPage=nextPage;BuildPage();}else UpdateFocus();
   return true;
  }
  return false;
 }
 void Activate()
 {
  string a=slots[selected].Action;notice.Text="";
  if(a.StartsWith("hdmi:")){SelectSource(int.Parse(a.Substring(5)));return;}
  if(a=="resume"){ResumeRecent();return;}
  if(a.StartsWith("page:")){page=int.Parse(a.Substring(5));selected=page==InputsPage?SourceIndex():0;onTabs=false;BuildPage();return;}
  if(a.StartsWith("app:")){Launch(a.Substring(4));return;}
  if(a=="controllers"){Launch("org.tizen.menu","add-bluetooth-keyboard");return;}
  if(a=="network"){Launch("org.tizen.menu","networksettings");return;}
  if(a=="volume"||a=="brightness"){RefreshSettings();if(a=="brightness"&&!CanEdit(a)){ShowError("Brightness is unavailable for the current input.");return;}editing=a;UpdateFocus();return;}
  if(a=="game-mode"){RefreshSettings();SetSetting(a,SettingValue(a)=="1"?"0":"1");return;}
  if(a=="sleeptimer"){RefreshSettings();int n;if(int.TryParse(SettingValue(a),out n))SetSetting(a,((n+1)%5).ToString());return;}
  if(a=="mute"){int n;int r=sound_manager_get_master_mute(out n);if(r==0)r=sound_manager_set_master_mute(n==1?0:1);if(r!=0)ShowError("Use the remote's mute button.");RefreshSettings();UpdateFocus();WriteState("mute_changed");}
 }
 // Apps open when Select is released; holding it opens their options instead.
 LibraryApp SelectedApp()
 {
  if(onTabs||selected<0||selected>=slots.Count)return null;var s=slots[selected];
  if(s.Kind!=SlotKind.Tile||!s.Action.StartsWith("app:"))return null;
  var app=library.Find(a=>a.Id==s.Action.Substring(4));return app==null||app.Utility?null:app;
 }
 void PressSelect()
 {
  selectHeld=true;selectConsumed=false;holdTimer?.Stop();
  holdTimer=new Tizen.NUI.Timer(600);
  holdTimer.Tick+=(s,e)=>{if(selectHeld&&!selectConsumed){selectConsumed=true;OpenOptions();}return false;};
  holdTimer.Start();
 }
 void ReleaseSelect()
 {
  holdTimer?.Stop();bool tap=selectHeld&&!selectConsumed;selectHeld=false;
  if(tap){try{Activate();}catch(Exception ex){ShowError(ex.Message);}}
 }
 void OpenOptions(){var app=SelectedApp();if(app!=null)ShowMenu(app.Name,null,AppOptions.For(app,DockIds(),page==HomePage),app);}
 void ShowMenu(string title,string message,List<string> options,LibraryApp app)
 {
  ClearOverlay();menuOptions=options;menuIndex=0;menuApp=app;
  int w=680,rowH=78,top=message==null?118:190,h=top+options.Count*rowH+30,x=(1920-w)/2,y=(1080-h)/2;
  Overlay(new View{Size2D=new Size2D(1920,1080),BackgroundColor=new Color(0,0,0,.45f)});
  var panel=Overlay(new View{Position2D=new Position2D(x,y),Size2D=new Size2D(w,h),BackgroundColor=new Color(.13f,.14f,.17f,1),CornerRadius=new Vector4(28,28,28,28)});
  panel.Add(Label(title,40,34,w-80,56,36,ink,"Semibold"));
  if(message!=null){var m=Label(message,40,100,w-80,76,24,muted);m.MultiLine=true;panel.Add(m);}
  for(int i=0;i<options.Count;i++){
   var row=new View{Position2D=new Position2D(24,top+i*rowH),Size2D=new Size2D(w-48,rowH-10),CornerRadius=new Vector4(18,18,18,18)};
   var label=Label(OptionText[options[i]],26,0,w-100,rowH-10,29,ink);row.Add(label);panel.Add(row);menuRows.Add(row);menuLabels.Add(label);
  }
  UpdateMenuFocus();WriteState("menu:"+string.Join("|",options));
 }
 View Overlay(View v){overlay.Add(v);Window.Default.Add(v);return v;}
 void ClearOverlay(){foreach(var v in overlay){Window.Default.Remove(v);v.Dispose();}overlay.Clear();menuRows.Clear();menuLabels.Clear();}
 void UpdateMenuFocus()
 {
  for(int i=0;i<menuRows.Count;i++){bool f=i==menuIndex;menuRows[i].BackgroundColor=f?accent:new Color(0,0,0,0);menuLabels[i].TextColor=f?white:ink;menuLabels[i].FontFamily="Quiet Inter "+(f?"Semibold":"Medium");}
 }
 void CloseMenu()
 {
  ClearOverlay();menuOptions=null;menuApp=null;
  if(pendingBuild){pendingBuild=false;BuildPage();}
  WriteState("menu_closed");
 }
 void MenuKey(string k)
 {
  if(k=="Up"||k=="Down"){menuIndex=(menuIndex+(k=="Up"?-1:1)+menuOptions.Count)%menuOptions.Count;UpdateMenuFocus();}
  else if(k=="Return"||k=="KP_Enter")ChooseOption(menuOptions[menuIndex]);
  else if(k=="XF86Back"||k=="Back"||k=="Escape"||k=="XF86Exit")CloseMenu();
 }
 void ChooseOption(string option)
 {
  var app=menuApp;
  // Cancel is listed first so a stray Select never deletes anything.
  if(option=="delete"){ShowMenu("Delete "+app.Name+"?","This removes the app and its data from the TV.",new List<string>{"cancel","confirm-delete"},app);return;}
  CloseMenu();
  if(option=="open")Launch(app.Id);
  else if(option=="add")SaveDock(HomeDock.Add(DockIds(),app.Id),app.Id,"Added "+app.Name+" to Home.");
  else if(option=="remove")SaveDock(HomeDock.Remove(DockIds(),app.Id),null,"Removed "+app.Name+" from Home.");
  else if(option=="move")StartMove(app);
  else if(option=="confirm-delete")StartUninstall(app);
 }
 // Like Samsung's own reorder: the dock parts around the app, Left/Right slide it
 // one slot per press, Select keeps the new order and Back puts it back.
 void StartMove(LibraryApp app)
 {
  int at=slots.FindIndex(s=>s.Row==DockRow&&s.Action=="app:"+app.Id);if(at<0)return;
  onTabs=false;selected=at;UpdateFocus(false);notice.Text="";
  moveIds=DockIds();moving=app.Id;
  // Like Samsung's dim, everything but the dock row and its instructions sits under it.
  moveDim?.RaiseToTop();foreach(var i in Row(DockRow))slots[i].Card.RaiseToTop();tileName.RaiseToTop();detail.RaiseToTop();notice.RaiseToTop();
  PlaceDock();WriteState("move_started:"+app.Id);
 }
 void MoveKey(string k)
 {
  if(k=="Left"||k=="Right"){var next=HomeDock.Move(moveIds,moving,k=="Left"?-1:1);if(string.Join("|",next)!=string.Join("|",moveIds)){moveIds=next;ArrangeDock(moveIds);PlaceDock();WriteState("moving:"+string.Join("|",moveIds));}}
  else if(k=="Return"||k=="KP_Enter")EndMove(true);
  else if(k=="XF86Back"||k=="Back"||k=="Escape"||k=="XF86Exit")EndMove(false);
 }
 // Dock navigation reads its order from slots, so a move reorders them; focus follows its tile.
 void ArrangeDock(List<string> ids)
 {
  var dock=Row(DockRow);var old=dock.ConvertAll(i=>slots[i]);var focus=slots[selected];
  var order=new List<Slot>();foreach(var id in ids){var s=old.Find(x=>x.Action=="app:"+id);if(s!=null)order.Add(s);}
  order.AddRange(old.FindAll(s=>!order.Contains(s)));
  for(int n=0;n<dock.Count;n++)slots[dock[n]]=order[n];selected=slots.IndexOf(focus);
 }
 // Stopping the previous slide leaves each tile where it is, so a press mid-slide continues from there.
 void PlaceDock()
 {
  if(moveAnimation!=null){moveAnimation.Stop();moveAnimation.Dispose();moveAnimation=null;}
  moveAnimation=new Animation(300){DefaultAlphaFunction=new AlphaFunction(new Vector2(.3f,0),new Vector2(.15f,1))};
  var dock=Row(DockRow);int at=moving==null?-1:dock.FindIndex(i=>slots[i].Action=="app:"+moving);
  for(int n=0;n<dock.Count;n++){
   var s=slots[dock[n]];s.X=DockX(n,at);
   moveAnimation.AnimateTo(s.Card,"Position",new Position(s.X+s.W/2f,s.Y+s.H/2f,0));
   moveAnimation.AnimateTo(s.Card,"Opacity",at>=0&&!s.Action.StartsWith("app:")?.1f:1f);
  }
  if(moveDim!=null)moveAnimation.AnimateTo(moveDim,"Opacity",at>=0?1f:0f);
  if(at<0)DropArrows();
  else{
   var card=slots[dock[at]].Card;
   if(moveArrows==null){moveArrows=new View[2];for(int i=0;i<2;i++){moveArrows[i]=new ImageView{ResourceUrl=Root+"move-arrow.svg",Size2D=new Size2D(MoveArrow,MoveArrow),PivotPoint=new Position(.5f,.5f,.5f),PositionUsesPivotPoint=true,Position=new Position(i==0?-MoveArrow/2:Tile+MoveArrow/2,Tile/2,0),Orientation=new Rotation(new Radian(new Degree(i*180)),PositionAxis.Z)};card.Add(moveArrows[i]);}}
   int p=moveIds.IndexOf(moving);if(p>0)moveArrows[0].Show();else moveArrows[0].Hide();if(p<moveIds.Count-1)moveArrows[1].Show();else moveArrows[1].Hide();
  }
  HomeHints(moveAnimation);
  moveAnimation.Play();
 }
 void DropArrows(){if(moveArrows!=null)foreach(var a in moveArrows){a.Unparent();a.Dispose();}moveArrows=null;}
 void EndMove(bool keep)
 {
  var ids=moveIds;moving=null;moveIds=null;
  bool saved=keep&&WriteDock(ids);
  if(!saved)ArrangeDock(DockIds());
  PlaceDock();
  if(pendingBuild){pendingBuild=false;BuildPage();}
  WriteState(saved?"move_saved":"move_cancelled");
 }
 bool WriteDock(List<string> ids)
 {
  try{File.WriteAllText(DockFile,HomeDock.Serialize(ids));}
  catch(Exception ex){ShowError("Home could not be saved.");WriteState("dock_save_failed:"+ex.GetType().Name);return false;}
  dockIds=ids;return true;
 }
 void SaveDock(List<string> ids,string focusId,string message)
 {
  if(!WriteDock(ids))return;
  if(page==HomePage){
   BuildPage();int at=focusId==null?-1:slots.FindIndex(s=>s.Action=="app:"+focusId);
   var dock=Row(DockRow);onTabs=false;selected=at>=0?at:dock.Count>0?dock[0]:selected;UpdateFocus(false);
  }
  notice.Text=message;WriteState("dock:"+string.Join("|",ids));
 }
 // Samsung's app service performs the uninstall; its callback may arrive on
 // another thread, so a main-loop timer applies the outcome.
 void StartUninstall(LibraryApp app)
 {
  if(uninstalling!=null){ShowError("Still deleting "+uninstalling.Name+".");return;}
  try{
   appService??=new AppManager();uninstallOutcome=null;
   uninstallHandler=(storeId,type,progress,error,data)=>{
    if(type==AppManager.CallBackEventType.WAS_EVENT_UNINSTALL_SUCCESS)uninstallOutcome="ok";
    else if(type==AppManager.CallBackEventType.WAS_EVENT_UNINSTALL_FAILED)uninstallOutcome="failed:"+error;};
   appService.Uninstall(new InstallControl(app.StoreId),uninstallHandler);
  }catch(Exception ex){ShowError(app.Name+" could not be deleted.");WriteState("uninstall_failed:"+app.Id+":"+ex.GetBaseException().GetType().Name);return;}
  uninstalling=app;notice.Text="Deleting "+app.Name+"…";WriteState("uninstall_started:"+app.Id);
  int waited=0;uninstallPoll?.Stop();uninstallPoll=new Tizen.NUI.Timer(250);
  uninstallPoll.Tick+=(s,e)=>{waited+=250;string outcome=uninstallOutcome;if(outcome==null&&waited<120000)return true;FinishUninstall(outcome??"timeout");return false;};
  uninstallPoll.Start();
 }
 void FinishUninstall(string outcome)
 {
  var app=uninstalling;uninstalling=null;
  if(outcome=="ok"){
   if(dockIds!=null&&dockIds.Contains(app.Id))SaveDock(HomeDock.Remove(dockIds,app.Id),null,"");
   notice.Text=app.Name+" was deleted.";QueryInstalled();
  }else ShowError(app.Name+" could not be deleted.");
  WriteState("uninstall:"+app.Id+":"+outcome);
 }
 void RefreshSettings(){
  currentVolume=sound_manager_get_master_volume(out var v)==0?v:-1;
  currentMute=sound_manager_get_master_mute(out var m)==0?m:-1;
  foreach(var id in new[]{"brightness","game-mode","sleeptimer"}){
   try{if(CSharpGetItemValue(id,out var name,out var value)&&!string.IsNullOrEmpty(value)){settingValues[id]=value;settingNames[id]=name;}else{settingValues.Remove(id);settingNames.Remove(id);}
    settingEnabled[id]=CSharpGetItemEnable(id,out var enabled)&&enabled;
   }catch{settingValues.Remove(id);settingNames.Remove(id);settingEnabled[id]=false;}
  }
  try{if(!CSharpGetItemMinMaxStepValue("brightness",out brightnessMin,out brightnessMax,out brightnessStep)||brightnessStep<=0){brightnessMin=0;brightnessMax=50;brightnessStep=1;}}catch{}
 }
 string SettingValue(string id){return settingValues.TryGetValue(id,out var v)?v:"";}
 string SettingName(string id){return settingNames.TryGetValue(id,out var v)?v:"Unavailable";}
 bool CanEdit(string id){return settingEnabled.TryGetValue(id,out var enabled)&&enabled&&settingValues.ContainsKey(id);}
 void SetSetting(string id,string value){
  if(!CanEdit(id)){ShowError("This setting is unavailable for the current input.");return;}
  bool ok=false;try{ok=CSharpSetItemValue(id,value);}catch{}
  RefreshSettings();UpdateFocus();
  bool verified=ok&&SettingValue(id)==value;
  if(!verified)ShowError("The TV could not apply this setting. Try Advanced settings.");
  WriteState("setting:"+id+":"+value+":"+verified);
 }
 void ChangeBrightness(int delta){double n;if(double.TryParse(SettingValue("brightness"),NumberStyles.Float,CultureInfo.InvariantCulture,out n))SetSetting("brightness",Math.Max(brightnessMin,Math.Min(brightnessMax,n+delta*brightnessStep)).ToString(CultureInfo.InvariantCulture));}
 void ChangeVolume(int delta){int n;int r=sound_manager_get_master_volume(out n);if(r==0)r=sound_manager_set_master_volume(Math.Max(0,Math.Min(100,n+delta)));if(r!=0)ShowError("Use the remote's volume buttons.");RefreshSettings();UpdateFocus();WriteState("volume_changed");}
 // Back on Home and the Resume button both return to what Home leads with.
 void ResumeRecent()
 {
  if(recent is RecentItem.App a)Launch(a.Item.Id,resume:true);
  else SelectSource(recent is RecentItem.Input i?i.SourceType:lastSource);
 }
 void SelectSource(int source){int r=connect_source(source);if(r<0){ShowError("Input could not be switched. Open Advanced settings to check the connection.");WriteState("source_failed:"+r);return;}lastSource=source;Launch("org.tizen.tv-viewer");}
 void Launch(string id,string menuItem=null,bool resume=false)
 {
  IntPtr c;int r=app_control_create(out c);if(r!=0){ShowError("Could not open the app.");return;}
  try{r=app_control_set_app_id(c,id);if(r==0)r=app_control_set_operation(c,"http://tizen.org/appcontrol/operation/default");
   // Samsung's home marks a recent-app resume this way. It adds SkipPIN only for unlocked apps; Quiet Home never does, so a locked app still asks.
   if(r==0&&resume)r=app_control_add_extra_data(c,"Entry","recent");
   if(r==0&&id=="org.tizen.menu"){
    if(menuItem!=null){
     r=app_control_add_extra_data(c,"menuitem",menuItem);
     if(r==0)r=app_control_add_extra_data(c,"launchoption","pressed");
    }
    if(r==0)r=app_control_add_extra_data(c,"__K_EX_FORCE_BOOST","enable");
    if(r==0)r=app_control_add_extra_data(c,"__K_SPLASH_SCREEN","LA_UI_VR_TRANSPARENT");
   }
   if(r==0)r=app_control_send_launch_request(c,IntPtr.Zero,IntPtr.Zero);WriteState("launch:"+id+(menuItem==null?"":":"+menuItem)+(resume?":resume":"")+":"+r);if(r!=0)ShowError("Could not open this screen. Try Advanced settings.");}finally{app_control_destroy(c);}
 }
 void ShowError(string text){notice.Text=text;}
 void WriteState(string activity){try{File.WriteAllText(StateFile,DateTime.UtcNow.ToString("o")+"\npage="+PageNames[page].ToLowerInvariant()+"\nselected="+selected+"\narea="+(onTabs?"tabs":selected>=0&&selected<slots.Count?"row"+slots[selected].Row:"")+"\nactivity="+activity+"\nkey="+lastKey+"\nfont_loaded="+fontLoaded+"\nrecent="+recent.Key+"\nrecent_from="+recentFrom+"\npreview="+(page!=HomePage||recent is RecentItem.Nothing?"none":preview.Live?"live":"still")+"\nbuild_ms="+buildMilliseconds+"\nlibrary_page="+libraryPage+"\nlibrary_count="+library.Count+"\nlibrary_error="+libraryError+"\nlibrary_source="+librarySource+"\nstore_source="+storeSource+"\ndock="+(dockIds==null?"default":string.Join("|",dockIds))+"\nmenu="+(menuOptions==null?"":string.Join("|",menuOptions))+"\nmoving="+(moving??"")+"\nselected_name="+(selected>=0&&selected<slots.Count?slots[selected].Name:"")+"\nartwork_cached="+artworkCache.Count+"\nartwork_ready="+ArtworkReadyCount()+"\nartwork_pending="+ArtworkPending()+"\nartwork_views_created="+artworkViewsCreated+"\npage_artwork_cold="+pageArtworkCold+"\nprocess_id="+Process.GetCurrentProcess().Id+"\nediting="+editing+"\nvolume="+currentVolume+"\nmute="+currentMute+"\nbrightness="+SettingValue("brightness")+"\ngame-mode="+SettingValue("game-mode")+"\nsleeptimer="+SettingValue("sleeptimer")+"\n"+preview?.Lines);}catch{}}
 public static void Main(string[] args){using(var app=new QuietHome())app.Run(args);}

 // The recent app's own window, which Samsung's virtual screen linker scales
 // into the preview rect under Quiet Home's window, as Samsung's home does.
 // Off, or Live with the app's pid. Show and Release are safe to repeat.
 sealed class LivePreview
 {
  const string Lib="/usr/lib/libvirtualscreen-linker.so",Flux="FLUX";
  const int Screen=120;
  [StructLayout(LayoutKind.Sequential)] struct Rect{public int X,Y,W,H;}
  [StructLayout(LayoutKind.Sequential)] struct Corner{public float X,Y;}
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void StateChanged(string previous,string current,int result,IntPtr data);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void WindowResized(int screen,int pid,int result,int w,int h,IntPtr data);
  // Every linker call returns 1 on success.
  [DllImport(Lib)] static extern int VirtualScreenSetStateChangeCallback(StateChanged callback,IntPtr data);
  [DllImport(Lib)] static extern int VirtualScreenSetWindowResizedCallback(WindowResized callback,IntPtr data);
  [DllImport(Lib)] static extern int VirtualScreenEnableAutoRemoveByRestack();
  [DllImport(Lib)] static extern int VirtualScreenSetGeometry(string state,int screen,ref Rect rect);
  [DllImport(Lib)] static extern int VirtualScreenRequestStateChange(string state);
  [DllImport(Lib)] static extern int VirtualScreenSetParentPID(int screen,int pid);
  [DllImport(Lib)] static extern int VirtualScreenSetFocusSkip(int screen,int skip);
  [DllImport(Lib)] static extern int VirtualScreenAssignPID(int pid,int screen);
  [DllImport(Lib)] static extern int VirtualScreenDimSetAlpha(int screen,int alpha);
  [DllImport(Lib)] static extern int VirtualScreenDimDisable(int screen);
  [DllImport(Lib)] static extern int VirtualScreenRaise(int screen);
  [DllImport(Lib)] static extern int VirtualScreenDeleteVirtualScreen(int screen);
  readonly int x,y,w,h;
  readonly Action<string> changed;
  readonly List<string> log=new List<string>();
  // The linker keeps these, so they live as long as the process.
  StateChanged onState;
  WindowResized onResize;
  bool linked;
  View hole;
  string wanted,app="";
  int pid=-1;
  Tizen.NUI.Timer showTimer,releaseTimer;
  public LivePreview(int x,int y,int w,int h,Action<string> changed){this.x=x;this.y=y;this.w=w;this.h=h;this.changed=changed;}
  public bool Live=>pid>0;
  public string Lines{get{lock(log)return "vs_pid="+(Live?pid.ToString():"")+"\n"+string.Concat(log.ConvertAll(l=>"vs="+l+"\n"));}}
  void Log(string line){lock(log){log.Add(DateTime.UtcNow.ToString("HH:mm:ss.fff")+" "+line);if(log.Count>8)log.RemoveAt(0);}}
  // Writes transparent pixels with blending off, so the window under Quiet Home
  // shows through. Hidden, the still art beneath it shows instead.
  public View Hole()
  {
   hole=null;
   try{
    var format=new PropertyMap();format.Add("aPosition",new PropertyValue((int)PropertyType.Vector2));
    var corners=new VertexBuffer(format);corners.SetData(new[]{new Corner{X=-.5f,Y=-.5f},new Corner{X=.5f,Y=-.5f},new Corner{X=-.5f,Y=.5f},new Corner{X=.5f,Y=.5f}});
    var quad=new Geometry();quad.AddVertexBuffer(corners);quad.SetType(Geometry.Type.TRIANGLE_STRIP);
    var shader=new Shader("attribute mediump vec2 aPosition;\nuniform mediump mat4 uMvpMatrix;\nuniform mediump vec3 uSize;\nvoid main(){gl_Position=uMvpMatrix*vec4(aPosition*uSize.xy,0.0,1.0);}\n",
     "precision mediump float;\nvoid main(){gl_FragColor=vec4(0.0,0.0,0.0,0.0);}\n");
    var view=new View{Position2D=new Position2D(x,y),Size2D=new Size2D(w,h)};
    view.AddRenderer(new Renderer(quad,shader){BlendMode=(int)BlendModeType.Off});
    if(Live&&releaseTimer==null)view.Show();else view.Hide();
    hole=view;
   }catch(Exception ex){Log("hole_failed "+ex.GetType().Name);}
   return hole;
  }
  // The scene is about to be disposed; its hole goes with it.
  public void Detach(){hole=null;}
  // Waits for Quiet Home to settle in front, as Samsung's home does.
  public void Show(string appId)
  {
   releaseTimer?.Stop();releaseTimer=null;wanted=appId;
   showTimer?.Stop();showTimer=new Tizen.NUI.Timer(400);
   showTimer.Tick+=(s,e)=>{Apply();return false;};showTimer.Start();
  }
  public void Release(string why){showTimer?.Stop();releaseTimer?.Stop();releaseTimer=null;wanted=null;Unlink(why);}
  // After leaving Home the app is let go only once the frame without the hole is
  // surely on screen; until then the hole would show its full-size window.
  public void ReleaseSoon()
  {
   showTimer?.Stop();wanted=null;
   if(!Live||releaseTimer!=null)return;
   releaseTimer=new Tizen.NUI.Timer(200);
   releaseTimer.Tick+=(s,e)=>{releaseTimer=null;Unlink("left_home");changed("vs_released");return false;};releaseTimer.Start();
  }
  void Apply()
  {
   if(wanted==null)return;
   int target=RunningPid(wanted);
   if(target<=0||hole==null){Log("still "+wanted+(target<=0?" not_running":" no_hole"));Release("still");changed("vs_still");return;}
   if(Live&&pid==target){hole.Show();changed("vs_kept");return;}
   Unlink("switch");
   var rc=new List<string>();bool assigned=false,ok=false;
   bool Ok(string name,int result){rc.Add(name+"="+result);return result==1;}
   try{
    if(!linked){
     onState=(before,after,result,data)=>Log("state "+before+">"+after+" "+result);
     onResize=(screen,id,result,rw,rh,data)=>Log("resized pid="+id+" "+rw+"x"+rh+" "+result);
     Log("linked state_cb="+VirtualScreenSetStateChangeCallback(onState,IntPtr.Zero)+" resize_cb="+VirtualScreenSetWindowResizedCallback(onResize,IntPtr.Zero)+" autoremove="+VirtualScreenEnableAutoRemoveByRestack());
     linked=true;
    }
    var rect=new Rect{X=x,Y=y,W=w,H=h};
    ok=Ok("geometry",VirtualScreenSetGeometry(Flux,Screen,ref rect))
     &&Ok("state",VirtualScreenRequestStateChange(Flux))
     &&Ok("parent",VirtualScreenSetParentPID(Screen,Environment.ProcessId))
     &&Ok("focusskip",VirtualScreenSetFocusSkip(Screen,1))
     &&(assigned=Ok("assign",VirtualScreenAssignPID(target,Screen)))
     &&Ok("dimalpha",VirtualScreenDimSetAlpha(Screen,1))
     &&Ok("dimoff",VirtualScreenDimDisable(Screen))
     &&Ok("raise",VirtualScreenRaise(Screen));
    if(!ok){if(assigned)rc.Add("unassign="+VirtualScreenAssignPID(target,-1));rc.Add("delete="+VirtualScreenDeleteVirtualScreen(Screen));}
   }catch(Exception ex){ok=false;rc.Add("error="+ex.GetType().Name);}
   if(ok){pid=target;app=wanted;hole.Show();}
   Log((ok?"live ":"still ")+wanted+" pid="+target+" "+string.Join(" ",rc));
   changed(ok?"vs_live":"vs_still");
  }
  // The hole goes opaque before the app is let go.
  void Unlink(string why)
  {
   hole?.Hide();
   if(!Live)return;
   string rc;
   try{rc="unassign="+VirtualScreenAssignPID(pid,-1)+" delete="+VirtualScreenDeleteVirtualScreen(Screen);}catch(Exception ex){rc="error="+ex.GetType().Name;}
   Log("released "+app+" pid="+pid+" "+why+" "+rc);
   pid=-1;app="";
  }
  static int RunningPid(string appId){try{using(var c=new ApplicationRunningContext(appId))return c.ProcessId;}catch{return -1;}}
 }
}
