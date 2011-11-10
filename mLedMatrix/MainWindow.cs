using System;
using Gtk;
using System.Threading;
using Microsoft.Win32;

public partial class MainWindow : Gtk.Window
{
	private LedMatrix led_matrix;
	private Thread led_matrix_thread;
	string led_matrix_address;
	RegistryKey softwareKey;
	RegistryKey rootKey;
	
	public MainWindow () : base(Gtk.WindowType.Toplevel)
	{
		Build ();
		led_matrix = new LedMatrix("0.0.0.0");
		led_matrix_thread = new Thread(led_matrix.Runner);
		
		//entry_address.ModifyBase(StateType.Normal, new Gdk.Color(255,0,0));
		loadConfig();
		led_matrix_thread.Start();
	}

	private void loadConfig()
	{
		rootKey = Microsoft.Win32.Registry.CurrentUser;
		softwareKey = null;
		string hkcuKey = @"SOFTWARE\Mono.mLedMatrix";
		softwareKey = rootKey.CreateSubKey (hkcuKey);
		led_matrix_address = (string)softwareKey.GetValue("ip_address","172.28.1.124");
		led_matrix.setAddress(led_matrix_address);
		if((string)softwareKey.GetValue("screen") == "time")
		{
			radiobutton_time.Active = true;
			led_matrix.current_screen = screen.time;
		}
		if((string)softwareKey.GetValue("screen") == "winamp")
		{
			radiobutton_winamp.Active = true;
			led_matrix.current_screen = screen.winamp;
		}
		if((string)softwareKey.GetValue("screen") == "void")
		{
			radiobutton_void.Active = true;
			led_matrix.current_screen = screen.empty;
		}
		if((string)softwareKey.GetValue("screen") == "static_text")
		{
			radiobutton_static_text.Active = true;
			led_matrix.current_screen = screen.static_text;
		}
		if((string)softwareKey.GetValue("screen") == "all_on")
		{
			radiobutton_all.Active = true;
			led_matrix.current_screen = screen.all_on;
		}
		entry_address.Text = led_matrix_address;
	}
	
	private void saveConfig()
	{
		softwareKey.SetValue("ip_address",entry_address.Text);
		softwareKey.SetValue("static_text",entry_static_text.Text);
		if(radiobutton_time.Active)
			softwareKey.SetValue("screen","time");
		if(radiobutton_winamp.Active)
			softwareKey.SetValue("screen","winamp");
		if(radiobutton_void.Active)
			softwareKey.SetValue("screen","void");
		if(radiobutton_static_text.Active)
			softwareKey.SetValue("screen","static_text");
		if(radiobutton_all.Active)
			softwareKey.SetValue("screen","all_on");
	}
	
	protected void OnDeleteEvent (object sender, DeleteEventArgs a)
	{
		saveConfig();
		softwareKey.Flush ();
		led_matrix.requestStop();
		Application.Quit ();
		a.RetVal = true;
	}

	protected virtual void on_entry_address_activated (object sender, System.EventArgs e)
	{
		entry_address.HasFocus = false;
		button_address_ok.HasFocus = true;
		if(led_matrix.setAddress(entry_address.Text) == false)
			entry_address.Text = led_matrix_address;
	}
	
	protected virtual void hscale_shift_speed_changed (object sender, System.EventArgs e)
	{
		led_matrix.scroll_speed =(int)hscale_shift_speed.Value;
	}
	
	protected virtual void on_entry_static_text_activated (object sender, System.EventArgs e)
	{
	}
	
	protected virtual void on_hscale_x_pos_changed (object sender, System.EventArgs e)
	{
		led_matrix.static_text_x = (int)hscale_x_pos.Value;
	}
	
	protected virtual void radiobutton_time_toggled (object sender, System.EventArgs e)
	{
		led_matrix.current_screen = screen.time;
	}
	
	protected virtual void radiobutton_winamp_toggled (object sender, System.EventArgs e)
	{
		led_matrix.current_screen = screen.winamp;
	}
	
	protected virtual void radiobutton_void_toggled (object sender, System.EventArgs e)
	{
		led_matrix.current_screen = screen.empty;
	}
	
	protected virtual void radiobutton_static_text_toggled (object sender, System.EventArgs e)
	{
		led_matrix.current_screen = screen.static_text;
	}
	
	protected virtual void on_entry_static_text_changed (object sender, System.EventArgs e)
	{
		led_matrix.static_text = entry_static_text.Text;
	}
	
	protected virtual void on_vscale_y_pos_changed (object sender, System.EventArgs e)
	{
		led_matrix.static_text_y = (int)vscale_y_pos.Value;
	}
	
	protected virtual void on_radiobutton_all_toggled (object sender, System.EventArgs e)
	{
		led_matrix.current_screen = screen.all_on;
	}
	
	
	
	
	
	
	
	
}

