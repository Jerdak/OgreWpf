using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using WpfMogre.MogreUtilities;
using Mogre;
namespace WpfMogre
{
	/// <summary>
	/// Interaction logic for Window1.xaml
	/// </summary>
	public partial class Window1 : Window
	{
		#region OgreProperties
		private SceneManager OgreManager			{ get; set; }
		private Root OgreRoot						{ get; set; }
		private Mogre.RenderWindow OgreRenderWindow { get; set; }
		private RenderSystem OgreRenderSystem		{ get; set; }
		private Boolean OgreInitialized				{ get; set; }
		private Camera OgreCamera					{ get; set; }
		private System.Threading.Timer RenderTimer	{ get; set; }
		private LoadingBar LoadingBarWindow			{ get; set; }
		private Thread rcsThread					{ get; set; }
		private System.Windows.Forms.Panel RenderPanel { get; set;}
		#endregion

		#region ResourceGroupEventHandlers
		void ResourceGroupScriptingStarted(string groupName, uint scriptCount)
		{
			LoadingBarWindow.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(
					delegate()
					{
						LoadingBarWindow.ProgressBarInc = LoadingBarWindow.ProgressBarMaxSize * LoadingBarWindow.InitProportion / (float)scriptCount;
						LoadingBarWindow.Caption = "Parsing Scripts...";
					}
				)
			);
		}

		void ScriptParseStarted(string scriptName, out bool skipThisScript)
		{
			LoadingBarWindow.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(
					delegate()
					{
						LoadingBarWindow.Caption = scriptName;
					}
				)
			);
			skipThisScript = false;
		}

		void ScriptParseEnded(string scriptName, bool skipped)
		{
			LoadingBarWindow.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(
					delegate()
					{
						LoadingBarWindow.Value += LoadingBarWindow.ProgressBarInc;
					}
				)
			);

		}

		void ResourceGroupLoadStarted(string groupName, uint resourceCount)
		{
			LoadingBarWindow.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(
					delegate()
					{
						LoadingBarWindow.ProgressBarInc = LoadingBarWindow.ProgressBarMaxSize * (1 - LoadingBarWindow.InitProportion) / (float)resourceCount;
						LoadingBarWindow.Caption = "Loading Resources...";
					}
				)
			);
		}

		void ResourceLoadStarted(ResourcePtr resource)
		{
			LoadingBarWindow.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(
					delegate()
					{
						LoadingBarWindow.Caption = resource.Name;
					}
				)
			);
		}

		void WorldGeometryStageStarted(string description)
		{
			LoadingBarWindow.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(
					delegate()
					{
						LoadingBarWindow.Caption = description;
					}
				)
			);
		}

		void WorldGeometryStageEnded()
		{
			LoadingBarWindow.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(
					delegate()
					{
						LoadingBarWindow.Value += LoadingBarWindow.ProgressBarInc;
					}
				)
			);

		}
		#endregion

		#region OgreInitialization

		public void InitRenderPanel()
		{
			{	//Create Panel to wrap Mogre.
				RenderPanel = new System.Windows.Forms.Panel();
				RenderPanel.Name = "pnlOgre";
				RenderPanel.Size = new System.Drawing.Size(800, 600);
				RenderPanel.Location = new System.Drawing.Point(0, 0);
				renderFrame.Child = RenderPanel;
			}
		}
		public void InitRenderer()
		{
			try
			{
				OgreRoot = new Root();

				{	//Load config file data
					ConfigFile cf = new ConfigFile();
					cf.Load("./resources.cfg", "\t:=", true);
					ConfigFile.SectionIterator seci = cf.GetSectionIterator();
					String secName, typeName, archName;

					while (seci.MoveNext())
					{
						secName = seci.CurrentKey;
						ConfigFile.SettingsMultiMap settings = seci.Current;
						foreach (KeyValuePair<String, String> pair in settings)
						{
							typeName = pair.Key;
							archName = pair.Value;
							ResourceGroupManager.Singleton.AddResourceLocation(archName, typeName, secName);
						}
					}
				}


				OgreRenderSystem = OgreRoot.GetRenderSystemByName("Direct3D9 Rendering Subsystem");
				OgreRenderSystem.SetConfigOption("Full Screen", "No");
				OgreRenderSystem.SetConfigOption("Video Mode", "800 x 600 @ 32-bit colour");

				OgreRoot.RenderSystem = OgreRenderSystem;
				OgreRoot.Initialise(false, "Main Ogre Window");

				NameValuePairList misc = new NameValuePairList();
				misc["externalWindowHandle"] = RenderPanel.Handle.ToString();
				misc["FSAA"] = "4";
				OgreRenderWindow = OgreRoot.CreateRenderWindow("Main RenderWindow", 800, 600, false, misc);

				OgreRenderWindow.IsActive = true;
				OgreRenderWindow.IsAutoUpdated = true;

				MaterialManager.Singleton.SetDefaultTextureFiltering(TextureFilterOptions.TFO_ANISOTROPIC);

				//Trigger background resource load
				StartResourceThread();
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error during renderer initialization:" + ex.Message + "," + ex.StackTrace);
			}
		}
		public void InitTimer()
		{
			RenderTimer = new System.Threading.Timer(new System.Threading.TimerCallback(renderTick));
			RenderTimer.Change(1, 1);
		}

		#endregion

		public Window1()
		{
			InitializeComponent();
		}
		

		///Resize Mogre screen
		public void Resize(uint w, uint h)
		{
			if (OgreRoot == null || OgreRenderWindow == null || OgreCamera == null) return;

			OgreRenderWindow.Resize(w, h);
			OgreCamera.AspectRatio = (float)w / (float)h;

		}


		///Setup dummy Mogre scene.
		public void SetupScene()
		{
			SceneNode node = null;
			Entity ent = null;

			OgreManager = OgreRoot.CreateSceneManager(SceneType.ST_GENERIC, "MainSceneManager");
			OgreManager.AmbientLight = new ColourValue(0.8f, 0.8f, 0.8f);
			
			OgreCamera = OgreManager.CreateCamera("MainCamera");
			OgreCamera.NearClipDistance = 1;
			OgreCamera.FarClipDistance = 1000;
			OgreRenderWindow.AddViewport(OgreCamera);

			ent = OgreManager.CreateEntity("knot", "knot.mesh");
			ent.SetMaterialName("Examples/DarkMaterial");
			node = OgreManager.RootSceneNode.CreateChildSceneNode("knotnode");
			node.AttachObject(ent);

			OgreCamera.Position = new Vector3(0, 0, -300);
			OgreCamera.LookAt(ent.BoundingBox.Center);
		
			//Create a single point light source
			Light light = OgreManager.CreateLight("MainLight");
			light.Position = new Vector3(0, 10, -25);
			light.Type = Light.LightTypes.LT_POINT;
			light.SetDiffuseColour(1.0f, 1.0f, 1.0f);
			light.SetSpecularColour(0.1f, 0.1f, 0.1f);
		}
		///Start resource load worker thread
		private void StartResourceThread()
		{
			LoadingBarWindow = new MogreUtilities.LoadingBar();
			LoadingBarWindow.Start();

			SetResourceEventHandlers(true);


			rcsThread = new Thread(new ThreadStart(ResourceWorkerThread));
			rcsThread.SetApartmentState(ApartmentState.STA);
			rcsThread.IsBackground = true;
			rcsThread.Start();
		}

		///Resource load worker thread
		private void ResourceWorkerThread()
		{
			MaterialManager.Singleton.SetDefaultTextureFiltering(TextureFilterOptions.TFO_ANISOTROPIC);
			TextureManager.Singleton.DefaultNumMipmaps = 5;
			ResourceGroupManager.Singleton.InitialiseAllResourceGroups();

			SetupScene();
			OgreInitialized = true;

			//The LoadingBar was created by a different thread so use the Dispatcher to trigger a close
			LoadingBarWindow.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(
					delegate()
					{
						LoadingBarWindow.Close();
					}
			));

			SetResourceEventHandlers(false);
		}

		///Turn resource event handlers on and off
		private void SetResourceEventHandlers(Boolean on)
		{
			if (on)
			{
				ResourceGroupManager.Singleton.ResourceGroupScriptingStarted += ResourceGroupScriptingStarted;
				ResourceGroupManager.Singleton.ScriptParseStarted += ScriptParseStarted;
				ResourceGroupManager.Singleton.ScriptParseEnded += ScriptParseEnded;
				ResourceGroupManager.Singleton.ResourceGroupLoadStarted += ResourceGroupLoadStarted;
				ResourceGroupManager.Singleton.ResourceLoadStarted += ResourceLoadStarted;
				ResourceGroupManager.Singleton.WorldGeometryStageStarted += WorldGeometryStageStarted;
				ResourceGroupManager.Singleton.WorldGeometryStageEnded += WorldGeometryStageEnded;
			}
			else
			{
				ResourceGroupManager.Singleton.ResourceGroupScriptingStarted -= ResourceGroupScriptingStarted;
				ResourceGroupManager.Singleton.ScriptParseStarted -= ScriptParseStarted;
				ResourceGroupManager.Singleton.ScriptParseEnded -= ScriptParseEnded;
				ResourceGroupManager.Singleton.ResourceGroupLoadStarted -= ResourceGroupLoadStarted;
				ResourceGroupManager.Singleton.ResourceLoadStarted -= ResourceLoadStarted;
				ResourceGroupManager.Singleton.WorldGeometryStageStarted -= WorldGeometryStageStarted;
				ResourceGroupManager.Singleton.WorldGeometryStageEnded -= WorldGeometryStageEnded;
			}
		}

		private void renderTick(object state)
		{
			if (OgreRoot == null || OgreInitialized == false) return;

			OgreRoot.RenderOneFrame();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			OgreCamera			= null;
			OgreInitialized		= false;
			OgreManager			= null;
			OgreRoot			= null;
			OgreRenderWindow	= null;
			OgreRenderSystem	= null;

			InitRenderPanel();
			InitRenderer();

			InitTimer();
		}
	}
}
