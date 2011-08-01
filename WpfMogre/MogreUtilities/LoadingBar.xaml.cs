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
using System.Windows.Shapes;
using Mogre;

namespace WpfMogre.MogreUtilities
{
	/// <summary>
	/// Interaction logic for LoadingBar.xaml
	/// </summary>
	public partial class LoadingBar : Window
	{
		public String Caption {

			set {
				lblCaption.Content = value;
			}
		}
		public Double Value {
			get {
				return pbProgress.Value;
			} 
			set {
				pbProgress.Value = Convert.ToDouble(value);

			}
		}
		public Double ProgressBarInc {get; set;}
		public Double ProgressBarMaxSize {get; set;}
		public Double InitProportion { get; set;}
		
		public LoadingBar()
		{
			InitializeComponent();

			
		}
		public void Start(){
			this.Show();
			this.Topmost = true;
			ProgressBarMaxSize = 100.0f;
			InitProportion = 0.50f;
			ProgressBarInc = 1;
		}
		public void Finish()
		{
			this.Hide();
		}
	}
}
