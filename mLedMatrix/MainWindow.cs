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
	
	public MainWindow () : base(Gtk.WindowType.Toplevel)
	{
		Build ();
		led_matrix = new LedMatrix("0.0.0.0");
		led_matrix_thread = new Thread(led_matrix.Runner);
		winamp = new Winamp_httpQ();
		
		//entry_address.ModifyBase(StateType.Normal, new Gdk.Color(255,0,0));
		loadConfig();
		led_matrix_thread.Start();
		winamp.title_changed += led_matrix.setWinampPlaylisttitle;
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
		//entry_static_text.Text = (string)softwareKey.GetValue("static_text");
	}
	
	private void saveConfig()
	{
		softwareKey.SetValue("ip_address",entry_address.Text);
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
	
	protected virtual void on_entry_static_text_activated (object sender, System.EventArgs e)
	{
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
		
	}
		
	protected virtual void on_hscale_x_pos_changed (object sender, System.EventArgs e)
	{
		led_matrix.static_text_x = (int)hscale_x_pos.Value;
		led_matrix.force_redraw = true;
	}
	
	protected virtual void on_vscale_y_pos_changed (object sender, System.EventArgs e)
	{
		led_matrix.static_text_y = (int)vscale_y_pos.Value;
		led_matrix.force_redraw = true;
	}
	
	protected virtual void on_radiobutton_all_toggled (object sender, System.EventArgs e)
	{
		led_matrix.current_screen = screen.all_on;
	}
	
	protected void on_combobox_font_time_changed (object sender, System.EventArgs e)
	{
		if(combobox_font_time.ActiveText == "Font8x12")
			led_matrix.fontname_time = "8x12";
		else if(combobox_font_time.ActiveText == "Font8x8")
			led_matrix.fontname_time = "8x8";
		
	}
	
	protected void on_combobox_font_static_text_changed (object sender, System.EventArgs e)
	{
		if(combobox_font_static_text.ActiveText == "Font8x12")
			led_matrix.fontname_static_text = "8x12";
		else if(combobox_font_static_text.ActiveText == "Font8x8")
			led_matrix.fontname_static_text = "8x8";
		led_matrix.force_redraw = true;
	}

	protected void on_togglebutton_static_text_auto_scroll_toggled (object sender, System.EventArgs e)
	{
		led_matrix.shift_auto_enabled = checkbutton_static_text_auto_scroll.Active;
		led_matrix.force_redraw = true;
		//throw new System.NotImplementedException ();
	}

	protected void static_text_changed (object o, Gtk.KeyReleaseEventArgs args)
	{
		//led_matrix.static_text = entry_static_text.Text;
		TextBuffer buffer;
		buffer = textview_static_text.Buffer;
		led_matrix.static_text = buffer.Text;
		led_matrix.force_redraw = true;
	}
}

