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
	bool no_tray_icon;
	RssPlugin rss;
	
	string text_time_default;
	string text_winamp_default;
	string text_winamp_2_default;
	string text_static_default;
	
	public MainWindow () : base(Gtk.WindowType.Toplevel)
	{
		Build ();
		
		entry_address.ModifyBase(StateType.Normal, new Gdk.Color(255,0,0));
		led_matrix = new LedMatrix("0.0.0.0");
		led_matrix_thread = new Thread(led_matrix.Runner);
		winamp = new Winamp_httpQ();
		rss = new RssPlugin();
		no_tray_icon = false;
		led_matrix.rss = rss;
		
		try
		{
			tray_icon = new StatusIcon(new Gdk.Pixbuf("icon.png"));
		}
		catch(Exception ex)
		{
			no_tray_icon = true;
		}
		if(no_tray_icon == false)
		{
			tray_icon.Visible = true;
			tray_icon.Tooltip = "LedMatrix Control";
			tray_icon.Activate += delegate { this.Visible = !this.Visible; };
		}
		text_time_default = "%c%r%2%+%+%h:%m:%s";
		text_winamp_default = "%8%c%r%r%a%n%c%g%t";
		text_winamp_2_default = "%2%+%+%r%a %o- %g%t";
		text_static_default = "%c%r%8%h:%m:%s%n%8%g%-%R";
		
		loadConfig();
		led_matrix.connection_status_changed_handler += led_matrix_connection_changed;
		led_matrix_thread.Start();
		winamp.title_changed_handler += led_matrix.setWinampPlaylisttitle;
		winamp.title_changed_handler += set_winamp;
		winamp.status_changed_handler += set_winamp;
		winamp.connection_changed_handler += winamp_connection_changed;
		rss.url = entry_rss.Text;
	}
	
	private void led_matrix_connection_changed(bool connected)
	{
		if(connected == true)
			Gtk.Application.Invoke (delegate {
              entry_address.ModifyBase(StateType.Normal, new Gdk.Color(0,255,0));});
		else
			Gtk.Application.Invoke (delegate {
              entry_address.ModifyBase(StateType.Normal, new Gdk.Color(255,0,0));});
	}
	
	private void winamp_connection_changed(bool connected)
	{
		if(connected == true)
			Gtk.Application.Invoke (delegate {
              entry_winamp_address.ModifyBase(StateType.Normal, new Gdk.Color(0,255,0));});
		else
			Gtk.Application.Invoke (delegate {
              entry_winamp_address.ModifyBase(StateType.Normal, new Gdk.Color(255,0,0));});
	}

	private void loadConfig()
	{
		rootKey = Microsoft.Win32.Registry.CurrentUser;
		softwareKey = null;
		string hkcuKey = @"SOFTWARE\Mono.mLedMatrix";
		softwareKey = rootKey.CreateSubKey (hkcuKey);
		
		led_matrix_address = (string)softwareKey.GetValue("ip_address","192.168.0.222");
		led_matrix.setAddress(led_matrix_address);
		
		winamp.winamp_address = (string)softwareKey.GetValue("winamp_address","localhost");
		winamp.winamp_port = (int)softwareKey.GetValue("winamp_port",4800);
		winamp.winamp_pass = (string)softwareKey.GetValue("winamp_pass","pass");
		entry_winamp_address.Text = winamp.winamp_address;
		entry_winamp_port.Text = winamp.winamp_port.ToString();
		entry_winamp_pass.Text = winamp.winamp_pass;
		
		entry_time.Text = (string)softwareKey.GetValue("text_time",text_time_default);
		entry_winamp.Text = (string)softwareKey.GetValue("text_winamp",text_winamp_default);
		entry_winamp_2.Text = (string)softwareKey.GetValue("text_winamp_2",text_winamp_2_default);
		entry_static_text.Text = (string)softwareKey.GetValue("text_static_text",text_static_default);
		
		entry_rss.Text = (string)softwareKey.GetValue("rss","http://rss.kicker.de/live/bundesliga");
		
		if((string)softwareKey.GetValue("screen") == "time")
		{
			radiobutton_time.Active = true;
			led_matrix.led_matrix_string = entry_time.Text;
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
			led_matrix.led_matrix_string = entry_static_text.Text;
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
		int port;
		softwareKey.SetValue("ip_address",entry_address.Text);
		softwareKey.SetValue("text_time",entry_time.Text);
		softwareKey.SetValue("text_winamp",entry_winamp.Text);
		softwareKey.SetValue("text_winamp_2",entry_winamp_2.Text);
		softwareKey.SetValue("text_static_text",entry_static_text.Text);
		softwareKey.SetValue("scroll_speed",led_matrix.scroll_speed);
		softwareKey.SetValue("winamp_address",entry_winamp_address.Text);
		int.TryParse(entry_winamp_port.Text,out port);
		softwareKey.SetValue("winamp_port",port);
		softwareKey.SetValue("winamp_pass",entry_winamp_pass.Text);
		softwareKey.SetValue("rss",entry_rss.Text);
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
		led_matrix.led_matrix_string = entry_time.Text;
	}
	
	private void set_winamp(bool playing, string playlisttitle)
	{
		if(playing == false)
		{
			led_matrix.led_matrix_string = entry_time.Text;
			//radiobutton_time.Active = true;
		}
		else
		{
			//radiobutton_winamp.Active = true;
			if(led_matrix.stringWidth("%a","8x8") > 65 &&
				led_matrix.stringWidth("%t","8x8") > 65)
				led_matrix.led_matrix_string = entry_winamp_2.Text;
			else
				led_matrix.led_matrix_string = entry_winamp.Text;
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
	
	protected virtual void static_text_changed (object sender, System.EventArgs e)
	{
		//TextBuffer buffer;
		//buffer = textview_static_text.Buffer;
		led_matrix.led_matrix_string = entry_static_text.Text;
		radiobutton_static_text.Active = true;
	}
	
	protected virtual void on_radiobutton_all_toggled (object sender, System.EventArgs e)
	{
		//led_matrix.current_screen = screen.all_on;
	}

	protected void on_button_reset_clicked (object sender, System.EventArgs e)
	{
		entry_time.Text = text_time_default;
		entry_winamp.Text = text_winamp_default;
		entry_winamp_2.Text = text_winamp_2_default;
		entry_static_text.Text = text_static_default;
	}

	protected void on_radiobutton_static_text_toggled (object sender, System.EventArgs e)
	{
		led_matrix.led_matrix_string = entry_static_text.Text;
	}

	protected void on_entry_rss_activated (object sender, System.EventArgs e)
	{
		rss.url = entry_rss.Text;
	}
}

