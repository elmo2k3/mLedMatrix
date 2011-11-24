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
	Winamp_httpQ winamp;
	StatusIcon tray_icon;
	
	public MainWindow () : base(Gtk.WindowType.Toplevel)
	{
		Build ();
		led_matrix = new LedMatrix("0.0.0.0");
		led_matrix_thread = new Thread(led_matrix.Runner);
		winamp = new Winamp_httpQ();
		tray_icon = new StatusIcon(new Gdk.Pixbuf("icon.png"));
		tray_icon.Visible = true;
		tray_icon.Tooltip = "LedMatrix Control";
		tray_icon.Activate += delegate { this.Visible = !this.Visible; };
		
		//entry_address.ModifyBase(StateType.Normal, new Gdk.Color(255,0,0));
		loadConfig();
		led_matrix_thread.Start();
		winamp.title_changed_handler += led_matrix.setWinampPlaylisttitle;
		winamp.title_changed_handler += set_winamp;
		winamp.status_changed_handler += set_winamp;
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
		}
		if((string)softwareKey.GetValue("screen") == "winamp")
		{
			radiobutton_winamp.Active = true;
		}
		if((string)softwareKey.GetValue("screen") == "void")
		{
			radiobutton_void.Active = true;
		}
		if((string)softwareKey.GetValue("screen") == "static_text")
		{
			radiobutton_static_text.Active = true;
		}
		if((string)softwareKey.GetValue("screen") == "all_on")
		{
			radiobutton_all.Active = true;
		}
		entry_address.Text = led_matrix_address;
		led_matrix.scroll_speed = (int)softwareKey.GetValue("scroll_speed",10);
		hscale_shift_speed.Value = led_matrix.scroll_speed;
		//entry_static_text.Text = (string)softwareKey.GetValue("static_text");
	}
	
	private void saveConfig()
	{
		softwareKey.SetValue("ip_address",entry_address.Text);
		softwareKey.SetValue("scroll_speed",led_matrix.scroll_speed);
		//softwareKey.SetValue("static_text",entry_static_text.Text);
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
	
	protected virtual void radiobutton_time_toggled (object sender, System.EventArgs e)
	{
		led_matrix.led_matrix_string = "%c%r%2%+%+%h:%m:%s";
	}
	
	private void set_winamp(bool playing, string playlisttitle)
	{
		if(playing == false)
		{
			led_matrix.led_matrix_string = "%c%r%2%+%+%h:%m:%s";
			//radiobutton_time.Active = true;
		}
		else
		{
			//radiobutton_winamp.Active = true;
			if(led_matrix.stringWidth("%a","8x8") <= 64 &&
				led_matrix.stringWidth("%t","8x8") <= 64)
			{
				led_matrix.led_matrix_string = "%8%c%r%r%a\n%c%g%t";
			}
			else
				led_matrix.led_matrix_string = "%2%+%+%r%a %o- %g%t";
		}
	}
	
	protected virtual void radiobutton_winamp_toggled (object sender, System.EventArgs e)
	{
		set_winamp(true,"");
	}
	
	protected virtual void radiobutton_void_toggled (object sender, System.EventArgs e)
	{
		led_matrix.led_matrix_string = "";
	}
	
	protected virtual void radiobutton_static_text_toggled (object sender, System.EventArgs e)
	{
		TextBuffer buffer;
		buffer = textview_static_text.Buffer;
		led_matrix.led_matrix_string = buffer.Text;
	}
	
	
	protected virtual void on_radiobutton_all_toggled (object sender, System.EventArgs e)
	{
		//led_matrix.current_screen = screen.all_on;
	}

	protected void static_text_changed (object o, Gtk.KeyReleaseEventArgs args)
	{
		//led_matrix.static_text = entry_static_text.Text;
		TextBuffer buffer;
		buffer = textview_static_text.Buffer;
		led_matrix.led_matrix_string = buffer.Text;
	}
}

