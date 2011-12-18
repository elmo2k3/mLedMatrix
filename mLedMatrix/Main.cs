using System;
using Gtk;

namespace mLedMatrix
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			
			Application.Init ();
			MainWindow win = new MainWindow ();
			//MainWindow2 win2 = new MainWindow2 ();
			win.Show ();
			//win2.Show();
			Application.Run ();
		}
	}
}

