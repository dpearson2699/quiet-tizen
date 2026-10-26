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
 const string FocusStroke="/usr/apps/com.samsung.tv.theme-resource/shared/res/PRINCIPLE/FLUX/i_focus_stroke/i_focus_stroke_squircle_13.svg";
 sealed class Slot { public View Card,Shadow,FocusRing; public TextLabel Label; public string Name,Action,Description; public int Row,X,Y,W,H; }
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
 TextLabel toolbarHint;
 Animation focusAnimation;
 bool fontLoaded;
 Tizen.NUI.Timer clockTimer;
 int page, selected, area=1, lastSource;
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
 [DllImport("libsource-api.so")] static extern int connect_source(int sourceType);
 [DllImport("libsource-api.so")] static extern IntPtr get_source_list();
 [DllImport("libc.so.6")] static extern void free(IntPtr p);
 [DllImport("libvconf.so.0")] static extern int vconf_get_int(string key,out int value);
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
  Window.Default.BackgroundColor=new Color(0.06f,0.065f,0.08f,1);
  Window.Default.Title="Quiet Home";Window.Default.KeyEvent+=OnKey;
  try{dockIds=HomeDock.Parse(File.ReadAllText(DockFile));}catch{dockIds=null;}
  PreloadArtwork();QueryInstalled();
  RefreshSource();selected=SourceIndex();BuildPage();
  clockTimer=new Tizen.NUI.Timer(30000);
  clockTimer.Tick+=(s,e)=>{if(clock!=null)clock.Text=DateTime.Now.ToString("h:mm tt");return true;};clockTimer.Start();WriteState("created");
 }
 protected override void OnResume(){base.OnResume();clockTimer?.Start();RefreshSource();QueryInstalled();if(tabs!=null&&page==2){RefreshSettings();UpdateFocus(false);}WriteState("resumed");}
 protected override void OnAppControlReceived(AppControlReceivedEventArgs e){base.OnAppControlReceived(e);string requested; if(e.ReceivedAppControl.ExtraData.TryGet("quiet_page",out requested)&&requested=="settings")OpenSettings("settings_request");else if(requested=="apps"){page=1;area=1;selected=0;BuildPage();}else Home("home_request");}
 protected override void OnPause(){base.OnPause();editing="";holdTimer?.Stop();selectHeld=false;if(menuOptions!=null)CloseMenu();if(moving!=null)EndMove(false);clockTimer?.Stop();WriteState("paused");}
 string DockFile=>DirectoryInfo.Data+"dock.json";
 void Home(string activity){clockTimer?.Start();RefreshSource();if(tabs!=null){bool unchanged=page==0&&area==1&&editing==""&&selected==SourceIndex();page=0;area=1;editing="";selected=SourceIndex();if(!unchanged)BuildPage();WriteState(activity);}}
 void OpenSettings(string activity){clockTimer?.Start();page=2;area=1;selected=0;editing="";BuildPage();WriteState(activity);}
 void RefreshSource(){
  try{IntPtr p=get_source_list();if(p!=IntPtr.Zero){string json=Marshal.PtrToStringAnsi(p);free(p);var parsed=Sources.Parse(json);if(parsed.Count>0){inputs.Clear();inputs.AddRange(parsed);}}}catch{}
  try{int n;if(vconf_get_int("memory/system/source_type",out n)==0&&n>0)lastSource=n;}catch{}
  if(inputs.Count>0&&!inputs.Exists(s=>s.SourceType==lastSource))lastSource=inputs[0].SourceType;
 }
 int SourceIndex(){for(int i=0;i<inputs.Count;i++)if(inputs[i].SourceType==lastSource)return i;return 0;}
 View Add(View v){scene.Add(v);Window.Default.Add(v);return v;}
 View Panel(int x,int y,int w,int h,Color color,int radius=18){return Add(new View{Position2D=new Position2D(x,y),Size2D=new Size2D(w,h),BackgroundColor=color,CornerRadius=new Vector4(radius,radius,radius,radius)});}
 TextLabel Text(string text,int x,int y,int w,int h,float size,Color color,string face="Medium")
 {return (TextLabel)Add(Label(text,x,y,w,h,size,color,face));}
 TextLabel Label(string text,int x,int y,int w,int h,float size,Color color,string face="Medium")
 {return new TextLabel{Text=text,Position2D=new Position2D(x,y),Size2D=new Size2D(w,h),PixelSize=size,FontFamily="Quiet Inter "+face,TextColor=color,VerticalAlignment=VerticalAlignment.Center};}
 void BuildPage()
 {
  var stopwatch=Stopwatch.StartNew();
  if(focusAnimation!=null){focusAnimation.Stop();focusAnimation.Dispose();focusAnimation=null;}
  ParkArtwork();pageArtworkCold=0;
  foreach(var v in scene){Window.Default.Remove(v);v.Dispose();}scene.Clear();slots.Clear();
  Add(new ImageView{ResourceUrl=Root+"free-background.svg",Size2D=new Size2D(1920,1080)});
  Text(page==0?"Home":page==1?"Your Apps":"Quick Settings",76,55,1350,100,76,ink,"Display");
  tabs=new TextLabel[3];string[] nav={"Inputs","Apps","Settings"};int[] tx={54,221,366};int[] tw={153,131,182};
  for(int i=0;i<3;i++){tabs[i]=Text(nav[i],tx[i],158,tw[i],68,30,muted);tabs[i].HorizontalAlignment=HorizontalAlignment.Center;}
  clock=Text(DateTime.Now.ToString("h:mm tt"),1540,170,300,45,24,muted);clock.HorizontalAlignment=HorizontalAlignment.End;
  if(page==0){
   BuildInputs();
  }else if(page==1){
   BuildLibrary();
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
  if(page==0){
  Add(new ImageView{ResourceUrl=Root+"glass-shelf.svg",Position2D=new Position2D(0,784),Size2D=new Size2D(1920,280)});
  var docked=DockApps();
  for(int i=0;i<docked.Count;i++)Dock(docked[i].Name,"app:"+docked[i].Id,i,docked.Count+1);
  Dock("Your Apps","page:1",docked.Count,docked.Count+1);
  }
  Toolbar("Apps","page:1","apps",0);Toolbar("Inputs","page:0","hdmi",1);Toolbar("Settings","page:2","settings",2);
  toolbarHint=Text("",1530,170,310,45,24,ink);toolbarHint.HorizontalAlignment=HorizontalAlignment.End;
  detail=Text("",80,1030,1320,35,20,muted);
  notice=Text("",80,1008,1760,34,22,accent);
  var hint=Text(page==1?(AppLibrary.PageCount(library.Count)<=1?"Hold Select for options · Back to Home":libraryPage+1<AppLibrary.PageCount(library.Count)?"Hold Select for options · Down for more":"Hold Select for options · Up for previous"):page==0?(moving!=null?"":"Hold Select on an app for options · Back to input"):"Back to input",1110,1030,730,35,20,muted);hint.HorizontalAlignment=HorizontalAlignment.End;
  selected=Math.Max(0,Math.Min(selected,slots.Count-1));UpdateFocus(false);
  buildMilliseconds=stopwatch.ElapsedMilliseconds;WriteState("page_ready");
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
   slots.Add(new Slot{Card=card,FocusRing=FocusRing(card),Label=label,Name=app.Name,Action="app:"+app.Id,Description=app.Description,Row=row,X=x,Y=y,W=Tile,H=Tile});
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
 View TileCard(int x,int y,LibraryApp app)
 {
  var card=SquircleCard(x,y,HexColor(app.Background));
  AddAppArtwork(card,app,Tile,Tile);return card;
 }
 View SquircleCard(int x,int y,Color color)
 {
  var card=Panel(x+Tile/2,y+Tile/2,Tile,Tile,color,Tile/2);
  card.CornerSquareness=new Vector4(TileSquareness,TileSquareness,TileSquareness,TileSquareness);
  card.PivotPoint=new Position(.5f,.5f,.5f);card.PositionUsesPivotPoint=true;return card;
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
 List<LibraryApp> DockApps()=>HomeDock.Resolve(moving!=null?moveIds:dockIds,library);
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
   if(menuOptions!=null||moving!=null)pendingBuild=true;else if(editing==""&&(page==0||page==1))BuildPage();}
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
  var card=Panel(x+w/2,y+h/2,w,h,new Color(0,0,0,0),24);card.PivotPoint=new Position(.5f,.5f,.5f);card.PositionUsesPivotPoint=true;
  card.Add(new ImageView{ResourceUrl=Root+"quick-"+icon+".svg",Size2D=new Size2D(w,h)});
  card.Add(Label(name,26,25,348,42,29,white,"Semibold"));
  var value=Label("",26,151,358,56,38,white,"Display");card.Add(value);
  card.Add(Label(description,26,226,370,34,20,new Color(.95f,.96f,1,1)));
  slots.Add(new Slot{Card=card,Label=value,Name=name,Action=action,Description=description,Row=row,X=x,Y=y,W=w,H=h});
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
  var card=Panel(x+w/2,y+h/2,w,h,new Color(0,0,0,0),18);card.PivotPoint=new Position(.5f,.5f,.5f);card.PositionUsesPivotPoint=true;
  if(art.Kind==SourceArtKind.Illustration){
   card.Add(new ImageView{ResourceUrl=Root+art.Path,Size2D=new Size2D(w,h)});
  }else{
   card.BackgroundColor=new Color(.145f,.16f,.2f,1);
   int g=Math.Min(w,h)*2/5;
   card.Add(new ImageView{ResourceUrl=art.Path,Position2D=new Position2D((w-g)/2,(h-g)/2-20),Size2D=new Size2D(g,g)});
  }
  var caption=Text(label,x,716,w,45,31,ink,"Semibold");caption.HorizontalAlignment=HorizontalAlignment.Center;
  var sub=Text(description,x,760,w,36,24,muted);sub.HorizontalAlignment=HorizontalAlignment.Center;
  slots.Add(new Slot{Card=card,Shadow=shadow,Label=caption,Name=label,Action=action,Description=description,Row=0,X=x,Y=y,W=w,H=h});
 }
 void Dock(string label,string action,int n,int count)
 {
  int x=(1920-count*DockPitch+TileGap)/2+n*DockPitch,y=857;View card;
  var app=action.StartsWith("app:")?library.Find(a=>a.Id==action.Substring(4)):null;
  if(app!=null)card=TileCard(x,y,app);
  else{
   card=SquircleCard(x,y,new Color(1,1,1,.09f));
   card.Add(new ImageView{ResourceUrl=Root+"symbol-apps.png",Color=ink,Position2D=new Position2D(Tile/2-22,Tile/2-22),Size2D=new Size2D(44,44)});
  }
  slots.Add(new Slot{Card=card,FocusRing=FocusRing(card),Name=label,Action=action,Description=label,Row=1,X=x,Y=y,W=Tile,H=Tile});
 }
 void Toolbar(string label,string action,string icon,int n)
 {
  int x=1596+n*88;
  var card=Panel(x+34,103,68,68,new Color(0,0,0,0),34);card.PivotPoint=new Position(.5f,.5f,.5f);card.PositionUsesPivotPoint=true;
  card.Add(new ImageView{ResourceUrl=Root+"glass-button.svg",Position2D=new Position2D(-16,-16),Size2D=new Size2D(100,112)});
  card.Add(new ImageView{ResourceUrl=Root+"symbol-"+icon+".png",Color=ink,Position2D=new Position2D(19,19),Size2D=new Size2D(30,30)});
  slots.Add(new Slot{Card=card,Name=label,Action=action,Description=label,Row=-1,X=x,Y=69,W=68,H=68});
 }
 List<int> Row(int row){var r=new List<int>();for(int i=0;i<slots.Count;i++)if(slots[i].Row==row)r.Add(i);return r;}
 void UpdateFocus(bool animate=true)
 {
  if(focusAnimation!=null){focusAnimation.Stop();focusAnimation.Dispose();focusAnimation=null;}
  if(animate)focusAnimation=new Animation(180);
  for(int i=0;i<3;i++){tabs[i].TextColor=i==page?ink:muted;tabs[i].FontFamily="Quiet Inter "+(i==page?"Semibold":"Medium");tabs[i].BackgroundColor=area==0&&i==page?new Color(1,1,1,.14f):new Color(0,0,0,0);tabs[i].CornerRadius=new Vector4(34,34,34,34);}
  for(int i=0;i<slots.Count;i++){
   var s=slots[i];bool f=area!=0&&selected==i;
   bool tile=page==1&&s.Row>=0||page==0&&s.Row==1;
   float scale=f?(tile?TileFocusScale:page==2&&s.Row>=0?1.035f:s.Row==0?1.05f:1.10f):1;
   s.Card.BorderlineWidth=!tile&&s.Row==0?1:0;
   s.Card.BorderlineColor=new Color(1,1,1,f?.8f:.12f);
   var pos=new Position(s.X+s.W/2f,s.Y+s.H/2f-(f&&s.Row==0&&!tile?6:0),0);
   if(animate){focusAnimation.AnimateTo(s.Card,"Scale",new Vector3(scale,scale,1));focusAnimation.AnimateTo(s.Card,"Position",pos);}else{s.Card.Scale=new Vector3(scale,scale,1);s.Card.Position=pos;}
   if(s.Shadow!=null)s.Shadow.Opacity=f?1:.55f;
   if(s.FocusRing!=null)s.FocusRing.Opacity=f?1:0;
   if(s.Label!=null&&page!=2)s.Label.FontFamily="Quiet Inter "+(f?"Semibold":"Medium");
   if(s.Label!=null&&page==1)s.Label.TextColor=f?ink:muted;
   if(f)s.Card.RaiseToTop();
  }
  if(focusAnimation!=null)focusAnimation.Play();
  clock.Opacity=area==3?0:1;toolbarHint.Text=area==3?slots[selected].Name:"";
  detail.Text=page==1&&area==1&&slots[selected].Row>=0?slots[selected].Name+" · "+slots[selected].Description:area==0?"":area==2?slots[selected].Name:"";
  if(page==2){
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
  if(moving!=null&&area==2&&selected<slots.Count&&slots[selected].Action=="app:"+moving){
   slots[selected].Card.BorderlineWidth=5;slots[selected].Card.BorderlineColor=accent;slots[selected].FocusRing.Opacity=0;
   detail.Text="Left / Right to move · Select to keep · Back to cancel";}
  WriteState("focus");
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
   if(select&&editing==""&&area!=0&&SelectedApp()!=null){PressSelect();return;}
   if(page==1&&HandleLibraryKey(k))return;
   if(editing!=""){if(k=="Left"||k=="Right"){int d=k=="Left"?-1:1;if(editing=="volume")ChangeVolume(d);else ChangeBrightness(d);}else if(k=="Return"||k=="KP_Enter"||k=="XF86Back"||k=="Back"||k=="Escape"){editing="";UpdateFocus();}return;}
   if(k=="Up"){if(area==2){int col=Row(1).IndexOf(selected);area=1;selected=Row(0)[page==2?col:0];}else if(area==1)area=0;UpdateFocus();}
   else if(k=="Down"){if(area==0||area==3){area=1;selected=Row(0)[0];}else if(area==1){int col=Row(0).IndexOf(selected);area=2;selected=Row(1)[page==2?col:0];}UpdateFocus();}
   else if(k=="Left"||k=="Right"){
    int d=k=="Left"?-1:1;
    if(area==0){int next=page+d;if(next<0||next>2){area=3;selected=Row(-1)[next<0?2:0];UpdateFocus();}else{page=next;selected=0;BuildPage();}}
    else if(area==3){var r=Row(-1);int n=r.IndexOf(selected)+d;if(n<0||n>=r.Count){area=0;page=n<0?2:0;selected=0;BuildPage();}else{selected=r[n];UpdateFocus();}}
    else{var r=Row(area-1);int n=r.IndexOf(selected);selected=r[(n+d+r.Count)%r.Count];UpdateFocus();}
   }
   else if(k=="Return"||k=="KP_Enter"){if(area==0){area=1;selected=0;UpdateFocus();}else Activate();}
   else if(k=="XF86Back"||k=="Back"||k=="Escape"||k=="XF86Exit")SelectSource(lastSource);
  }catch(Exception ex){ShowError(ex.Message);}
 }
 bool HandleLibraryKey(string key)
 {
  if(key=="XF86Back"||key=="Back"||key=="Escape"||key=="XF86Exit"){Home("apps_back");return true;}
  if(library.Count==0)return key=="Up"||key=="Down";
  if((area==0||area==3)&&key=="Down"){area=1;selected=0;UpdateFocus();return true;}
  if(area!=1)return false;
  if(key=="Left"||key=="Right"||key=="Up"||key=="Down"){
   int next=AppLibrary.Move(library.Count,libraryPage*AppLibrary.PageSize+selected,key);
   if(next<0){area=0;UpdateFocus();return true;}
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
  if(a.StartsWith("page:")){page=int.Parse(a.Substring(5));selected=page==0?SourceIndex():0;area=1;BuildPage();return;}
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
  if(selected<0||selected>=slots.Count)return null;var s=slots[selected];
  if(!s.Action.StartsWith("app:")||!(page==1&&s.Row>=0||page==0&&s.Row==1))return null;
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
 void OpenOptions(){var app=SelectedApp();if(app!=null)ShowMenu(app.Name,null,AppOptions.For(app,DockIds(),page==0),app);}
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
 // Like Samsung's own reorder: Left/Right slide the app one slot per press,
 // Select keeps the new order and Back puts it back.
 void StartMove(LibraryApp app)
 {
  moveIds=DockIds();moving=app.Id;ShowMoving();WriteState("move_started:"+app.Id);
 }
 void MoveKey(string k)
 {
  if(k=="Left"||k=="Right"){var next=HomeDock.Move(moveIds,moving,k=="Left"?-1:1);if(string.Join("|",next)!=string.Join("|",moveIds)){moveIds=next;ShowMoving();}}
  else if(k=="Return"||k=="KP_Enter")EndMove(true);
  else if(k=="XF86Back"||k=="Back"||k=="Escape"||k=="XF86Exit")EndMove(false);
 }
 void ShowMoving()
 {
  BuildPage();int at=slots.FindIndex(s=>s.Action=="app:"+moving);
  if(at>=0){area=2;selected=at;UpdateFocus(false);}
  WriteState("moving:"+string.Join("|",moveIds));
 }
 void EndMove(bool keep)
 {
  string id=moving;var ids=moveIds;moving=null;moveIds=null;pendingBuild=false;
  if(keep)SaveDock(ids,id,"");
  else if(page==0){BuildPage();int at=slots.FindIndex(s=>s.Action=="app:"+id);if(at>=0){area=2;selected=at;UpdateFocus(false);}}
  WriteState(keep?"move_saved":"move_cancelled");
 }
 void SaveDock(List<string> ids,string focusId,string message)
 {
  try{File.WriteAllText(DockFile,HomeDock.Serialize(ids));}
  catch(Exception ex){ShowError("Home could not be saved.");WriteState("dock_save_failed:"+ex.GetType().Name);return;}
  dockIds=ids;
  if(page==0){
   BuildPage();int at=focusId==null?-1:slots.FindIndex(s=>s.Action=="app:"+focusId);
   var dock=Row(1);area=2;selected=at>=0?at:dock.Count>0?dock[0]:selected;UpdateFocus(false);
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
 void SelectSource(int source){int r=connect_source(source);if(r<0){ShowError("Input could not be switched. Open Advanced settings to check the connection.");WriteState("source_failed:"+r);return;}lastSource=source;Launch("org.tizen.tv-viewer");}
 void Launch(string id,string menuItem=null)
 {
  IntPtr c;int r=app_control_create(out c);if(r!=0){ShowError("Could not open the app.");return;}
  try{r=app_control_set_app_id(c,id);if(r==0)r=app_control_set_operation(c,"http://tizen.org/appcontrol/operation/default");
   if(r==0&&id=="org.tizen.menu"){
    if(menuItem!=null){
     r=app_control_add_extra_data(c,"menuitem",menuItem);
     if(r==0)r=app_control_add_extra_data(c,"launchoption","pressed");
    }
    if(r==0)r=app_control_add_extra_data(c,"__K_EX_FORCE_BOOST","enable");
    if(r==0)r=app_control_add_extra_data(c,"__K_SPLASH_SCREEN","LA_UI_VR_TRANSPARENT");
   }
   if(r==0)r=app_control_send_launch_request(c,IntPtr.Zero,IntPtr.Zero);WriteState("launch:"+id+(menuItem==null?"":":"+menuItem)+":"+r);if(r!=0)ShowError("Could not open this screen. Try Advanced settings.");}finally{app_control_destroy(c);}
 }
 void ShowError(string text){notice.Text=text;}
 void WriteState(string activity){try{File.WriteAllText(StateFile,DateTime.UtcNow.ToString("o")+"\npage="+page+"\nselected="+selected+"\narea="+area+"\nactivity="+activity+"\nkey="+lastKey+"\nfont_loaded="+fontLoaded+"\nstyle=free-my-tv\nbuild_ms="+buildMilliseconds+"\nlibrary_page="+libraryPage+"\nlibrary_count="+library.Count+"\nlibrary_error="+libraryError+"\nlibrary_source="+librarySource+"\nstore_source="+storeSource+"\ndock="+(dockIds==null?"default":string.Join("|",dockIds))+"\nmenu="+(menuOptions==null?"":string.Join("|",menuOptions))+"\nmoving="+(moving??"")+"\nselected_name="+(selected>=0&&selected<slots.Count?slots[selected].Name:"")+"\nartwork_cached="+artworkCache.Count+"\nartwork_ready="+ArtworkReadyCount()+"\nartwork_pending="+ArtworkPending()+"\nartwork_views_created="+artworkViewsCreated+"\npage_artwork_cold="+pageArtworkCold+"\nprocess_id="+Process.GetCurrentProcess().Id+"\nediting="+editing+"\nvolume="+currentVolume+"\nmute="+currentMute+"\nbrightness="+SettingValue("brightness")+"\ngame-mode="+SettingValue("game-mode")+"\nsleeptimer="+SettingValue("sleeptimer")+"\n");}catch{}}
 public static void Main(string[] args){using(var app=new QuietHome())app.Run(args);}
}
